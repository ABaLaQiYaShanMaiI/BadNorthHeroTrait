using System;
using System.Collections.Generic;
using System.Reflection;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthYuri
{
    /// <summary>
    /// 心灵精英 (Mind Elite) - 指挥官的强大精神力辐射整个小队。
    /// 英雄获得心灵光环，范围内所有友军获得伤害提升、攻击加速、精准度提高和恐惧免疫。
    /// </summary>
    public class MindElite : HeroUpgradeDefinition
    {
        public static readonly string MINDELITE_ID = "Hero_Trait_Yuri";

        // ── 光环效果参数 ──
        private const float DAMAGE_MULT = 1.4f;
        private const float ATTACK_SPEED_MULT = 1.25f; // 攻击冷却缩短
        private const float KNOCKBACK_MULT = 1.5f;
        private const float STUN_MULT = 1.5f;
        private const float MAX_SPEED_BUFF = 1.2f;      // 移速提升
        private const float FEAR_RESISTANCE = 0.05f;     // 恐惧抗性（接近免疫）

        // ── 英雄自身增强 ──
        private const float HERO_SCALE = 1.1f;
        private const float HERO_HEALTH_MULT = 1.5f;
        private const float HERO_DAMAGE_MULT = 1.75f;

        // ── 反射缓存 ──
        private static bool _fearFieldAttempted = false;
        private static FieldInfo _fearImmunityField = null;

        // ── 避免重复应用（按小队记录） ──
        private static HashSet<EnglishSquad> _mindEliteAppliedSquads = new HashSet<EnglishSquad>();

        public MindElite()
        {
            Debugger.Log("MINDELITE CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = (HeroUpgradeTypeEnum)4; // Trait
            this.upgradeType.canBeStartItem = true;
            this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
            this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
            this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
            this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
            this.affectsPortrait = false;
            base.name = MINDELITE_ID;
            this.nameTerm = "NACU/TRAIT/MIND/NAME";
            this.shortDescription = "NACU/TRAIT/MIND/DESCSHORT";
            this.infoSprite = CustomSprites.Sprites["trait_yuri"];
            HeroUpgradeDefinition.Level[] array = new HeroUpgradeDefinition.Level[1];
            int num = 0;
            HeroUpgradeDefinition.Level level = default(HeroUpgradeDefinition.Level);
            level.cost = 0;
            level.description = "NACU/TRAIT/MIND/DESC";
            array[num] = level;
            this.levels = array;
        }

        /// <summary>
        /// 安全获取或添加组件
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
        /// 设置恐惧免疫 - 通过反射设置 Agent 的 fear 相关字段
        /// </summary>
        private static void SetFearImmunity(Agent agent)
        {
            if (ReferenceEquals(agent, null)) return;

            try
            {
                // 尝试设置 fearImmunity
                if (!_fearFieldAttempted)
                {
                    _fearFieldAttempted = true;
                    _fearImmunityField = typeof(Agent).GetField("fearImmunity",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                if (!ReferenceEquals(_fearImmunityField, null))
                {
                    _fearImmunityField.SetValue(agent, FEAR_RESISTANCE);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[MindElite] 设置恐惧免疫失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 强化步兵单位
        /// </summary>
        private void EnhanceSwordsman(Agent agent)
        {
            Swordsman component = agent.GetComponent<Swordsman>();
            if (component == null) return;

            for (int i = 0; i < component.damageLevels.Length; i++)
            {
                component.damageLevels[i] *= DAMAGE_MULT;
            }
            for (int i = 0; i < component.knockbackLevels.Length; i++)
            {
                component.knockbackLevels[i] *= KNOCKBACK_MULT;
            }
            for (int i = 0; i < component.stunLevels.Length; i++)
            {
                component.stunLevels[i] *= STUN_MULT;
            }
            agent.maxSpeed *= MAX_SPEED_BUFF;

            Debugger.Log("[MindElite] 步兵心灵强化完成: dmg=" + DAMAGE_MULT + "x, spd=" + MAX_SPEED_BUFF + "x");
        }

        /// <summary>
        /// 强化弓箭手单位
        /// </summary>
        private void EnhanceArchery(Agent agent)
        {
            Archery component = agent.GetComponent<Archery>();
            if (component == null) return;

            agent.maxSpeed *= MAX_SPEED_BUFF;

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
                                cooldownField.SetValue(setting, cd * (1f / ATTACK_SPEED_MULT));
                            }
                            if (!ReferenceEquals(spreadField, null))
                            {
                                float sp = (float)spreadField.GetValue(setting);
                                spreadField.SetValue(setting, sp * 0.6f); // 更精准
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
                                        dmgField.SetValue(atk, (float)dmgField.GetValue(atk) * DAMAGE_MULT);
                                    if (!ReferenceEquals(kbField, null))
                                        kbField.SetValue(atk, (float)kbField.GetValue(atk) * KNOCKBACK_MULT);
                                    if (!ReferenceEquals(stField, null))
                                        stField.SetValue(atk, (float)stField.GetValue(atk) * STUN_MULT);
                                }
                            }
                        }
                    }
                }
            }

            component.Setup();
            Debugger.Log("[MindElite] 弓箭手心灵强化完成: dmg=" + DAMAGE_MULT + "x, atkSpd=" + ATTACK_SPEED_MULT + "x");
        }

        /// <summary>
        /// 强化矛兵单位（如果有 Spear 组件）
        /// </summary>
        private void EnhanceSpear(Agent agent)
        {
            Spear component = agent.GetComponent<Spear>();
            if (component == null) return;

            agent.maxSpeed *= MAX_SPEED_BUFF;

            // 尝试增加矛兵伤害
            try
            {
                FieldInfo damageField = typeof(Spear).GetField("damage",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!ReferenceEquals(damageField, null))
                {
                    float dmg = (float)damageField.GetValue(component);
                    damageField.SetValue(component, dmg * DAMAGE_MULT);
                }
            }
            catch { }

            Debugger.Log("[MindElite] 矛兵心灵强化完成");
        }

        /// <summary>
        /// 强化英雄自身
        /// </summary>
        private void EnhanceHero(Agent agent)
        {
            agent.scale = HERO_SCALE;
            agent.maxHealth *= HERO_HEALTH_MULT;
            agent.health = agent.maxHealth;

            Swordsman swordsman = agent.GetComponent<Swordsman>();
            if (swordsman != null)
            {
                for (int i = 0; i < swordsman.damageLevels.Length; i++)
                {
                    swordsman.damageLevels[i] *= HERO_DAMAGE_MULT;
                }
                for (int i = 0; i < swordsman.knockbackLevels.Length; i++)
                {
                    swordsman.knockbackLevels[i] *= KNOCKBACK_MULT;
                }
                for (int i = 0; i < swordsman.stunLevels.Length; i++)
                {
                    swordsman.stunLevels[i] *= STUN_MULT;
                }
            }

            // 英雄恐惧免疫
            SetFearImmunity(agent);

            Plugin.Logger.LogInfo("[MindElite] 英雄自身心灵强化完成: health=" + HERO_HEALTH_MULT + "x, dmg=" + HERO_DAMAGE_MULT + "x");
        }

        /// <summary>
        /// 心灵强化单个 Agent
        /// </summary>
        private void ApplyMindBuff(Agent agent)
        {
            if (ReferenceEquals(agent, null)) return;

            // 给所有单位加恐惧抗性
            SetFearImmunity(agent);

            Swordsman swordsman = agent.GetComponent<Swordsman>();
            Archery archery = agent.GetComponent<Archery>();
            Spear spear = agent.GetComponent<Spear>();

            if (swordsman != null)
            {
                EnhanceSwordsman(agent);
            }
            else if (archery != null)
            {
                EnhanceArchery(agent);
            }
            else if (spear != null)
            {
                EnhanceSpear(agent);
            }
            else
            {
                // 未知单位类型，仅应用移速和恐惧免疫
                agent.maxSpeed *= MAX_SPEED_BUFF;
                Debugger.Log("[MindElite] " + agent.name + " 未知兵种类型，仅应用基础心灵强化");
            }
        }

        // ── 主入口 ──
        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            if (_mindEliteAppliedSquads.Contains(squad))
            {
                Plugin.Logger.LogInfo("[MindElite] 心灵强化已应用于该小队，跳过");
                return;
            }

            // 强化英雄
            EnhanceHero(squad.heroAgent);

            // 为新生成的 Agent 应用心灵强化
            squad.onAgentCreated += this.ApplyMindBuff;

            // 对现有 Agent 应用心灵强化
            foreach (Agent agent in squad.agents)
            {
                this.ApplyMindBuff(agent);
            }
            foreach (Agent agent in squad.livingAgents)
            {
                if (!squad.agents.Contains(agent))
                {
                    this.ApplyMindBuff(agent);
                }
            }

            _mindEliteAppliedSquads.Add(squad);
            Plugin.Logger.LogInfo(string.Format("[MindElite] 已应用到小队 {0}", squad.name));
        }
    }
}