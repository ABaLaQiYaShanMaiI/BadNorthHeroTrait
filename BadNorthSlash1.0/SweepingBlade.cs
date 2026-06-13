// Author: ABaLaQiYaShanMaiI
using System;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthSlash
{
    /// <summary>
    /// 横扫之刃 (Slash / Sweeping Blade) - 忠实还原魔改版 FancyTraits/Slash.cs
    /// 英雄获得高眩晕抗性 (stunMultiplier=0.1)，并获得 SlashSword 组件（满血+溅射）。
    /// 当等级≥1且小兵为 Swordsman 时，小兵也获得 SlashSword 组件。
    /// </summary>
    public class SweepingBlade : HeroUpgradeDefinition
    {
        public static readonly string Slash_ID = "Hero_Trait_SlashV10";

        public SweepingBlade()
        {
            this.upgradeType = TraitHelper.CreateTraitUpgradeType();
            TraitHelper.SetupBaseDefinition(this, Slash_ID,
                "ABaLaQiYaShanMaiI/TRAIT/SLASH/NAME",
                "ABaLaQiYaShanMaiI/TRAIT/SLASH/DESCSHORT",
                CustomSprites.Sprites["trait_slash"],
                TraitHelper.CreateSingleLevel("ABaLaQiYaShanMaiI/TRAIT/SLASH/DESC"));
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            squad.heroAgent.GetComponent<Stun>().stunMultiplier = 0.1f;
            ComponentHelper.GetOrAddComponent<SlashSword>(squad.heroAgent.gameObject);

            if (squad.level >= 1 && squad.minionPrefab.GetComponent<Swordsman>())
            {
                ComponentHelper.GetOrAddComponent<SlashSword>(squad.minionPrefab.gameObject);
            }
        }
    }
}