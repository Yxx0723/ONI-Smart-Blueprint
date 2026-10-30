using System;
using System.Collections.Generic;

namespace OSB.Domain
{
    /// <summary>
    /// 格偏移的朝向变换。
    ///
    /// **公式逐字镜像游戏 <c>Rotatable.GetRotatedCellOffset(CellOffset, Orientation)</c>**（已反编译核实）：
    ///   R90   → ( y, -x)
    ///   R180  → (-x, -y)
    ///   R270  → (-y,  x)
    ///   FlipH → (-x,  y)
    ///   FlipV → ( x, -y)
    ///   Neutral → 恒等
    ///
    /// 与游戏唯一的有意差异：游戏的 default 分支会把 <see cref="BuildingOrientation.NumRotations"/>
    /// 当恒等变换**静默**处理，本实现改为抛异常。哨兵值被当作真实朝向使用是典型的隐性 bug，
    /// 宁可让它在测试里炸掉，也不要在游戏里悄悄算错格子。
    /// </summary>
    public static class GeometryOrientation
    {
        /// <summary>该朝向值是否为"旋转形态数量"哨兵，而非可用朝向。</summary>
        public static bool IsSentinel(BuildingOrientation orientation) =>
            orientation == BuildingOrientation.NumRotations;

        /// <summary>朝向值是否落在合法枚举范围内。</summary>
        public static bool IsDefined(BuildingOrientation orientation) =>
            orientation >= BuildingOrientation.Neutral && orientation <= BuildingOrientation.FlipV;

        /// <summary>
        /// 把单个格偏移按朝向变换。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// 传入哨兵值 <see cref="BuildingOrientation.NumRotations"/> 或未定义值时抛出。
        /// </exception>
        public static Point2i Rotate(Point2i offset, BuildingOrientation orientation)
        {
            switch (orientation)
            {
                case BuildingOrientation.Neutral:
                    return offset;

                case BuildingOrientation.R90:
                    return new Point2i(offset.Y, -offset.X);

                case BuildingOrientation.R180:
                    return new Point2i(-offset.X, -offset.Y);

                case BuildingOrientation.R270:
                    return new Point2i(-offset.Y, offset.X);

                case BuildingOrientation.FlipH:
                    return new Point2i(-offset.X, offset.Y);

                case BuildingOrientation.FlipV:
                    return new Point2i(offset.X, -offset.Y);

                case BuildingOrientation.NumRotations:
                    throw new ArgumentOutOfRangeException(
                        nameof(orientation),
                        orientation,
                        "NumRotations 是旋转数量哨兵值，不是可用朝向。使用前必须先过滤。");

                default:
                    throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "未定义的朝向值。");
            }
        }

        /// <summary>批量变换格偏移，顺序保持不变。</summary>
        public static Point2i[] RotateAll(IList<Point2i> offsets, BuildingOrientation orientation)
        {
            if (offsets == null)
            {
                throw new ArgumentNullException(nameof(offsets));
            }

            var result = new Point2i[offsets.Count];
            for (int i = 0; i < offsets.Count; i++)
            {
                result[i] = Rotate(offsets[i], orientation);
            }

            return result;
        }

        /// <summary>
        /// 变换后格集合的最小包围盒（格坐标，闭区间）。
        /// 空集合返回 <see cref="Rect2i.Empty"/>。
        /// </summary>
        public static Rect2i BoundsOf(IReadOnlyList<Point2i> offsets, BuildingOrientation orientation)
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
                Point2i p = Rotate(offsets[i], orientation);
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            return Rect2i.FromMinMax(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// 判断变换后的格集合是否仍落在 [0,width) × [0,height) 内。
        ///
        /// **仅对 <see cref="BuildingOrientation.Neutral"/> 有意义。**
        /// 旋转会让原始偏移出现负值（例如 (1,0) 在 R90 下变 (0,-1)），游戏落位时会重新锚定，
        /// 所以对旋转朝向调用本方法几乎必然返回 false——那不是缺陷，而是用错了判据。
        /// 判断旋转是否合法请比较 <see cref="BoundsOf"/> 的尺寸。
        /// </summary>
        public static bool FitsInSize(IReadOnlyList<Point2i> offsets, BuildingOrientation orientation, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            if (offsets == null || offsets.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < offsets.Count; i++)
            {
                Point2i p = Rotate(offsets[i], orientation);
                if (p.X < 0 || p.X >= width || p.Y < 0 || p.Y >= height)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
