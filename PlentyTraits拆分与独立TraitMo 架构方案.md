# PlentyTraits 拆分与独立 Trait Mod 架构方案

## 背景与目标

PlentyTraits 目前为一个包含 4 个特质的单 DLL Mod。本方案旨在将其拆分为 4 个独立的 Mod（BadNorthCheaperClass、BadNorthRegenerative、BadNorthThorns、BadNorthAxeThrower），每个 Mod 仅负责一个特质，且均保持对 MMHOOK 的高度利用，确保 **不修改任何原始游戏文件**，并尽可能实现多游戏版本兼容。BNAPI 将作为共享 API 层被所有 Mod 引用，尽量不修改其代码。

## 核心理念

- **单一职责**：每个特质 DLL 完全独立，仅包含该特质的定义、图标、本地化文本及相关辅助组件。
- **公共基础**：BNAPI 负责提供特质注册、精灵加载、本地化注入等通用能力，所有特质 Mod 均强依赖 BNAPI。
- **零侵入性**：所有运行时修改均通过 MMHOOK 生成的委托实现，不对 Assembly-CSharp.dll 做任何直接或间接修改。
- **版本解耦**：通过接口与反射技巧，降低 Mod 对特定游戏版本的硬编码依赖，提升通用性。

## 1. 项目结构拆分策略

### 1.1 原始 PlentyTraits 结构回顾

一个 .csproj / DLL 包含 Plugin.cs + 四个特质 .cs 文件 + 辅助组件（SelfHealing 等）。
入口 Plugin.cs 负责一次性加载所有资源、注册特质、添加本地化。
BNAPI 作为外部引用，提供 CustomTraits, CustomSprites, CustomText。

### 1.2 拆分后的独立 Mod 结构

每个特质 Mod 成为一个独立的 Visual Studio 项目（或至少编译为独立 DLL），其结构如下：

```
BadNorthHeroTraits/
├── BadNorthCheaperClass/
│   └── BadNorthCheaperClass/
│       ├── CheaperClass.cs
│       ├── Plugin.cs
│       ├── Properties/AssemblyInfo.cs
│       ├── Resources/trait_cheaperclass.png
│       └── BadNorthCheaperClass.csproj
├── BadNorthAxeThrower/
│   └── BadNorthAxeThrower/
│       ├── AxeThrower.cs          (已增加模板获取容错)
│       ├── Plugin.cs
│       ├── Properties/AssemblyInfo.cs
│       ├── Resources/trait_axe.png
│       └── BadNorthAxeThrower.csproj
├── BadNorthThorns/
│   └── BadNorthThorns/
│       ├── Thorns.cs
│       ├── Plugin.cs
│       ├── Properties/AssemblyInfo.cs
│       ├── Resources/trait_thorns.png
│       └── BadNorthThorns.csproj
└── BadNorthRegenerative/
    └── BadNorthRegenerative/
        ├── Regenerative.cs
        ├── Plugin.cs
        ├── Properties/AssemblyInfo.cs
        ├── Resources/trait_regenerative.png
        └── BadNorthRegenerative.csproj
```

**关键点：**

- 每个 Mod 的 Plugin.cs 仅负责自身特质的注册、资源加载和本地化术语添加，不再包含其他特质的代码。
- 所有共用的辅助组件（如 SelfHealing）可以保留在某个 Mod 中，或更优雅地作为一个共享的"组件库" Mod。但为了保证每个特质完全自包含，且避免跨 Mod 引用问题，建议 **每个 Mod 内部包含自己所需的辅助组件源文件**（代码体积极小，可复制），无需单独依赖。
- BNAPI 保持独立，不做任何特质层面的修改，只提供基础 API。

## 2. 通用性与版本兼容设计

### 2.1 利用 MMHOOK 实现版本解耦

MMHOOK 生成的委托绑定了方法签名，但其底层是通过 MonoMod 的 Detour 机制在运行时动态查找并替换方法。因此，只要游戏 C# 层的核心接口（类名、方法签名）未变，MMHOOK 生成的委托就可以在不同的游戏小版本上工作。

为保证最大兼容性，需要注意：

- 使用 MMHOOK 生成的委托时，不要假设任何方法的 IL 偏移，只依赖公开的类名和方法名。
- 避免使用硬编码的字段名通过 Traverse 或反射访问私有字段，而是尽可能利用游戏已有的公共属性或方法。如果必须反射，则做好异常处理和回退。

### 2.2 对官方类型的弱引用（反射 + 回退）

PlentyTraits 中存在一些直接引用游戏内部类型（如 VikingReference、LevelStateObjectReferences、AxeThrowing 等）的代码。如果游戏版本更新导致这些类型或层级发生变化，Mod 可能崩溃。

**已实施的容错方案（AxeThrower）：**

```csharp
private static AxeThrowing GetAxeThrowingTemplate()
{
    // 方法1: 从 LevelStateObjectReferences.dict 获取
    try
    {
        if (LevelStateObjectReferences.dict != null &&
            LevelStateObjectReferences.dict.TryGetValue("Viking_AxeThrower", out var reference) &&
            reference is VikingReference vikingRef &&
            vikingRef.viking != null &&
            vikingRef.viking.agent != null)
        {
            var template = vikingRef.viking.agent.GetComponent<AxeThrowing>();
            if (template != null) return template;
        }
    }
    catch (System.Exception ex) { /* 日志记录 */ }

    // 方法2: 遍历 Resources 查找 AxeThrowing 组件
    try
    {
        var allAxeThrowings = Resources.FindObjectsOfTypeAll<AxeThrowing>();
        if (allAxeThrowings != null && allAxeThrowings.Length > 0)
            return allAxeThrowings[0];
    }
    catch (System.Exception ex) { /* 日志记录 */ }

    // 方法3: 均失败，返回 null（调用方使用默认值）
    return null;
}
```

三级容错策略：
1. **精确查找**：从 `LevelStateObjectReferences.dict` 通过 key 获取（原逻辑，但增加了 null 检查和 TryGetValue）
2. **模糊查找**：遍历 `Resources.FindObjectsOfTypeAll<AxeThrowing>()` 获取任意可用模板
3. **硬编码默认值**：使用内置的 `AttackSettings` 默认值，保证特质退化但仍可玩

### 2.3 依赖最小化

- **强依赖**：BNAPI、MMHOOK、BepInEx。
- **无其它 Mod 依赖**：每个特质 Mod 不互相依赖。
- BNAPI 内可加入一些"安全糖"方法，例如 TryGetComponentFromDictOrFind，供所有特质 Mod 使用，以减少每个 Mod 中的重复容错代码。

这样，即使某个特质 Mod 因为版本不兼容而暂时失效，也不会影响其它特质 Mod 的运行。

## 3. 注册与加载流程的通用化

### 3.1 BNAPI 的现有注册机制

CustomTraits.RegisterTrait 直接将 HeroUpgradeDefinition 加入全局列表，并通过钩子 MetaInventory.InitStartingUpgrades 确保起始特质被解锁。该机制本身已经非常通用，无需改动。

### 3.2 每个 Mod 的 OnEnable 最小模板

```csharp
public class Plugin : BaseUnityPlugin {
    public const string TRAIT_ID = "Hero_Trait_XXX";
    public const string MOD_NAME = "BadNorthXXX";
    public static ManualLogSource Logger;
    
    void OnEnable() {
        Logger = base.Logger;
        string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";
        
        // 1. 加载精灵
        CustomSprites.AddCustomSprite(modPath, "trait_xxx");
        
        // 2. 注册特质
        CustomTraits.RegisterTrait(
            ScriptableObject.CreateInstance<XXX>(),
            TRAIT_ID,
            true/false  // alwaysUnlocked
        );
        
        // 3. 添加本地化文本
        CustomText.CustomTermsAdded += AddTerms;
        
        Logger.LogInfo($"{MOD_NAME} loaded.");
    }
    
    void AddTerms() { ... }
}
```

该模板在所有特质 Mod 中保持一致，仅需修改类名和字符串，大大降低了新 Mod 的创建门槛。

### 3.3 本地化术语的重复注册保护（已实施）

**风险**：拆分后若多个 Mod 同时安装，可能因术语冲突（如多个 Mod 注册相同 Term）导致加载失败。

**已实施的解决方案**：在 BNAPI 的 `AddCustomTerm` 方法中增加重复检查：

```csharp
public static void AddCustomTerm(string term, string text)
{
    LanguageSourceData source = LocalizationManager.Sources[0];
    
    // 防重复保护：检查术语是否已存在
    if (source.ContainsTerm(term))
    {
        Plugin.logger.LogInfo($"TERM \"{term}\" 已存在，跳过注册");
        return;
    }
    
    source.AddTerm(term);
    // ... 设置翻译
}
```

当术语已存在时，跳过注册并记录日志，避免抛出异常。

## 4. 资源打包与分发

### 4.1 独立图标

每个特质 Mod 的压缩包内应包含对应的 .png 图标文件，路径为 Mod DLL 所在目录。CustomSprites.AddCustomSprite 会从此路径加载，确保图标随 Mod 一起发布。

### 4.2 避免资源冲突

图标文件名建议统一使用 `trait_xxx.png`，其中 xxx 与特质 ID 的一部分对应，如 `trait_cheaperclass.png`。由于每个 Mod 的图标文件独立存放，不会冲突。

### 4.3 发布与安装

- 每个 Mod 发布为一个 .zip 包，内含 Plugins/ 文件夹放置 DLL 和 PNG。
- BNAPI 作为前置依赖，单独发布。
- MMHOOK 由 BepInEx 的 MonoMod 运行时自动生成（若用户正确安装了 BepInEx 和 MMHOOK 生成器），或作为单独文件提供，需在安装说明中明确。

## 5. 未来扩展模式

基于此架构，创造新特质 Mod 的步骤如下：

1. 复制任一个现有特质 Mod 的项目结构。
2. 修改 Plugin.cs 中的字符串标识和注册项。
3. 编写新的 HeroUpgradeDefinition 子类，实现 OnAppliedToSquad（或继承 HeroTraitCheaperUpgrades 等基类）。
4. 准备对应图标，添加到 Resources 并设置编译时复制。
5. 在 AssemblyInfo.cs 中设定合适的 Mod GUID 和版本号。
6. 编译、测试、发布。

如果未来需要拆分其他含多特质的 Mod，只需重复上述过程，每个特质抽取为独立 DLL，共享 BNAPI 工具库。

## 6. 实施清单与风险提示

### 6.1 拆分步骤（已完成）

- [x] 创建四个新项目：每个包含特质代码 + 入口 Plugin.cs
- [x] 复制辅助组件到需要的项目内
- [x] 增加 AxeThrower 模板获取三级容错（LevelStateObjectReferences → Resources → 默认值）
- [x] 增加 BNAPI AddCustomTerm 重复术语保护（ContainsTerm 检查）
- [ ] 测试：分别安装每一个 Mod，确保互相不冲突，且都能正常加载、生效
- [ ] 文档：提供各 Mod 单独的 README 及依赖说明

### 6.2 潜在问题与应对

- **术语重复添加崩溃**：✅ 已通过 BNAPI 防重复机制解决
- **图标加载失败**：需确保 Mod 的 Plugin.cs 中计算路径的方式与实际部署结构一致（BepInEx 下 DLL 默认在 plugins 子文件夹内）
- **游戏版本升级导致字段变更**：✅ AxeThrower 已增加三级容错；其他特质可通过类似模式逐步加固
- **MMHOOK 版本不匹配**：使用 MMHOOK 的 Mod 需与游戏核心程序集版本匹配。建议在 Mod 发布标注支持的游戏版本范围，或通过 [BepInProcess] 限制进程名，避免在不匹配的游戏上运行

## 7. 总结

该技术方案通过 **单一职责拆分、依赖集中管理** 和 **反射容错设计** 实现了高度模块化、可扩展的特质 Mod 体系。所有的修改均限定在 BepInEx 插件加载的 DLL 内，无需触碰任何游戏原始资源或代码，完美契合"不修改源代码"的目标。BNAPI 作为稳定 API 层，已增加术语重复保护，为未来数十个独立特质 Mod 提供统一支撑。

**下一步行动建议：**

1. 编译并测试第一个特质（如 CheaperClass）作为模板，验证整个流程
2. 将四个特质逐个编译、测试
3. 编写"创建新特质 Mod 指南"，供社区使用

此模式可轻松推广至任何基于 HeroUpgradeDefinition 的自定义内容，为后续批量生产模组奠定坚实基础。
