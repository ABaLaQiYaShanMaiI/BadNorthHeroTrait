using System;
using System.Collections.Generic;
using Voxels.TowerDefense;

namespace BadNorthSlash
{
    /// <summary>
    /// 横扫之刃组件 - 忠实还原魔改版 FancyTraits/SlashSword.cs
    /// 当步兵攻击激活后进入准备状态，在 pursuing/ready/hunting 状态下触发：
    /// 1. 恢复满血
    /// 2. 对 0.4m 内的额外敌人造成递减的溅射伤害（最多2个额外目标）
    /// 溅射效果逐次递减：击退 ×0.65，眩晕 ×0.8，伤害 ×0.8
    /// </summary>
    public class SlashSword : AgentComponent
    {
        /// <summary>横扫准备标志（当 Swordsman.attack.active 时置为 true）</summary>
        private bool Slashready;

        private void Update()
        {
            Swordsman component = base.agent.GetComponent<Swordsman>();

            // 当攻击激活时标记可横扫
            if (component.attack.active)
            {
                this.Slashready = true;
            }

            // 在追逐、就绪或狩猎状态下，如果已准备好，触发横扫
            if ((component.pursuing.active || component.ready.active || component.hunting.active) && this.Slashready)
            {
                // 先恢复满血
                base.agent.health = base.agent.maxHealth;

                // 获取 0.4m 范围内的敌方 Agent
                List<Agent> staticListRadiusSorted = AgentEnumerators.GetStaticListRadiusSorted(
                    base.agent.chestPos, 0.4f, base.agent.faction.enemy);

                if (staticListRadiusSorted.Count > 0)
                {
                    // 获取对第一个敌人的攻击模板
                    Attack attack = component.GetAttack(staticListRadiusSorted[0]);
                    attack.soundPrefix = string.Empty;
                    attack.monoAttacker = this;

                    int num = 0;
                    foreach (Agent agent in staticListRadiusSorted)
                    {
                        // 跳过主要目标
                        if (agent == component.target)
                            continue;

                        attack.pos = agent.chestPos;
                        attack.direction = base.agent.lookDir;
                        agent.DealDamage(attack);

                        // 溅射衰减
                        attack.knockback *= 0.65f;
                        attack.stun *= 0.8f;
                        attack.damage *= 0.8f;

                        // 最多额外攻击 2 个目标
                        if (++num >= 2)
                            break;
                    }
                }

                this.Slashready = false;
            }
        }
    }
}