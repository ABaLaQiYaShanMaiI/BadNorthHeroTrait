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

        private static void EnsureReflectionInit()
        {
            if (_upgradesField != null) return;

            Type metaInventoryType = typeof(Voxels.TowerDefense.ProfileInternals.MetaInventory);
            _upgradesField = metaInventoryType.GetField("upgrades", BindingFlags.Instance | BindingFlags.NonPublic);
            _upgradeEntryType = metaInventoryType.GetNestedType("UpgradeEntry", BindingFlags.NonPublic);

            if (_upgradeEntryType != null)
            {
                _upgradeEntryUpgradeField = _upgradeEntryType.GetField("upgrade");
                _upgradeEntryIsStartingField = _upgradeEntryType.GetField("isStarting");
                _upgradeEntryConstructor = _upgradeEntryType.GetConstructor(new Type[]
                {
                    typeof(HeroUpgradeDefinition),
                    typeof(bool)
                });
            }
        }

        private static IList GetUpgrades(Voxels.TowerDefense.ProfileInternals.MetaInventory self)
        {
            EnsureReflectionInit();
            return (IList)_upgradesField.GetValue(self);
        }

        private static object CreateUpgradeEntry(HeroUpgradeDefinition def, bool isStarting)
        {
            EnsureReflectionInit();
            return _upgradeEntryConstructor.Invoke(new object[] { def, isStarting });
        }

        private static object GetUpgradeEntryUpgrade(object entry)
        {
            EnsureReflectionInit();
            return _upgradeEntryUpgradeField.GetValue(entry);
        }

        private static bool GetUpgradeEntryIsStarting(object entry)
        {
            EnsureReflectionInit();
            return (bool)_upgradeEntryIsStartingField.GetValue(entry);
        }

        private static void SetUpgradeEntryIsStarting(object entry, bool value)
        {
            EnsureReflectionInit();
            _upgradeEntryIsStartingField.SetValue(entry, value);
        }

        internal static void ApplyHooks()
        {
            On.Voxels.TowerDefense.ProfileInternals.MetaInventory.hook_InitStartingUpgrades hook_InitStartingUpgrades;
            if ((hook_InitStartingUpgrades = O._0__MetaInventory_InitStartingUpgrades) == null)
            {
                hook_InitStartingUpgrades = (O._0__MetaInventory_InitStartingUpgrades = new On.Voxels.TowerDefense.ProfileInternals.MetaInventory.hook_InitStartingUpgrades(CustomTraits.MetaInventory_InitStartingUpgrades));
            }
            On.Voxels.TowerDefense.ProfileInternals.MetaInventory.InitStartingUpgrades += hook_InitStartingUpgrades;
        }

        private static void MetaInventory_InitStartingUpgrades(On.Voxels.TowerDefense.ProfileInternals.MetaInventory.orig_InitStartingUpgrades orig, Voxels.TowerDefense.ProfileInternals.MetaInventory self)
        {
            foreach (string traitID in CustomTraits.startingTraits)
            {
                HeroUpgradeDefinition registeredTrait = CustomTraits.GetRegisteredTrait(traitID);
                if (registeredTrait == null)
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

                if (upgradeEntry == null)
                {
                    GetUpgrades(self).Add(CreateUpgradeEntry(registeredTrait, true));
                }
                else if (!GetUpgradeEntryIsStarting(upgradeEntry))
                {
                    SetUpgradeEntryIsStarting(upgradeEntry, true);
                }
            }

            // 移除空引用条目
            var upgrades = GetUpgrades(self);
            List<int> ids = new List<int>();
            for (int i = 0; i < upgrades.Count; i++)
            {
                object entry = upgrades[i];
                object upgrade = GetUpgradeEntryUpgrade(entry);
                PropertyInfo defProp = upgrade.GetType().GetProperty("definition");
                object def = defProp.GetValue(upgrade);
                if (def == null)
                {
                    ids.Add(i);
                }
            }
            if (ids.Count > 0)
            {
                ids.Sort((a, b) => b.CompareTo(a));
                foreach (int id in ids)
                {
                    upgrades.RemoveAt(id);
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
