using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Voxels.TowerDefense;
using Voxels.TowerDefense.ProfileInternals;

namespace BadNorthAPI
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

        // 日志门控：反射/构造器失败只报一次
        private static bool _reflectionErrorLogged = false;
        private static bool _upgradesFieldErrorLogged = false;
        private static bool _constructorErrorLogged = false;
        private static bool _upgradeFieldErrorLogged = false;
        private static bool _isStartingFieldErrorLogged = false;

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

                Plugin.logger.LogInfo("[BadNorthAPI] Listing UpgradeEntry constructors:");
                foreach (ConstructorInfo ctor in _upgradeEntryType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    ParameterInfo[] parameters = ctor.GetParameters();
                    StringBuilder sb = new StringBuilder("[BadNorthAPI]   ctor(");
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(parameters[i].ParameterType.FullName);
                    }
                    sb.Append(")");
                    Plugin.logger.LogInfo(sb.ToString());
                }

                // 优先级明确的构造器匹配策略：
                // 优先匹配 (HeroUpgradeDefinition, bool)
                // 其次 (HeroUpgradeDefinition, int)
                // 再次 (HeroUpgradeDefinition, int, bool)
                // 最后才考虑 (HeroUpgradeDefinition, object) 等宽泛情况
                ConstructorInfo[] allCtors = _upgradeEntryType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                // Priority 1: (HeroUpgradeDefinition, bool)
                _upgradeEntryConstructor = FindConstructorWithParams(allCtors,
                    typeof(HeroUpgradeDefinition), typeof(bool));
                if (!ReferenceEquals(_upgradeEntryConstructor, null))
                {
                    Plugin.logger.LogInfo("[BadNorthAPI] Found UpgradeEntry constructor (Priority 1): (HeroUpgradeDefinition, bool)");
                }
                else
                {
                    // Priority 2: (HeroUpgradeDefinition, int)
                    _upgradeEntryConstructor = FindConstructorWithParams(allCtors,
                        typeof(HeroUpgradeDefinition), typeof(int));
                    if (!ReferenceEquals(_upgradeEntryConstructor, null))
                    {
                        Plugin.logger.LogInfo("[BadNorthAPI] Found UpgradeEntry constructor (Priority 2): (HeroUpgradeDefinition, int)");
                    }
                    else
                    {
                        // Priority 3: (HeroUpgradeDefinition, int, bool)
                        _upgradeEntryConstructor = FindConstructorWithParams(allCtors,
                            typeof(HeroUpgradeDefinition), typeof(int), typeof(bool));
                        if (!ReferenceEquals(_upgradeEntryConstructor, null))
                        {
                            Plugin.logger.LogInfo("[BadNorthAPI] Found UpgradeEntry constructor (Priority 3): (HeroUpgradeDefinition, int, bool)");
                        }
                        else
                        {
                            // Priority 4 (fallback): (HeroUpgradeDefinition, object)
                            _upgradeEntryConstructor = FindConstructorWithParams(allCtors,
                                typeof(HeroUpgradeDefinition), typeof(object));
                            if (!ReferenceEquals(_upgradeEntryConstructor, null))
                            {
                                Plugin.logger.LogInfo("[BadNorthAPI] Found UpgradeEntry constructor (Priority 4 - fallback): (HeroUpgradeDefinition, object)");
                            }
                        }
                    }
                }

                if (ReferenceEquals(_upgradeEntryConstructor, null))
                {
                    Plugin.logger.LogError("[BadNorthAPI] Failed to find compatible UpgradeEntry constructor. Custom traits will be disabled.");
                    _reflectionFailed = true;
                }
            }
            else
            {
                Plugin.logger.LogError("[BadNorthAPI] Failed to find nested type UpgradeEntry. Game version may be incompatible. Custom traits will be disabled.");
                _reflectionFailed = true;
            }
        }

        /// <summary>
        /// 在给定构造函数列表中精确匹配指定参数类型序列的构造函数
        /// </summary>
        private static ConstructorInfo FindConstructorWithParams(ConstructorInfo[] ctors, params Type[] paramTypes)
        {
            foreach (ConstructorInfo ctor in ctors)
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                if (parameters.Length != paramTypes.Length)
                    continue;

                bool match = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (!parameters[i].ParameterType.Equals(paramTypes[i]))
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return ctor;
            }
            return null;
        }

        private static IList GetUpgrades(Voxels.TowerDefense.ProfileInternals.MetaInventory self)
        {
            EnsureReflectionInit();
            if (_reflectionFailed || ReferenceEquals(_upgradesField, null))
            {
                if (!_upgradesFieldErrorLogged)
                {
                    _upgradesFieldErrorLogged = true;
                    Plugin.logger.LogError("[BadNorthAPI] Cannot get upgrades: reflection initialization failed. (此错误仅显示一次)");
                }
                return null;
            }
            return (IList)_upgradesField.GetValue(self);
        }

        private static object CreateUpgradeEntry(HeroUpgradeDefinition def, bool isStarting)
        {
            EnsureReflectionInit();
            if (_reflectionFailed || ReferenceEquals(_upgradeEntryConstructor, null))
            {
                if (!_constructorErrorLogged)
                {
                    _constructorErrorLogged = true;
                    Plugin.logger.LogError("[BadNorthAPI] Cannot create UpgradeEntry: constructor not found. Game version may be incompatible. (此错误仅显示一次)");
                }
                return null;
            }

            ParameterInfo[] parameters = _upgradeEntryConstructor.GetParameters();
            object[] args = new object[parameters.Length];

            // 第一个参数始终是 HeroUpgradeDefinition
            args[0] = def;

            // 根据参数数量动态构建
            if (parameters.Length == 2)
            {
                // 2参数: (HeroUpgradeDefinition, bool/int/object)
                if (parameters[1].ParameterType.Equals(typeof(bool)))
                    args[1] = isStarting;
                else if (parameters[1].ParameterType.Equals(typeof(int)))
                    args[1] = isStarting ? 1 : 0;
                else if (parameters[1].ParameterType.Equals(typeof(object)))
                    args[1] = (object)isStarting;
                else
                {
                    if (!_constructorErrorLogged)
                    {
                        _constructorErrorLogged = true;
                        Plugin.logger.LogError(string.Format("[BadNorthAPI] Unexpected 2-param UpgradeEntry constructor type: {0}. (此错误仅显示一次)",
                            parameters[1].ParameterType.FullName));
                    }
                    return null;
                }
            }
            else if (parameters.Length == 3)
            {
                // 3参数: (HeroUpgradeDefinition, int, bool)
                // 第二个参数是 int（等级索引？），第三个参数是 bool（isStarting）
                args[1] = 0; // 默认等级索引为0
                args[2] = isStarting;
            }
            else
            {
                if (!_constructorErrorLogged)
                {
                    _constructorErrorLogged = true;
                    Plugin.logger.LogError(string.Format("[BadNorthAPI] Unexpected UpgradeEntry constructor parameter count: {0}. (此错误仅显示一次)",
                        parameters.Length));
                }
                return null;
            }

            return _upgradeEntryConstructor.Invoke(args);
        }

        private static object GetUpgradeEntryUpgrade(object entry)
        {
            EnsureReflectionInit();
            if (_reflectionFailed || ReferenceEquals(_upgradeEntryUpgradeField, null))
            {
                if (!_upgradeFieldErrorLogged)
                {
                    _upgradeFieldErrorLogged = true;
                    Plugin.logger.LogError("[BadNorthAPI] Cannot get upgrade entry upgrade: reflection initialization failed. (此错误仅显示一次)");
                }
                return null;
            }
            return _upgradeEntryUpgradeField.GetValue(entry);
        }

        private static bool GetUpgradeEntryIsStarting(object entry)
        {
            EnsureReflectionInit();
            if (_reflectionFailed || ReferenceEquals(_upgradeEntryIsStartingField, null))
            {
                if (!_isStartingFieldErrorLogged)
                {
                    _isStartingFieldErrorLogged = true;
                    Plugin.logger.LogError("[BadNorthAPI] Cannot get upgrade entry isStarting: reflection initialization failed. (此错误仅显示一次)");
                }
                return false;
            }
            return (bool)_upgradeEntryIsStartingField.GetValue(entry);
        }

        private static void SetUpgradeEntryIsStarting(object entry, bool value)
        {
            EnsureReflectionInit();
            if (_reflectionFailed || ReferenceEquals(_upgradeEntryIsStartingField, null))
            {
                if (!_isStartingFieldErrorLogged)
                {
                    _isStartingFieldErrorLogged = true;
                    Plugin.logger.LogError("[BadNorthAPI] Cannot set upgrade entry isStarting: reflection initialization failed. (此错误仅显示一次)");
                }
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
            // 先执行原始逻辑，让原版初始化容器，再补丁自定义特质
            orig.Invoke(self);

            // 如果反射初始化失败，跳过所有自定义特性处理
            EnsureReflectionInit();
            if (_reflectionFailed)
            {
                return;
            }

            // 获取 upgrades 列表，如果为空则跳过
            IList upgrades = GetUpgrades(self);
            if (ReferenceEquals(upgrades, null))
            {
                return;
            }

            foreach (string traitID in CustomTraits.startingTraits)
            {
                HeroUpgradeDefinition registeredTrait = CustomTraits.GetRegisteredTrait(traitID);
                if (ReferenceEquals(registeredTrait, null))
                    continue;

                // 检查该特质是否已存在于 upgrades 列表中
                bool alreadyPresent = false;
                for (int i = 0; i < upgrades.Count; i++)
                {
                    object entry = upgrades[i];
                    object upgrade = GetUpgradeEntryUpgrade(entry);
                    if (!ReferenceEquals(upgrade, null))
                    {
                        PropertyInfo defProp = upgrade.GetType().GetProperty("definition");
                        if (!ReferenceEquals(defProp, null))
                        {
                            object def = defProp.GetValue(upgrade, null);
                            if (ReferenceEquals(def, registeredTrait))
                            {
                                alreadyPresent = true;
                                // 确保它是 starting 的
                                if (!GetUpgradeEntryIsStarting(entry))
                                {
                                    SetUpgradeEntryIsStarting(entry, true);
                                }
                                break;
                            }
                        }
                    }
                }

                if (!alreadyPresent)
                {
                    object newEntry = CreateUpgradeEntry(registeredTrait, true);
                    if (!ReferenceEquals(newEntry, null))
                    {
                        upgrades.Add(newEntry);
                        Plugin.logger.LogInfo("[BadNorthAPI] Added starting trait: " + traitID);
                    }
                    else
                    {
                        Plugin.logger.LogWarning("[BadNorthAPI] Skipping starting trait " + traitID + " because UpgradeEntry creation failed.");
                    }
                }
            }

            // 移除空引用条目（倒序删除避免索引偏移）
            List<int> idsToRemove = new List<int>();
            for (int i = 0; i < upgrades.Count; i++)
            {
                object entry = upgrades[i];
                object upgrade = GetUpgradeEntryUpgrade(entry);
                if (ReferenceEquals(upgrade, null))
                {
                    idsToRemove.Add(i);
                    continue;
                }
                PropertyInfo defProp = upgrade.GetType().GetProperty("definition");
                if (!ReferenceEquals(defProp, null))
                {
                    object def = defProp.GetValue(upgrade, null);
                    if (ReferenceEquals(def, null))
                    {
                        idsToRemove.Add(i);
                    }
                }
            }
            if (idsToRemove.Count > 0)
            {
                idsToRemove.Sort((a, b) => b.CompareTo(a));
                foreach (int id in idsToRemove)
                {
                    upgrades.RemoveAt(id);
                }
                Plugin.logger.LogInfo("[BadNorthAPI] Removed " + idsToRemove.Count + " null-reference upgrade entries.");
            }
        }

        public static HeroUpgradeDefinition GetRegisteredTrait(string traitID)
        {
            Dictionary<string, int> dict = ResourceList<HeroUpgradeDefinition>.dictionary;
            if (!dict.ContainsKey(traitID))
            {
                Plugin.logger.LogWarning("[BadNorthAPI] Cannot find registered trait with ID " + traitID + ".");
                return null;
            }
            int index = dict[traitID];
            List<HeroUpgradeDefinition> list = ResourceList<HeroUpgradeDefinition>.list;
            if (index < 0 || index >= list.Count)
            {
                Plugin.logger.LogWarning("[BadNorthAPI] Registered trait index out of range for ID " + traitID + ".");
                return null;
            }
            return list[index];
        }

        /// <summary>
        /// 注册自定义特质。
        /// 包含重复注册保护：如果 traitID 已存在，记录警告并跳过注册。
        /// </summary>
        /// <param name="trait">特质定义实例</param>
        /// <param name="traitID">特质唯一ID</param>
        /// <param name="alwaysUnlocked">是否总是解锁（作为起始特质）</param>
        public static void RegisterTrait(HeroUpgradeDefinition trait, string traitID, bool alwaysUnlocked = false)
        {
            // 空值保护
            if (ReferenceEquals(trait, null))
            {
                Plugin.logger.LogError("[BadNorthAPI] RegisterTrait: trait 为 null，跳过注册");
                return;
            }
            if (string.IsNullOrEmpty(traitID))
            {
                Plugin.logger.LogError("[BadNorthAPI] RegisterTrait: traitID 为空，跳过注册");
                return;
            }

            // 重复注册保护：检查 dictionary 是否已包含
            Dictionary<string, int> dict = ResourceList<HeroUpgradeDefinition>.dictionary;
            if (dict.ContainsKey(traitID))
            {
                Plugin.logger.LogWarning("[BadNorthAPI] RegisterTrait: traitID \"" + traitID + "\" 已经注册过，跳过重复注册");
                return;
            }

            // 检查 list 中是否已存在相同引用
            List<HeroUpgradeDefinition> list = ResourceList<HeroUpgradeDefinition>.list;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], trait))
                {
                    Plugin.logger.LogWarning("[BadNorthAPI] RegisterTrait: trait 实例已存在于 list 中（索引 " + i + "），跳过重复注册");
                    return;
                }
            }

            list.Add(trait);
            dict.Add(traitID, list.Count - 1);
            Plugin.logger.LogInfo("[BadNorthAPI] Registered trait " + traitID);

            if (alwaysUnlocked)
            {
                if (!CustomTraits.startingTraits.Contains(traitID))
                {
                    CustomTraits.startingTraits.Add(traitID);
                    Plugin.logger.LogInfo("[BadNorthAPI] Added trait " + traitID + " to starting traits!");
                }
                else
                {
                    Plugin.logger.LogWarning("[BadNorthAPI] startingTraits already contains " + traitID + ", 跳过重复添加");
                }
            }
        }

        public static List<string> startingTraits = new List<string>();
    }
}