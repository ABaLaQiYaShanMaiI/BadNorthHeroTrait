using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Voxels.TowerDefense;
using Voxels.TowerDefense.ProfileInternals;

namespace BNAPI
{
    public static class CustomTraits
    {
        // 使用普通类名替代编译器生成的 <>O
        private static class O
        {
            public static On.Voxels.TowerDefense.ProfileInternals.MetaInventory.hook_InitStartingUpgrades _0__MetaInventory_InitStartingUpgrades;
        }

        // 反射获取 MetaInventory 的私有字段 upgrades
        private static FieldInfo _upgradesField;
        private static Type _upgradeEntryType;
        private static FieldInfo _upgradeEntryUpgradeField;
        private static FieldInfo _upgradeEntryIsStartingField;
        private static ConstructorInfo _upgradeEntryConstructor;
        private static bool _reflectionFailed = false;

        private static void EnsureReflectionInit()
        {
            // 使用 ReferenceEquals 避免 Mono 2.0 下 FieldInfo.op_Inequality 缺失问题
            if (!ReferenceEquals(_upgradesField, null)) return;
            if (_reflectionFailed) return;

            Type metaInventoryType = typeof(Voxels.TowerDefense.ProfileInternals.MetaInventory);
            _upgradesField = metaInventoryType.GetField("upgrades", BindingFlags.Instance | BindingFlags.NonPublic);
            _upgradeEntryType = metaInventoryType.GetNestedType("UpgradeEntry", BindingFlags.NonPublic);

            if (!ReferenceEquals(_upgradeEntryType, null))
            {
                _upgradeEntryUpgradeField = _upgradeEntryType.GetField("upgrade");
                _upgradeEntryIsStartingField = _upgradeEntryType.GetField("isStarting");
                _upgradeEntryConstructor = _upgradeEntryType.GetConstructor(new Type[]
                {
                    typeof(HeroUpgradeDefinition),
                    typeof(bool)
                });

                if (ReferenceEquals(_upgradeEntryConstructor, null))
                {
                    Plugin.logger.LogError("[BNAPI] Failed to find UpgradeEntry constructor. Game version may be incompatible. Custom traits will be disabled.");
                    _reflectionFailed = true;
                }
            }
            else
            {
                Plugin.logger.LogError("[BNAPI] Failed to find nested type UpgradeEntry. Game version may be incompatible. Custom traits will be disabled.");
                _reflectionFailed = true;
            }
        }

        private static IList GetUpgrades(Voxels.TowerDefense.ProfileInternals.MetaInventory self)
        {
            EnsureReflectionInit();
            if (_reflectionFailed || ReferenceEquals(_upgradesField, null))
            {
                Plugin.logger.LogError("[BNAPI] Cannot get upgrades: reflection initialization failed.");
                return null;
            }
            return (IList)_upgradesField.GetValue(self);
        }

        private static object CreateUpgradeEntry(HeroUpgradeDefinition def, bool isStarting)
        {
            EnsureReflectionInit();
            if (_reflectionFailed || ReferenceEquals(_upgradeEntryConstructor, null))
            {
                Plugin.logger.LogError("[BNAPI] Cannot create UpgradeEntry: constructor not found. Game version may be incompatible.");
                return null;
            }
            return _upgradeEntryConstructor.Invoke(new object[] { def, isStarting });
        }

        private static object GetUpgradeEntryUpgrade(object entry)
        {
            EnsureReflectionInit();
            if (_reflectionFailed || ReferenceEquals(_upgradeEntryUpgradeField, null))
            {
                Plugin.logger.LogError("[BNAPI] Cannot get upgrade entry upgrade: reflection initialization failed.");
                return null;
            }
            return _upgradeEntryUpgradeField.GetValue(entry);
        }

        private static bool GetUpgradeEntryIsStarting(object entry)
        {
            EnsureReflectionInit();
            if (_reflectionFailed || ReferenceEquals(_upgradeEntryIsStartingField, null))
            {
                Plugin.logger.LogError("[BNAPI] Cannot get upgrade entry isStarting: reflection initialization failed.");
                return false;
            }
            return (bool)_upgradeEntryIsStartingField.GetValue(entry);
        }

        private static void SetUpgradeEntryIsStarting(object entry, bool value)
        {
            EnsureReflectionInit();
            if (_reflectionFailed || ReferenceEquals(_upgradeEntryIsStartingField, null))
            {
                Plugin.logger.LogError("[BNAPI] Cannot set upgrade entry isStarting: reflection initialization failed.");
                return;
            }
            _upgradeEntryIsStartingField.SetValue(entry, value);
        }

        internal static void ApplyHooks()
        {
            On.Voxels.TowerDefense.ProfileInternals.MetaInventory.hook_InitStartingUpgrades hook_InitStartingUpgrades;
            if (ReferenceEquals(hook_InitStartingUpgrades = O._0__MetaInventory_InitStartingUpgrades, null))
            {
                hook_InitStartingUpgrades = (O._0__MetaInventory_InitStartingUpgrades = new On.Voxels.TowerDefense.ProfileInternals.MetaInventory.hook_InitStartingUpgrades(CustomTraits.MetaInventory_InitStartingUpgrades));
            }
            On.Voxels.TowerDefense.ProfileInternals.MetaInventory.InitStartingUpgrades += hook_InitStartingUpgrades;
        }

        private static void MetaInventory_InitStartingUpgrades(On.Voxels.TowerDefense.ProfileInternals.MetaInventory.orig_InitStartingUpgrades orig, Voxels.TowerDefense.ProfileInternals.MetaInventory self)
        {
            // 如果反射初始化失败，跳过所有自定义特性处理，直接调用原始方法
            EnsureReflectionInit();
            if (_reflectionFailed)
            {
                Plugin.logger.LogError("[BNAPI] Reflection initialization failed. Skipping custom traits processing.");
                orig.Invoke(self);
                return;
            }

            foreach (string traitID in CustomTraits.startingTraits)
            {
                HeroUpgradeDefinition registeredTrait = CustomTraits.GetRegisteredTrait(traitID);
                if (ReferenceEquals(registeredTrait, null))
                    continue;

                // 使用反射调用 Get 方法
                object upgradeEntry = null;
                try
                {
                    upgradeEntry = typeof(Voxels.TowerDefense.ProfileInternals.MetaInventory)
                        .GetMethod("Get", new Type[] { typeof(HeroUpgradeDefinition) })
                        .Invoke(self, new object[] { registeredTrait });
                }
                catch { }

                if (ReferenceEquals(upgradeEntry, null))
                {
                    object newEntry = CreateUpgradeEntry(registeredTrait, true);
                    if (!ReferenceEquals(newEntry, null))
                    {
                        IList upgrades = GetUpgrades(self);
                        if (!ReferenceEquals(upgrades, null))
                        {
                            upgrades.Add(newEntry);
                        }
                    }
                    else
                    {
                        Plugin.logger.LogWarning("[BNAPI] Skipping starting trait " + traitID + " because UpgradeEntry creation failed.");
                    }
                }
                else if (!GetUpgradeEntryIsStarting(upgradeEntry))
                {
                    SetUpgradeEntryIsStarting(upgradeEntry, true);
                }
            }

            // 移除空引用条目
            IList upgradesList = GetUpgrades(self);
            if (!ReferenceEquals(upgradesList, null))
            {
                List<int> ids = new List<int>();
                for (int i = 0; i < upgradesList.Count; i++)
                {
                    object entry = upgradesList[i];
                    object upgrade = GetUpgradeEntryUpgrade(entry);
                    if (ReferenceEquals(upgrade, null))
                    {
                        ids.Add(i);
                        continue;
                    }
                    PropertyInfo defProp = upgrade.GetType().GetProperty("definition");
                    if (!ReferenceEquals(defProp, null))
                    {
                        object def = defProp.GetValue(upgrade, null);
                        if (ReferenceEquals(def, null))
                        {
                            ids.Add(i);
                        }
                    }
                }
                if (ids.Count > 0)
                {
                    ids.Sort((a, b) => b.CompareTo(a));
                    foreach (int id in ids)
                    {
                        upgradesList.RemoveAt(id);
                    }
                }
            }

            orig.Invoke(self);
        }

        public static HeroUpgradeDefinition GetRegisteredTrait(string traitID)
        {
            if (!ResourceList<HeroUpgradeDefinition>.dictionary.ContainsKey(traitID))
            {
                Plugin.logger.LogWarning("Cannot find registered trait with ID " + traitID + ".");
                return null;
            }
            return ResourceList<HeroUpgradeDefinition>.dictionary[traitID];
        }

        public static void RegisterTrait(HeroUpgradeDefinition trait, string traitID, bool alwaysUnlocked = false)
        {
            ResourceList<HeroUpgradeDefinition>.list.Add(trait);
            ResourceList<HeroUpgradeDefinition>.dictionary.Add(traitID, ArrayExtensions.Last<HeroUpgradeDefinition>(ResourceList<HeroUpgradeDefinition>.list));
            Plugin.logger.LogInfo("Registered trait " + traitID);
            if (alwaysUnlocked)
            {
                CustomTraits.startingTraits.Add(traitID);
                Plugin.logger.LogInfo("Added trait " + traitID + " to starting traits!");
            }
        }

        public static List<string> startingTraits = new List<string>();
    }
}
