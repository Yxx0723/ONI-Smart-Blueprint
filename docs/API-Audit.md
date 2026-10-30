# API-Audit.md —— OSB 游戏接口审计

> 阶段：P0 ｜ 审计日期：2026-10-30 ｜ 适用游戏构建：**U59-744825-S (release)**

本文件是本项目**唯一**的游戏 API 事实来源。任何代码调用游戏类型前，必须能在此找到对应条目；
找不到的，先补审计，不得凭印象写。

---

## 1. 审计方法与证据分级

**被审计对象**

| 项 | 值 |
|---|---|
| 游戏安装 | `~/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app` |
| Managed 目录 | `.../Contents/Resources/Data/Managed` |
| 游戏构建 | `U59-744825-S (release)`（来源：`Player.log` 第 12 行 `[INFO] Build: U59-744825-S (release)`） |
| `Assembly-CSharp.dll` | Version **0.0.0.0**（Unity 程序集不携带版本号，无法用版本区分构建），SHA-256 前 32 位 `9b6e9ef6d1cef9477d20a7cbb70680e8` |
| `0Harmony.dll` | Version **2.4.2.0** |
| Unity | **6000.3.5f2**（来源：`Player.log` `Initialize engine version`） |
| 工具 | `ilspycmd 11.0.0.9375`（ICSharpCode.Decompiler 11.0.0.9375） |

**证据分级**（每条结论都会标注）

| 级别 | 含义 |
|---|---|
| **E1** | 反编译游戏程序集读到的源码级事实（最强，可逐行复核） |
| **E2** | 运行时探针在真实游戏内确认（本轮 `GameProbe` 产出） |
| **E3** | 游戏自身日志或安装文件中的事实 |
| **E4** | 从 E1–E3 合理推断，**尚未独立验证**（使用时须标风险） |

> **本次审计未能覆盖的部分**：首次审计时本机网络不可用（HTTP 代理 127.0.0.1:1082 未运行），
> 无法在线核对任何第三方仓库的 LICENSE 原文。第 6 节单独列出。

---

## 2. 关键结论速览

1. **多 DLL 加载：支持**，但有硬约束——同一程序集内最多一个 `UserMod2` 子类，超过即整个 MOD 加载中止。（A-001）
2. **"只下施工计划、不瞬间建成"：有原生接口**——`BuildingDef.TryPlace` → `Instantiate` → 使用 `BuildingUnderConstruction` 预制体 + `Constructable`。而 `Build()` 走的是 `BuildingComplete`，是作弊/沙盒用的瞬间建成路径。（A-005 / A-006 / A-007）
3. **建筑是多格 footprint**：由 `WidthInCells` / `HeightInCells` / `PlacementOffsets[]` 三个字段共同决定，不是单点。（A-004）
4. **朝向系统**：`Orientation` 枚举 7 值（含 `NumRotations` 占位），旋转后的格偏移必须走 `Rotatable.GetRotatedCellOffset`，不能自己算。（A-012 / A-013）

---

## 3. 已核实条目

### A-001 多 DLL 加载规则 ★核心

| 字段 | 内容 |
|---|---|
| 程序集 | `Assembly-CSharp`, Version=0.0.0.0 |
| 类型 | `KMod.DLLLoader`（`internal static class`） |
| 成员 | `public static LoadedModData LoadDLLs(Mod ownerMod, string harmonyId, string path, bool isDev)`<br>`public static void PostLoadDLLs(string harmonyId, LoadedModData modData, IReadOnlyList<Mod> mods)` |
| 证据 | **E1** —— 反编译 `KMod.DLLLoader` |
| 验证方式 | 1) `ilspycmd -t KMod.DLLLoader`；2) `GameProbe` 反射核对 4 参 `LoadDLLs` 与 3 参 `PostLoadDLLs` 存在 |
| 适用版本 | 744825 |
| 风险 | 低（逻辑稳定十余年）；若 Klei 改动此方法，`GameProbe` 会记 `签名不匹配` |

**读到的实现要点（逐条都会影响架构）：**

1. 遍历 MOD 目录 `DirectoryInfo(path).GetFiles()`，凡以 `.dll` 结尾且不以 `._` 开头，一律 `Assembly.LoadFrom`。
   → **多 DLL 受支持**，无数量限制。
2. 对**每个**程序集查找 `UserMod2` 子类：
   - 找到 0 个 → `isDev` 时 `LogWarning("... has no classes inheriting from UserMod, creating one...")`，然后 `new UserMod2()`；
   - 找到 1 个 → `Activator.CreateInstance`；
   - 找到 ≥2 个 → `LogError("Found more than one class inheriting UserMod2 ... Aborting load.")` 并 **`return null`**，整个 MOD 加载失败。
3. 随后对**每个**实例调用 `OnLoad(harmony)`；`OnAllModsLoaded` 同理（在 `PostLoadDLLs`）。
4. `UserMod2.OnLoad` 的基类默认实现是 `harmony.PatchAll(assembly)`。
   → **推论（E4）**：把 `[HarmonyPatch]` 类放在 `OSB.GameAdapter.dll` 是可行的，它会由自动补出的空实例调用 `PatchAll` 生效。
   本轮 P0 未放任何补丁，故该推论未经运行验证，标记为待验证。

**对本项目的约束：**
- 部署 3 个 DLL 是安全的，前提是 `OSB.Domain` 与 `OSB.GameAdapter` **都不得**含 `UserMod2` 子类。
- 已写入 `AGENTS.md` 第 3 节。

---

### A-002 Mod 元信息字段

| 字段 | 内容 |
|---|---|
| 类型 | `KMod.Mod.PackagedModInfo`（嵌套于 `KMod.Mod : IHasDlcRestrictions`） |
| 成员 | `string supportedContent`、`string[] requiredDlcIds`、`string[] forbiddenDlcIds`、`int lastWorkingBuild`、`int minimumSupportedBuild`、`int APIVersion`、`string version` |
| 常量 | `KMod.Mod.MOD_API_VERSION_NONE=0`、`_HARMONY1=1`、`_HARMONY2=2`、`MOD_API_VERSION=2` |
| 证据 | **E1** |
| 实践佐证 | **E3** —— 本机已成功加载的 `ONIMod` 的 `mod_info.yaml` 使用同一组字段（`supportedContent: ALL` / `minimumSupportedBuild` / `version` / `APIVersion: 2`） |
| 风险 | 低 |

→ 本项目 `OSB.Mod/mod_info.yaml` 字段名与语义以此为准。

---

### A-003 MOD 入口

| 字段 | 内容 |
|---|---|
| 类型 | `KMod.UserMod2`（`public class`） |
| 属性 | `public Assembly assembly { get; set; }`、`public string path { get; set; }`、`public Mod mod { get; set; }` |
| 方法 | `public virtual void OnLoad(Harmony harmony)`、`public virtual void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods)` |
| 证据 | **E1** |
| 风险 | 低 |

注：`assembly` / `path` / `mod` 三个属性由加载器**赋值**，MOD 代码只读，不要覆写。

---

### A-004 建筑几何与会话结构 ★核心

| 字段 | 内容 |
|---|---|
| 类型 | `BuildingDef` |
| 几何字段 | `public int WidthInCells`、`public int HeightInCells`、`public CellOffset[] PlacementOffsets`、`public CellOffset attachablePosition`、`public CellOffset UtilityInputOffset`、`public CellOffset UtilityOutputOffset` |
| 图层字段 | `public ObjectLayer ObjectLayer`（默认 `ObjectLayer.Building`）、`public ObjectLayer TileLayer`（默认 `NumLayers`）、`public ObjectLayer ReplacementLayer`、`public Grid.SceneLayer SceneLayer`、`public Grid.SceneLayer ForegroundLayer` |
| 属性 | `public bool IsTilePiece => TileLayer != ObjectLayer.NumLayers;` |
| 规则字段 | `public BuildLocationRule BuildLocationRule`、`public bool Cancellable`、`public bool DragBuild`、`public bool CanMove`、`public string[] RequiredDlcIds` / `ForbiddenDlcIds`、`public List<Tag> ReplacementTags` |
| 证据 | **E1** |
| 风险 | 中 —— `PlacementOffsets` 与 `WidthInCells×HeightInCells` 的对应关系未逐建筑验证；P1 捕获时必须两者同时读取并交叉校验 |

**为什么重要**：方案文档 4.2 要求"建筑是多格 footprint，不是仅一个中心点"。此条证实了游戏侧确有这三个字段，且
`PlacementOffsets.Length` 未必等于 `WidthInCells*HeightInCells`（存在锚点偏移与非矩形占位），
所以 `OSB.Domain.Footprint` 保留了 `Anchor` 概念。

---

### A-005 施工订单创建路径 ★最核心

| 字段 | 内容 |
|---|---|
| 类型 | `BuildingDef` |
| 签名-1 | `public GameObject Instantiate(Vector3 pos, Orientation orientation, IList<Tag> selected_elements, int layer = 0)` |
| 签名-2 | `public GameObject TryPlace(GameObject src_go, Vector3 pos, Orientation orientation, IList<Tag> selected_elements, string facadeID, bool restrictToActiveWorld, int layer = 0)` |
| 证据 | **E1** |
| 风险 | **中高** —— 这是本项目的核心依赖；错误使用会污染世界状态 |

**`Instantiate` 实现（关键 3 行，原文语义）：**

```
GameObject go = GameUtil.KInstantiate(BuildingUnderConstruction, pos, Grid.SceneLayer.Front, null, layer);
go.GetComponent<PrimaryElement>().ElementID = element.id;      // 材料主元素
go.GetComponent<Constructable>().SelectedElementsTags = selected_elements;
go.SetActive(true);
```

→ **结论：`Instantiate` 生成的是"施工中"预制体，不是成品。** 这正是 FR-04「只下施工计划」需要的语义。

**`TryPlace` 行为**：先 `IsValidPlaceLocation(...)` 通过，才 `Instantiate(...)`；否则返回 `null`。
即**内置了放置合法性校验**，且不会在非法位置留下任何对象。

---

### A-006 原生建造工具的下单入口（行为证据）

| 字段 | 内容 |
|---|---|
| 类型 | `BuildTool`（`OnDragTool` / `OnLeftClickUp` → `private void TryBuild(int cell)`） |
| 证据 | **E1** |
| 风险 | 低（本条目只作行为参照，不直接调用） |

`TryBuild` 中明确的分支：

```csharp
bool instant = DebugHandler.InstantBuildMode || (Game.Instance.SandboxModeActive && SandboxToolParameterMenu.instance.settings.InstantBuild);
if (!instant)  gameObject = def.TryPlace(visualizer, pos, buildingOrientation, selectedElements, facadeID);
else           gameObject = def.Build(cell, buildingOrientation, null, selectedElements, ..., playsound:false, GameClock.Instance.GetTime());
```

→ **游戏自己的"正常建造"就是调 `TryPlace`，"瞬间建成"才调 `Build`。** 两者语义由官方代码本身定义，
我们照抄这个分工即可，不需要自创机制。

---

### A-007 瞬间建成路径（禁止使用）

| 字段 | 内容 |
|---|---|
| 签名 | `public GameObject Build(int cell, Orientation orientation, Storage resource_storage, IList<Tag> selected_elements, float temperature, bool playsound = true, float timeBuilt = -1f)` |
| 证据 | **E1** |
| 风险 | **高** —— 直接产出 `BuildingComplete`，绕过材料与施工过程，违反方案 1.4「不实现瞬间生成真实建筑」 |

实现要点：`Create(pos, resource_storage, selected_elements, CraftRecipe, temperature, BuildingComplete)`，
然后 `MarkArea(...)`、播建造音效、回填 `Deconstructable.constructionElements`、触发 `-1661515756` 事件。

**仅在测试调试时考虑，产品代码禁止调用。**

---

### A-008 放置合法性校验

| 字段 | 内容 |
|---|---|
| 签名 | `public bool IsValidPlaceLocation(GameObject source_go, Vector3 pos, Orientation orientation, out string fail_reason)`<br>`public bool IsValidPlaceLocation(GameObject source_go, Vector3 pos, Orientation orientation, bool replace_tile, out string fail_reason)`<br>`public bool IsValidPlaceLocation(GameObject source_go, int cell, Orientation orientation, out string fail_reason)`<br>`public bool IsValidPlaceLocation(GameObject source_go, int cell, Orientation orientation, bool replace_tile, out string fail_reason)`<br>`public bool IsValidPlaceLocation(GameObject source_go, int cell, Orientation orientation, bool replace_tile, out string fail_reason, bool restrictToActiveWorld)` |
| 内部 | 私有 `IsAreaClear(...)` 逐格检查 `PlacementOffsets`（经 `Rotatable.GetRotatedCellOffset` 旋转），并检查 `Grid.IsCellOffsetValid` / `Grid.WorldIdx == ClusterManager.Instance.activeWorldId` / `Grid.IsValidBuildingCell` |
| 证据 | **E1** |
| 风险 | 中 |

**`fail_reason` 是 `LocString` 类型的可读文案** → 直接可用于 FR-10 的"错误可解释"，
不必自己发明失败原因文本（自造文本会与游戏本地化脱节）。

---

### A-009 施工中建筑组件

| 字段 | 内容 |
|---|---|
| 类型 | `Constructable : Workable, ISaveLoadable` |
| 成员 | `public bool isDiggingRequired`、`public bool IsReplacementTile`、`public IList<Tag> SelectedElementsTags`、`public Recipe Recipe => building.Def.CraftRecipe` |
| 证据 | **E1** |
| 风险 | 低 |

注：`Constructable` 的公开面很小，`OnSpawn` / `OnCleanUp` / `OnCompleteWork` 都是 `protected override`。
→ **推论（E4）**：无法通过公开接口直接读出"这条施工任务的进度/归属"，撤销机制需要另行设计（见第 5 节 UNKNOWN-3）。

---

### A-010 新增建筑定义（本期不用）

| 字段 | 内容 |
|---|---|
| 类型 | `IBuildingConfig`（abstract）、`BuildingConfigManager` |
| 成员 | `public abstract BuildingDef CreateBuildingDef()`、`public virtual void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)`、`public abstract void DoPostConfigureComplete(GameObject go)`、`public virtual void DoPostConfigurePreview(BuildingDef def, GameObject go)`、`public virtual void DoPostConfigureUnderConstruction(GameObject go)`<br>`BuildingConfigManager.Instance.RegisterBuilding(IBuildingConfig config)` |
| 证据 | **E1** |
| 风险 | 低（P0/P1 均不需要新增建筑；OSB 只复用原版建筑） |

**记录原因**：未来若要做"OSB 专属占位建筑"或蓝图标记物，这是唯一正路。留着备查。

---

### A-011 网格坐标系统

| 字段 | 内容 |
|---|---|
| 类型 | `Grid`（静态） |
| 静态字段 | `public static int WidthInCells`、`HeightInCells`、`CellCount`、`InvalidCell = -1`、`TopBorderHeight = 2`、`public static Dictionary<int, GameObject>[] ObjectLayers`、`public static ObjectLayerIndexer Objects`、`public static BuildFlagsSolidIndexer Solid`、`public static byte[] Visible` |
| 静态方法 | `public static int XYToCell(int x, int y)`、`public static int OffsetCell(int cell, CellOffset offset)`、`public static int OffsetCell(int cell, int x, int y)`、`public static int PosToCell(Vector3 pos)`、`public static Vector3 CellToPos(int cell)`、`public static CellOffset GetOffset(int cell)`、`CellAbove/Below/Left/Right(int cell)` |
| 证据 | **E1** |
| 风险 | 中 —— `Grid.WidthInCells/HeightInCells` 是**整个集群**尺寸，不等于当前世界尺寸；世界边界必须用世界自身范围判定 |

**已注意到的坑**：`Grid.XYToCell(x,y)` 的 y 轴方向与数学直觉相反的可能性未被排除。
→ **E4，必须由 P1 编写"格坐标往返测试"确认**：`CellToXY`→`XYToCell` 往返一致性 + 实地比对。

---

### A-012 朝向枚举

| 字段 | 内容 |
|---|---|
| 类型 | `Orientation`（`public enum`） |
| 值 | `Neutral, R90, R180, R270, NumRotations, FlipH, FlipV` |
| 证据 | **E1** |
| 风险 | 低 |

**注意 `NumRotations` 夹在中间**：它不是可用的朝向值，而是一个"可旋转数量"哨兵。
遍历时必须显式跳过，否则会把非法朝向传给游戏。

---

### A-013 朝向相关的格偏移换算

| 字段 | 内容 |
|---|---|
| 类型 | `Rotatable` |
| 签名 | `public static CellOffset GetRotatedCellOffset(CellOffset offset, Orientation orientation)`<br>`public static CellOffset GetRotatedCellOffset(int x, int y, Orientation orientation)`<br>`public void SetOrientation(Orientation new_orientation)`、`public Orientation Rotate()` |
| 证据 | **E1** |
| 风险 | 低 |

**决策**：凡涉及"建筑占位在某个朝向下覆盖哪些格"，一律调用游戏静态方法，**不使用** `OSB.Domain.Point2i.RotateClockwise90`。
Domain 的旋转只用于纯规划层（如模块组合的抽象拓扑），真正落位前必须由适配层用游戏方法复核。

---

### A-014 建筑预制体查询

| 字段 | 内容 |
|---|---|
| 类型 | `Assets`（静态） |
| 成员 | `public static List<BuildingDef> BuildingDefs`、`public static BuildingDef GetBuildingDef(string prefab_id)`、`public static void AddBuildingDef(BuildingDef def)` |
| 证据 | **E1** |
| 运行验证 | **见 `docs/Phase-Reports/P0-report.md` 的探针输出** |

`GetBuildingDef` 返回 `null` 表示 ID 不存在 → P1 捕获时必须校验，不得把未知 ID 静默写入蓝图。

---

### A-015 UI 挂载点候选

| 字段 | 内容 |
|---|---|
| 类型 | `PlanScreen` |
| 成员 | `public static PlanScreen Instance { get; private set; }`、`public ProductInfoScreen ProductInfoScreen { get; private set; }`、`public void OnSelectBuilding(GameObject button_go, BuildingDef def, string facadeID = null)`、`public void Refresh()`、`public void ForceRefreshAllBuildingToggles()`、`public void OpenCategoryByName(string category)`、`public void RefreshCategoryPanelTitle()` |
| 类型 | `BuildTool` |
| 成员 | `public void Activate(BuildingDef def, IList<Tag> selected_elements)`、`public void Activate(BuildingDef def, IList<Tag> selected_elements, string facadeID)`、`public void Deactivate()`、`public void SetToolOrientation(Orientation orientation)` |
| 证据 | **E1** |
| 风险 | **高** —— `PlanScreen` 1891 行、`BuildTool` 553 行，内部耦合深；直接改其 UI 结构极易与其它 MOD 冲突 |

**结论**：UI 挂载点**尚未定案**。P2 阶段需专项验证三条候选路径：
1. `PlanScreen` 增加自定义类别按钮；
2. 独立 `KScreen` + 热键入口（脱离 PlanScreen，冲突面最小）；
3. `BuildTool.Activate` 接管（复用原生建造预览，最省事但侵入性最强）。
P0 不做选择。

---

### A-016 版本自检

| 字段 | 内容 |
|---|---|
| 类型 | `BuildWatermark` |
| 签名 | `public static string GetBuildText()` |
| 证据 | **E1** |
| 用途 | 运行期把真实构建号写进日志，避免"以为在测 A 版本、实际跑 B 版本" |

---

### A-017 世界上下文

| 字段 | 内容 |
|---|---|
| 类型 | `ClusterManager` |
| 成员 | `public static ClusterManager Instance`、`public int activeWorldId => activeWorldIdx`、`public WorldContainer activeWorld => GetWorld(activeWorldId)` |
| 证据 | **E1** |
| 用途 | 多世界（Spaced Out!）场景下判定放置目标世界，避免跨世界误判 |

### A-018 运行时签名核对结果（E2 —— 真实游戏内）

| 字段 | 内容 |
|---|---|
| 执行点 | `OsbMod.OnAllModsLoaded` → `GameProbe.Run()` |
| 游戏 | `U59-744825-S (release)`，Steam 版 macOS |
| 结果 | **11 / 11 全部命中** |

```
[OSB][P0] GameBuild=U59-744825-S
[OSB][P0] ClusterManager.Instance=<null，尚未初始化>
[OSB][P0] Game.Instance=<null>
[OSB][P0] OK BuildingDef.TryPlace(6 参) 命中 1/3
[OSB][P0] OK BuildingDef.TryReplaceTile(5 参) 命中 1/2
[OSB][P0] OK BuildingDef.Instantiate(4 参) 命中 1/1
[OSB][P0] OK BuildingDef.IsValidPlaceLocation(6 参) 命中 1/5
[OSB][P0] OK BuildingDef.RunOnArea(3 参) 命中 1/1
[OSB][P0] OK BuildingDef.MarkArea(4 参) 命中 1/1
[OSB][P0] OK BuildingDef.Build(7 参) 命中 1/2
[OSB][P0] OK UserMod2.OnLoad(1 参) 命中 1/1
[OSB][P0] OK UserMod2.OnAllModsLoaded(2 参) 命中 1/1
[OSB][P0] OK DLLLoader.LoadDLLs(4 参) 命中 1/1
[OSB][P0] OK DLLLoader.PostLoadDLLs(3 参) 命中 1/1
[OSB][P0] Assets.BuildingDefs=<null>
```

**由此升级为 E1+E2 双证实的条目**：A-001、A-003、A-005、A-006、A-007、A-008。

**新确认的时序事实（E2）**：在 `OnAllModsLoaded` 时刻，`Grid.CellCount == 0`、
`ClusterManager.Instance == null`、`Game.Instance == null`、`Assets.BuildingDefs == null`。
→ **游戏数据库（Db）与世界上下文都在此之后才初始化。** 任何需要建筑定义或世界数据的逻辑，
必须推迟到 Db 初始化之后执行（如首帧 `Game.Update`），不能放在 `OnAllModsLoaded`。

**新发现的引擎行为（E2）**：MOD 加载期若抛出异常并伴随 `Debug.LogError`，ONI 会扫描堆栈、
把命中的程序集登记进"疑似肇事 MOD"列表：

```
ONI Smart Blueprint's assembly declared the method OSB.GameAdapter.GameProbe:LogError
in the stack trace, adding to referenced mods list
```

被登记后，游戏因任何原因崩溃时该 MOD 都会被算作崩溃源，并触发安全模式
（`mods.json` 中所有 MOD 被置为 `enabled: false`，含无关 MOD）。
→ 已写入 `AGENTS.md` 第 4 节，作为项目级硬规则。

---

## 3.5 P1 新增审计条目

### L-001 游戏数据库初始化时序 ★核心

| 字段 | 内容 |
|---|---|
| 证据 | **E1** |
| 适用版本 | 744825 |
| 风险 | 低（时序稳定），但**用错就会拿到 null** |

反编译 `Assets.cs` 得到的完整链路：

```csharp
// Assets.cs
protected override void OnPrefabInit() { ...; CreatePrefabs(); }

private void CreatePrefabs()
{
    Db.Get();                                   // ← 游戏数据库在此初始化
    BuildingDefs = new List<BuildingDef>();     // ← 清空并准备填充
    foreach (KPrefabID prefabAsset in PrefabAssets) { ...; AddPrefab(prefabAsset); }
    LegacyModMain.Load();                       // ← 所有 Mod 建筑在此注册
    Db.Get().PostProcess();
    ComplexRecipeManager.Get().PostProcess();
}

// LegacyModMain.cs:151
private static void LoadBuildings(List<Type> types)
{
    ...
    GeneratedBuildings.LoadGeneratedBuildings(types);
}

// GeneratedBuildings.cs:8 → BuildingConfigManager.cs:114
public void RegisterBuilding(IBuildingConfig config) { ...; Assets.AddBuildingDef(buildingDef); }
```

**结论：`Assets.CreatePrefabs()` 返回之后，`Assets.BuildingDefs` 才是完整的（含全部 Mod 建筑）。**

`KMod.UserMod2.OnAllModsLoaded` 在其之前，所以 P0 探针在那里读到 null —— 那不是缺陷，是时机不对。

**本项目采用的挂载点**：`Assets.CreatePrefabs` 的 Harmony postfix（主）+ `Game.Update` postfix（兜底）。
两者都经 `OneShotInitializer` 保护，成功后短路。

**被否决的方案**：在主菜单之外的早期阶段调用 `Db.Get()` 强行触发初始化。
`Db.Get()` 在 `_Instance == null` 时会立即 `Resources.Load<Db>("Db")` 并 `Initialize()`，
提前调用会打乱游戏自身的初始化顺序——这是"看起来能跑、实际埋雷"的典型做法，不采用。

---

### L-002 `Assets.OnSpawn` 也会调用 `Db.Get()`

| 字段 | 内容 |
|---|---|
| 签名 | `protected override void OnSpawn()` → `base.OnSpawn(); Db.Get();` |
| 证据 | **E1** |
| 说明 | 可作为"Db 一定已就绪"的次级判据，但它晚于 `CreatePrefabs`，对采集没有额外价值 |

---

### G-001 Grid 坐标数学 ★核心

| 字段 | 内容 |
|---|---|
| 证据 | **E1**（反编译）+ **E2**（游戏内 `GridCoordinateVerifier` 实测，见 P1 报告） |
| 风险 | 中 —— `OffsetCell` 不做越界检查，是真实的隐性 bug 来源 |

```csharp
public static int XYToCell(int x, int y)                  => x + y * WidthInCells;
public static int CellColumn(int cell)                    => cell % WidthInCells;
public static int CellRow(int cell)                       => cell / WidthInCells;
public static void CellToXY(int cell, out int x, out int y);
public static Vector3 CellToPos(int cell)                 => (CellSizeInMeters * (cell % W), CellSizeInMeters * (cell / W), 0);
public static int PosToCell(Vector3 pos)                  => (int)(pos.y + 0.05f) * WidthInCells + (int)pos.x;
public static int OffsetCell(int cell, int x, int y)      => cell + x + y * WidthInCells;   // 无边界检查
public static bool IsCellOffsetValid(int cell, int x, int y);                              // 有边界检查
public static bool IsValidCell(int cell)                  => cell >= 0 && cell < CellCount;
public static bool IsWorldValidCell(int cell)             => IsValidCell(cell) && WorldIdx[cell] != byte.MaxValue;
public static bool IsValidBuildingCell(int cell);         // 额外检查世界边界与 TopBorderHeight
```

**Y 轴方向结论（UNKNOWN-1 已解决）**：格 Y 轴与 Unity 世界 Y 轴**同向，向上为正，未翻转**。
推导依据：`CellToPos` 用 `CellSizeInMeters * (cell / Width)` 作为世界 y，与 `CellRow` 完全一致。

**必须记住的坑**：从最后一列 `OffsetCell(cell, 1, 0)` 会绕回**下一行首列**，得到一个合法索引。
任何构造性使用前必须调 `IsCellOffsetValid`。领域层 `GridGeometry.Offset` 保留了这个行为并有专门测试。

---

### R-001 旋转换算

| 字段 | 内容 |
|---|---|
| 签名 | `public static CellOffset GetRotatedCellOffset(CellOffset offset, Orientation orientation)` |
| 证据 | **E1** |
| 风险 | 中 —— 哨兵值会被 default 分支静默吞掉 |

```csharp
Orientation.R90   => new CellOffset( offset.y, -offset.x)
Orientation.R180  => new CellOffset(-offset.x, -offset.y)
Orientation.R270  => new CellOffset(-offset.y,  offset.x)
Orientation.FlipH => new CellOffset(-offset.x,  offset.y)
Orientation.FlipV => new CellOffset( offset.x, -offset.y)
_                 => offset          // ← Neutral 与 NumRotations 都走这里
```

**领域层有意偏离**：`GeometryOrientation.Rotate` 对 `NumRotations` 抛 `ArgumentOutOfRangeException`，
而不是静默返回原值。哨兵被当真实朝向用是隐性 bug，宁可在测试里炸掉。

---

### R-002 旋转形态与合法朝向

| 字段 | 内容 |
|---|---|
| 类型 | `PermittedRotations`（`Unrotatable, R90, R360, FlipH, FlipV`）；`BuildingDef.PermittedRotations` 字段 |
| 证据 | **E1**（`Rotatable.Rotate` 实现） |
| 风险 | 低 |

```csharp
switch (permittedRotations)
{
    case PermittedRotations.R360:  orientation = (Orientation)((int)(orientation + 1) % 4); break;
    case PermittedRotations.R90:   orientation = orientation == Neutral ? R90 : Neutral;    break;
    case PermittedRotations.FlipH: orientation = orientation == Neutral ? FlipH : Neutral;  break;
    case PermittedRotations.FlipV: orientation = orientation == Neutral ? FlipV : Neutral;  break;
}
```

→ 合法集合：`Unrotatable={Neutral}`、`R90={Neutral,R90}`、`R360={Neutral,R90,R180,R270}`、
`FlipH={Neutral,FlipH}`、`FlipV={Neutral,FlipV}`。领域层 `PermittedRotationInfo` 已建模并有测试。

**枚举数值必须对齐**：`Orientation` = `Neutral0, R901, R1802, R2703, NumRotations4, FlipH5, FlipV6`。
领域层 `BuildingOrientation` 已按此编号，可直接整数转换。

---

### T-001 科技解锁读取

| 字段 | 内容 |
|---|---|
| 签名 | `Database.Techs.TryGetTechForTechItem(string itemId)` → `Tech`；`Database.Techs.IsTechItemComplete(string id)` → `bool`；`Tech.IsComplete()` → `bool`；`Tech.unlockedItemIDs` |
| 证据 | **E1** |
| 风险 | **高** —— 两个方向的静默错误 |

```csharp
// Tech.IsComplete()：不在殖民地时恒为 false（fail-closed）
public bool IsComplete()
{
    if (Research.Instance != null) return Research.Instance.Get(this)?.IsComplete() ?? false;
    return false;
}

// Database.Techs.IsTechItemComplete(id)：找不到 item 时返回 true（fail-open）
public bool IsTechItemComplete(string id)
{
    foreach (Tech resource in resources)
        foreach (TechItem unlockedItem in resource.unlockedItems)
            if (unlockedItem.Id == id) return resource.IsComplete();
    return true;
}
```

**三条调用限制**：
1. 必须在 `Db.Get()` 之后（即 `Assets.CreatePrefabs()` 之后）。
2. 主菜单阶段 `Research.Instance == null`，所有科技都会显示"未解锁"——上下文不成立，不是数据错误。
3. `IsTechItemComplete` 对不存在的 item 返回 `true`。判"已解锁"必须先确认 item 真实存在。

---

### M-001 材料可用性读取

| 字段 | 内容 |
|---|---|
| 签名 | `ElementLoader.FindElementByTag(Tag)` / `FindElementByName(string)` / `GetElement(Tag)` / `FindElements(Func<Element,bool>)`；`Element.id`（SimHashes）/`Element.tag`（Tag）/`Element.state` |
| 证据 | **E1** |
| 风险 | 低 |

- `GetElement` 是字典直查，**查不到返回 null，不抛异常**——必须判空。
- 元素表在 `Assets` 初始化期间建立，故需在 `CreatePrefabs()` 之后调用。
- `BuildingDef.DefaultElements()` 依赖 `MaterialCategory` 映射，个别建筑会抛异常，必须逐个 try/catch。
- **本类不回答"材料够不够"**：库存判定依赖具体殖民地，属于放置期逻辑。

---

### H-001 Harmony 补丁的应用方式（有意偏离惯例）

| 字段 | 内容 |
|---|---|
| 事实 | `KMod.DLLLoader` 为无 `UserMod2` 子类的程序集自动 `new UserMod2()`，其基类 `OnLoad` 执行 `harmony.PatchAll(该程序集)` |
| 证据 | **E1**（`DLLLoader.LoadDLLs`） |
| 本项目做法 | **不使用 `[HarmonyPatch]` 特性**，改由 `OsbMod.OnLoad` 显式 `harmony.Patch(...)` |

**理由**：特性路径没有 try/catch，特性写错 = 整包 MOD 加载失败，并可能被 ONI 记为崩溃源（P0 已踩过一次）。
显式挂载可以逐条捕获、逐条记录，单条失败不影响其余功能。

**代价**：放弃了一处便利。若未来确有需要，可以加特性补丁——但要清楚它会在那条无保护路径上执行。

---

## 4. 对方案文档的事实修正

| 文档位置 | 原文 | 实测 | 处置 |
|---|---|---|---|
| 12.2 节 | macOS 常见 MOD 目录 `~/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local` | 本机**不存在** `mods/Local`，实际生效的是 `mods/Dev`（另有 `mods/Steam`） | 本项目统一用 `mods/Dev`，已在 csproj 与 build.sh 落实 |
| 12.3 节 | `minimumSupportedBuild: 744825` | 与本机 `U59-744825-S` 一致 | 保留 |
| 0.1 节 | "官方稳定版 744825，另有 761015 public_testing" | 本机为 744825，未切测试分支 | 无需处理 |

---

## 5. 未解决 / 阻塞项

| 编号 | 项目 | 状态 | 说明 |
|---|---|---|---|
| UNKNOWN-1 | `Grid.XYToCell` 的 y 轴方向 | **已解决（P1）** | 见 G-001：Y 轴向上为正、未翻转。含领域层往返测试 + 游戏内运行时核对 |
| UNKNOWN-2 | 科技解锁读取（`Tech` / `Techs`） | **已解决（P1）** | 见 T-001，含 fail-open / fail-closed 两处陷阱 |
| UNKNOWN-3 | 施工任务的可撤销句柄 | `UNKNOWN` | `Constructable` 公开面不足以标识"我创建的这条任务"。方案 7.3 要求"无可靠 ID 时禁止自动撤销"，倾向 P2 先不做撤销 |
| UNKNOWN-4 | 材料可用性读取（`ElementLoader`） | **已解决（P1）** | 见 M-001 |
| UNKNOWN-5 | `GameAdapter` 内 `[HarmonyPatch]` 特性补丁能否由自动补出的空 `UserMod2` 实例生效 | **机制已确证（E1）** | 见 H-001。机制成立，但本项目**刻意不采用**该路径，改为显式挂载 |
| UNKNOWN-6 | 存档中是否可安全存放自定义组件 | `UNKNOWN` | 方案 7.3 要求"禁用 MOD 后存档仍可加载"，须专门设计并测试；P1 未涉及存档 |
| UNKNOWN-7 | 建筑 `PlacementOffsets` 的锚定约定是否统一 | `部分确认（P1）` | 运行时采集显示并非所有建筑都从 (0,0) 起算，须以实际采集结果与异常统计为准 |
| BLOCKED-1 | 第三方 LICENSE 原文核对 | `BLOCKED` | 网络代理故障已定位（直连可用），但尚未安排复核。方案文档关于 "Mayall = MIT / Sgt_Imalas = All Rights Reserved" 的结论**仅为二手信息，未经本项目核实** |

---

## 6. 许可审计现状

| 对象 | 方案文档声称 | 本项目核实情况 |
|---|---|---|
| Mayall 原版 Blueprints（`github.com/Mayall0513/oni-mods`） | MIT | **未核实**（无网络） |
| Sgt_Imalas Blueprints Expanded | 根 LICENSE 为 All Rights Reserved | **未核实**（无网络） |

**执行规则**：在完成原文核对之前，**不得**从上述任一仓库复制任何代码、字符串或资源。
仅可参考"功能存在性"这一层信息（例如"Expanded 有镜像功能"属于产品事实，不属于代码）。

---

## 7. 复现审计的命令

```bash
export PATH="$HOME/.dotnet/tools:$PATH"
MANAGED="$HOME/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app/Contents/Resources/Data/Managed"

# 列出全部类型（16472 个）
ilspycmd -l c "$MANAGED/Assembly-CSharp.dll" > /tmp/osb_classes.txt

# 单类型反编译
ilspycmd -t KMod.DLLLoader      "$MANAGED/Assembly-CSharp.dll"
ilspycmd -t BuildingDef         "$MANAGED/Assembly-CSharp.dll"
ilspycmd -t BuildTool           "$MANAGED/Assembly-CSharp.dll"

# 程序集版本（Assembly-CSharp 为 0.0.0.0，只能靠 Build 号 + 哈希区分）
shasum -a 256 "$MANAGED/Assembly-CSharp.dll"

# 游戏真实构建号
grep -m2 "Build:" "$HOME/Library/Logs/Klei/Oxygen Not Included/Player.log"
```
