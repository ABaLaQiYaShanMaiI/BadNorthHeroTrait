// Author: ABaLaQiYaShanMaiI
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
            this.upgradeType = TraitHelper.CreateTraitUpgradeType();
            TraitHelper.SetupBaseDefinition(this, CHEAPERCLASS_ID,
                "ABaLaQiYaShanMaiI/TRAIT/CCLASS/NAME",
                "ABaLaQiYaShanMaiI/TRAIT/CCLASS/DESCSHORT",
                CustomSprites.Sprites["trait_cheaperclass"],
                TraitHelper.CreateSingleLevel("ABaLaQiYaShanMaiI/TRAIT/CCLASS/DESC"));
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
                    Plugin.Logger.LogInfo(string.Format("[CheaperClass] 处理 HeroDef {0}，共修改 {1}/{2} 个升级费用", heroName, modifiedUpgrades, totalUpgrades));
                    return true;
                }

                if (!found)
                {
                    Plugin.Logger.LogWarning(string.Format("[CheaperClass] HeroDef {0}：未找到 upgrades 列表字段", heroName));
                }
                return found;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning(string.Format("[CheaperClass] ApplyDiscountToHeroDef 异常: {0}", ex.Message));
                return false;
            }
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            Plugin.Logger.LogInfo(string.Format("[CheaperClass] OnAppliedToSquad entered - squad={0}, upgradeLevel={1}", squad != null ? squad.name : "null", upgradeLevel));

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
                    Plugin.Logger.LogInfo(string.Format("[CheaperClass] 已应用到小队 {0}，折扣应用成功", squad.name));
                }
                else
                {
                    Plugin.Logger.LogWarning(string.Format("[CheaperClass] 应用到小队 {0} 失败", squad.name));
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError("[CheaperClass] 应用折扣时出错: " + ex.Message);
            }
        }
    }
}