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
        public static readonly string Slash_ID = "Hero_Trait_Slash";

        public SweepingBlade()
        {
            Plugin.Logger.LogInfo("Slash CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = HeroUpgradeTypeEnum.Trait;
            this.upgradeType.canBeStartItem = true;
            this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
            this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
            this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
            this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
            this.affectsPortrait = false;
            base.name = Slash_ID;
            this.nameTerm = "YYYYY/TRAIT/SLASH/NAME";
            this.shortDescription = "YYYYY/TRAIT/SLASH/DESCSHORT";
            this.infoSprite = CustomSprites.Sprites["trait_slash"];
            this.levels = new HeroUpgradeDefinition.Level[]
            {
                new HeroUpgradeDefinition.Level
                {
                    cost = 0,
                    description = "YYYYY/TRAIT/SLASH/DESC"
                }
            };
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            squad.heroAgent.GetComponent<Stun>().stunMultiplier = 0.1f;
            squad.heroAgent.GetOrAddComponent<SlashSword>();

            if (squad.level >= 1 && squad.minionPrefab.GetComponent<Swordsman>())
            {
                squad.minionPrefab.GetOrAddComponent<SlashSword>();
            }
        }
    }
}