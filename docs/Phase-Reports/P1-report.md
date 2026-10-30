# P1 阶段报告

```text
阶段：P1
目标：建立真实可靠的 ONI 建筑几何数据库与基础游戏数据读取能力
游戏版本：U59-744825-S (release)
状态：PASS（含 1 项待复验的表述修正，见 §9）
```

---

## 1. 完成的功能

| # | 功能 | 状态 |
|---|---|---|
| 1 | 数据库就绪时机定位与一次性初始化 | **完成**，含状态机与四类边界测试 |
| 2 | 建筑几何采集（ID/名称/宽高/PlacementOffsets/朝向/图层/材料） | **完成**，424 个建筑 |
| 3 | Grid 坐标审计与运行时核对 | **完成**，16 项全部通过 |
| 4 | 科技解锁与材料可用性接口审计 | **完成** |
| 5 | Harmony 补丁应用方式与三程序集结构验证 | **完成**，改用显式挂载 |
| 6 | 测试、构建、游戏内验证 | **完成**，77 用例 / 315 断言 |

**未做（按阶段要求）**：未创建任何建筑、未执行任何放置、未修改存档、未触碰其他 MOD。

---

## 2. 新增与修改的文件

**新增 · OSB.Domain（6 个）**

| 文件 | 职责 |
|---|---|
| `PermittedRotation.cs` | 旋转形态 → 合法朝向集合；`Next()` 镜像 `Rotatable.Rotate` |
| `GeometryOrientation.cs` | 逐字镜像 `Rotatable.GetRotatedCellOffset` 的五种变换 |
| `GridGeometry.cs` | 镜像 `Grid` 索引数学 |
| `BuildingGeometry.cs` | 建筑几何 DTO + 异常码 GEO001–GEO007 |
| `BuildingGeometryTable.cs` | 几何库（查重、封存、异常汇总） |
| `OneShotInitializer.cs` | 可测试的一次性初始化状态机 |

**新增 · OSB.GameAdapter（6 个）**

| 文件 | 职责 |
|---|---|
| `OsbLog.cs` | 统一日志，**不含 LogError** |
| `GameDatabaseReady.cs` | 补丁挂载 + 两个初始化器 + 报告 |
| `BuildingGeometryCollector.cs` | `BuildingDef` → 领域 DTO |
| `GridCoordinateVerifier.cs` | 16 项运行时坐标核对 |
| `TechAccessor.cs` | 科技接口审计 |
| `MaterialAccessor.cs` | 材料接口审计 |

**新增 · 测试（4 个）**：`OrientationTests.cs`、`GridGeometryTests.cs`、`GeometryTableTests.cs`、`InitializerTests.cs`

**修改**：`LayerKind.cs`（朝向枚举重编号对齐游戏）、`BuildingGeometry`（见 §5 修正）、
`OsbMod.cs`（挂载补丁）、三个 `.csproj`、`API-Audit.md`

---

## 3. 真实 API 与证据

完整签名见 `docs/API-Audit.md` 的 L-001 / L-002 / G-001 / R-001 / R-002 / T-001 / M-001 / H-001。
全部为 **E1（反编译）** 证据，关键项另有 **E2（游戏内运行时）** 佐证。

### 3.1 数据库初始化时序（L-001）

```csharp
Assets.OnPrefabInit() → CreatePrefabs()
    ├─ Db.Get()                        // 数据库初始化
    ├─ BuildingDefs = new List<>()     // 清空准备
    ├─ foreach PrefabAssets → AddPrefab
    ├─ LegacyModMain.Load()
    │    └─ GeneratedBuildings.LoadGeneratedBuildings(types)
    │         └─ BuildingConfigManager.RegisterBuilding
    │              └─ Assets.AddBuildingDef(def)   // 建筑入库
    └─ Db.Get().PostProcess()
```

→ 采集挂在 `Assets.CreatePrefabs` 的 postfix，主菜单前完成。

### 3.2 建筑锚定约定（**P1 最重要的发现**）

```csharp
// BuildingDef.GenerateOffsets(width, height)
int num = width / 2 - width + 1;
PlacementOffsets[n].x = j + num;    // j ∈ [0, width)
PlacementOffsets[n].y = i;          // i ∈ [0, height)
```

**ONI 建筑水平居中、底部对齐锚定**：

| 宽度 W | 起始 x | x 范围 | 锚点在占位矩形内的位置 |
|---|---|---|---|
| 1 | 0 | 0 | (0,0) |
| 2 | 0 | 0..1 | (0,0) |
| 3 | −1 | −1..1 | **(1,0)** |
| 4 | −1 | −1..2 | **(1,0)** |
| 5 | −2 | −2..2 | **(2,0)** |

**后果**：若按"偏移从 (0,0) 开始"建模，3 格宽以上的建筑落位会整体错位一格。
领域层已用 `BuildingGeometry.AnchorInFootprint` 表达该偏移，并有 1..8 全宽度公式测试。

### 3.3 声明尺寸 ≠ 实际占位

```csharp
// FlowerVaseHangingConfig
BuildingTemplates.CreateBuildingDef("FlowerVaseHanging", 1, 2, ...);  // 声明 1x2
obj.GenerateOffsets(1, 1);                                            // 实际占 1x1
```

→ **权威是 `PlacementOffsets`**，`WidthInCells`/`HeightInCells` 只描述视觉尺寸。
领域层暴露 `ActualSize`（由包围盒得出）与 `SizeMatchesDeclaration`。

### 3.4 Grid 坐标（G-001）

```csharp
XYToCell(x, y) = x + y * WidthInCells;
CellToXY(cell)  → (cell % W, cell / W);
OffsetCell(cell, x, y) = cell + x + y * W;   // 无边界检查
```

**Y 轴方向：向上为正，未翻转。** UNKNOWN-1 关闭。

### 3.5 科技（T-001）

`Techs.Init()` 中注册的 item ID **就是建筑 PrefabID**（`"RationBox"`、`"AdvancedApothecary"`…）。
两处静默陷阱：`Tech.IsComplete()` 在无殖民地时恒 false（fail-closed）；
`IsTechItemComplete(id)` 找不到 item 时返回 true（fail-open）。

### 3.6 材料（M-001）

`DefaultMaterialTags` 里既有元素（`SandStone`）也有制成品（`BasicFabric`、`PlasticGasket`）。
后者查 `ElementLoader` 必然为 null，**属正常**。

### 3.7 Harmony（H-001）

`KMod.DLLLoader` 会为无 `UserMod2` 子类的程序集自动 `new UserMod2()`，其 `OnLoad` 执行
`harmony.PatchAll(该程序集)` —— 机制成立（E1）。但**本项目刻意不用 `[HarmonyPatch]` 特性**：
那条路径没有 try/catch，特性写错即整包 MOD 加载失败，并可能被记为崩溃源（P0 已踩过）。
改为 `OsbMod.OnLoad` 中显式 `harmony.Patch(...)`，逐条 try/catch、逐条记日志。

**P0 那条警告的解释**：`OSB.Domain` 与 `OSB.GameAdapter` 都没有 `UserMod2` 子类，
加载器为它们各补一个空实例并打印 warning。这是**正常且预期**的，不是错误。
空实例的 `PatchAll` 对 GameAdapter 是空操作（该程序集无特性补丁），因此不产生初始化冲突。

---

## 4. 建筑几何采集结果（真实游戏内）

```
建筑总数=424，异常项=4
可旋转=128，多格=315，弃用=24，占位格总数=2721，最大占位=CrewCapsule(5x19)
旋转形态分布：Unrotatable=296 R90=12 R360=61 FlipH=49 FlipV=6
```

**样本核查**

| PrefabID | 尺寸 | 占位格 | 旋转 | 图层 | 建造规则 | 材料 |
|---|---|---|---|---|---|---|
| Tile | 1x1 | 1 | Unrotatable | Building | Tile | BuildableRaw |
| Door | 1x2 | 2 | R90 | Building | Tile | Metal |
| ManualGenerator | 2x2 | 4 | Unrotatable | Building | OnFloor | Metal |
| Outhouse | 2x3 | 6 | Unrotatable | Building | OnFloor | BuildableRaw & BuildingWood |
| RationBox | 2x2 | 4 | Unrotatable | Building | OnFloor | BuildableRaw |
| Ladder | 1x1 | 1 | Unrotatable | Building | Anywhere | BuildableRaw & BuildingWood |
| Battery | 1x2 | 2 | Unrotatable | Building | OnFloor | Metal |

**剩余 4 条异常**（同一批的 2 个建筑 × 2 个码）

```
GEO002 [FlowerVaseHanging]      占位格数 1 与声明宽×高 2 不一致
GEO003 [FlowerVaseHanging]      实际占位 1x1 与声明尺寸 1x2 不同
GEO002 [FlowerVaseHangingFancy] 占位格数 1 与声明宽×高 2 不一致
GEO003 [FlowerVaseHangingFancy] 实际占位 1x1 与声明尺寸 1x2 不同
```

经源码核实为**游戏刻意设计**（`GenerateOffsets(1,1)`），非数据错误。保留上报以便审查，不计为失败。

**误报修正过程**：首轮采集上报 149 条异常，全部为 `GEO003`。经反编译定位为"偏移必须从 (0,0) 起算"
这一错误前提所致（宽度 ≥3 的建筑偏移含负值）。修正判据为"包围盒尺寸 vs 声明尺寸"后降至 4 条。

---

## 5. Grid 坐标验证结果

```
WidthInCells=908 HeightInCells=272 CellCount=246976 CellSizeInMeters=1
=== Grid 坐标核对结束：16 项通过，全部通过 ===
```

| # | 核对项 | 结果 |
|---|---|---|
| 1 | `XYToCell(x,y) == x + y*WidthInCells` | ✓ |
| 2 | `XYToCell` ↔ `CellToXY` 互逆（6 格采样） | ✓ |
| 3 | `OffsetCell` 与数学偏移一致 | ✓ |
| 4 | `CellToPos` / `PosToCell` 往返一致，`pos=(cellSize*x, cellSize*y)` | ✓ |
| 5 | **格 Y 轴向上为正、未翻转** | ✓ |
| 6 | 领域层索引方向与游戏同向 | ✓ |
| 7–10 | `IsValidCell` 四个边界 | ✓ |
| 11–15 | `IsCellOffsetValid` 四向边界 | ✓ |
| 16 | **证实 `OffsetCell` 不校验边界，最后一列 +1 绕到下一行首列** | ✓ |

**结论：坐标规则已证实，P2 的放置功能可以基于此开发。**

---

## 6. 科技与材料接口结果

**科技**：`科技总数=99`。首轮样本（`Tile`/`Door`/`ManualGenerator`/`Ladder`/`Battery`）
全部返回"无科技门槛"——经核实这是**正确的**，这些建筑开局即解锁、本就不在科技树中。
样本已改为受科技约束的建筑（`RationBox`/`AdvancedApothecary`/`Electrolyzer`/`GasConduit`/`FarmTile`），
但**该项表述修正尚未在游戏内复跑**（见 §9）。

**材料**：`有默认材料的建筑=424，主材料为元素=405，主材料为制成品/虚拟材料=19`。
后者查元素表为 null 属正常，已按此重新表述（同样未复跑）。

---

## 7. 测试与构建

```
OSB.Domain.Tests —— 共 77 个用例
用例 77/77 通过；断言通过 315 条，失败 0 条
结果：PASS
```

| 类别 | 用例数 | 覆盖 |
|---|---|---|
| P0 基础几何与蓝图 | 28 | Point2i / Rect2i / Footprint / Blueprint 校验码 |
| P1 朝向变换 | 10 |五种变换公式、哨兵拒绝、复合不变量、枚举数值对齐 |
| P1 栅格索引 | 9 | 公式、往返、边界、绕回风险、非法宽度 |
| P1 建筑几何与几何库 | 20 | 居中锚定、锚点公式、声明/实际尺寸、7 个异常码、封存 |
| P1 初始化时序 | 9 | 未就绪、重复、失败重试、到顶终止、重入、重置、启动时序模拟 |

**开发过程中测试抓到 2 个真实代码缺陷**：
1. `IsNullOrEmpty` 未覆盖纯空白名称 → 回退失效（已修）。
2. `GEO005` 用 `FitsInSize` 判定旋转占位 → 旋转后偏移含负值必然误报（已改为比较包围盒尺寸）。

另有 2 条是**测试自身的期望写错**（`Next()` 归一再切换的语义、`FitsInSize` 的适用边界），
已修正测试而非放宽断言。

**构建**：`dotnet build -c Debug` 成功，0 错误。警告为 `NU1900`（NuGet 源不可达）与
`MSB3277`（游戏程序集的 `System.Net.Http` 版本冲突，自动择主版本），均不影响产物。

---

## 8. 游戏实际运行结果

| 项 | 结果 |
|---|---|
| 补丁挂载 | `补丁已挂载：Assets.CreatePrefabs → OnCreatePrefabs` / `Game.Update → OnGameUpdate` |
| 采集时机 | `建筑几何采集完成，来源=Assets.CreatePrefabs，尝试次数=1` |
| 一次性保证 | 主菜单全程只采集一次；`Game.Update` 兜底未被使用 |
| Grid 核对时机 | `来源=Game.Update，尝试次数=1`（进入世界后首帧） |
| 未就绪处理 | `Grid 坐标核对等待进入世界（触发点 Assets.CreatePrefabs）` 只打印一次 |
| 崩溃 | 无。日志无 OSB 相关异常堆栈 |
| 存档 | 全程未读写 `save_files/`；验证使用新开殖民地 |

**一次与 OSB 无关的报错**（记录在案，便于后续排查）：

```
[08:46:47.421] [ONIMod][禁止传送] 添加/擦除工具组已加入工具栏，basicTools.Count=13
[08:46:47.458] ToolMenu NullReferenceException @ InstantiateCollectionsUI
```

相隔 37 毫秒，由用户另一 MOD `Atmo Suit Teleport` 注册自定义工具组后触发，
**堆栈中无任何 OSB 帧**。OSB 补丁不接触 `ToolMenu`。

---

## 9. 未解决问题

| 编号 | 问题 | 影响 | 处置 |
|---|---|---|---|
| P1-1 | 科技与材料的**表述修正**未在游戏内复跑 | 低 | 代码已改，待下次启动确认；不影响已采集数据 |
| P1-2 | `FlowerVaseHanging(Fancy)` 声明 1x2 实际 1x1 | 低 | 已确认游戏设计，数据如实记录 |
| P1-3 | 图纸层尚未使用 `AnchorInFootprint` | 中 | P2 落位功能必须接入，否则 3 格宽建筑错位 |
| P1-4 | `Blueprint.BuildingRecord.Footprint` 仍用声明尺寸构造 | 中 | P2 需改为从 `BuildingGeometry.ActualSize` 取值 |
| P1-5 | 施工任务可撤销句柄（UNKNOWN-3） | 中 | 仍未找到可靠标识，P2 倾向不做撤销 |
| P1-6 | 存档兼容性（UNKNOWN-6） | 中 | 未涉及；P2 若引入自定义组件必须先设计 |
| P1-7 | 第三方 LICENSE 原文（BLOCKED-1） | 中 | 网络可直连但尚未安排复核 |
| P1-8 | 仍在用零依赖自建测试运行器 | 低 | NuGet 可直连后应迁移到 xunit |

---

## 10. P2 开发建议（仅建议，不执行）

1. **先把 `AnchorInFootprint` 接进数据模型**：`Blueprint.BuildingRecord` 增加锚点字段，
   并用 `ActualSize` 取代声明尺寸。这是落位正确性的前置条件，比任何新功能都优先。
2. **蓝图捕获**只读实现：扫描区域内建筑 → 转 `BuildingRecord`，不写入世界。
   可在新开殖民地内完成验证，无需触碰真实存档。
3. **放置前的三层校验**：结构校验（已有）→ 几何校验（`IsCellOffsetValid` 逐格）→ 游戏校验
   （`BuildingDef.TryPlace` 内置）。三层缺一不可，不要跳过中间层直接依赖游戏。
4. **不要急着接 UI**。`PlanScreen` 与 `BuildTool` 耦合深（1891 / 553 行），
   建议先用热键 + 自建 `KScreen` 打通端到端，再考虑嵌入原生建造菜单。
5. **测试存档**：任何写世界的功能动工前，先建专用测试存档并写进 AGENTS.md。
