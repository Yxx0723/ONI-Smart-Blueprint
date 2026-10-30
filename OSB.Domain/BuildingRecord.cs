using System;
using System.Collections.Generic;

namespace OSB.Domain
{
    /// <summary>
    /// 蓝图中的单个建筑记录。
    ///
    /// ## 三个必须分清的概念（P1 实测确立，违反任一都会导致落位错位）
    ///
    /// | 概念 | 来源 | 用途 |
    /// |---|---|---|
    /// | **声明尺寸** <see cref="DeclaredSize"/> | 游戏 `WidthInCells` / `HeightInCells` | **只描述视觉**，不参与任何占用判定 |
    /// | **实际包围盒** <see cref="OccupiedBounds()"/> | 由 <see cref="PlacementOffsets"/> 旋转后求出 | 用于裁剪、预览框、范围预览 |
    /// | **真实占用格集合** <see cref="OccupiedCells()"/> | 逐个 `PlacementOffsets` 旋转求和 | **唯一可用于重叠 / 地形 / 边界判定的依据** |
    ///
    /// **禁止用包围盒替代真实占用格**：声明 1x2 的建筑可能只占 1 格
    /// （`FlowerVaseHanging` 就调用 `GenerateOffsets(1,1)`），用包围盒判定会凭空多占一格。
    ///
    /// ## 三套坐标的换算关系
    ///
    /// ```
    /// 世界坐标 WorldCapturePosition   ← 捕获时记录，不参与蓝图内部运算
    ///        │  减去蓝图原点 Origin
    ///        ▼
    /// 蓝图局部坐标 LocalPosition      ← 归一化后的锚点坐标
    ///        │  加上 rotate(PlacementOffsets[i], Orientation)
    ///        ▼
    /// 局部占用格 OccupiedCells()
    ///        │  加上 Origin
    ///        ▼
    /// 真实游戏格 GameCells(origin)
    /// ```
    ///
    /// **关键点：锚点不是占位矩形的左下角。** ONI 的建筑水平居中锚定
    /// （`BuildingDef.GenerateOffsets`：`x = j + width/2 - width + 1`），
    /// 3 格宽建筑锚定在 (0,0) 时实际占 x ∈ [-1,1]。
    /// 需要矩形左下角时用 <see cref="FootprintCorner"/>，不要直接拿 <see cref="LocalPosition"/>。
    /// </summary>
    public sealed class BuildingRecord
    {
        /// <summary>蓝图内唯一标识。</summary>
        public string RecordId { get; set; }

        /// <summary>ONI 建筑 Prefab ID。</summary>
        public string PrefabId { get; set; }

        /// <summary>锚点格的蓝图局部坐标（归一化后）。**不是**占位矩形左下角。</summary>
        public Point2i LocalPosition { get; set; }

        /// <summary>捕获时锚点的世界格坐标。用于回溯与存档对齐，不参与蓝图内部运算。</summary>
        public Point2i WorldCapturePosition { get; set; }

        public BuildingOrientation Orientation { get; set; }

        public LayerKind Layer { get; set; }

        /// <summary>建造材料 Tag。空字符串表示使用游戏默认材料。</summary>
        public string MaterialTag { get; set; }

        /// <summary>
        /// 声明尺寸（游戏 `WidthInCells` × `HeightInCells`）。
        /// **仅用于展示与校验，不得用于计算占用。**
        /// </summary>
        public Point2i DeclaredSize { get; set; }

        /// <summary>
        /// 原始占位偏移（Neutral 朝向下，相对锚点）。**不得丢弃，不得用包围盒替代。**
        /// 顺序与游戏一致，可能含重复或非矩形分布。
        /// </summary>
        public IList<Point2i> PlacementOffsets { get; set; } = new List<Point2i>();

        /// <summary>
        /// 可安全读取的建筑设置。键为设置类型名，值为序列化文本。
        /// 未知键必须原样保留，不得静默丢弃。
        /// </summary>
        public IDictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        public int OffsetCount => PlacementOffsets == null ? 0 : PlacementOffsets.Count;

        private bool HasOffsets => OffsetCount > 0;

        /// <summary>Neutral 朝向下占位偏移的包围盒（相对锚点）。可能含负坐标。</summary>
        public Rect2i NeutralBounds => ComputeBounds(PlacementOffsets);

        /// <summary>实际占位尺寸（由包围盒得出）。**这是"占几格"的权威值。**</summary>
        public Point2i ActualSize
        {
            get
            {
                Rect2i bounds = NeutralBounds;
                return bounds.IsEmpty ? Point2i.Zero : new Point2i(bounds.Width, bounds.Height);
            }
        }

        /// <summary>
        /// 锚点格在 Neutral 占位矩形内的位置（相对矩形左下角）。
        /// 3 宽建筑为 (1,0)，1 宽建筑为 (0,0)；Y 方向恒为 0（底部对齐）。
        /// </summary>
        public Point2i AnchorInFootprint
        {
            get
            {
                Rect2i bounds = NeutralBounds;
                return bounds.IsEmpty ? Point2i.Zero : new Point2i(-bounds.MinX, -bounds.MinY);
            }
        }

        /// <summary>声明尺寸与实际占位是否一致。不一致不一定是错误（游戏允许）。</summary>
        public bool SizeMatchesDeclaration =>
            ActualSize.X == DeclaredSize.X && ActualSize.Y == DeclaredSize.Y;

        /// <summary>指定朝向下，锚点在占位矩形内的位置。</summary>
        public Point2i AnchorInFootprintFor(BuildingOrientation orientation)
        {
            Rect2i bounds = ComputeBounds(RotatedOffsets(orientation));
            return bounds.IsEmpty ? Point2i.Zero : new Point2i(-bounds.MinX, -bounds.MinY);
        }

        /// <summary>指定朝向下、相对锚点的占位偏移（新数组，不修改原始数据）。</summary>
        public Point2i[] RotatedOffsets(BuildingOrientation orientation)
        {
            if (!HasOffsets)
            {
                return Array.Empty<Point2i>();
            }

            return GeometryOrientation.RotateAll(PlacementOffsets, orientation);
        }

        /// <summary>蓝图局部坐标下的真实占用格（按当前 <see cref="Orientation"/>）。</summary>
        public IReadOnlyList<Point2i> OccupiedCells() => OccupiedCells(Orientation);

        /// <summary>蓝图局部坐标下的真实占用格（指定朝向）。</summary>
        public IReadOnlyList<Point2i> OccupiedCells(BuildingOrientation orientation)
        {
            return OccupiedCellsAt(LocalPosition, orientation);
        }

        /// <summary>以任意锚点计算真实占用格。世界坐标换算走这里。</summary>
        public IReadOnlyList<Point2i> OccupiedCellsAt(Point2i anchorCell, BuildingOrientation orientation)
        {
            if (!HasOffsets)
            {
                return Array.Empty<Point2i>();
            }

            var result = new Point2i[PlacementOffsets.Count];
            for (int i = 0; i < PlacementOffsets.Count; i++)
            {
                result[i] = anchorCell + GeometryOrientation.Rotate(PlacementOffsets[i], orientation);
            }

            return result;
        }

        /// <summary>蓝图局部坐标下的占位包围盒。<b>不得用于占用判定。</b></summary>
        public Rect2i OccupiedBounds() => OccupiedBounds(Orientation);

        public Rect2i OccupiedBounds(BuildingOrientation orientation)
        {
            return OffsetRect(ComputeBounds(RotatedOffsets(orientation)), LocalPosition);
        }

        /// <summary>以任意锚点计算占位包围盒。<b>不得用于占用判定。</b></summary>
        public Rect2i OccupiedBoundsAt(Point2i anchorCell, BuildingOrientation orientation)
        {
            return OffsetRect(ComputeBounds(RotatedOffsets(orientation)), anchorCell);
        }

        /// <summary>
        /// 占位矩形左下角（指定锚点与朝向下的世界格）。
        /// 等价于"锚点 + 包围盒最小偏移"，是 <see cref="AnchorForFootprintCorner"/> 的逆运算。
        /// </summary>
        public Point2i FootprintCorner(Point2i anchorCell, BuildingOrientation orientation)
        {
            Rect2i local = ComputeBounds(RotatedOffsets(orientation));
            if (local.IsEmpty)
            {
                return anchorCell;
            }

            return new Point2i(anchorCell.X + local.MinX, anchorCell.Y + local.MinY);
        }

        /// <summary>由"矩形左下角"反推锚点格。<see cref="FootprintCorner"/> 的逆运算。</summary>
        public Point2i AnchorForFootprintCorner(Point2i cornerCell, BuildingOrientation orientation)
        {
            Rect2i local = ComputeBounds(RotatedOffsets(orientation));
            if (local.IsEmpty)
            {
                return cornerCell;
            }

            return new Point2i(cornerCell.X - local.MinX, cornerCell.Y - local.MinY);
        }

        /// <summary>真实游戏格（蓝图原点 + 局部锚点 + 旋转偏移）。</summary>
        public IReadOnlyList<Point2i> GameCells(Point2i blueprintOrigin)
        {
            var worldAnchor = new Point2i(
                blueprintOrigin.X + LocalPosition.X,
                blueprintOrigin.Y + LocalPosition.Y);
            return OccupiedCellsAt(worldAnchor, Orientation);
        }

        /// <summary>
        /// 结构化校验。返回空集合表示该记录本身无问题。
        ///
        /// **本方法不得抛出异常**：输入非法正是它要报告的东西。
        /// 因此遇到哨兵朝向或缺偏移时会先报告、再提前退出，
        /// 而不是让后续的占位计算炸掉调用方。
        /// </summary>
        public IEnumerable<ValidationIssue> Validate()
        {
            if (string.IsNullOrWhiteSpace(RecordId))
            {
                yield return new ValidationIssue("OSB006", IssueSeverity.Blocker, "建筑记录缺少 RecordId。", LocalPosition);
            }

            if (string.IsNullOrWhiteSpace(PrefabId))
            {
                yield return new ValidationIssue("OSB008", IssueSeverity.Blocker, "建筑记录缺少 PrefabId。", LocalPosition);
            }

            bool sentinelOrientation = GeometryOrientation.IsSentinel(Orientation);
            if (sentinelOrientation)
            {
                yield return new ValidationIssue(
                    "OSB014",
                    IssueSeverity.Blocker,
                    "Orientation 为 NumRotations 哨兵值，不是可用朝向。",
                    LocalPosition);
            }

            if (DeclaredSize.X <= 0 || DeclaredSize.Y <= 0)
            {
                yield return new ValidationIssue(
                    "OSB013",
                    IssueSeverity.Blocker,
                    "声明尺寸非正：" + DeclaredSize.X + "x" + DeclaredSize.Y,
                    LocalPosition);
            }

            if (!HasOffsets)
            {
                yield return new ValidationIssue(
                    "OSB009",
                    IssueSeverity.Blocker,
                    "缺少 PlacementOffsets，无法确定真实占用格；不得用包围盒替代。",
                    LocalPosition);
                yield break;
            }

            // 声明尺寸与实际占位不一致：游戏允许（吊兰声明 1x2 实占 1x1），只提示不阻断。
            // NeutralBounds 不涉及旋转，哨兵朝向下也可安全求值。
            if (!SizeMatchesDeclaration)
            {
                yield return new ValidationIssue(
                    "OSB012",
                    IssueSeverity.Warning,
                    "声明尺寸 " + DeclaredSize.X + "x" + DeclaredSize.Y
                    + " 与实际占位 " + ActualSize.X + "x" + ActualSize.Y + " 不同（以实际占位为准）",
                    LocalPosition);
            }

            if (sentinelOrientation)
            {
                // 哨兵值无法参与旋转，几何检查到此为止，避免抛异常打断整份蓝图的校验
                yield break;
            }

            // 归一化检查基于**真实占用格**，不是锚点：
            // 3 宽建筑锚定在 (0,0) 时占 x=-1..1，锚点非负但实际已越界。
            foreach (Point2i cell in OccupiedCells())
            {
                if (cell.X < 0 || cell.Y < 0)
                {
                    yield return new ValidationIssue(
                        "OSB010",
                        IssueSeverity.Blocker,
                        "占用格出现负坐标 " + cell + "，蓝图未正确归一化。",
                        cell);
                    yield break;
                }
            }
        }

        public override string ToString() =>
            (RecordId ?? "?") + ":" + (PrefabId ?? "?") + "@" + LocalPosition
            + " " + Orientation + " cells=" + OffsetCount;

        private static Rect2i ComputeBounds(IList<Point2i> offsets)
        {
            if (offsets == null || offsets.Count == 0)
            {
                return Rect2i.Empty;
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int i = 0; i < offsets.Count; i++)
            {
                Point2i p = offsets[i];
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            return Rect2i.FromMinMax(minX, minY, maxX, maxY);
        }

        private static Rect2i OffsetRect(Rect2i rect, Point2i delta)
        {
            return rect.IsEmpty
                ? Rect2i.Empty
                : new Rect2i(rect.X + delta.X, rect.Y + delta.Y, rect.Width, rect.Height);
        }
    }
}
