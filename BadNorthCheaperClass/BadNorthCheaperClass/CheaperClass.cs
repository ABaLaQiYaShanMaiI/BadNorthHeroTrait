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

        // 防止重复应用折扣
        private bool discountApplied = false;

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
        /// 当特质应用到实装小队时，通过反射修改英雄的升级定义，应用折扣
        /// 使用 OnAppliedToSquad 而非 OnAttachedToHero，因为后者不是虚方法
        /// </summary>
        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            // 防止重复应用
            if (discountApplied || squad == null || squad.hero == null)
            {
                Plugin.Logger.LogInfo("[CheaperClass] 跳过折扣应用 (已应用=" + discountApplied + ")");
                return;
            }

            try
            {
                var heroDef = squad.hero;

                // 自适应字段查找：遍历所有非静态字段，寻找名字包含 "upgrades"（复数）且实现了 IList 的字段
                // 使用复数形式避免误匹配其他包含 "upgrade" 但不相关的字段（如已购买的升级列表、已禁用的升级列表等）
                var heroDefType = typeof(HeroDefinition);
                var fields = heroDefType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                bool found = false;

                foreach (var field in fields)
                {
                    if (!field.Name.ToLower().Contains("upgrades"))
                        continue;
                    if (!typeof(System.Collections.IList).IsAssignableFrom(field.FieldType))
                        continue;

                    var upgradesList = field.GetValue(heroDef) as System.Collections.IList;
                    if (upgradesList == null || upgradesList.Count == 0)
                    {
                        Plugin.Logger.LogWarning(string.Format("[CheaperClass] 字段 {0} 为空或元素数量为0", field.Name));
                        continue;
                    }

                    // 遍历所有升级，修改其每个等级的费用
                    foreach (var item in upgradesList)
                    {
                        var upgrade = item as HeroUpgradeDefinition;
                        if (upgrade == null || upgrade == this) continue;

                        var levels = upgrade.levels;
                        if (levels == null) continue;

                        for (int i = 0; i < levels.Length; i++)
                        {
                            int originalCost = levels[i].cost;
                            int discountedCost = Mathf.RoundToInt(originalCost * (1f - DISCOUNT));
                            levels[i].cost = discountedCost;
                            Plugin.Logger.LogInfo(string.Format("[CheaperClass] {0} 等级{1} 费用 {2} -> {3}", upgrade.name, i, originalCost, discountedCost));
                        }
                    }

                    discountApplied = true;
                    found = true;
                    Plugin.Logger.LogInfo("[CheaperClass] 折扣应用成功");
                    return; // 找到并处理完后直接返回
                }

                if (!found)
                {
                    Plugin.Logger.LogWarning("[CheaperClass] 未找到任何包含 upgrades 的列表字段，可能特质的折扣功能无法生效");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError("[CheaperClass] 应用折扣时出错: " + ex.Message);
            }
        }
    }
}
