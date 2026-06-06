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
            Plugin.Logger.LogInfo("Yuri CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = HeroUpgradeTypeEnum.Trait;
            this.upgradeType.canBeStartItem = true;
            this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
            this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
            this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
            this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
            this.affectsPortrait = false;
            base.name = YURI_ID;
            this.nameTerm = "YYYYY/TRAIT/YURI/NAME";
            this.shortDescription = "YYYYY/TRAIT/YURI/DESCSHORT";
            this.infoSprite = CustomSprites.Sprites["trait_yuri"];
            this.levels = new HeroUpgradeDefinition.Level[]
            {
                new HeroUpgradeDefinition.Level
                {
                    cost = 0,
                    description = "YYYYY/TRAIT/YURI/DESC"
                }
            };
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            squad.heroAgent.GetOrAddComponent<YuriComponent>();
            squad.heroAgent.GetOrAddComponent<YuriComponent>().PSIswitch = true;
            squad.heroAgent.GetComponent<Stun>().stunMultiplier = 0.1f;
        }
    }
}