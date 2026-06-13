// Author: ABaLaQiYaShanMaiI
using System.Reflection;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthAxeThrower
{
    public class AxeThrower : HeroUpgradeDefinition
    {
        public static readonly string AXETHROWER_ID = "Hero_Trait_AxeThrowerV10";

        private const string FIELD_PREPARE_SOUND = "prepareSound";
        private const string FIELD_THROWING_AXE_PREFAB = "throwingAxePrefab";
        private const string FIELD_TRAJECTORY_UTILITY = "trajectoryUtility";
        private const string FIELD_ATTACK_SETTINGS = "attackSettings";
        private const string FIELD_AMMO = "ammo";

        // ── 日志门控 ──
        private static void GameplayLog(string message) => Debugger.Log(Plugin.EnableGameplayLog, message);
        private static void GameplayLogWarn(string message) => Debugger.LogWarning(Plugin.EnableGameplayLog, message);

        public AxeThrower()
        {
            this.upgradeType = TraitHelper.CreateTraitUpgradeType();
            TraitHelper.SetupBaseDefinition(this, AXETHROWER_ID,
                "ABaLaQiYaShanMaiI/TRAIT/AXE/NAME",
                "ABaLaQiYaShanMaiI/TRAIT/AXE/DESCSHORT",
                CustomSprites.Sprites["trait_axethrower"],
                TraitHelper.CreateSingleLevel("ABaLaQiYaShanMaiI/TRAIT/AXE/DESC"));
        }

        // ── 反射辅助（使用 ReflectionHelper 统一入口） ──

        private static object GetFieldValue(AxeThrowing instance, string fieldName)
        {
            return ReflectionHelper.GetFieldValue(instance, fieldName, "AxeThrower");
        }

        private static bool SetFieldValue(AxeThrowing instance, string fieldName, object value)
        {
            return ReflectionHelper.SetFieldValue(instance, fieldName, value, "AxeThrower");
        }

        // ── 三级容错获取 AxeThrowing 模板（加合法性校验） ──

        private static AxeThrowing GetAxeThrowingTemplate()
        {
            AxeThrowing template = null;

            // Level 1: 从 LevelStateObjectReferences 获取
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
                        GameplayLogWarn("[AxeThrower] VikingReference 反射失败: " + ex.Message);
                    }

                    if (!ReferenceEquals(agentObj, null))
                    {
                        template = agentObj.GetComponent<AxeThrowing>();
                        if (!ReferenceEquals(template, null))
                        {
                            GameplayLog("[AxeThrower] 从 LevelStateObjectReferences 成功获取模板");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                GameplayLogWarn("[AxeThrower] LevelStateObjectReferences 获取失败: " + ex.Message);
            }

            // Level 2: 从 Resources.FindObjectsOfTypeAll 获取
            if (ReferenceEquals(template, null))
            {
                try
                {
                    AxeThrowing[] allAxeThrowings = Resources.FindObjectsOfTypeAll<AxeThrowing>();
                    if (!ReferenceEquals(allAxeThrowings, null) && allAxeThrowings.Length > 0)
                    {
                        template = allAxeThrowings[0];
                        GameplayLog("[AxeThrower] 从 Resources.FindObjectsOfTypeAll 获取模板 (共 " + allAxeThrowings.Length + " 个)");
                    }
                }
                catch (System.Exception ex)
                {
                    GameplayLogWarn("[AxeThrower] Resources.FindObjectsOfTypeAll 获取失败: " + ex.Message);
                }
            }

            // 合法性校验：模板必须完整可用
            if (!ReferenceEquals(template, null))
            {
                bool valid = true;
                if (ReferenceEquals(GetFieldValue(template, FIELD_PREPARE_SOUND), null))
                {
                    GameplayLogWarn("[AxeThrower] 模板校验失败：prepareSound 缺失");
                    valid = false;
                }
                if (ReferenceEquals(GetFieldValue(template, FIELD_THROWING_AXE_PREFAB), null))
                {
                    GameplayLogWarn("[AxeThrower] 模板校验失败：throwingAxePrefab 缺失");
                    valid = false;
                }
                if (ReferenceEquals(GetFieldValue(template, FIELD_ATTACK_SETTINGS), null))
                {
                    GameplayLogWarn("[AxeThrower] 模板校验失败：attackSettings 缺失");
                    valid = false;
                }
                if (!valid)
                {
                    GameplayLogWarn("[AxeThrower] 模板不完整，视为无效");
                    template = null;
                }
            }

            if (ReferenceEquals(template, null))
            {
                GameplayLogWarn("[AxeThrower] 无法获取有效的 AxeThrowing 模板");
            }

            return template;
        }

        /// <summary>
        /// 安全深拷贝 AttackSettings（如果是 class 类型则逐字段复制，避免引用共享污染）。
        /// </summary>
        private static AttackSettings CloneAttackSettings(AttackSettings source)
        {
            AttackSettings clone = new AttackSettings();
            clone.damage = source.damage;
            clone.knockback = source.knockback;
            clone.stun = source.stun;
            clone.launchImpulse = source.launchImpulse;
            // 保留其他可能的字段默认值
            return clone;
        }

        private static void ApplyLevelScaling(AxeThrowing comp, int squadLevel)
        {
            object settingsObj = GetFieldValue(comp, FIELD_ATTACK_SETTINGS);
            if (ReferenceEquals(settingsObj, null))
            {
                GameplayLogWarn("[AxeThrower] 无法获取 attackSettings，跳过等级缩放");
                return;
            }
            AttackSettings attackSettings = (AttackSettings)settingsObj;
            GameplayLog(string.Format("[AxeThrower] 应用缩放前: launchImpulse={0}, damage={1}, knockback={2}, stun={3}",
                attackSettings.launchImpulse, attackSettings.damage, attackSettings.knockback, attackSettings.stun));

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
                GameplayLogWarn(string.Format("[AxeThrower] launchImpulse 为 {0}（来自模板），设置为默认值 1.0f", attackSettings.launchImpulse));
                attackSettings.launchImpulse = 1.0f;
            }

            SetFieldValue(comp, FIELD_ATTACK_SETTINGS, attackSettings);
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            // 使用 GetOrAddComponent 防止重复挂载组件
            ComponentHelper.GetOrAddComponent<LineOfSight>(squad.heroAgent.gameObject);
            AxeThrowing comp = ComponentHelper.GetOrAddComponent<AxeThrowing>(squad.heroAgent.gameObject);

            AxeThrowing template = GetAxeThrowingTemplate();

            if (!ReferenceEquals(template, null))
            {
                // 安全复制：逐字段复制，AttackSettings 做深拷贝
                SetFieldValue(comp, FIELD_PREPARE_SOUND, GetFieldValue(template, FIELD_PREPARE_SOUND));
                SetFieldValue(comp, FIELD_THROWING_AXE_PREFAB, GetFieldValue(template, FIELD_THROWING_AXE_PREFAB));
                SetFieldValue(comp, FIELD_TRAJECTORY_UTILITY, GetFieldValue(template, FIELD_TRAJECTORY_UTILITY));

                object templateSettingsObj = GetFieldValue(template, FIELD_ATTACK_SETTINGS);
                if (!ReferenceEquals(templateSettingsObj, null))
                {
                    AttackSettings clonedSettings = CloneAttackSettings((AttackSettings)templateSettingsObj);
                    SetFieldValue(comp, FIELD_ATTACK_SETTINGS, clonedSettings);
                }
                else
                {
                    SetFieldValue(comp, FIELD_ATTACK_SETTINGS, null);
                }

                GameplayLog("[AxeThrower] 成功从模板复制攻击属性（AttackSettings 已深拷贝）");
            }
            else
            {
                GameplayLogWarn("[AxeThrower] 无模板可用，使用默认值");
            }

            ApplyLevelScaling(comp, squad.hero.squadLevel);

            GameplayLog("[AxeThrower] 等级缩放已应用, 等级=" + squad.hero.squadLevel);

            comp.Setup();

            object finalAmmo = GetFieldValue(comp, FIELD_AMMO);
            object finalSettingsObj = GetFieldValue(comp, FIELD_ATTACK_SETTINGS);
            if (!ReferenceEquals(finalSettingsObj, null))
            {
                AttackSettings finalSettings = (AttackSettings)finalSettingsObj;
                GameplayLog(string.Format("[AxeThrower] 验证：ammo={0}, damage={1}, knockback={2}, stun={3}, launchImpulse={4}",
                    finalAmmo, finalSettings.damage, finalSettings.knockback, finalSettings.stun, finalSettings.launchImpulse));
            }
            else
            {
                GameplayLogWarn(string.Format("[AxeThrower] 验证：ammo={0}, attackSettings 获取失败", finalAmmo));
            }

            GameplayLog(string.Format("[AxeThrower] 已应用到小队 {0}，等级={1}", squad.name, squad.hero.squadLevel));
        }
    }
}