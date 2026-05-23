using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.upgrades;

namespace BadNorthCheaperClass
{
    public class CheaperClass : HeroTraitCheaperUpgrades
    {
        public static readonly string CHEAPERCLASS_ID = "Hero_Trait_CheaperClass";

        public CheaperClass()
        {
            Plugin.Logger.LogInfo("CHEAPERCLASS CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = 4;
            this.upgradeType.canBeStartItem = true;
            this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
            this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
            this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
            this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
            this.affectsPortrait = false;
            base.name = CHEAPERCLASS_ID;
            this.nameTerm = "NACU/TRAIT/CCLASS/NAME";
            this.shortDescription = "NACU/TRAIT/CCLASS/DESCSHORT";
            this.infoSprite = CustomSprites.Sprites["trait_cheaperclass"];
            HeroUpgradeDefinition.Level[] array = new HeroUpgradeDefinition.Level[1];
            int num = 0;
            HeroUpgradeDefinition.Level level = default(HeroUpgradeDefinition.Level);
            level.cost = 0;
            level.description = "NACU/TRAIT/CCLASS/DESC";
            array[num] = level;
            this.levels = array;
            this.discount = 0.4f;
            this.affectsType = 1;
        }
    }
}
