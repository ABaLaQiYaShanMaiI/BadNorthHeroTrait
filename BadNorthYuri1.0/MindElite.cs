using System;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthYuri
{
    /// <summary>
    /// 心灵精英 (Yuri / Mind Elite) - 忠实还原魔改版 FancyTraits/Yuri.cs
    /// 给英雄添加 YuriComponent，该组件会按等级周期性释放心灵冲击，
    /// 对附近敌人造成伤害+眩晕。英雄获得高眩晕抗性 (stunMultiplier=0.1)。
    /// </summary>
    public class MindElite : HeroUpgradeDefinition
    {
        public static readonly string YURI_ID = "Hero_Trait_Yuri";

        public MindElite()
        {
            this.upgradeType = TraitHelper.CreateTraitUpgradeType();
            TraitHelper.SetupBaseDefinition(this, YURI_ID,
                "YYYYY/TRAIT/YURI/NAME",
                "YYYYY/TRAIT/YURI/DESCSHORT",
                CustomSprites.Sprites["trait_yuri"],
                TraitHelper.CreateSingleLevel("YYYYY/TRAIT/YURI/DESC"));
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            YuriComponent yuri = ComponentHelper.GetOrAddComponent<YuriComponent>(squad.heroAgent.gameObject);
            yuri.PSIswitch = true;
            squad.heroAgent.GetComponent<Stun>().stunMultiplier = 0.1f;
        }
    }
}