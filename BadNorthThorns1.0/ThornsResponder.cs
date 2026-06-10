using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthThorns
{
    /// <summary>
    /// 荆棘反伤组件 - 将反伤行为从 HeroUpgradeDefinition 分离到独立 MonoBehaviour 组件。
    /// 每个 Agent 拥有自己的 ThornsResponder 实例，避免多 Agent 共享同一响应器，
    /// 同时支持按实例追踪状态并防止递归反伤。
    /// </summary>
    public class ThornsResponder : MonoBehaviour, IAttackResponder
    {
        /// <summary>用于标记此为荆棘产生的二次伤害，防止递归反伤</summary>
        private const string THORNS_SOUND_PREFIX = "Thorns_Retaliate";

        /// <summary>反伤音效前缀（可配置）</summary>
        public string soundPrefix = "Sfx/English/Sword";

        private bool _isDealingThornsDamage = false;

        private void Awake()
        {
            Agent agent = this.GetComponent<Agent>();
            if (!ReferenceEquals(agent, null))
            {
                if (!agent.attackResponders.Contains(this))
                {
                    agent.attackResponders.Add(this);
                }
            }
        }

        private void OnDestroy()
        {
            Agent agent = this.GetComponent<Agent>();
            if (!ReferenceEquals(agent, null))
            {
                agent.attackResponders.Remove(this);
            }
        }

        public void ModifyAttack(ref Attack attack)
        {
            // 防止递归：如果这是荆刺产生的二次伤害，直接跳过
            if (attack.soundPrefix == THORNS_SOUND_PREFIX)
                return;

            // 防止重入：当前正在处理荆棘伤害
            if (_isDealingThornsDamage)
                return;

            // 防御判断：攻击者是否有效
            if (ReferenceEquals(attack.monoAttacker, null))
                return;

            CloseCombatBrain closeCombatBrain = attack.monoAttacker as CloseCombatBrain;
            if (ReferenceEquals(closeCombatBrain, null))
                return;

            if (ReferenceEquals(closeCombatBrain.agent, null))
                return;

            // 攻击者已死亡/失效则跳过
            if (closeCombatBrain.agent.isDead)
                return;

            _isDealingThornsDamage = true;
            try
            {
                closeCombatBrain.agent.DealDamage(new Attack(
                    1.5f, 1f, 1f, -attack.direction, attack.pos,
                    closeCombatBrain, closeCombatBrain.enSquad,
                    THORNS_SOUND_PREFIX,
                    ScriptableObjectSingleton<PrefabManager>.instance.hitEffect
                ));
            }
            finally
            {
                _isDealingThornsDamage = false;
            }
        }
    }
}