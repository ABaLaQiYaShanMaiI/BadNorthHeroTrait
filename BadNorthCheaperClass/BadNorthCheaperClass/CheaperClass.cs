using System;
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

            Plugin.Logger.LogInfo("[CheaperClass] OnAppliedToSquad entered - squad=" + (squad?.name ?? "null") + ", upgradeLevel=" + upgradeLevel);

            // 防止重复应用
            if (discountApplied)
            {
                Plugin.Logger.LogInfo("[CheaperClass] 跳过：折扣已应用 (discountApplied=true)");
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
                Plugin.Logger.LogInfo($"[CheaperClass] 开始处理英雄，小队名称={squad.name}，小队等级={squad.hero.squadLevel}");

                // 自适应字段查找：遍历所有非静态字段，寻找名字包含 "upgrades"（复数）且实现了 IList 的字段
                // 使用复数形式避免误匹配其他包含 "upgrade" 但不相关的字段（如已购买的升级列表、已禁用的升级列表等）
                var heroDefType = typeof(HeroDefinition);
                var fields = heroDefType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                bool found = false;

                Plugin.Logger.LogInfo($"[CheaperClass] HeroDefinition 共有 {fields.Length} 个字段");
                foreach (var field in fields)
                {
                    Plugin.Logger.LogInfo($"[CheaperClass]   字段: {field.Name} ({field.FieldType.Name})");
                }

                foreach (var field in fields)
                {
                    if (!field.Name.ToLower().Contains("upgrades"))
                    {
                        Plugin.Logger.LogInfo($"[CheaperClass] 跳过字段 {field.Name}：名称不包含 upgrades");
                        continue;
                    }
                    if (!typeof(System.Collections.IList).IsAssignableFrom(field.FieldType))
                    {
                        Plugin.Logger.LogInfo($"[CheaperClass] 跳过字段 {field.Name}：类型 {field.FieldType.Name} 不是 IList");
                        continue;
                    }

                    var upgradesList = field.GetValue(heroDef) as System.Collections.IList;
                    Plugin.Logger.LogInfo($"[CheaperClass] 找到候选字段 {field.Name}，元素数量={upgradesList?.Count ?? 0}");
                    if (upgradesList == null || upgradesList.Count == 0)
                    {
                        Plugin.Logger.LogWarning(string.Format("[CheaperClass] 字段 {0} 为空或元素数量为0", field.Name));
                        continue;
                    }

                    // 遍历所有升级，修改其每个等级的费用
                    int totalUpgrades = upgradesList.Count;
                    int modifiedUpgrades = 0;

                    // 反射缓存：SerializableHeroUpgrade 可能包含 definition/upgrade 字段指向 HeroUpgradeDefinition
                    Type serializableUpgradeType = null;
                    FieldInfo serializableDefinitionField = null;
                    FieldInfo serializableLevelsField = null;

                    foreach (var item in upgradesList)
                    {
                        if (item == null)
                        {
                            Plugin.Logger.LogInfo("[CheaperClass]   跳过 null 条目");
                            continue;
                        }

                        HeroUpgradeDefinition upgrade = item as HeroUpgradeDefinition;

                        // 如果元素不是 HeroUpgradeDefinition，尝试通过 SerializableHeroUpgrade 获取内部引用
                        if (upgrade == null)
                        {
                            Type itemType = item.GetType();
                            if (serializableUpgradeType == null || serializableUpgradeType != itemType)
                            {
                                serializableUpgradeType = itemType;
                                serializableDefinitionField = itemType.GetField("definition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (serializableDefinitionField == null)
                                    serializableDefinitionField = itemType.GetField("upgrade", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                // 也尝试直接获取 levels 字段
                                serializableLevelsField = itemType.GetField("levels", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            }

                            if (serializableDefinitionField != null)
                            {
                                upgrade = serializableDefinitionField.GetValue(item) as HeroUpgradeDefinition;
                            }

                            if (upgrade == null && serializableLevelsField != null)
                            {
                                // 直接操作 levels 数组（共享引用）
                                HeroUpgradeDefinition.Level[] itemLevels = serializableLevelsField.GetValue(item) as HeroUpgradeDefinition.Level[];
                                if (itemLevels != null)
                                {
                                    Plugin.Logger.LogInfo($"[CheaperClass]   通过序列化类型 {itemType.Name} 直接处理 levels，等级数={itemLevels.Length}");
                                    for (int i = 0; i < itemLevels.Length; i++)
                                    {
                                        int originalCost = itemLevels[i].cost;
                                        int discountedCost = Mathf.RoundToInt(originalCost * (1f - DISCOUNT));
                                        itemLevels[i].cost = discountedCost;
                                        Plugin.Logger.LogInfo(string.Format("[CheaperClass]   {0}(序列化) 等级{1} 费用 {2} -> {3}", itemType.Name, i, originalCost, discountedCost));
                                    }
                                    modifiedUpgrades++;
                                    continue;
                                }
                            }

                            if (upgrade == null)
                            {
                                Plugin.Logger.LogInfo($"[CheaperClass]   跳过 {itemType.Name}：无法获取 HeroUpgradeDefinition 引用");
                                continue;
                            }
                        }

                        if (upgrade == this)
                        {
                            Plugin.Logger.LogInfo("[CheaperClass]   跳过自身");
                            continue;
                        }

                        var levels = upgrade.levels;
                        if (levels == null)
                        {
                            Plugin.Logger.LogInfo($"[CheaperClass]   {upgrade.name} 无等级定义，跳过");
                            continue;
                        }

                        Plugin.Logger.LogInfo($"[CheaperClass]   处理 {upgrade.name}，等级数={levels.Length}");
                        for (int i = 0; i < levels.Length; i++)
                        {
                            int originalCost = levels[i].cost;
                            int discountedCost = Mathf.RoundToInt(originalCost * (1f - DISCOUNT));
                            levels[i].cost = discountedCost;
                            Plugin.Logger.LogInfo(string.Format("[CheaperClass]   {0} 等级{1} 费用 {2} -> {3}", upgrade.name, i, originalCost, discountedCost));
                        }
                        modifiedUpgrades++;
                    }

                    discountApplied = true;
                    found = true;
                    Plugin.Logger.LogInfo($"[CheaperClass] 已应用到小队 {squad.name}，共处理 {modifiedUpgrades}/{totalUpgrades} 个升级，折扣应用成功");
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
