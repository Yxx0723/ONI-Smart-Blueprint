using System;

namespace OSB.Domain
{
    /// <summary>
    /// 栅格整数坐标。约定：X 向右为正，Y 向上为正；原点的语义由调用方定义
    /// （蓝图内为蓝图局部原点，世界内为 ONI 世界原点）。世界坐标只在放置阶段转换。
    /// 本类型不依赖任何游戏程序集。
    /// </summary>
    public readonly struct Point2i : IEquatable<Point2i>
    {
        public static readonly Point2i Zero = new Point2i(0, 0);

        public int X { get; }
        public int Y { get; }

        public Point2i(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Point2i other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Point2i other && Equals(other);

        public override int GetHashCode() => unchecked((X * 397) ^ Y);

        public override string ToString() => "(" + X + ", " + Y + ")";

        public static Point2i operator +(Point2i a, Point2i b) => new Point2i(a.X + b.X, a.Y + b.Y);

        public static Point2i operator -(Point2i a, Point2i b) => new Point2i(a.X - b.X, a.Y - b.Y);

        public static bool operator ==(Point2i a, Point2i b) => a.Equals(b);

        public static bool operator !=(Point2i a, Point2i b) => !a.Equals(b);

        /// <summary>
        /// 绕原点顺时针旋转 90° 的纯几何变换。
        /// 注意：这里的"顺时针"是数学约定；ONI 的实际坐标系方向必须由 GameAdapter 校准后
        /// 才能把此结果当作 Orientation 使用（见 docs/API-Audit.md 中的 Rotatable 条目）。
        /// </summary>
        public Point2i RotateClockwise90() => new Point2i(Y, -X);

        /// <summary>沿 Y 轴镜像的纯几何变换。</summary>
        public Point2i MirrorX() => new Point2i(-X, Y);
    }
}
