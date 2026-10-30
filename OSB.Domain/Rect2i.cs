using System;

namespace OSB.Domain
{
    /// <summary>
    /// 整数栅格矩形。(X, Y) 为左下角，Width/Height 为格数（非"最大坐标"）。
    /// 空矩形（Width 或 Height &lt;= 0）参与集合运算时被安全忽略。
    /// </summary>
    public readonly struct Rect2i : IEquatable<Rect2i>
    {
        public static readonly Rect2i Empty = new Rect2i(0, 0, 0, 0);

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        public Rect2i(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int MinX => X;
        public int MinY => Y;
        public int MaxX => X + Width - 1;
        public int MaxY => Y + Height - 1;

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public int Area => IsEmpty ? 0 : Width * Height;

        public bool Contains(Point2i p) =>
            !IsEmpty && p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;

        public bool Intersects(Rect2i other)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return false;
            }

            return MinX <= other.MaxX && MaxX >= other.MinX
                && MinY <= other.MaxY && MaxY >= other.MinY;
        }

        /// <summary>返回交集；无交集时返回 Empty。</summary>
        public Rect2i Intersect(Rect2i other)
        {
            if (!Intersects(other))
            {
                return Empty;
            }

            int minX = Math.Max(MinX, other.MinX);
            int minY = Math.Max(MinY, other.MinY);
            int maxX = Math.Min(MaxX, other.MaxX);
            int maxY = Math.Min(MaxY, other.MaxY);
            return new Rect2i(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>返回最小包围盒；空矩形被忽略。</summary>
        public Rect2i Union(Rect2i other)
        {
            if (IsEmpty)
            {
                return other;
            }

            if (other.IsEmpty)
            {
                return this;
            }

            int minX = Math.Min(MinX, other.MinX);
            int minY = Math.Min(MinY, other.MinY);
            int maxX = Math.Max(MaxX, other.MaxX);
            int maxY = Math.Max(MaxY, other.MaxY);
            return new Rect2i(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>由"包含端点"的最小/最大坐标构造。</summary>
        public static Rect2i FromMinMax(int minX, int minY, int maxX, int maxY)
        {
            if (maxX < minX || maxY < minY)
            {
                return Empty;
            }

            return new Rect2i(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        public bool Equals(Rect2i other) =>
            X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        public override bool Equals(object obj) => obj is Rect2i other && Equals(other);

        public override int GetHashCode() =>
            unchecked((((X * 397) ^ Y) * 397 ^ Width) * 397 ^ Height);

        public override string ToString() =>
            "[" + X + "," + Y + " " + Width + "x" + Height + "]";
    }
}
