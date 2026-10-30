# P2-A 阶段报告 —— 蓝图数据模型修正

```text
阶段：P2-A
目标：把 P1 实测确立的锚定与占位事实正式固化进蓝图数据模型
游戏版本：U59-744825-S (release)
状态：PASS（纯模型层，无游戏内行为）
```

---

## 1. 修改文件

| 文件 | 变更 |
|---|---|
| `OSB.Domain/BuildingRecord.cs` | **重写**。删去 `Footprint` 属性，改为持有原始 `PlacementOffsets` + `DeclaredSize`；新增锚点/包围盒/占用格三组 API 与 7 个转换方法 |
| `OSB.Domain/Blueprint.cs` | **重写**。`SchemaVersion` 1→2；`Origin`→`WorldOrigin`；包围盒与重叠检测改为基于真实占用格；`Normalize` 改为按占用格归一 |
| `OSB.Domain/GeometryOrientation.cs` | `RotateAll` 形参由 `IReadOnlyList` 放宽为 `IList`，以接受 `BuildingRecord.PlacementOffsets` |
| `OSB.Domain/Footprint.cs` | 仅改注释：标注"P2-A 起不再用于占用判定，仅作声明尺寸的几何辅助" |
| `tests/BlueprintTests.cs` | **重写**。占位改为按游戏 `GenerateOffsets` 公式生成；12 个用例 → 19 个 |
| `tests/CoordinateConversionTests.cs` | **新增**。11 个几何转换用例 |
| `tests/Program.cs`、`tests/OSB.Domain.Tests.csproj` | 登记新用例与新文件 |

**未修改**：`OSB.GameAdapter`、`OSB.Mod`、`BuildingGeometry`。本阶段不触碰游戏交互层。

---

## 2. 技术实现

### 2.1 三个概念彻底分离

| 概念 | API | 用途 | 禁止用途 |
|---|---|---|---|
| 声明尺寸 | `DeclaredSize` | 展示、日志、兼容性校验 | **任何占用计算** |
| 实际包围盒 | `OccupiedBounds()` / `OccupiedBoundsAt()` | 预览框、裁剪、范围估算 | **重叠 / 地形 / 边界判定** |
| 真实占用格 | `OccupiedCells()` / `OccupiedCellsAt()` | **一切占用判定的唯一依据** | — |

`PlacementOffsets` 以原始形式保存，**不排序、不去重、不折叠为矩形**。旋转时生成新数组，
原数组永不被改写（有测试锁死）。

### 2.2 锚点不是左下角

```
FootprintCorner(anchor, orientation)        = anchor + bounds_min(rotate(offsets))
AnchorForFootprintCorner(corner, orientation) = corner - bounds_min(rotate(offsets))
```

两者互为逆运算，已对 **宽度 1..8 × 高度 1..3 × 4 种朝向** 共 96 组组合做往返测试。

锚点在占位矩形内的位置：

| 声明宽度 | `AnchorInFootprint.X` |
|---|---|
| 1、2 | 0 |
| 3、4 | 1 |
| 5、6 | 2 |
| 7、8 | 3 |

Y 方向恒为 0（底部对齐）。旋转后该值随之变化，例如 3×1 水平建筑在 R90 下由 `(1,0)` 变为 `(0,1)`。

### 2.3 归一化基准改为占用格

这是本次修正的关键。旧实现按**锚点**最小值归一：

```
3 宽建筑锚定 (0,0) → 占用 x ∈ [-1,1]
按锚点归一：dx = 0 → 不动 → 最左格仍在 -1，越界
```

新实现按**占用格**最小值归一：

```
dx = -1 → 锚点平移到 (1,0) → 占用 x ∈ [0,2] ✓
```

`OSB010`（负坐标）也随之改为检查占用格而非锚点。这一条直接对应任务书第 6 项
「修复宽度 ≥3 格建筑的落位偏移」，并有宽度 1..8 的全覆盖回归测试。

### 2.4 重叠检测改为格集合

旧实现用矩形相交判定。新实现按图层建立 `Dictionary<Point2i, string>` 逐格登记。

有一个专门用例锁死这个语义：构造两组**包围盒完全重合但格互不重叠**的形状
（`{(0,0),(2,0),(0,1),(0,2)}` 与 `{(1,0),(2,1),(2,2)}`），断言
「包围盒相交」为真、「报告重叠」为假。若将来有人改回矩形判定，该用例会立即失败。

### 2.5 声明尺寸 ≠ 实际占位（任务书第 7 项）

`FlowerVaseHanging` 这类建筑无需任何特判：模型从不使用 `DeclaredSize` 计算占用，
所以它天然只占 1 格。校验时额外报一条 `OSB012`，级别为 **Warning 而非 Blocker**
——游戏允许这种不一致，阻断它是错的。有测试断言 `OSB012.Severity == Warning`
且 `BlocksPlacement == false`，并断言占用图中不含 `(0,1)`。

### 2.6 校验函数不得抛异常

`Validate()` 遇到哨兵朝向时曾直接抛 `ArgumentOutOfRangeException`
（由 `GeometryOrientation.Rotate` 抛出），导致整份蓝图校验中断。

已修正为：先报告 `OSB014`，跳过依赖旋转的检查后提前退出。
`Blueprint.Validate` 同样把"几何可安全求值"作为参与重叠检测的前提。

**这条不是妥协**：校验函数的职责就是处理非法输入，它自己崩掉等于失职。

---

## 3. API 证据

本阶段**未引入任何新的游戏 API**。所有实现依据均来自 P1 已登记的条目：

| 依据 | 出处 | 证据级别 |
|---|---|---|
| `x = j + (width/2 - width + 1)`，`y = i` | `BuildingDef.GenerateOffsets`，见 `API-Audit.md` §3.5 | E1 |
| 绕 90°/180°/270° 与翻转的偏移变换公式 | `Rotatable.GetRotatedCellOffset`，见 R-001 | E1 |
| `PermittedRotations` 与合法朝向集合 | `Rotatable.Rotate`，见 R-002 | E1 |
| 声明尺寸与实际占位可不同（`GenerateOffsets(1,1)`） | `FlowerVaseHangingConfig`，见 §3.3 | E1 |

测试中的占位生成函数 `GameOffsets(width, height)` 直接复刻上式，与游戏同源。

---

## 4. 单元测试

```
OSB.Domain.Tests —— 共 95 个用例
用例 95/95 通过；断言通过 530 条，失败 0 条
结果：PASS
```

| 分组 | 用例数 | 说明 |
|---|---|---|
| P0 基础几何 | 28 | Point2i / Rect2i / Footprint，保持不变 |
| P1 朝向变换 | 10 | 不变 |
| P1 栅格索引 | 9 | 不变 |
| P1 建筑几何与几何库 | 20 | 不变 |
| P1 初始化时序 | 9 | 不变 |
| **P2-A 蓝图结构** | **19** | 由 12 个改写 + 7 个新增 |
| **P2-A 几何转换** | **11** | 新增 |
| 合计 | **95** | 原 77 个用例无一删除，全部保留或按新语义改写 |

**P2-A 新增/改写的关键用例**

| 用例 | 断言要点 |
|---|---|
| `Blueprint.CenteredAnchorNormalization` | 3 宽建筑锚定 (0,0) 时报 OSB010；归一后锚点变 (1,0)、最左格 0 |
| `Blueprint.WideBuildingsNoShift` | 宽度 1..8 归一后，最左格恒为 0、最右格恒为 width−1、锚点恒等于 `AnchorInFootprint.X` |
| `Blueprint.BoundsOverlapCellsDisjoint` | 包围盒相交但格不重叠 → 不得报重叠 |
| `Blueprint.DeclaredSizeMismatchIsWarning` | OSB012 为 Warning 且不阻断；占用图不含声明高度那一格 |
| `Blueprint.SentinelOrientationBlocked` | 哨兵朝向报 OSB014 且**不抛异常** |
| `Conversion.FootprintCornerRoundtrip` | 96 组「宽×高×朝向」锚点⇄矩形角往返 |
| `Conversion.RotationImmutable` | 旋转后原始 `PlacementOffsets` 逐项不变 |
| `Conversion.AnchorInFootprintPerWidth` | 宽度 1..10 的锚点偏移符合游戏公式 |

**改写而非放宽**：`Blueprint.InvalidFootprint` 与 `Blueprint.NegativeLocalPosition` 等 6 个
旧用例建立在"锚点=左下角"的错误前提上，其断言本身已不成立。已按新语义重写并加强
（例如负坐标检查从"锚点非负"改为"占用格非负"），未删除任何保护性断言。

**开发过程中测试抓到的 2 个问题**

1. 编译期：`IList<Point2i>` 与 `IReadOnlyList<Point2i>` 不兼容 → 调整 `RotateAll` 形参。
2. 运行期：`Validate()` 在哨兵朝向下抛出未捕获异常 → 重构为报告后提前退出（见 §2.6）。

---

## 5. 实际游戏测试

**NOT RUN —— 本阶段无游戏内可观测行为。**

P2-A 只改动 `OSB.Domain` 内的数据结构与校验逻辑，`OSB.GameAdapter` 与 `OSB.Mod` 均未修改，
MOD 在游戏中的行为与 P1 完全一致（仍只做几何采集与坐标核对）。

需要说明的是：`OSB.Domain.dll` 在 17:08 已重新部署，`OSB.GameAdapter` 依赖它。
**结构未变、公开 API 未删除**，理论上不会影响加载，但本轮未安排启动验证。
建议在 P2-B 首次启动时顺带确认无加载回归（判据：`[OSB][P1] 补丁已挂载` 两行仍在、
`建筑总数=424` 不变）。

---

## 6. 未解决问题

| 编号 | 问题 | 影响 | 处置 |
|---|---|---|---|
| A-1 | `Point2i` 是只读属性结构，`System.Text.Json` 无法直接反序列化 | 中 | P2-B 序列化时必须提供 `JsonConverter` 或改用 DTO，不能等到联调时才发现 |
| A-2 | `Footprint` 结构已无生产代码引用，仅剩测试 | 低 | 保留并加注释警示；是否删除待 P2-B 定型后决定 |
| A-3 | `Blueprint.Validate()` 的负坐标检查只覆盖 X/Y 负方向，未检查是否超出世界边界 | 低 | 世界边界属于放置期校验（P2-C），此处不做 |
| A-4 | `Settings` 字典尚未定义已知键的序列化格式 | 中 | P2-B 需要选定格式并保留未知键（opaque 语义） |
| A-5 | 蓝图尚未与 `BuildingGeometryTable` 建立引用校验（PrefabId 是否真实存在） | 中 | 属于适配层职责，P2-B 捕获时校验 |

---

## 7. 下一阶段建议（仅建议，不执行）

1. **P2-B 开工第一件事是定序列化格式**，特别是 `Point2i` 与 `Settings`。
   建议 `System.Text.Json` + 自定义 `Point2iConverter`（写 `{"x":1,"y":2}` 或 `[1,2]`），
   并在 `Blueprint` 上保留 `SchemaVersion` 的显式读写校验。
2. **捕获流程先做"只读快照 + 立即回读比对"**：扫描 → 转记录 → 序列化 → 反序列化 →
   与快照逐字段比对。这一步能在不碰世界的前提下验证整条数据链路。
3. **`BuildingRecord` 的构造必须由 `BuildingGeometry` 驱动**，不要从 `BuildingDef` 直接取字段，
   否则 `AnchorInFootprint` 与 `ActualSize` 会与几何库不一致。
4. **测试殖民地**：P2-B 要求"使用新建测试殖民地验证"，建议开工前先建一个固定种子的测试存档
   并记录在 `AGENTS.md`，避免每次临时找地方。
5. **不要提前写放置逻辑**。P2-C 的冲突检测必须建立在 P2-B 验证过的捕获数据之上，
   否则会在错误的数据模型上叠加错误。
