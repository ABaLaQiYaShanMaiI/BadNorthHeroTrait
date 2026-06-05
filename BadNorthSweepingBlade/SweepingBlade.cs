using System;
using System.Collections.Generic;
using System.Reflection;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthSweepingBlade
{
    /// <summary>
    /// 横扫之刃 (Sweeping Blade) - 所有近战单位的攻击变为范围伤害。
    /// 每次近战攻击会对其攻击方向扇形范围内的额外敌人造成溅射伤害。
    /// </summary>
    public class SweepingBlade : HeroUpgradeDefinition, IAttackResponder
    {
        public static readonly string SWEEPINGBLADE_ID = "Hero_Trait_SweepingBlade";

        // ── 横扫参数 ──
        private const float CLEAVE_RADIUS = 2.5f;       // 溅射范围
        private const float CLEAVE_ANGLE = 120f;          // 扫掠角度（度）
        private const float CLEAVE_DAMAGE_RATIO = 0.6f;   // 溅射伤害比例
        private const float CLEAVE_KNOCKBACK_MULT = 0.5f;  // 溅射击退比例

        // ── 英雄额外强化 ──
        private const float HERO_CLEAVE_RADIUS = 3.5f;
        private const float HERO_CLEAVE_DAMAGE_RATIO = 0.8f;

        // ── 冷却控制 ──
        private static Dictionary<Agent, float> _lastCleaveTime = new Dictionary<Agent, float>();
        private const float CLEAVE_COOLDOWN = 0.3f; // 同一单位至少间隔 x 秒才能再次触发横扫

        public SweepingBlade()
        {
            Debugger.Log("SWEEPINGBLADE CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = (HeroUpgradeTypeEnum)4; // Trait
            this.upgradeType.canBeStartItem = true;
            this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
            this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
            this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
            this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
            this.affectsPortrait = false;
            base.name = SWEEPINGBLADE_ID;
            this.nameTerm = "NACU/TRAIT/SWEEP/NAME";
            this.shortDescription = "NACU/TRAIT/SWEEP/DESCSHORT";
            this.infoSprite = CustomSprites.Sprites["trait_sweepingblade"];
            HeroUpgradeDefinition.Level[] array = new HeroUpgradeDefinition.Level[1];
            int num = 0;
            HeroUpgradeDefinition.Level level = default(HeroUpgradeDefinition.Level);
            level.cost = 0;
            level.description = "NACU/TRAIT/SWEEP/DESC";
            array[num] = level;
            this.levels = array;
        }

        /// <summary>
        /// 获取攻击来源的 Agent
        /// </summary>
        private static Agent GetAttackerAgent(MonoBehaviour monoAttacker)
        {
            if (ReferenceEquals(monoAttacker, null)) return null;

            Agent agent = monoAttacker as Agent;
            if (!ReferenceEquals(agent, null)) return agent;

            agent = monoAttacker.GetComponent<Agent>();
            if (!ReferenceEquals(agent, null)) return agent;

            agent = monoAttacker.GetComponentInParent<Agent>();
            if (!ReferenceEquals(agent, null)) return agent;

            // 尝试通过反射查找 agent 字段
            try
            {
                Type type = monoAttacker.GetType();
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (FieldInfo fi in fields)
                {
                    if (typeof(Agent).IsAssignableFrom(fi.FieldType))
                    {
                        Agent found = fi.GetValue(monoAttacker) as Agent;
                        if (!ReferenceEquals(found, null)) return found;
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 判断 Agent 是否属于敌方小队
        /// </summary>
        private static bool IsEnemy(Agent attacker, Agent target)
        {
            if (ReferenceEquals(attacker, null) || ReferenceEquals(target, null)) return false;
            if (ReferenceEquals(attacker, target)) return false;

            try
            {
                EnglishSquad attackerSquad = attacker.squad as EnglishSquad;
                EnglishSquad targetSquad = target.squad as EnglishSquad;

                if (ReferenceEquals(attackerSquad, null) || ReferenceEquals(targetSquad, null))
                    return false;

                // 同一小队不是敌人
                if (ReferenceEquals(attackerSquad, targetSquad))
                    return false;

                // 不同 EnglishSquad 即为敌人
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查是否在扇形范围内
        /// </summary>
        private static bool IsInCone(Vector3 origin, Vector3 direction, Vector3 targetPos, float radius, float angleDegrees)
        {
            float distance = Vector3.Distance(origin, targetPos);
            if (distance > radius) return false;

            Vector3 toTarget = (targetPos - origin).normalized;
            float dot = Vector3.Dot(direction, toTarget);
            float cosAngle = Mathf.Cos(angleDegrees * 0.5f * Mathf.Deg2Rad);

            return dot >= cosAngle;
        }

        /// <summary>
        /// 获取所有敌方目标
        /// </summary>
        private static List<Agent> GetAllEnemies(Agent attacker, Vector3 origin, Vector3 direction, float radius)
        {
            List<Agent> enemies = new List<Agent>();

            try
            {
                // 使用 Physics.OverlapSphere 或遍历所有 Agent
                Collider[] colliders = Physics.OverlapSphere(origin, radius);
                foreach (Collider col in colliders)
                {
                    if (ReferenceEquals(col, null)) continue;
                    Agent targetAgent = col.GetComponent<Agent>();
                    if (ReferenceEquals(targetAgent, null))
                        targetAgent = col.GetComponentInParent<Agent>();

                    if (ReferenceEquals(targetAgent, null)) continue;
                    if (targetAgent.health <= 0) continue;
                    if (!IsEnemy(attacker, targetAgent)) continue;
                    if (!IsInCone(origin, direction, targetAgent.transform.position, radius, CLEAVE_ANGLE)) continue;

                    if (!enemies.Contains(targetAgent))
                        enemies.Add(targetAgent);
                }
            }
            catch { }

            return enemies;
        }

        /// <summary>
        /// 判断是否为英雄（指挥官）
        /// </summary>
        private static bool IsHero(Agent agent)
        {
            if (ReferenceEquals(agent, null)) return false;
            try
            {
                if (!ReferenceEquals(agent.squad, null))
                {
                    EnglishSquad squad = agent.squad as EnglishSquad;
                    if (!ReferenceEquals(squad, null) && !ReferenceEquals(squad.heroAgent, null))
                    {
                        return ReferenceEquals(squad.heroAgent, agent);
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 执行横扫溅射伤害
        /// </summary>
        private void PerformCleave(Agent attacker, Attack originalAttack)
        {
            // 冷却检查
            if (_lastCleaveTime.ContainsKey(attacker))
            {
                if (Time.time - _lastCleaveTime[attacker] < CLEAVE_COOLDOWN)
                    return;
                _lastCleaveTime[attacker] = Time.time;
            }
            else
            {
                _lastCleaveTime[attacker] = Time.time;
            }

            // 判断是否为英雄以使用不同的参数
            bool isHero = IsHero(attacker);
            float cleaveRadius = isHero ? HERO_CLEAVE_RADIUS : CLEAVE_RADIUS;
            float damageRatio = isHero ? HERO_CLEAVE_DAMAGE_RATIO : CLEAVE_DAMAGE_RATIO;

            Vector3 origin = attacker.chestPos;
            Vector3 direction = originalAttack.direction;

            if (direction.magnitude < 0.01f)
            {
                direction = attacker.transform.forward;
            }

            List<Agent> targets = GetAllEnemies(attacker, origin, direction, cleaveRadius);

            if (targets.Count == 0)
            {
                if (Debugger.Enabled)
                    Debugger.Log("[SweepingBlade] 无横扫目标");
                return;
            }

            Debugger.Log(string.Format("[SweepingBlade] 横扫命中 {0} 个目标，半径={1}", targets.Count, cleaveRadius));

            foreach (Agent target in targets)
            {
                if (ReferenceEquals(target, null) || target.health <= 0) continue;

                float cleaveDamage = originalAttack.damage * damageRatio;
                float cleaveKnockback = originalAttack.knockback * CLEAVE_KNOCKBACK_MULT;
                float cleaveStun = originalAttack.stun * 0.5f;

                Vector3 targetDirection = (target.chestPos - origin).normalized;

                Attack cleaveAttack = new Attack(
                    cleaveDamage,
                    cleaveKnockback,
                    cleaveStun,
                    targetDirection,
                    target.chestPos,
                    attacker as MonoBehaviour,
                    attacker.squad,
                    "Sfx/English/Sword",
                    ScriptableObjectSingleton<PrefabManager>.instance.hitEffect
                );

                target.DealDamage(cleaveAttack);
            }
        }

        // ── IAttackResponder 实现 ──
        public void ModifyAttack(ref Attack attack)
        {
            CloseCombatBrain closeCombatBrain = attack.monoAttacker as CloseCombatBrain;
            JumpAttack jumpAttack = attack.monoAttacker as JumpAttack;

            if (closeCombatBrain != null)
            {
                Agent attacker = GetAttackerAgent(closeCombatBrain);
                if (!ReferenceEquals(attacker, null))
                {
                    PerformCleave(attacker, attack);
                }
            }

            if (jumpAttack != null)
            {
                Agent attacker = GetAttackerAgent(jumpAttack);
                if (!ReferenceEquals(attacker, null))
                {
                    PerformCleave(attacker, attack);
                }
            }
        }

        // ── 主入口 ──
        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            // 为所有新创建的 Agent 添加横扫响应器
            squad.onAgentCreated += (Agent agent) =>
            {
                if (!agent.attackResponders.Contains(this))
                {
                    agent.attackResponders.Add(this);
                    Debugger.Log(string.Format("[SweepingBlade] 已为 {0} 添加横扫响应器", agent.name));
                }
            };

            // 为现有 Agent 添加横扫响应器
            foreach (Agent agent in squad.agents)
            {
                if (!agent.attackResponders.Contains(this))
                {
                    agent.attackResponders.Add(this);
                }
            }
            foreach (Agent agent in squad.livingAgents)
            {
                if (!squad.agents.Contains(agent) && !agent.attackResponders.Contains(this))
                {
                    agent.attackResponders.Add(this);
                }
            }

            Plugin.Logger.LogInfo(string.Format("[SweepingBlade] 已应用到小队 {0}", squad.name));
        }

        /// <summary>
        /// 清理冷却记录（当组件被销毁时）
        /// 注意：仅清理属于已销毁 Squad 的条目，避免影响其他正在使用横扫的小队。
        /// </summary>
        private void OnDestroy()
        {
            try
            {
                // 清理已失效的引用（Agent 为 null 的条目）
                List<Agent> keysToRemove = new List<Agent>();
                foreach (var kvp in _lastCleaveTime)
                {
                    if (ReferenceEquals(kvp.Key, null))
                        keysToRemove.Add(kvp.Key);
                }
                foreach (Agent key in keysToRemove)
                {
                    _lastCleaveTime.Remove(key);
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("[SweepingBlade] OnDestroy 清理冷却记录异常: " + ex.Message);
            }
        }
    }
}