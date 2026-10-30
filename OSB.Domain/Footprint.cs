using System;
using System.Collections.Generic;

namespace OSB.Domain
{
    /// <summary>
    /// 建筑占位形状。ONI 的建筑是一个多格 footprint，不是单个中心点：
    /// 已验证 BuildingDef 暴露 WidthInCells / HeightInCells / PlacementOffsets
    /// （见 docs/API-Audit.md）。本类型只描述几何，不描述可建造性。
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
