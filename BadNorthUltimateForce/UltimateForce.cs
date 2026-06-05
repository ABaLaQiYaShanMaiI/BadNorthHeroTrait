using System;
using System.Reflection;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthUltimateForce
{
    /// <summary>
    /// 终极部队 (Ultimate Force) - 打造超精英全能战士。
    /// 小队人数大幅减少，但每个单位都拥有巨人般的体型、极高的伤害和护甲。
    /// 所有单位获得飞斧投掷能力和跳劈能力。
    /// </summary>
    public class UltimateForce : HeroUpgradeDefinition
    {
        public static readonly string ULTIMATEFORCE_ID = "Hero_Trait_UltimateForce";

        // ── 通用属性 ──
        private const float SCALE = 1.4f;
        private const float HEALTH_MULT = 4f;
        private const float MAX_SPEED = 3f;
        private const float STUN_MULTIPLIER = 1E-06f; // 免疫眩晕

        // ── 步兵 (Swordsman) 增强 ──
        private const float SWORD_DAMAGE_MULT = 3f;
        private const float SWORD_KNOCKBACK_MULT = 3f;
        private const float SWORD_STUN_MULT = 3f;
        private readonly float[] EliteArmorLevels = { 10f, 15f, 20f, 25f };

        // ── 小队人数削减 ──
        private const float SQUAD_COUNT_RATIO = 0.33f; // 人数变为原来的1/3
        private const int MIN_SQUAD_COUNT = 2;          // 最少保留2人

        // ── 反射缓存 ──
        private static FieldInfo _armorField = null;
        private static bool _armorFieldAttempted = false;
        private static FieldInfo _stunField = null;
        private static bool _stunFieldAttempted = false;

        public UltimateForce()
        {
            Debugger.Log("ULTIMATEFORCE CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = (HeroUpgradeTypeEnum)4; // Trait
            this.upgradeType.canBeStartItem = true;
            this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
            this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
            this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
            this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
            this.affectsPortrait = false;
            base.name = ULTIMATEFORCE_ID;
            this.nameTerm = "NACU/TRAIT/ULTIMATE/NAME";
            this.shortDescription = "NACU/TRAIT/ULTIMATE/DESCSHORT";
            this.infoSprite = CustomSprites.Sprites["trait_ultimateforce"];
            HeroUpgradeDefinition.Level[] array = new HeroUpgradeDefinition.Level[1];
            int num = 0;
            HeroUpgradeDefinition.Level level = default(HeroUpgradeDefinition.Level);
            level.cost = 0;
            level.description = "NACU/TRAIT/ULTIMATE/DESC";
            array[num] = level;
            this.levels = array;
        }

        // ── 避免重复应用 ──
        private static bool _ultimateApplied = false;

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
        /// 设置护甲值
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
        }

        /// <summary>
        /// 设置眩晕免疫
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
        }

        /// <summary>
        /// 为步兵添加跳劈能力（从 Viking_Twohanded 获取模板）
        /// </summary>
        private static void AddJumpAttack(Agent agent)
        {
            Swordsman swordsman = agent.GetComponent<Swordsman>();
            if (swordsman == null) return;
            if (agent.GetComponent<JumpAttack>() != null) return;

            try
            {
                JumpAttack template = null;

                // 尝试从 LevelStateObjectReferences 获取
                if (!ReferenceEquals(LevelStateObjectReferences.dict, null) &&
                    LevelStateObjectReferences.dict.TryGetValue("Viking_Twohanded", out UnityEngine.Object reference) &&
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
                    catch { }

                    if (!ReferenceEquals(templateObj, null))
                    {
                        template = templateObj.GetComponent<JumpAttack>();
                    }
                }

                // 备用方案
                if (ReferenceEquals(template, null))
                {
                    JumpAttack[] allJumps = Resources.FindObjectsOfTypeAll<JumpAttack>();
                    if (!ReferenceEquals(allJumps, null) && allJumps.Length > 0)
                    {
                        foreach (var ja in allJumps)
                        {
                            if (!ReferenceEquals(ja.gameObject, agent.gameObject))
                            {
                                template = ja;
                                break;
                            }
                        }
                    }
                }

                if (!ReferenceEquals(template, null))
                {
                    JumpAttack newJump = agent.gameObject.AddComponent<JumpAttack>();
                    // 复制关键字段
                    FieldInfo[] fields = typeof(JumpAttack).GetFields(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (FieldInfo fi in fields)
                    {
                        if (fi.Name != "agent" && fi.Name != "enSquad" && !fi.Name.Contains("target"))
                        {
                            try { fi.SetValue(newJump, fi.GetValue(template)); }
                            catch { }
                        }
                    }
                    newJump.Setup(agent);

                    if (!swordsman.actions.Contains(newJump))
                    {
                        swordsman.actions.Add(newJump);
                    }

                    // 添加 JumpComponent
                    if (agent.GetComponent<JumpComponent>() == null)
                    {
                        agent.gameObject.AddComponent<JumpComponent>();
                    }

                    Debugger.Log("[UltimateForce] 为 " + agent.name + " 添加跳劈能力");
                }
                else
                {
                    Plugin.Logger.LogWarning("[UltimateForce] 无法获取 JumpAttack 模板，跳过跳劈添加");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[UltimateForce] 添加跳劈失败: " + ex.Message);
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

            // 添加跳劈
            AddJumpAttack(agent);

            Debugger.Log("[UltimateForce] 步兵终极强化完成: dmg=" + SWORD_DAMAGE_MULT + "x, kb=" + SWORD_KNOCKBACK_MULT + "x");
        }

        /// <summary>
        /// 强化弓箭手单位
        /// </summary>
        private void EnhanceArchery(Agent agent)
        {
            Archery component = agent.GetComponent<Archery>();
            if (component == null) return;

            // 尝试从重装弓箭手获取模板
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
                    catch { }

                    if (!ReferenceEquals(templateObj, null))
                    {
                        Archery template = templateObj.GetComponent<Archery>();
                        if (!ReferenceEquals(template, null))
                        {
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
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[UltimateForce] 强化弓箭手模板失败: " + ex.Message);
            }

            // 调整射击参数 - 更高伤害、更快冷却、更精准
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
                                cooldownField.SetValue(setting, cd * 0.7f); // 更快的冷却
                            }
                            if (!ReferenceEquals(spreadField, null))
                            {
                                float sp = (float)spreadField.GetValue(setting);
                                spreadField.SetValue(setting, sp * 0.3f); // 更精准
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

                                    if (!ReferenceEquals(dmgField, null))
                                        dmgField.SetValue(atk, (float)dmgField.GetValue(atk) * 2.5f);
                                    if (!ReferenceEquals(kbField, null))
                                        kbField.SetValue(atk, (float)kbField.GetValue(atk) * 2f);
                                }
                            }
                        }
                    }
                }
            }

            component.Setup();
            Debugger.Log("[UltimateForce] 弓箭手终极强化完成");
        }

        /// <summary>
        /// 执行单个 Agent 的终极强化
        /// </summary>
        private void ApplyUltimate(Agent agent)
        {
            if (ReferenceEquals(agent, null)) return;

            agent.scale = SCALE;
            agent.maxHealth *= HEALTH_MULT;
            agent.health = agent.maxHealth;
            agent.maxSpeed = MAX_SPEED;

            SetAgentStunMultiplier(agent, STUN_MULTIPLIER);
            SetAgentArmor(agent, EliteArmorLevels);

            Swordsman swordsman = agent.GetComponent<Swordsman>();
            Archery archery = agent.GetComponent<Archery>();

            if (swordsman != null)
            {
                EnhanceSwordsman(agent);
            }
            else if (archery != null)
            {
                EnhanceArchery(agent);
            }
            else
            {
                Debugger.Log("[UltimateForce] " + agent.name + " 不支持的兵种类型，仅应用基础强化");
            }

            // 更换移动和死亡音效为坦克风格（如果有 Swordsman）
            if (swordsman != null)
            {
                agent.hurtSound = "Sfx/English/Tank/Hurt";
                if (agent.body != null)
                {
                    agent.body.baseMoveSoundRef = "Sfx/English/Tank/Move";
                }
                Death death = agent.GetComponent<Death>();
                if (death != null)
                {
                    death.deathSound = "Sfx/English/Tank/Die";
                }
                swordsman.swordSound = "Sfx/English/Tank";
                swordsman.swingSound = "Sfx/English/Tank/Swing";
            }
        }

        // ── 主入口 ──
        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            if (_ultimateApplied)
            {
                Plugin.Logger.LogInfo("[UltimateForce] 终极强化已全局应用，跳过");
                return;
            }

            // 大幅削减小队人数
            int newCount = Mathf.Max(MIN_SQUAD_COUNT, Mathf.FloorToInt(squad.maxCount * SQUAD_COUNT_RATIO));
            squad.maxCount = newCount;
            Plugin.Logger.LogInfo("[UltimateForce] 小队人数调整为: " + newCount);

            // 为新生成的 Agent 应用强化
            squad.onAgentSpawned += this.ApplyUltimate;

            // 对现有 Agent 应用强化
            foreach (Agent agent in squad.agents)
            {
                this.ApplyUltimate(agent);
            }
            foreach (Agent agent in squad.livingAgents)
            {
                if (!squad.agents.Contains(agent))
                {
                    this.ApplyUltimate(agent);
                }
            }

            _ultimateApplied = true;
            Plugin.Logger.LogInfo(string.Format("[UltimateForce] 已应用到小队 {0}，精英人数: {1}", squad.name, newCount));
        }
    }
}