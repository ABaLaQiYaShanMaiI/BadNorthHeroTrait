using System;
using System.Reflection;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthFlyer
{
    /// <summary>
    /// 神鹰 (Flyer) - 产生让敌人浮空的力量。
    /// 英雄获得飞斧能力，所有步兵单位获得击飞效果。
    /// </summary>
    public class Flyer : HeroUpgradeDefinition
    {
        public static readonly string FLYER_ID = "Hero_Trait_Flyer";

        // ── 反射字段名常量 ──
        private const string FIELD_PREPARE_SOUND = "prepareSound";
        private const string FIELD_THROWING_AXE_PREFAB = "throwingAxePrefab";
        private const string FIELD_TRAJECTORY_UTILITY = "trajectoryUtility";
        private const string FIELD_ATTACK_SETTINGS = "attackSettings";
        private const string FIELD_AMMO = "ammo";

        // ── 飞斧参数 ──
        private const float HERO_AXE_DAMAGE_MULT = 0.5f;
        private const float HERO_LAUNCH_IMPULSE = 9f;
        private const int HERO_AXE_AMMO = 500;

        // ── 英雄击飞参数 ──
        private const float HERO_MAX_SPEED = 8f;
        private const float HERO_WANNAFLY = 5f;
        private const int HERO_KNOCKBACK_LEVEL_INDEX = 3;
        private const float HERO_KNOCKBACK_MULT = 2f;

        // ── 小兵减速参数 ──
        private const float MINION_MAX_SPEED = 2.5f;
        private const float MINION_FLY_POWER = 5f;
        private const float SPEAR_FLY_POWER = 4f;
        private const float ARCHER_FLY_POWER = 3f;

        private static bool _wannaFlyWarningLogged = false;

        public Flyer()
        {
            Debugger.Log("FLYER CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = (HeroUpgradeTypeEnum)4; // Trait
            this.upgradeType.canBeStartItem = true;
            this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
            this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
            this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
            this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
            this.affectsPortrait = false;
            base.name = FLYER_ID;
            this.nameTerm = "YYYYY/TRAIT/FLYER/NAME";
            this.shortDescription = "YYYYY/TRAIT/FLYER/DESCSHORT";
            this.infoSprite = CustomSprites.Sprites["trait_flyer"];
            HeroUpgradeDefinition.Level[] array = new HeroUpgradeDefinition.Level[1];
            int num = 0;
            HeroUpgradeDefinition.Level level = default(HeroUpgradeDefinition.Level);
            level.cost = 0;
            level.description = "YYYYY/TRAIT/FLYER/DESC";
            array[num] = level;
            this.levels = array;
        }

        // ── 反射辅助 ──
        private static object GetFieldValue(AxeThrowing instance, string fieldName)
        {
            FieldInfo field = typeof(AxeThrowing).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (ReferenceEquals(field, null))
            {
                Plugin.Logger.LogWarning(string.Format("[Flyer] 反射字段 {0} 未找到", fieldName));
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
                Plugin.Logger.LogWarning(string.Format("[Flyer] 反射字段 {0} 未找到，无法设置", fieldName));
                return;
            }
            field.SetValue(instance, value);
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

        // ── 三级容错获取 AxeThrowing 模板 ──
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
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning("[Flyer] VikingReference 反射失败: " + ex.Message);
                    }

                    if (!ReferenceEquals(agentObj, null))
                    {
                        AxeThrowing template = agentObj.GetComponent<AxeThrowing>();
                        if (!ReferenceEquals(template, null))
                        {
                            Plugin.Logger.LogInfo("[Flyer] 从 LevelStateObjectReferences 成功获取 AxeThrowing 模板");
                            return template;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[Flyer] LevelStateObjectReferences 获取 AxeThrowing 失败: " + ex.Message);
            }

            try
            {
                AxeThrowing[] allAxeThrowings = Resources.FindObjectsOfTypeAll<AxeThrowing>();
                if (!ReferenceEquals(allAxeThrowings, null) && allAxeThrowings.Length > 0)
                {
                    Plugin.Logger.LogInfo("[Flyer] 从 Resources.FindObjectsOfTypeAll 获取 AxeThrowing 模板 (共 " + allAxeThrowings.Length + " 个)");
                    return allAxeThrowings[0];
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[Flyer] Resources.FindObjectsOfTypeAll 获取 AxeThrowing 失败: " + ex.Message);
            }

            Plugin.Logger.LogWarning("[Flyer] 无法获取 AxeThrowing 模板");
            return null;
        }

        /// <summary>
        /// 为英雄添加飞斧能力
        /// </summary>
        private void HeroAxe(Agent agent)
        {
            if (agent.GetComponent<AxeThrowing>() != null)
                return;

            GetOrAddComponent<LineOfSight>(agent.gameObject);
            AxeThrowing template = GetAxeThrowingTemplate();

            AxeThrowing axeThrowing = agent.gameObject.AddComponent<AxeThrowing>();
            if (!ReferenceEquals(template, null))
            {
                SetFieldValue(axeThrowing, FIELD_PREPARE_SOUND, GetFieldValue(template, FIELD_PREPARE_SOUND));
                SetFieldValue(axeThrowing, FIELD_THROWING_AXE_PREFAB, GetFieldValue(template, FIELD_THROWING_AXE_PREFAB));
                SetFieldValue(axeThrowing, FIELD_TRAJECTORY_UTILITY, GetFieldValue(template, FIELD_TRAJECTORY_UTILITY));
                SetFieldValue(axeThrowing, FIELD_ATTACK_SETTINGS, GetFieldValue(template, FIELD_ATTACK_SETTINGS));
                Plugin.Logger.LogInfo("[Flyer] 成功从模板复制飞斧属性");
            }
            else
            {
                Plugin.Logger.LogWarning("[Flyer] 无模板可用，飞斧使用默认值");
            }

            SetFieldValue(axeThrowing, FIELD_AMMO, HERO_AXE_AMMO);

            // 调整飞斧伤害和发射冲量
            object settingsObj = GetFieldValue(axeThrowing, FIELD_ATTACK_SETTINGS);
            if (!ReferenceEquals(settingsObj, null))
            {
                AttackSettings attackSettings = (AttackSettings)settingsObj;
                attackSettings.damage = attackSettings.damage * HERO_AXE_DAMAGE_MULT;
                attackSettings.launchImpulse = HERO_LAUNCH_IMPULSE;
                SetFieldValue(axeThrowing, FIELD_ATTACK_SETTINGS, attackSettings);
            }

            axeThrowing.Setup();
            Plugin.Logger.LogInfo("[Flyer] 英雄飞斧已配置: ammo=" + HERO_AXE_AMMO + ", damageMult=" + HERO_AXE_DAMAGE_MULT + ", launchImpulse=" + HERO_LAUNCH_IMPULSE);
        }

        /// <summary>
        /// 设置英雄的Wannafly（击飞）效果
        /// </summary>
        private void HeroFlyPower(Agent agent)
        {
            Swordsman swordsman = agent.GetComponent<Swordsman>();
            if (swordsman != null && swordsman.knockbackLevels.Length > HERO_KNOCKBACK_LEVEL_INDEX)
            {
                swordsman.knockbackLevels[HERO_KNOCKBACK_LEVEL_INDEX] *= HERO_KNOCKBACK_MULT;
            }

            // 通过反射设置 Wannafly 字段（如果存在）
            try
            {
                if (swordsman != null)
                {
                    FieldInfo wannaFlyField = typeof(Swordsman).GetField("Wannafly",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (!ReferenceEquals(wannaFlyField, null))
                    {
                        wannaFlyField.SetValue(swordsman, HERO_WANNAFLY);
                    }
                    else if (!_wannaFlyWarningLogged)
                    {
                        _wannaFlyWarningLogged = true;
                        Plugin.Logger.LogWarning("[Flyer] Swordsman.Wannafly field not found. (此警告仅显示一次)");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[Flyer] 设置 Wannafly 失败: " + ex.Message);
            }

            agent.maxSpeed = HERO_MAX_SPEED;
            Plugin.Logger.LogInfo("[Flyer] 英雄击飞效果已配置: maxSpeed=" + HERO_MAX_SPEED + ", knockbackMult=" + HERO_KNOCKBACK_MULT);
        }

        /// <summary>
        /// 为小兵设置减速和击飞效果
        /// </summary>
        private void SlowMinion(Agent agent)
        {
            agent.maxSpeed = MINION_MAX_SPEED;
            Swordsman swordsman = agent.GetComponent<Swordsman>();
            Spear spear = agent.GetComponent<Spear>();
            Archery archery = agent.GetComponent<Archery>();

            if (swordsman != null)
            {
                try
                {
                    FieldInfo wannaFlyField = typeof(Swordsman).GetField("Wannafly",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (!ReferenceEquals(wannaFlyField, null))
                    {
                        wannaFlyField.SetValue(swordsman, MINION_FLY_POWER);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[Flyer] 设置小兵 Swordsman.Wannafly 失败: " + ex.Message);
                }
            }

            if (spear != null)
            {
                try
                {
                    FieldInfo spearFlyField = typeof(Spear).GetField("spearfly",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (!ReferenceEquals(spearFlyField, null))
                    {
                        spearFlyField.SetValue(spear, SPEAR_FLY_POWER);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[Flyer] 设置 Spear.spearfly 失败: " + ex.Message);
                }
            }

            if (archery != null)
            {
                try
                {
                    FieldInfo archerFlyField = typeof(Archery).GetField("archerfly",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (!ReferenceEquals(archerFlyField, null))
                    {
                        archerFlyField.SetValue(archery, ARCHER_FLY_POWER);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[Flyer] 设置 Archery.archerfly 失败: " + ex.Message);
                }
            }

            Debugger.Log("[Flyer] 小兵减速/击飞已配置: maxSpeed=" + MINION_MAX_SPEED);
        }

        /// <summary>
        /// 复制双刀维京的动画控制器参数
        /// </summary>
        private void CopyAnimatorParameters(Animator source, Animator target)
        {
            foreach (AnimatorControllerParameter param in source.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Bool)
                {
                    target.SetBool(param.name, source.GetBool(param.name));
                }
                else if (param.type == AnimatorControllerParameterType.Float)
                {
                    target.SetFloat(param.name, source.GetFloat(param.name));
                }
                else if (param.type == AnimatorControllerParameterType.Int)
                {
                    target.SetInteger(param.name, source.GetInteger(param.name));
                }
                else if (param.type == AnimatorControllerParameterType.Trigger && source.GetBool(param.name))
                {
                    target.SetTrigger(param.name);
                }
            }
        }

        /// <summary>
        /// 将英雄的动画控制器替换为双刀维京的样式
        /// </summary>
        private void CopyTwoHandedAnimator(Agent agent)
        {
            try
            {
                if (!LevelStateObjectReferences.dict.TryGetValue("Viking_Twohanded", out UnityEngine.Object reference) ||
                    !(reference is VikingReference vikingRef))
                {
                    Plugin.Logger.LogWarning("[Flyer] 无法获取 Viking_Twohanded 引用，跳过动画复制");
                    return;
                }

                GameObject templateObj = null;
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
                        templateObj = vikingClone.gameObject;
                    else if (!ReferenceEquals(viking, null))
                        templateObj = viking.gameObject;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[Flyer] VikingReference(Twohanded) 反射失败: " + ex.Message);
                    return;
                }

                if (ReferenceEquals(templateObj, null))
                {
                    Plugin.Logger.LogWarning("[Flyer] 无法获取双刀维京的 GameObject");
                    return;
                }

                Animator sourceAnimator = templateObj.GetComponent<Animator>();
                Animator targetAnimator = agent.GetComponent<Animator>();

                if (ReferenceEquals(sourceAnimator, null) || ReferenceEquals(targetAnimator, null))
                {
                    Plugin.Logger.LogWarning("[Flyer] Animator 组件缺失");
                    return;
                }

                targetAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
                CopyAnimatorParameters(sourceAnimator, targetAnimator);
                targetAnimator.updateMode = sourceAnimator.updateMode;
                targetAnimator.cullingMode = sourceAnimator.cullingMode;
                targetAnimator.applyRootMotion = sourceAnimator.applyRootMotion;

                Debugger.Log("[Flyer] 英雄动画控制器已替换为双刀维京样式");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[Flyer] CopyTwoHandedAnimator 异常: " + ex.Message);
            }
        }

        // ── 主入口 ──
        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            // 英雄：飞斧 + 击飞效果
            this.HeroAxe(squad.heroAgent);
            this.HeroFlyPower(squad.heroAgent);
            this.CopyTwoHandedAnimator(squad.heroAgent);

            // 小兵：减速 + 击飞效果
            this.SlowMinion(squad.minionPrefab);

            Plugin.Logger.LogInfo(string.Format("[Flyer] 已应用到小队 {0}", squad.name));
        }
    }
}