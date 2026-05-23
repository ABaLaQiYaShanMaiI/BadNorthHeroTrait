using System.Collections.Generic;
using System.Reflection;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthCheaperClass
{
    public class CheaperClass : HeroUpgradeDefinition
    {
        public static readonly string CHEAPERCLASS_ID = "Hero_Trait_CheaperClass";

        // 折扣率：40% off
        private const float DISCOUNT = 0.4f;

        public CheaperClass()
        {
            Plugin.Logger.LogInfo("CHEAPERCLASS CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = (HeroUpgradeTypeEnum)4;
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
        }

        /// <summary>
        /// 当特质附加到英雄时，通过反射修改英雄的升级定义，应用折扣
        /// </summary>
        public override void OnAttachedToHero(HeroDefinition hero, int level)
        {
            base.OnAttachedToHero(hero, level);

            try
            {
                // 通过反射获取 hero 的 upgrades 列表
                var upgradesField = typeof(HeroDefinition).GetField("upgrades",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (upgradesField == null)
                {
                    Plugin.Logger.LogWarning("[CheaperClass] 无法找到 HeroDefinition.upgrades 字段");
                    return;
                }

                var upgrades = upgradesField.GetValue(hero) as IList<HeroUpgradeDefinition>;
                if (upgrades == null)
                {
                    Plugin.Logger.LogWarning("[CheaperClass] upgrades 为空或类型不匹配");
                    return;
                }

                // 遍历所有升级，修改其每个等级的费用
                foreach (var upgrade in upgrades)
                {
                    if (upgrade == null || upgrade == this) continue;

                    var levels = upgrade.levels;
                    if (levels == null) continue;

                    for (int i = 0; i < levels.Length; i++)
                    {
                        int originalCost = levels[i].cost;
                        int discountedCost = Mathf.RoundToInt(originalCost * (1f - DISCOUNT));
                        levels[i].cost = discountedCost;
                        Plugin.Logger.LogInfo($"[CheaperClass] 升级 {upgrade.name} 等级 {i} 费用: {originalCost} -> {discountedCost}");
                    }
                }

                Plugin.Logger.LogInfo("[CheaperClass] 成功应用 40% 折扣到所有英雄升级");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError("[CheaperClass] 应用折扣时出错: " + ex.Message);
            }
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            Plugin.Logger.LogInfo("[CheaperClass] Applied 40% discount to hero upgrades");
        }
    }
}
