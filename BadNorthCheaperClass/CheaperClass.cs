using System;
using System.Collections.Generic;
using System.Reflection;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthCheaperClass
{
    public class CheaperClass : HeroUpgradeDefinition
    {
        public static readonly string CHEAPERCLASS_ID = "Hero_Trait_CheaperClass";

        private const float DISCOUNT = 0.4f;

        private bool discountApplied = false;
        private static bool _globalDiscountApplied = false;

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

        public static bool ApplyDiscountToHeroDef(HeroDefinition heroDef)
        {

            if (ReferenceEquals(heroDef, null))
            {
                Plugin.Logger.LogWarning("[CheaperClass] ApplyDiscountToHeroDef: heroDef 为 null");
                return false;
            }

            try
            {
                string heroName = "(unknown)";

                try 
                { 
                    var nameProp = typeof(HeroDefinition).GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (!ReferenceEquals(nameProp, null))
                        heroName = nameProp.GetValue(heroDef, null) as string ?? "(unknown)";
                    else
                    {
                        var nameField = typeof(HeroDefinition).GetField("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (!ReferenceEquals(nameField, null))
                            heroName = nameField.GetValue(heroDef) as string ?? "(unknown)";
                    }
                } 
                catch { }

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
                    if (ReferenceEquals(upgradesList, null) || upgradesList.Count == 0)
                        continue;

                    int totalUpgrades = upgradesList.Count;
                    int modifiedUpgrades = 0;

                    Type serializableUpgradeType = null;
                    FieldInfo serializableDefinitionField = null;
                    FieldInfo serializableLevelsField = null;
                    FieldInfo serializableUpgradeField = null;

                    foreach (var item in upgradesList)
                    {
                        if (ReferenceEquals(item, null))
                            continue;

                        HeroUpgradeDefinition upgrade = item as HeroUpgradeDefinition;

                        if (ReferenceEquals(upgrade, null))
                        {
                            Type itemType = item.GetType();
                            if (ReferenceEquals(serializableUpgradeType, null) || !ReferenceEquals(serializableUpgradeType, itemType))
                            {
                                serializableUpgradeType = itemType;
                                serializableDefinitionField = itemType.GetField("definition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                serializableUpgradeField = itemType.GetField("upgrade", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (ReferenceEquals(serializableDefinitionField, null))
                                    serializableDefinitionField = serializableUpgradeField;
                                serializableLevelsField = itemType.GetField("levels", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            }

                            if (!ReferenceEquals(serializableDefinitionField, null))
                                upgrade = serializableDefinitionField.GetValue(item) as HeroUpgradeDefinition;

                            if (ReferenceEquals(upgrade, null) && !ReferenceEquals(serializableLevelsField, null))
                            {
                                HeroUpgradeDefinition.Level[] itemLevels = serializableLevelsField.GetValue(item) as HeroUpgradeDefinition.Level[];
                                if (!ReferenceEquals(itemLevels, null))
                                {
                                    for (int i = 0; i < itemLevels.Length; i++)
                                    {
                                        int originalCost = itemLevels[i].cost;
                                        int discountedCost = Mathf.RoundToInt(originalCost * (1f - DISCOUNT));
                                        itemLevels[i].cost = discountedCost;
                                    }
                                    modifiedUpgrades++;
                                    continue;
                                }
                            }

                            if (ReferenceEquals(upgrade, null))
                                continue;
                        }

                        if (upgrade is CheaperClass)
                        {
                            Plugin.Logger.LogInfo("[CheaperClass]   跳过自身");
                            continue;
                        }

                        var levels = upgrade.levels;
                        if (ReferenceEquals(levels, null))
                            continue;

                        for (int i = 0; i < levels.Length; i++)
                        {
                            int originalCost = levels[i].cost;
                            int discountedCost = Mathf.RoundToInt(originalCost * (1f - DISCOUNT));
                            levels[i].cost = discountedCost;
                        }
                        modifiedUpgrades++;
                    }

                    found = true;
                    Plugin.Logger.LogInfo($"[CheaperClass] 处理 HeroDef {heroName}，共修改 {modifiedUpgrades}/{totalUpgrades} 个升级费用");
                    return true;
                }

                if (!found)
                {
                    Plugin.Logger.LogWarning($"[CheaperClass] HeroDef {heroName}：未找到 upgrades 列表字段");
                }
                return found;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[CheaperClass] ApplyDiscountToHeroDef 异常: {ex.Message}");
                return false;
            }
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            Plugin.Logger.LogInfo("[CheaperClass] OnAppliedToSquad entered - squad=" + (squad?.name ?? "null") + ", upgradeLevel=" + upgradeLevel);

            if (discountApplied)
            {
                Plugin.Logger.LogInfo("[CheaperClass] 跳过：实例折扣已应用 (discountApplied=true)");
                return;
            }
            if (_globalDiscountApplied)
            {
                Plugin.Logger.LogInfo("[CheaperClass] 跳过：全局折扣已应用 (_globalDiscountApplied=true)");
                return;
            }
            if (ReferenceEquals(squad, null))
            {
                Plugin.Logger.LogWarning("[CheaperClass] 跳过：squad 为 null");
                return;
            }
            if (ReferenceEquals(squad.hero, null))
            {
                Plugin.Logger.LogWarning("[CheaperClass] 跳过：squad.hero 为 null");
                return;
            }

            try
            {
                var heroDef = squad.hero;

                bool result = ApplyDiscountToHeroDef(heroDef);
                if (result)
                {
                    discountApplied = true;
                    _globalDiscountApplied = true;
                    Plugin.Logger.LogInfo($"[CheaperClass] 已应用到小队 {squad.name}，折扣应用成功");
                }
                else
                {
                    Plugin.Logger.LogWarning($"[CheaperClass] 应用到小队 {squad.name} 失败");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError("[CheaperClass] 应用折扣时出错: " + ex.Message);
            }
        }
    }
}
