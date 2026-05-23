using System.Reflection;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthAxeThrower
{
    public class AxeThrower : HeroUpgradeDefinition
    {
        public static readonly string AXETHROWER_ID = "Hero_Trait_AxeThrower";

        // 反射字段名称（AxeThrowing 的私有字段）
        private const string FIELD_PREPARE_SOUND = "prepareSound";
        private const string FIELD_THROWING_AXE_PREFAB = "throwingAxePrefab";
        private const string FIELD_TRAJECTORY_UTILITY = "trajectoryUtility";
        private const string FIELD_ATTACK_SETTINGS = "attackSettings";
        private const string FIELD_AMMO = "ammo";

        public AxeThrower()
        {
            Plugin.Logger.LogInfo("AXETHROWER CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = (HeroUpgradeTypeEnum)4; // AxeThrower = 4
            this.upgradeType.canBeStartItem = true;
            this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
            this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
            this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
            this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
            this.affectsPortrait = false;
            base.name = AXETHROWER_ID;
            this.nameTerm = "NACU/TRAIT/AXE/NAME";
            this.shortDescription = "NACU/TRAIT/AXE/DESCSHORT";
            this.infoSprite = CustomSprites.Sprites["trait_axe"];
            HeroUpgradeDefinition.Level[] array = new HeroUpgradeDefinition.Level[1];
            int num = 0;
            HeroUpgradeDefinition.Level level = default(HeroUpgradeDefinition.Level);
            level.cost = 0;
            level.description = "NACU/TRAIT/AXE/DESC";
            array[num] = level;
            this.levels = array;
        }

        /// <summary>
        /// 获取或添加组件（Unity 扩展方法替代品）
        /// </summary>
        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (ReferenceEquals(comp, null))
            {
                comp = go.AddComponent<T>();
            }
            return comp;
        }

        /// <summary>
        /// 通过反射获取 AxeThrowing 的字段值（非泛型，避免类型转换错误）
        /// 使用 ReferenceEquals 避免 Mono 2.0 下 FieldInfo.op_Inequality 缺失问题
        /// </summary>
        private static object GetFieldValue(AxeThrowing instance, string fieldName)
        {
            FieldInfo field = typeof(AxeThrowing).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (ReferenceEquals(field, null))
            {
                Plugin.Logger.LogWarning(string.Format("[AxeThrower] 反射字段 {0} 未找到", fieldName));
                return null;
            }
            return field.GetValue(instance);
        }

        /// <summary>
        /// 通过反射设置 AxeThrowing 的字段值（非泛型，避免类型转换错误）
        /// 使用 ReferenceEquals 避免 Mono 2.0 下 FieldInfo.op_Inequality 缺失问题
        /// </summary>
        private static void SetFieldValue(AxeThrowing instance, string fieldName, object value)
        {
            FieldInfo field = typeof(AxeThrowing).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (ReferenceEquals(field, null))
            {
                Plugin.Logger.LogWarning(string.Format("[AxeThrower] 反射字段 {0} 未找到，无法设置", fieldName));
                return;
            }
            field.SetValue(instance, value);
        }

        /// <summary>
        /// 安全获取 AxeThrowing 模板，支持多级容错：
        /// 1. 尝试从 LevelStateObjectReferences.dict 获取
        /// 2. 失败时遍历 Resources 查找
        /// 3. 均失败则返回 null
        /// </summary>
        private static AxeThrowing GetAxeThrowingTemplate()
        {
            // 方法1: 从 LevelStateObjectReferences.dict 获取
            try
            {
                if (!ReferenceEquals(LevelStateObjectReferences.dict, null) &&
                    LevelStateObjectReferences.dict.TryGetValue("Viking_AxeThrower", out UnityEngine.Object reference) &&
                    reference is VikingReference vikingRef)
                {
                    // 通过反射访问 VikingReference 的字段（不同游戏版本字段名可能不同）
                    // 使用 ReferenceEquals 和显式 null 检查避免 FieldInfo.op_Inequality 缺失
                    GameObject agentObj = null;
                    try
                    {
                        FieldInfo vikingCloneField = typeof(VikingReference).GetField("vikingClone",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        FieldInfo vikingField = typeof(VikingReference).GetField("viking",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                        Component vikingClone = null;
                        if (!ReferenceEquals(vikingCloneField, null))
                            vikingClone = vikingCloneField.GetValue(vikingRef) as Component;

                        Component viking = null;
                        if (!ReferenceEquals(vikingField, null))
                            viking = vikingField.GetValue(vikingRef) as Component;

                        if (!ReferenceEquals(vikingClone, null))
                            agentObj = vikingClone.gameObject;
                        else if (!ReferenceEquals(viking, null))
                            agentObj = viking.gameObject;
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Logger.LogWarning("[AxeThrower] VikingReference 反射失败: " + ex.Message);
                    }

                    if (!ReferenceEquals(agentObj, null))
                    {
                        AxeThrowing template = agentObj.GetComponent<AxeThrowing>();
                        if (!ReferenceEquals(template, null))
                        {
                            Plugin.Logger.LogInfo("[AxeThrower] 从 LevelStateObjectReferences 成功获取模板");
                            return template;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("[AxeThrower] LevelStateObjectReferences 获取失败: " + ex.Message);
            }

            // 方法2: 遍历 Resources 查找 AxeThrowing 组件
            try
            {
                AxeThrowing[] allAxeThrowings = Resources.FindObjectsOfTypeAll<AxeThrowing>();
                if (!ReferenceEquals(allAxeThrowings, null) && allAxeThrowings.Length > 0)
                {
                    Plugin.Logger.LogInfo("[AxeThrower] 从 Resources.FindObjectsOfTypeAll 获取模板 (共 " + allAxeThrowings.Length + " 个)");
                    return allAxeThrowings[0];
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("[AxeThrower] Resources.FindObjectsOfTypeAll 获取失败: " + ex.Message);
            }

            // 方法3: 均失败，返回 null
            Plugin.Logger.LogWarning("[AxeThrower] 无法获取 AxeThrowing 模板");
            return null;
        }

        /// <summary>
        /// 根据小队等级应用等级缩放（使用反射访问私有字段）
        /// </summary>
        private static void ApplyLevelScaling(AxeThrowing comp, int squadLevel)
        {
            // 获取 attackSettings 的副本（AttackSettings 是结构体）
            AttackSettings attackSettings = (AttackSettings)GetFieldValue(comp, FIELD_ATTACK_SETTINGS);

            switch (squadLevel)
            {
                case 0:
                    SetFieldValue(comp, FIELD_AMMO, 5);
                    attackSettings.launchImpulse = attackSettings.launchImpulse * 0.9f;
                    break;
                case 1:
                    SetFieldValue(comp, FIELD_AMMO, 8);
                    attackSettings.damage = attackSettings.damage * 1.33f;
                    attackSettings.knockback = attackSettings.knockback * 1.33f;
                    attackSettings.stun = attackSettings.stun * 1.5f;
                    break;
                case 2:
                    SetFieldValue(comp, FIELD_AMMO, 11);
                    attackSettings.damage = attackSettings.damage * 1.66f;
                    attackSettings.launchImpulse = attackSettings.launchImpulse * 1.1f;
                    attackSettings.knockback = attackSettings.knockback * 1.66f;
                    attackSettings.stun = attackSettings.stun * 2f;
                    break;
                default:
                    SetFieldValue(comp, FIELD_AMMO, 14);
                    attackSettings.damage = attackSettings.damage * 2f;
                    attackSettings.launchImpulse = attackSettings.launchImpulse * 1.2f;
                    attackSettings.knockback = attackSettings.knockback * 2f;
                    attackSettings.stun = attackSettings.stun * 2.5f;
                    break;
            }

            // 将修改后的结构体写回
            SetFieldValue(comp, FIELD_ATTACK_SETTINGS, attackSettings);
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            // 添加 LineOfSight 组件（掷斧手需要视线系统）
            GetOrAddComponent<LineOfSight>(squad.heroAgent.gameObject);

            // 获取模板（带容错）
            AxeThrowing template = GetAxeThrowingTemplate();

            // 添加 AxeThrowing 组件
            squad.heroAgent.gameObject.AddComponent<AxeThrowing>();
            AxeThrowing comp = squad.heroAgent.GetComponent<AxeThrowing>();

            if (!ReferenceEquals(template, null))
            {
                // 从模板复制所有关键战斗属性（使用非泛型反射，避免类型转换错误）
                SetFieldValue(comp, FIELD_PREPARE_SOUND, GetFieldValue(template, FIELD_PREPARE_SOUND));
                SetFieldValue(comp, FIELD_THROWING_AXE_PREFAB, GetFieldValue(template, FIELD_THROWING_AXE_PREFAB));
                SetFieldValue(comp, FIELD_TRAJECTORY_UTILITY, GetFieldValue(template, FIELD_TRAJECTORY_UTILITY));
                SetFieldValue(comp, FIELD_ATTACK_SETTINGS, GetFieldValue(template, FIELD_ATTACK_SETTINGS));

                Plugin.Logger.LogInfo("[AxeThrower] 成功从模板复制攻击属性");
            }
            else
            {
                // 使用内置默认值
                Plugin.Logger.LogWarning("[AxeThrower] 无模板可用，使用默认值");
            }

            // 应用等级缩放
            ApplyLevelScaling(comp, squad.hero.squadLevel);

            Plugin.Logger.LogInfo("[AxeThrower] 等级缩放已应用, 等级=" + squad.hero.squadLevel);

            // 初始化组件
            comp.Setup();
        }
    }
}
