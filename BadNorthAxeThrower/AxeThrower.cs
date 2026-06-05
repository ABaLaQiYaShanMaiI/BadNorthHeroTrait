using System.Reflection;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthAxeThrower
{
    public class AxeThrower : HeroUpgradeDefinition
    {
        public static readonly string AXETHROWER_ID = "Hero_Trait_AxeThrower";

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

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (ReferenceEquals(comp, null))
            {
                comp = go.AddComponent<T>();
            }
            return comp;
        }

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

        // 三级容错获取 AxeThrowing 模板
        private static AxeThrowing GetAxeThrowingTemplate()
        {
            try
            {
                if (!ReferenceEquals(LevelStateObjectReferences.dict, null) &&
                    LevelStateObjectReferences.dict.TryGetValue("Viking_AxeThrower", out UnityEngine.Object reference) &&
                    reference is VikingReference vikingRef)
                {
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

            Plugin.Logger.LogWarning("[AxeThrower] 无法获取 AxeThrowing 模板");
            return null;
        }

        private static void ApplyLevelScaling(AxeThrowing comp, int squadLevel)
        {
            object settingsObj = GetFieldValue(comp, FIELD_ATTACK_SETTINGS);
            if (ReferenceEquals(settingsObj, null))
            {
                Plugin.Logger.LogWarning("[AxeThrower] 无法获取 attackSettings，跳过等级缩放");
                return;
            }
            AttackSettings attackSettings = (AttackSettings)settingsObj;
            Plugin.Logger.LogInfo($"[AxeThrower] 应用缩放前: launchImpulse={attackSettings.launchImpulse}, damage={attackSettings.damage}, knockback={attackSettings.knockback}, stun={attackSettings.stun}");

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

            if (attackSettings.launchImpulse <= 0f)
            {
                Plugin.Logger.LogWarning($"[AxeThrower] launchImpulse 为 {attackSettings.launchImpulse}（来自模板），设置为默认值 1.0f");
                attackSettings.launchImpulse = 1.0f;
            }

            SetFieldValue(comp, FIELD_ATTACK_SETTINGS, attackSettings);
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            GetOrAddComponent<LineOfSight>(squad.heroAgent.gameObject);
            AxeThrowing template = GetAxeThrowingTemplate();
            squad.heroAgent.gameObject.AddComponent<AxeThrowing>();
            AxeThrowing comp = squad.heroAgent.GetComponent<AxeThrowing>();

            if (!ReferenceEquals(template, null))
            {
                SetFieldValue(comp, FIELD_PREPARE_SOUND, GetFieldValue(template, FIELD_PREPARE_SOUND));
                SetFieldValue(comp, FIELD_THROWING_AXE_PREFAB, GetFieldValue(template, FIELD_THROWING_AXE_PREFAB));
                SetFieldValue(comp, FIELD_TRAJECTORY_UTILITY, GetFieldValue(template, FIELD_TRAJECTORY_UTILITY));
                SetFieldValue(comp, FIELD_ATTACK_SETTINGS, GetFieldValue(template, FIELD_ATTACK_SETTINGS));

                Plugin.Logger.LogInfo("[AxeThrower] 成功从模板复制攻击属性");
            }
            else
            {
                Plugin.Logger.LogWarning("[AxeThrower] 无模板可用，使用默认值");
            }
            ApplyLevelScaling(comp, squad.hero.squadLevel);

            Plugin.Logger.LogInfo("[AxeThrower] 等级缩放已应用, 等级=" + squad.hero.squadLevel);

            comp.Setup();
            object finalAmmo = GetFieldValue(comp, FIELD_AMMO);
            object finalSettingsObj = GetFieldValue(comp, FIELD_ATTACK_SETTINGS);
            if (!ReferenceEquals(finalSettingsObj, null))
            {
                AttackSettings finalSettings = (AttackSettings)finalSettingsObj;
                Plugin.Logger.LogInfo($"[AxeThrower] 验证：ammo={finalAmmo}, damage={finalSettings.damage}, knockback={finalSettings.knockback}, stun={finalSettings.stun}, launchImpulse={finalSettings.launchImpulse}");
            }
            else
            {
                Plugin.Logger.LogWarning($"[AxeThrower] 验证：ammo={finalAmmo}, attackSettings 获取失败");
            }

            Plugin.Logger.LogInfo($"[AxeThrower] 已应用到小队 {squad.name}，等级={squad.hero.squadLevel}");
        }
    }
}
