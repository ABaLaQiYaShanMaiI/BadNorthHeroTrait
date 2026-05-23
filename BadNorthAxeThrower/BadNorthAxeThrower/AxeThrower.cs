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
        /// 通过反射获取 AxeThrowing 的私有字段值
        /// </summary>
        private static T GetField<T>(AxeThrowing instance, string fieldName)
        {
            var field = typeof(AxeThrowing).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Plugin.Logger.LogWarning($"[AxeThrower] 反射字段 {fieldName} 未找到");
                return default(T);
            }
            return (T)field.GetValue(instance);
        }

        /// <summary>
        /// 通过反射设置 AxeThrowing 的私有字段值
        /// </summary>
        private static void SetField<T>(AxeThrowing instance, string fieldName, T value)
        {
            var field = typeof(AxeThrowing).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Plugin.Logger.LogWarning($"[AxeThrower] 反射字段 {fieldName} 未找到，无法设置");
                return;
            }
            field.SetValue(instance, value);
        }

        /// <summary>
        /// 通过反射获取 VikingReference 的私有字段值
        /// </summary>
        private static T GetVikingField<T>(VikingReference instance, string fieldName)
        {
            var field = typeof(VikingReference).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Plugin.Logger.LogWarning($"[AxeThrower] VikingReference 反射字段 {fieldName} 未找到");
                return default(T);
            }
            return (T)field.GetValue(instance);
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
                if (LevelStateObjectReferences.dict != null &&
                    LevelStateObjectReferences.dict.TryGetValue("Viking_AxeThrower", out var reference) &&
                    reference is VikingReference vikingRef)
                {
                    // 通过反射访问私有字段 vikingClone 或 viking
                    GameObject vikingAgent = GetVikingField<GameObject>(vikingRef, "vikingClone");
                    if (vikingAgent == null)
                    {
                        vikingAgent = GetVikingField<GameObject>(vikingRef, "viking");
                    }

                    if (vikingAgent != null)
                    {
                        var template = vikingAgent.GetComponent<AxeThrowing>();
                        if (template != null)
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
                var allAxeThrowings = Resources.FindObjectsOfTypeAll<AxeThrowing>();
                if (allAxeThrowings != null && allAxeThrowings.Length > 0)
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
            AttackSettings attackSettings = GetField<AttackSettings>(comp, FIELD_ATTACK_SETTINGS);

            switch (squadLevel)
            {
                case 0:
                    SetField(comp, FIELD_AMMO, 5);
                    attackSettings.launchImpulse = attackSettings.launchImpulse * 0.9f;
                    break;
                case 1:
                    SetField(comp, FIELD_AMMO, 8);
                    attackSettings.damage = attackSettings.damage * 1.33f;
                    attackSettings.knockback = attackSettings.knockback * 1.33f;
                    attackSettings.stun = attackSettings.stun * 1.5f;
                    break;
                case 2:
                    SetField(comp, FIELD_AMMO, 11);
                    attackSettings.damage = attackSettings.damage * 1.66f;
                    attackSettings.launchImpulse = attackSettings.launchImpulse * 1.1f;
                    attackSettings.knockback = attackSettings.knockback * 1.66f;
                    attackSettings.stun = attackSettings.stun * 2f;
                    break;
                default:
                    SetField(comp, FIELD_AMMO, 14);
                    attackSettings.damage = attackSettings.damage * 2f;
                    attackSettings.launchImpulse = attackSettings.launchImpulse * 1.2f;
                    attackSettings.knockback = attackSettings.knockback * 2f;
                    attackSettings.stun = attackSettings.stun * 2.5f;
                    break;
            }

            // 将修改后的结构体写回
            SetField(comp, FIELD_ATTACK_SETTINGS, attackSettings);
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            // 添加 LineOfSight 组件（掷斧手需要视线系统）
            squad.heroAgent.GetOrAddComponent<LineOfSight>();

            // 获取模板（带容错）
            AxeThrowing template = GetAxeThrowingTemplate();

            // 添加 AxeThrowing 组件
            squad.heroAgent.gameObject.AddComponent<AxeThrowing>();
            AxeThrowing comp = squad.heroAgent.GetComponent<AxeThrowing>();

            if (template != null)
            {
                // 从模板复制所有关键战斗属性（通过反射访问私有字段）
                SetField(comp, FIELD_PREPARE_SOUND, GetField<AudioSource>(template, FIELD_PREPARE_SOUND));
                SetField(comp, FIELD_THROWING_AXE_PREFAB, GetField<GameObject>(template, FIELD_THROWING_AXE_PREFAB));
                SetField(comp, FIELD_TRAJECTORY_UTILITY, GetField<TrajectoryUtility>(template, FIELD_TRAJECTORY_UTILITY));
                SetField(comp, FIELD_ATTACK_SETTINGS, GetField<AttackSettings>(template, FIELD_ATTACK_SETTINGS));

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
