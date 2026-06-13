// Author: ABaLaQiYaShanMaiI
using System;
using System.Reflection;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthTitan
{
    /// <summary>
    /// 泰坦 (Titan) - 真正的巨人之力，盾弓皆可，升级后起效。
    /// 需要小队等级 >= 1 才能生效，大幅提升单位属性但减少小队人数。
    /// </summary>
    public class Titan : HeroUpgradeDefinition
    {
        public static readonly string Titan_ID = "Hero_Trait_TitanV10";

        // ── 通用参数 ──
        private const float SCALE = 1.25f;
        private const float STUN_MULTIPLIER = 1E-06f; // 几乎免疫眩晕

        // ── 步兵 (Swordsman) 参数 ──
        private const float SWORD_DAMAGE_MULT = 2f;
        private const float SWORD_KNOCKBACK_MULT = 1.5f;
        private const float SWORD_STUN_MULT = 1.5f;
        private const float SWORD_MAX_SPEED = 3f;
        private readonly float[] SwordArmorLevels = { 3f, 5f, 7f, 8f };

        // ── 弓箭手 (Archery) 参数 ──
        private const float ARCHER_MAX_SPEED = 2.5f;
        private const float ARCHER_COOLDOWN_MULT = 1.3f;
        private const float ARCHER_SPREAD_MULT = 0.4f;
        private const float ARCHER_DAMAGE_MULT = 1.5f;
        private const float ARCHER_KNOCKBACK_MULT = 1.1f;
        private const float ARCHER_STUN_MULT = 1.1f;
        private readonly float[] ArcherArmorLevels = { 2f, 3f, 4f, 5f };

        // ── 反射相关 ──
        private static FieldInfo _armorField = null;
        private static bool _armorFieldAttempted = false;
        private static FieldInfo _stunField = null;
        private static bool _stunFieldAttempted = false;

        public Titan()
        {
            this.upgradeType = TraitHelper.CreateTraitUpgradeType();
            TraitHelper.SetupBaseDefinition(this, Titan_ID,
                "ABaLaQiYaShanMaiI/TRAIT/TITAN/NAME",
                "ABaLaQiYaShanMaiI/TRAIT/TITAN/DESCSHORT",
                CustomSprites.Sprites["trait_titan"],
                TraitHelper.CreateSingleLevel("ABaLaQiYaShanMaiI/TRAIT/TITAN/DESC"));
        }

        /// <summary>
        /// 安全设置 Agent 的护甲值（通过反射，避免依赖特定版本字段名）
        /// </summary>
        private static void SetAgentArmor(Agent agent, float[] armorValues)
        {
            if (ReferenceEquals(agent, null) || armorValues == null) return;

            if (!_armorFieldAttempted)
            {
                _armorFieldAttempted = true;
                _armorField = typeof(Armor).GetField("armor",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (!ReferenceEquals(_armorField, null))
            {
                Armor armorComp = agent.GetComponent<Armor>();
                if (!ReferenceEquals(armorComp, null))
                {
                    _armorField.SetValue(armorComp, armorValues);
                }
            }
            else
            {
                Plugin.Logger.LogWarning("[Titan] Armor.armor 反射字段未找到，无法设置护甲");
            }
        }

        /// <summary>
        /// 安全设置 Agent 的眩晕倍率（通过反射）
        /// </summary>
        private static void SetAgentStunMultiplier(Agent agent, float value)
        {
            if (ReferenceEquals(agent, null)) return;

            if (!_stunFieldAttempted)
            {
                _stunFieldAttempted = true;
                _stunField = typeof(Stun).GetField("stunMultiplier",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (!ReferenceEquals(_stunField, null))
            {
                Stun stunComp = agent.GetComponent<Stun>();
                if (!ReferenceEquals(stunComp, null))
                {
                    _stunField.SetValue(stunComp, value);
                }
            }
            else
            {
                Plugin.Logger.LogWarning("[Titan] Stun.stunMultiplier 反射字段未找到，无法设置眩晕免疫");
            }
        }

        /// <summary>
        /// 泰坦化步兵单位
        /// </summary>
        private void TitanizeSwordsman(Agent agent)
        {
            Swordsman component = agent.GetComponent<Swordsman>();
            if (component == null) return;

            for (int i = 0; i < component.damageLevels.Length; i++)
            {
                component.damageLevels[i] *= SWORD_DAMAGE_MULT;
            }
            for (int i = 0; i < component.knockbackLevels.Length; i++)
            {
                component.knockbackLevels[i] *= SWORD_KNOCKBACK_MULT;
            }
            for (int i = 0; i < component.stunLevels.Length; i++)
            {
                component.stunLevels[i] *= SWORD_STUN_MULT;
            }
            agent.maxSpeed = SWORD_MAX_SPEED;
            SetAgentArmor(agent, SwordArmorLevels);

            Debugger.Log(Plugin.EnableGameplayLog, "[Titan] 步兵泰坦化完成: damageMult=" + SWORD_DAMAGE_MULT + ", speed=" + SWORD_MAX_SPEED);
        }

        /// <summary>
        /// 泰坦化弓箭手单位 - 从 Viking_TankArcher 获取模板
        /// </summary>
        private void TitanizeArchery(Agent agent)
        {
            Archery component = agent.GetComponent<Archery>();
            if (component == null) return;

            agent.maxSpeed = ARCHER_MAX_SPEED;

            // 从重装弓箭手获取箭矢和声音模板
            try
            {
                if (!ReferenceEquals(LevelStateObjectReferences.dict, null) &&
                    LevelStateObjectReferences.dict.TryGetValue("Viking_TankArcher", out UnityEngine.Object reference) &&
                    reference is VikingReference vikingRef)
                {
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
                        Plugin.Logger.LogWarning("[Titan] VikingReference(TankArcher) 反射失败: " + ex.Message);
                    }

                    if (!ReferenceEquals(templateObj, null))
                    {
                        Archery template = templateObj.GetComponent<Archery>();
                        if (!ReferenceEquals(template, null))
                        {
                            // 使用反射复制箭矢和声音属性
                            FieldInfo arrowPrefabField = typeof(Archery).GetField("arrowPrefab",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            FieldInfo drawSoundField = typeof(Archery).GetField("drawSound",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            FieldInfo shootSoundField = typeof(Archery).GetField("shootSound",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            FieldInfo trajectoryField = typeof(Archery).GetField("trajectoryCalculator",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                            if (!ReferenceEquals(arrowPrefabField, null))
                                arrowPrefabField.SetValue(component, arrowPrefabField.GetValue(template));
                            if (!ReferenceEquals(drawSoundField, null))
                                drawSoundField.SetValue(component, drawSoundField.GetValue(template));
                            if (!ReferenceEquals(shootSoundField, null))
                                shootSoundField.SetValue(component, shootSoundField.GetValue(template));
                            if (!ReferenceEquals(trajectoryField, null))
                                trajectoryField.SetValue(component, trajectoryField.GetValue(template));

                            Plugin.Logger.LogInfo("[Titan] 成功从重装弓箭手复制箭矢/声音模板");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[Titan] 获取重装弓箭手模板失败: " + ex.Message);
            }

            // 调整射击参数
            FieldInfo archerySettingsField = typeof(Archery).GetField("_archerySettings",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (!ReferenceEquals(archerySettingsField, null))
            {
                var settings = archerySettingsField.GetValue(component);
                if (settings != null && settings is Array settingsArray)
                {
                    Type settingType = settingsArray.GetType().GetElementType();
                    if (!ReferenceEquals(settingType, null))
                    {
                        FieldInfo cooldownField = settingType.GetField("cooldown",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        FieldInfo spreadField = settingType.GetField("spread",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        FieldInfo attackSettingsField = settingType.GetField("attackSettings",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        for (int i = 0; i < settingsArray.Length; i++)
                        {
                            object setting = settingsArray.GetValue(i);
                            if (ReferenceEquals(setting, null)) continue;

                            if (!ReferenceEquals(cooldownField, null))
                            {
                                float cd = (float)cooldownField.GetValue(setting);
                                cooldownField.SetValue(setting, cd * ARCHER_COOLDOWN_MULT);
                            }
                            if (!ReferenceEquals(spreadField, null))
                            {
                                float sp = (float)spreadField.GetValue(setting);
                                spreadField.SetValue(setting, sp * ARCHER_SPREAD_MULT);
                            }
                            if (!ReferenceEquals(attackSettingsField, null))
                            {
                                object atk = attackSettingsField.GetValue(setting);
                                if (!ReferenceEquals(atk, null))
                                {
                                    Type atkType = atk.GetType();
                                    FieldInfo dmgField = atkType.GetField("damage",
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    FieldInfo kbField = atkType.GetField("knockback",
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    FieldInfo stField = atkType.GetField("stun",
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                                    if (!ReferenceEquals(dmgField, null))
                                        dmgField.SetValue(atk, (float)dmgField.GetValue(atk) * ARCHER_DAMAGE_MULT);
                                    if (!ReferenceEquals(kbField, null))
                                        kbField.SetValue(atk, (float)kbField.GetValue(atk) * ARCHER_KNOCKBACK_MULT);
                                    if (!ReferenceEquals(stField, null))
                                        stField.SetValue(atk, (float)stField.GetValue(atk) * ARCHER_STUN_MULT);
                                }
                            }
                        }
                    }
                }
            }

            component.Setup();
            SetAgentArmor(agent, ArcherArmorLevels);

            Debugger.Log(Plugin.EnableGameplayLog, "[Titan] 弓箭手泰坦化完成: damageMult=" + ARCHER_DAMAGE_MULT + ", spreadMult=" + ARCHER_SPREAD_MULT);
        }

        /// <summary>
        /// 泰坦化单个 Agent
        /// </summary>
        private void Titanize(Agent agent)
        {
            if (ReferenceEquals(agent, null)) return;

            agent.scale = SCALE;
            SetAgentStunMultiplier(agent, STUN_MULTIPLIER);

            Swordsman swordsman = agent.GetComponent<Swordsman>();
            Archery archery = agent.GetComponent<Archery>();

            if (swordsman != null)
            {
                TitanizeSwordsman(agent);
            }
            else if (archery != null)
            {
                TitanizeArchery(agent);
            }
            else
            {
                Debugger.Log(Plugin.EnableGameplayLog, "[Titan] " + agent.name + " 既非步兵也非弓箭手，跳过泰坦化");
            }
        }

        // ── 主入口 ──
        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            // 泰坦特质需要小队等级 >= 1 才能生效
            if (squad.level < 1)
            {
                Debugger.Log(Plugin.EnableGameplayLog, "[Titan] 小队等级 " + squad.level + " < 1，泰坦效果未激活");
                return;
            }

            // 减少小队人数（因为个体更强）
            squad.maxCount = squad.maxCount / 2 + 1;

            // 为新生成的 Agent 应用泰坦化（防重复订阅）
            squad.onAgentCreated -= this.Titanize;
            squad.onAgentCreated += this.Titanize;

            // 对现有 Agent 应用泰坦化
            foreach (Agent agent in squad.agents)
            {
                this.Titanize(agent);
            }

            Debugger.Log(Plugin.EnableGameplayLog, string.Format("[Titan] 已应用到小队 {0}，小队人数: {1}", squad.name, squad.maxCount));
        }
    }
}