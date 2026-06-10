# 07 · 存档与Profile系统

> **本文档目标**：解析 Bad North 的存档读写机制——从 Profile 静态类 → BinaryFormatter → SaveGameHandler → 多文件存档流程。

---

## 1. 系统目标

存档系统解决：
- 管理用户全局存档（UserSave）和战役存档（CampaignSave）的读写
- 通过 BinaryFormatter 将可序列化对象转为字节流
- 支持多存档槽、Checkpoint（关卡中途存档）
- 处理 ScriptableObject 引用 → 字符串名称的序列化转换
- 跨平台文件操作（通过 BasePlatformManager / SaveGameHandlerCorePlatform）

---

## 2. 入口类

| 类 | 路径 | 行数 | 职责 |
|----|------|------|------|
| Profile | Voxels/TowerDefense/Profile.cs | 868 | ✅ 存档读写总入口（静态类） |
| CampaignSave | Voxels/TowerDefense/ProfileInternals/CampaignSave.cs | 445 | ✅ 战役存档数据 |
| UserSave | Voxels/TowerDefense/ProfileInternals/UserSave.cs | 137 | ✅ 用户全局存档 |
| SaveGameHandlerCorePlatform | Voxels/TowerDefense/ProfileInternals/SaveGameHandlerCorePlatform.cs | 483 | ✅ 平台文件操作处理器 |

---

## 3. 核心类

### 3.1 Profile — 存档总入口

**文件**: `Voxels/TowerDefense/Profile.cs`（868行）

```csharp
public static class Profile
{
    public static UserSave userSave = null;           // ✅ 用户存档
    public static CampaignSave campaign = null;       // ✅ 当前战役存档
    public static MetaSave meta = null;               // ✅ 元存档（存档槽管理）
    public static CampaignSaveMeta activeCampaignMeta = null;  // ✅ 当前活动战役的元数据
    public static UserSettings userSettings = null;   // ✅ 用户设置
    public static event Action<UpdateType> OnProfileUpdated;
}
```

**关键方法**：

| 方法 | 行号 | 说明 |
|------|------|------|
| `GameInit()` | 47-52 | ✅ `[RuntimeInitializeOnLoadMethod]` 游戏启动时初始化 |
| `ReloadProfile()` | 55-84 | ✅ 加载 user → settings → control mappings |
| `OnUserSaveLoaded(Callback)` | 129-152 | ✅ BinaryFormatter.Deserialize UserSave |
| `LoadCampaign(CampaignSaveMeta, bool)` | 211-241 | ✅ 加载战役 + 合法性检查 + CampaignManager.GenerateCampaign |
| `OnCampaignDeserialise(Callback)` | 307-326 | ✅ BinaryFormatter.Deserialize CampaignSave |
| `SaveCampaign(bool saveCheckpoint)` | 329-386 | ✅ **核心保存方法**：序列化并写入 3-4 个文件 |
| `CreateNewCampaign(int seed)` | 197-208 | ✅ 创建新战役 |
| `SaveSettings()` | 389-398 | ✅ 保存用户设置 |
| `SaveUserSave()` | 401-410 | ✅ 保存用户存档 |

### 3.2 CampaignSave — 战役存档

**文件**: `Voxels/TowerDefense/ProfileInternals/CampaignSave.cs`（445行）

**关键字段**：

| 字段 | 类型 | 说明 |
|------|------|------|
| serializedVersion | int | ✅ 版本号 = 19，PostLoad 验证 |
| seed | int | ✅ 世界种子 |
| heroes | List\<HeroDefinition\> | ✅ 所有英雄数据（含升级槽状态） |
| levelStates | List\<LevelState\> | ✅ 关卡状态（解锁、连接、种子） |
| inventory | List\<SerializableHeroUpgrade\> | ✅ 战役库存（道具/消耗品） |
| coinBank | int | ✅ 总金币池 |
| turnCount | int | ✅ 回合计数 |
| gameOverReason | GameOverReason | ✅ 游戏结束原因 |
| prefs | CampaignPrefs | ✅ 战役偏好（难度、教程等） |
| stats | CampaignStats | ✅ 战役统计 |
| day | Day | ✅ 游戏内时间 |
| weatherSystem | WorldWeather.WeatherSystem | 🟡 天气系统 |
| levelAtlas / paintAtlas | SavedTexture | 🟡 地图纹理 |

**序列化钩子**：

| 方法 | 行号 | 说明 |
|------|------|------|
| `PreSave(StreamingContext)` | 250-256 | ✅ 更新 savedPlayTime 和 timeStamp |
| `PostLoad(StreamingContext)` | 258-309 | ✅ 版本验证 + 字段修复 + 金币重新分配 |

### 3.3 UserSave — 用户存档

**文件**: `Voxels/TowerDefense/ProfileInternals/UserSave.cs`（137行）

**关键字段**：

| 字段 | 类型 | 说明 |
|------|------|------|
| campaignCount | int | ✅ 进行过的战役总数 |
| stats | CampaignStats | ✅ 全局统计 |
| campaignPrefs | CampaignPrefs | ✅ 偏好（难度等） |
| completionCounts | Dictionary\<Difficulty, int\> | ✅ 各难度通关次数 |
| maxDifficulty | Difficulty | ✅ 已解锁最高难度 |
| inventory | MetaInventory | ✅ 元库存（跨战役积累的道具） |
| savedPlayTime | int | ✅ 总游戏时间 |

### 3.4 存档文件体系

```
SaveGameHandlerCorePlatform 托管的文件：
    ├── "user"                                   ← UserSave 序列化
    ├── "settings"                               ← UserSettings 序列化
    ├── "control map - {playerId}"               ← Rewired 输入映射
    ├── "campaign - {slot}"                      ← CampaignSave 序列化
    ├── "campaign - {slot}.checkpoint"           ← 关卡中途存档
    └── "campaign - {slot}.meta"                 ← CampaignSaveMeta（摘要信息）
```

---

## 4. 数据流

### 4.1 保存流程（SaveCampaign）

```
Profile.SaveCampaign(saveCheckpoint)
    ↓
触发 OnProfileUpdated(Save) 事件 ✅ Profile.cs:337
    ↓
创建 3 个 MemoryStream ✅ Profile.cs:338-343
    ↓
BinaryFormatter.Serialize(stream2, campaign)     ← CampaignSave
BinaryFormatter.Serialize(stream, activeCampaignMeta) ← CampaignSaveMeta
BinaryFormatter.Serialize(stream3, userSave)     ← UserSave
    ↓
如果 saveCheckpoint：
    写入 4 个文件：targetFileName, checkpointFileName, metaFileName, "user"
否则：
    写入 3 个文件：targetFileName, metaFileName, "user"
    ↓
SaveGameUtilities.Save(...) → BasePlatformManager.Instance.Save(...) ✅
```

### 4.2 加载流程（ReloadProfile）

```
Profile.ReloadProfile()
    ↓
加载 3 个文件：["user", "settings", "control map - {0}"]
    ↓
OnUserSaveLoaded:
    BinaryFormatter.Deserialize → Profile.userSave ✅ Profile.cs:137
    ↓
OnUserSettingLoaded:
    BinaryFormatter.Deserialize → Profile.userSettings ✅ Profile.cs:95
    ↓ 设置加载完成后
SaveGameUtilities.GetHeaders(maxSlots, ...)  ← 加载所有存档槽元数据
    ↓
OnCampaignLoaded:
    填充 Profile.meta.campaigns[] ← CampaignSaveMeta 数组 ✅ Profile.cs:172-185
```

### 4.3 ScriptableObject 引用的序列化转换

```
[保存时]
    SerializableHeroUpgrade
        ├── definition (HeroUpgradeDefinition) ← [NonSerialized] 不序列化
        └── name (string) ← PreSave 时保存 definition.name ✅ SerializableHeroUpgrade.cs:105

    HeroDefinition
        ├── voice (HeroVoice) ← [NonSerialized]
        └── voiceName (string) ← PreSave 时保存 voice.name ✅ HeroDefinition.cs:434

[加载时]
    SerializableHeroUpgrade.PostLoad:
        definition ← ResourceList<HeroUpgradeDefinition>.Get(name) ✅ SerializableHeroUpgrade.cs:112

    HeroDefinition.PostLoad:
        voice ← ResourceList<HeroVoice>.Get(voiceName) ✅ HeroDefinition.cs:447
```

### 4.4 CampaignSave.PostLoad 金币重组

```
PostLoad: HeroDefinition.cs:438-462
    ↓
遍历所有英雄
    ├── 如果 recruited && alive：
    │   coinBank += hero.coins  ← 个人金币上交到战役金币池
    │   hero.coins = 0          ← 个人金币清零
    └── (已废弃的 _coins 字段只用于反序列化时的过渡)
```

---

## 5. 生命周期

| 阶段 | 机制 | 证据 |
|------|------|------|
| 游戏启动 | `[RuntimeInitializeOnLoadMethod] GameInit()` → meta 初始化 | ✅ Profile.cs:47-52 |
| 档案加载 | `ReloadProfile()` → 加载 user/settings/control mappings | ✅ Profile.cs:55-84 |
| 战役存档槽扫描 | `GetHeaders()` → 加载所有 meta 文件 | ✅ Profile.cs:119-124 |
| 战役开始 | `CreateNewCampaign(seed)` 或 `LoadCampaign(meta, bool)` | ✅ Profile.cs:197-208, 211-241 |
| 每回合/关卡存档 | `SaveCampaign(false)` | ✅ Profile.cs:329 |
| Checkpoint 存档 | `SaveCampaign(true)` | ✅ Profile.cs:329 |
| 版本验证 | `PostLoad` 检查 `serializedVersion == 19` | ✅ CampaignSave.cs:263 |
| 游戏结束 | `gameOver` 检查 → 决定是否删除存档 | ✅ CampaignSave.cs:89-94 |
| 存档卸载 | `Unload()` → 释放 levelAtlas / paintAtlas 纹理 | ✅ CampaignSave.cs:313-316 |

---

## 6. 与其他系统的依赖

| 系统 | 依赖关系 | 证据 |
|------|----------|------|
| 资源系统 | SerializableHeroUpgrade.PostLoad → ResourceList.Get 恢复引用 | ✅ SerializableHeroUpgrade.cs:112 |
| 英雄系统 | CampaignSave.heroes 包含所有 HeroDefinition | ✅ CampaignSave.cs:358 |
| 关卡系统 | CampaignSave.levelStates 包含所有 LevelState | ✅ CampaignSave.cs:355 |
| 战役管理 | LoadCampaign → CampaignManager.GenerateCampaign | ✅ Profile.cs:234 |
| UI | DoFailedToLoadModalOverlay 在加载失败时显示错误弹窗 | ✅ Profile.cs:480-483 |
| 平台抽象 | BasePlatformManager / SaveGameHandlerCorePlatform 处理文件 I/O | ✅ SaveGameHandlerCorePlatform.cs |

---

## 7. 证据状态

| 结论 | 状态 |
|------|------|
| Profile 是 `public static class`，静态入口 | ✅ Profile.cs:22 |
| 存档使用 BinaryFormatter 序列化/反序列化 | ✅ Profile.cs:92,134,159,309,344-349 |
| CampaignSave 版本号为 19，PostLoad 验证 | ✅ CampaignSave.cs:263,348 |
| UserSave 文件名为 "user" | ✅ UserSave.cs:44,110 |
| CampaignSave 文件名格式为 "campaign - {slot}" | ✅ CampaignSave.cs:31-33 |
| SerializableHeroUpgrade 通过 name 字符串保存/恢复 ScriptableObject 引用 | ✅ SerializableHeroUpgrade.cs:105,112 |
| PostLoad 时金币从英雄个人重新分配到 coinBank | ✅ CampaignSave.cs:301-308 |
| SaveCampaign 同时保存 CampaignSave + CampaignSaveMeta + UserSave | ✅ Profile.cs:345-349 |
| Checkpoint 存档多写一个 .checkpoint 文件 | ✅ Profile.cs:353-365 |

---

## 8. 关键风险/未解点

| 问题 | 状态 |
|------|------|
| Profile.cs 物理路径已记录为 Voxels/TowerDefense/Profile.cs，但建议再次打开文件确认实际仓库路径与文件名完全一致 | 🟡 路径来自引用链推断，建议最终核验 |
| MetaSave 和 CampaignSaveMeta 的完整结构未详细分析 | ❓ |
| LevelState 的完整字段和关卡图连接逻辑 | ❓ |
| WorldWeather.WeatherSystem 的序列化行为 | ❓ |
| SavedTexture 的序列化机制（位图序列化） | ❓ |
| 跨平台文件操作的完整路径和权限处理 | ❓ |
| 旧版本存档的迁移/兼容逻辑细节 | 🟡 PostLoad 中有多个字段的 null 检查和版本兼容代码 |