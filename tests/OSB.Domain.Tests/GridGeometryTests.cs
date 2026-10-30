using System;

namespace OSB.Domain.Tests
{
    /// <summary>
    /// 栅格索引数学测试。期望值来自游戏 <c>Grid</c> 的反编译实现（docs/API-Audit.md G-001）：
    ///   XYToCell(x,y) = x + y*W ；CellColumn = cell%W ；CellRow = cell/W
    /// </summary>
    internal static class GridGeometryTests
    {
        internal static void IndexFormula()
        {
            const int w = 10;

            Check.Equal(0, GridGeometry.ToIndex(0, 0, w), "(0,0) → 0");
            Check.Equal(3, GridGeometry.ToIndex(3, 0, w), "(3,0) → 3");
            Check.Equal(10, GridGeometry.ToIndex(0, 1, w), "(0,1) → 10");
            Check.Equal(23, GridGeometry.ToIndex(3, 2, w), "(3,2) → 23");
            Check.Equal(-7, GridGeometry.ToIndex(3, -1, w), "(3,-1) → -7（不做边界检查，与游戏一致）");
        }

        internal static void InverseMapping()
        {
            const int w = 10;

            GridGeometry.ToXY(23, w, out int x, out int y);
            Check.Equal(3, x, "索引 23 的列");
            Check.Equal(2, y, "索引 23 的行");

            Check.Equal(new Point2i(0, 0), GridGeometry.ToPoint(0, w), "索引 0");
            Check.Equal(new Point2i(9, 0), GridGeometry.ToPoint(9, w), "索引 9 是第 0 行最后一列");
            Check.Equal(new Point2i(0, 1), GridGeometry.ToPoint(10, w), "索引 10 是第 1 行首列");
        }

        internal static void RoundtripOverFullGrid()
        {
            const int w = 7;
            const int h = 5;

            int mismatches = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int index = GridGeometry.ToIndex(x, y, w);
                    Point2i back = GridGeometry.ToPoint(index, w);
                    if (back.X != x || back.Y != y)
                    {
                        mismatches++;
                    }
                }
            }

            Check.Equal(0, mismatches, "全网格 " + (w * h) + " 格的索引↔坐标往返必须全部一致");
        }

        internal static void BoundsChecking()
        {
            Check.True(GridGeometry.IsInBounds(0, 0, 10, 10), "(0,0) 在界内");
            Check.True(GridGeometry.IsInBounds(9, 9, 10, 10), "(9,9) 在界内");
            Check.False(GridGeometry.IsInBounds(10, 0, 10, 10), "x 越界");
            Check.False(GridGeometry.IsInBounds(0, 10, 10, 10), "y 越界");
            Check.False(GridGeometry.IsInBounds(-1, 0, 10, 10), "x 为负");
            Check.False(GridGeometry.IsInBounds(0, -1, 10, 10), "y 为负");
            Check.False(GridGeometry.IsInBounds(0, 0, 0, 10), "零宽一律不合格");
        }

        internal static void ValidIndex()
        {
            Check.True(GridGeometry.IsValidIndex(0, 100), "0 是合法索引");
            Check.True(GridGeometry.IsValidIndex(99, 100), "最后一个索引合法");
            Check.False(GridGeometry.IsValidIndex(100, 100), "等于 cellCount 非法");
            Check.False(GridGeometry.IsValidIndex(-1, 100), "负数非法");
            Check.False(GridGeometry.IsValidIndex(0, 0), "空网格无合法索引");
        }

        internal static void OffsetWraparoundHazard()
        {
            const int w = 10;
            const int h = 5;

            // 第 0 行最后一列（索引 9），向右偏移 +1
            int corner = GridGeometry.ToIndex(w - 1, 0, w);
            int wrapped = GridGeometry.Offset(corner, 1, 0, w);
            Point2i wrappedPoint = GridGeometry.ToPoint(wrapped, w);

            // 这是 Grid.OffsetCell 的真实行为：不加校验，直接绕到下一行首列
            Check.Equal(10, wrapped, "Offset(corner,+1) 的索引值为 10");
            Check.Equal(new Point2i(0, 1), wrappedPoint, "绕回后变成第 1 行第 0 列 —— 合法但错误");

            // 因此必须用带校验的版本
            Check.False(GridGeometry.IsOffsetInBounds(corner, 1, 0, w, h), "带校验的偏移必须判定为越界");
            Check.True(GridGeometry.IsOffsetInBounds(corner, -1, 0, w, h), "向左偏移合法");
            Check.True(GridGeometry.IsOffsetInBounds(corner, 0, 1, w, h), "向下（y+1）偏移合法");
        }

        internal static void OffsetVerticalAndDiagonal()
        {
            const int w = 10;
            int origin = GridGeometry.ToIndex(4, 4, w);

            Check.Equal(origin + w, GridGeometry.Offset(origin, 0, 1, w), "y+1 等于 +width");
            Check.Equal(origin + 1, GridGeometry.Offset(origin, 1, 0, w), "x+1 等于 +1");
            Check.Equal(origin + 2 + (3 * w), GridGeometry.Offset(origin, 2, 3, w), "斜向偏移");
        }

        internal static void InvalidWidthRejected()
        {
            Check.Throws<ArgumentOutOfRangeException>(() => GridGeometry.ToIndex(0, 0, 0), "宽度为 0 应抛异常");
            Check.Throws<ArgumentOutOfRangeException>(() => GridGeometry.ToIndex(0, 0, -5), "宽度为负应抛异常");
            Check.Throws<ArgumentOutOfRangeException>(() => GridGeometry.Offset(0, 1, 1, 0), "Offset 宽度为 0 应抛异常");
        }

        internal static void OffsetPoint()
        {
            Check.Equal(new Point2i(13, 27), GridGeometry.OffsetPoint(new Point2i(10, 20), new Point2i(3, 7)), "锚点加偏移");
            Check.Equal(new Point2i(7, 13), GridGeometry.OffsetPoint(new Point2i(10, 20), new Point2i(-3, -7)), "负偏移也允许");
        }
    }
}
