using System.Collections.Generic;
using System.Linq;

namespace OSB.Domain.Tests
{
    /// <summary>Point2i / Rect2i / Footprint 的几何与集合运算测试。</summary>
    internal static class GeometryTests
    {
        // ---------------- Point2i ----------------

        internal static void PointEquality()
        {
            var a = new Point2i(3, -4);
            var b = new Point2i(3, -4);

            Check.True(a == b, "相同坐标应相等");
            Check.True(a.Equals(b), "Equals 应与 == 一致");
            Check.True(a != new Point2i(4, -4), "X 不同应不等");
            Check.True(a != new Point2i(3, -3), "Y 不同应不等");
            Check.Equal(Point2i.Zero, new Point2i(0, 0), "Zero 常量应为 (0,0)");
            Check.Equal(new Point2i(5, 7), new Point2i(2, 3) + new Point2i(3, 4), "加法");
            Check.Equal(new Point2i(-1, -1), new Point2i(2, 3) - new Point2i(3, 4), "减法");
        }

        internal static void PointHash()
        {
            var a = new Point2i(11, 22);
            var b = new Point2i(11, 22);
            Check.Equal(a.GetHashCode(), b.GetHashCode(), "等价实例的哈希必须相同");

            // 作为字典键必须表现正确
            var map = new Dictionary<Point2i, string> { [a] = "v" };
            Check.Equal("v", map[b], "相等键应命中同一条目");
        }

        internal static void PointRotate()
        {
            // (x,y) -> (y,-x)
            Check.Equal(new Point2i(0, -1), new Point2i(1, 0).RotateClockwise90(), "(1,0) 顺转 90° 应为 (0,-1)");
            Check.Equal(new Point2i(3, -2), new Point2i(2, 3).RotateClockwise90(), "(2,3) 顺转 90° 应为 (3,-2)");
        }

        internal static void PointRotateFourTimes()
        {
            var original = new Point2i(7, -5);
            Point2i rotated = original;
            for (int i = 0; i < 4; i++)
            {
                rotated = rotated.RotateClockwise90();
            }

            Check.Equal(original, rotated, "旋转四次必须回到原点（TR-4.3 要求）");
        }

        internal static void PointMirrorTwice()
        {
            var original = new Point2i(-9, 4);
            Check.Equal(original, original.MirrorX().MirrorX(), "镜像两次必须还原");
        }

        // ---------------- Rect2i ----------------

        internal static void RectContains()
        {
            var rect = new Rect2i(2, 3, 4, 5); // x:2..5, y:3..7
            Check.Equal(2, rect.MinX, "MinX");
            Check.Equal(5, rect.MaxX, "MaxX");
            Check.Equal(3, rect.MinY, "MinY");
            Check.Equal(7, rect.MaxY, "MaxY");

            Check.True(rect.Contains(new Point2i(2, 3)), "左下角属于矩形");
            Check.True(rect.Contains(new Point2i(5, 7)), "右上角属于矩形（闭区间）");
            Check.False(rect.Contains(new Point2i(6, 7)), "X 越界");
            Check.False(rect.Contains(new Point2i(5, 8)), "Y 越界");
            Check.False(Rect2i.Empty.Contains(Point2i.Zero), "空矩形不含任何点");
        }

        internal static void RectIntersect()
        {
            var a = new Rect2i(0, 0, 4, 4);   // 0..3
            var b = new Rect2i(2, 2, 4, 4);   // 2..5
            Rect2i overlap = a.Intersect(b);

            Check.Equal(new Rect2i(2, 2, 2, 2), overlap, "交集应为 2x2 于 (2,2)");
            Check.Equal(4, overlap.Area, "交集面积");
        }

        internal static void RectIntersectDisjoint()
        {
            var a = new Rect2i(0, 0, 2, 2);
            var b = new Rect2i(5, 5, 2, 2);
            Check.True(a.Intersect(b).IsEmpty, "不相交应返回 Empty");
            Check.False(a.Intersects(b), "Intersects 应为 false");

            // 相邻但不重叠（a 到 1，b 从 2 开始）必须判定为不相交
            var c = new Rect2i(0, 0, 2, 2);
            var d = new Rect2i(2, 0, 2, 2);
            Check.False(c.Intersects(d), "紧邻的矩形不应视为相交");
        }

        internal static void RectUnion()
        {
            var a = new Rect2i(0, 0, 2, 2);   // 0..1
            var b = new Rect2i(3, 1, 2, 2);   // 3..4, 1..2
            Rect2i union = a.Union(b);

            Check.Equal(0, union.MinX, "Union MinX");
            Check.Equal(4, union.MaxX, "Union MaxX");
            Check.Equal(0, union.MinY, "Union MinY");
            Check.Equal(2, union.MaxY, "Union MaxY");
        }

        internal static void RectUnionWithEmpty()
        {
            var a = new Rect2i(1, 1, 3, 3);
            Check.Equal(a, Rect2i.Empty.Union(a), "Empty 与 A 的并集应为 A");
            Check.Equal(a, a.Union(Rect2i.Empty), "A 与 Empty 的并集应为 A");
        }

        internal static void RectFromMinMaxInvalid()
        {
            Check.True(Rect2i.FromMinMax(5, 5, 4, 10).IsEmpty, "maxX < minX 应返回 Empty");
            Check.True(Rect2i.FromMinMax(5, 5, 10, 4).IsEmpty, "maxY < minY 应返回 Empty");
            Check.Equal(new Rect2i(1, 2, 3, 4), Rect2i.FromMinMax(1, 2, 3, 5), "合法区间应正确换算");
        }

        internal static void RectEmptyArea()
        {
            Check.Equal(0, Rect2i.Empty.Area, "空矩形面积为 0");
            Check.Equal(0, new Rect2i(0, 0, -3, 5).Area, "负宽度面积为 0");
        }

        // ---------------- Footprint ----------------

        internal static void FootprintCellCount()
        {
            var fp = new Footprint(3, 4);
            Check.Equal(12, fp.CellCount, "3x4 footprint 应占 12 格");
            Check.True(fp.IsValid, "3x4 合法");
            Check.False(new Footprint(0, 4).IsValid, "零宽非法");
            Check.Equal(0, new Footprint(0, 4).CellCount, "非法 footprint 占 0 格");
        }

        internal static void FootprintCells()
        {
            var fp = new Footprint(2, 2);
            List<Point2i> cells = fp.Cells().ToList();

            Check.Equal(4, cells.Count, "2x2 应枚举 4 格");
            Check.True(cells.Contains(new Point2i(0, 0)), "含 (0,0)");
            Check.True(cells.Contains(new Point2i(1, 1)), "含 (1,1)");
            Check.False(cells.Contains(new Point2i(2, 0)), "不含 (2,0)");
            Check.False(cells.Contains(new Point2i(-1, 0)), "不含 (-1,0)");
            Check.True(fp.Contains(new Point2i(1, 0)), "局部 (1,0) 在 footprint 内");
        }

        internal static void FootprintWorldRect()
        {
            // 3x2，锚点在左下角
            var fp = new Footprint(3, 2, Point2i.Zero);
            Rect2i world = fp.ToWorldRect(new Point2i(10, 20));
            Check.Equal(new Rect2i(10, 20, 3, 2), world, "锚点即左下角");

            // 锚点在中心 (1,0)，放置于 (10,20) → 矩形应从 (9,20) 起
            var centered = new Footprint(3, 1, new Point2i(1, 0));
            Check.Equal(new Rect2i(9, 20, 3, 1), centered.ToWorldRect(new Point2i(10, 20)), "偏移锚点换算");
        }

        internal static void FootprintInvalid()
        {
            Check.False(new Footprint(0, 0).Cells().Any(), "非法 footprint 不应产出任何格");
            Check.True(new Footprint(0, 0).ToWorldRect(Point2i.Zero).IsEmpty, "非法 footprint 世界矩形应为空");
        }
    }
}
