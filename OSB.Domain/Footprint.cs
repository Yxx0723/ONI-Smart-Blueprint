using System;
using System.Collections.Generic;

namespace OSB.Domain
{
    /// <summary>
    /// 矩形占位描述（宽 × 高 + 锚点偏移）。
    ///
    /// ⚠️ **P2-A 起，本类型不再用于蓝图占用判定，仅作声明尺寸的几何辅助。**
    ///
    /// 原因：ONI 的声明尺寸（`WidthInCells`/`HeightInCells`）只描述视觉，
    /// 与真实占位可以不一致（`FlowerVaseHanging` 声明 1x2、实占 1x1）；
    /// 且建筑占位未必是矩形。用矩形代替真实占用格会虚占格子。
    ///
    /// **真实占用一律用 <see cref="BuildingRecord.OccupiedCells()"/>**，
    /// 它由原始 <see cref="BuildingRecord.PlacementOffsets"/> 逐个旋转求和得到。
    /// </summary>
    public readonly struct Footprint : IEquatable<Footprint>
    {
        public static readonly Footprint Single = new Footprint(1, 1, Point2i.Zero);

        public int Width { get; }
        public int Height { get; }
        /// <summary>建筑锚点在 footprint 内的局部偏移（世界坐标换算时的基准格）。</summary>
        public Point2i Anchor { get; }

        public Footprint(int width, int height, Point2i anchor)
        {
            Width = width;
            Height = height;
            Anchor = anchor;
        }

        public Footprint(int width, int height) : this(width, height, Point2i.Zero)
        {
        }

        public bool IsValid => Width > 0 && Height > 0;

        public int CellCount => IsValid ? Width * Height : 0;

        public bool Contains(Point2i local) =>
            IsValid && local.X >= 0 && local.X < Width && local.Y >= 0 && local.Y < Height;

        /// <summary>枚举 footprint 覆盖的全部局部格，顺序为逐行自下而上、自左向右。</summary>
        public IEnumerable<Point2i> Cells()
        {
            if (!IsValid)
            {
                yield break;
            }

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    yield return new Point2i(x, y);
                }
            }
        }

        /// <summary>
        /// 把锚点（世界格坐标）换算成 footprint 在世界中占据的矩形。
        /// Anchor 是相对于 footprint 左下角的偏移。
        /// </summary>
        public Rect2i ToWorldRect(Point2i anchorWorldCell) =>
            IsValid
                ? new Rect2i(anchorWorldCell.X - Anchor.X, anchorWorldCell.Y - Anchor.Y, Width, Height)
                : Rect2i.Empty;

        public bool Equals(Footprint other) =>
            Width == other.Width && Height == other.Height && Anchor.Equals(other.Anchor);

        public override bool Equals(object obj) => obj is Footprint other && Equals(other);

        public override int GetHashCode() => unchecked(((Width * 397) ^ Height) * 397 ^ Anchor.GetHashCode());

        public override string ToString() => Width + "x" + Height + "@" + Anchor;
    }
}
