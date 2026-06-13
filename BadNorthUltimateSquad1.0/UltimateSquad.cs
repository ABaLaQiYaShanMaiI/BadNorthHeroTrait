// Author: ABaLaQiYaShanMaiI
using System;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Flag;

namespace BadNorthUltimateSquad
{
    /// <summary>
    /// 终极部队 (Ultimate Squad) - 忠实还原魔改版 FancyTraits/UltimateSquad.cs
    /// 英雄获得坦克化改造（复制 Tank 动画、scale=1.28、护甲{6,8,11,14}、高伤害/击退/眩晕、免疫击退），
    /// 额外获得购买折扣和额外使用次数；小兵获得温和的全面强化。
    /// </summary>
    public class UltimateSquad : HeroUpgradeDefinition, IAttackResponder
    {
        public static readonly string ULTIMATE_ID = "Hero_Trait_UltimateSquad";

        public UltimateSquad()
        {
            this.upgradeType = TraitHelper.CreateTraitUpgradeType();
            TraitHelper.SetupBaseDefinition(this, ULTIMATE_ID,
                "ABaLaQiYaShanMaiI/TRAIT/ULTIMATE/NAME",
                "ABaLaQiYaShanMaiI/TRAIT/ULTIMATE/DESCSHORT",
                CustomSprites.Sprites["trait_ultimatesquad"],
                TraitHelper.CreateSingleLevel("ABaLaQiYaShanMaiI/TRAIT/ULTIMATE/DESC"));
        }

        // ── 主入口：应用到小队 ──

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            this.HeroEffect(squad.heroAgent);
            squad.onAgentCreated -= this.UltimateEffect;
            squad.onAgentCreated += this.UltimateEffect;
        }

        // ── 购买时额外效果 ──

        public override void OnPurchased(HeroDefinition hero, int level)
        {
            hero.maxUsesPerTurn += 1;
            hero.discount = -0.5f;
            hero.discountType = HeroUpgradeTypeEnum.Class;
        }

        // ── 英雄坦克化改造 ──

        private void HeroEffect(Agent agent)
        {
            Swordsman component = agent.GetComponent<Swordsman>();
            if (component)
            {
                this.CopyTank(agent);
                agent.GetComponent<Animator>().speed *= 1.125f;
                float scale = agent.scale;
                agent.scale = 1.28f;
                agent.maxSpeed *= 1.3f;

                // 调整旗帜大小以补偿缩放变化
                FlagPole flagPole = agent.GetComponentInChildren<FlagPole>(true);
                if (flagPole != null)
                {
                    flagPole.transform.localScale *= scale / agent.scale;
                }

                int i = 0;
                int num = component.damageLevels.Length;
                while (i < num)
                {
                    component.damageLevels[i] *= 2.5f;
                    component.knockbackLevels[i] *= 5f;
                    component.stunLevels[i] *= 2f;
                    i++;
                }

                agent.GetComponent<Armor>().armor = new float[] { 6f, 8f, 11f, 14f };
                agent.GetComponent<Stun>().stunMultiplier = 0.1f;

                TankBrain tankBrain = (LevelStateObjectReferences.dict["Viking_Tank"] as VikingReference).vikingClone.agent.GetComponent<TankBrain>();
                component.swingSound = tankBrain.swingSound;
                component.swordSound = tankBrain.swordSound;

                Body tankBody = (LevelStateObjectReferences.dict["Viking_Tank"] as VikingReference).vikingClone.agent.GetComponent<Body>();
                agent.body.baseMoveSoundRef = tankBody.baseMoveSoundRef;

                if (!agent.attackResponders.Contains(this))
                {
                    agent.attackResponders.Add(this);
                }
            }
        }

        // ── 小兵全面强化 ──

        private void UltimateEffect(Agent agent)
        {
            agent.scale *= 1.1f;
            agent.GetComponent<Death>().deathSound = "Sfx/Viking/Twohanded/Die";

            Body tankBody = (LevelStateObjectReferences.dict["Viking_Tank"] as VikingReference).vikingClone.agent.GetComponent<Body>();
            agent.body.baseMoveSoundRef = tankBody.baseMoveSoundRef;

            agent.maxHealth *= 1.8f;
            agent.health = agent.maxHealth;
            agent.maxSpeed *= 1.25f;
            agent.GetComponent<Stun>().stunMultiplier = 0.5f;

            // ── 步兵 (Swordsman) ──
            Swordsman swordsman = agent.GetComponent<Swordsman>();
            if (swordsman)
            {
                agent.GetComponent<Animator>().speed *= 1.1f;

                TankBrain tankBrain = (LevelStateObjectReferences.dict["Viking_Tank"] as VikingReference).vikingClone.agent.GetComponent<TankBrain>();
                swordsman.swingSound = tankBrain.swingSound;
                swordsman.swordSound = tankBrain.swordSound;

                int i = 0;
                int num = swordsman.damageLevels.Length;
                while (i < num)
                {
                    swordsman.damageLevels[i] *= 1.3f;
                    swordsman.knockbackLevels[i] *= 1.5f;
                    swordsman.stunLevels[i] *= 1.5f;
                    i++;
                }
            }

            // ── 弓箭手 (Archery) ──
            Archery archery = agent.GetComponent<Archery>();
            if (archery)
            {
                int j = 0;
                int num2 = archery._archerySettings.Length;
                while (j < num2)
                {
                    archery._archerySettings[j].attackSettings.damage = archery._archerySettings[j].attackSettings.damage * 1.2f;
                    archery._archerySettings[j].attackSettings.stun = archery._archerySettings[j].attackSettings.stun * 1.3f;
                    archery._archerySettings[j].attackSettings.knockback = archery._archerySettings[j].attackSettings.knockback * 1.4f;
                    archery._archerySettings[j].spread = archery._archerySettings[j].spread * 0.5f;
                    archery._archerySettings[j].holdTime = archery._archerySettings[j].holdTime * 0.6f;
                    archery._archerySettings[j].cooldown = archery._archerySettings[j].cooldown * 0.8f;
                    j++;
                }
            }

            // ── 矛兵 (Spear) ──
            Spear spear = agent.GetComponent<Spear>();
            if (spear)
            {
                spear.spearLength *= 1.3f;
                agent.attackResponders.Remove(spear);

                int k = 0;
                int num3 = spear.attackSettings.Length;
                while (k < num3)
                {
                    spear.attackSettings[k].damage = spear.attackSettings[k].damage * 1.5f;
                    spear.attackSettings[k].stun = spear.attackSettings[k].stun * 1.5f;
                    spear.attackSettings[k].knockback = spear.attackSettings[k].knockback * 1.5f;
                    k++;
                }
            }
        }

        // ── 复制 Tank 动画 ──

        private void CopyTank(Agent myAgent)
        {
            if (myAgent.GetComponent<Swordsman>())
            {
                Animator tankAnimator = (LevelStateObjectReferences.dict["Viking_Tank"] as VikingReference).vikingClone.agent.GetComponent<Animator>();
                RuntimeAnimatorController tankController = tankAnimator.runtimeAnimatorController;
                Animator myAnimator = myAgent.GetComponent<Animator>();
                myAnimator.runtimeAnimatorController = tankController;
                this.CopyAnimatorParameters(tankAnimator, myAnimator);
                myAnimator.updateMode = tankAnimator.updateMode;
                myAnimator.cullingMode = tankAnimator.cullingMode;
                myAnimator.applyRootMotion = tankAnimator.applyRootMotion;
            }
        }

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

        // ── IAttackResponder：英雄免疫击退 ──

        public void ModifyAttack(ref Attack attack)
        {
            attack.knockback = 0f;
        }
    }
}