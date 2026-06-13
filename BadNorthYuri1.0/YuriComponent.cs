// Author: ABaLaQiYaShanMaiI
using System;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthYuri
{
    /// <summary>
    /// 心灵冲击组件 - 忠实还原魔改版 FancyTraits/YuriComponent.cs
    /// 按等级周期性释放心灵冲击，对附近敌人造成伤害+眩晕。
    /// 技能数值随等级提高，冷却随剩余人数减少（≤6人时冷却×0.8）。
    /// </summary>
    public class YuriComponent : AgentComponent
    {
        /// <summary>各等级冷却时间</summary>
        public float[] cooldown = new float[] { 3.5f, 2.8f, 2.2f, 1.8f };

        /// <summary>各等级心灵冲击属性 (damage, knockback, launchImpulse, stun)</summary>
        private AttackSettings[] psipower = new AttackSettings[]
        {
            new AttackSettings(1f, 5f, 0f, 4f),
            new AttackSettings(1.5f, 6f, 0f, 6f),
            new AttackSettings(2f, 7f, 0f, 8f),
            new AttackSettings(2.5f, 8f, 0f, 30f)
        };

        /// <summary>当前冷却计时器</summary>
        private float cooldowntimer;

        /// <summary>各等级释放范围</summary>
        public float[] psirange = new float[] { 2f, 3f, 4f, 4.5f };

        /// <summary>是否启用心灵冲击（由 Yuri 特质设置）</summary>
        public bool PSIswitch;

        /// <summary>是否启用心灵加速器（由 SpeedUp 道具设置，冷却减半）</summary>
        public bool PSImdf;

        private void Update()
        {
            if (this.PSIswitch)
            {
                int num = Mathf.Clamp(base.agent.squad.level, 0, this.cooldown.Length - 1);

                if (this.cooldowntimer >= 0f)
                {
                    this.cooldowntimer -= Time.deltaTime;
                    return;
                }

                if (base.agent.enemyDist <= this.psirange[num] && base.agent.enemyAgent)
                {
                    this.cooldowntimer = this.cooldown[num];

                    // 人数 ≤6 时冷却加快 20%
                    if (base.agent.squad.livingAgents.Count <= 6)
                    {
                        this.cooldowntimer *= 0.8f;
                    }

                    // 心灵加速器效果：冷却减半
                    if (this.PSImdf)
                    {
                        this.cooldowntimer *= 0.5f;
                    }

                    AttackSettings settings = this.psipower[num];

                    // 播放特效
                    ScriptableObjectSingleton<PrefabManager>.instance.plungeLandEffect.PlayAt(base.agent.enemyAgent.chestPos);
                    ScriptableObjectSingleton<PrefabManager>.instance.bloodSplash.PlayAt(base.agent.enemyAgent.chestPos);

                    // 对敌人造成心灵冲击伤害
                    base.agent.enemyAgent.DealDamage(new Attack(
                        settings,
                        base.agent.lookDir,
                        base.agent.chestPos,
                        this,
                        base.agent.squad,
                        null,
                        null
                    ));
                }
            }
        }
    }
}