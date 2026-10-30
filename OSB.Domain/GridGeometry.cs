using System;

namespace OSB.Domain
{
    /// <summary>
    /// 栅格索引数学。
    ///
    /// **逐字镜像游戏 <c>Grid</c> 的实现**（已反编译核实，见 docs/API-Audit.md G-001）：
    ///   XYToCell(x, y)      = x + y * WidthInCells
    ///   CellColumn(cell)    = cell % WidthInCells
    ///   CellRow(cell)       = cell / WidthInCells
    ///   OffsetCell(cell,x,y)= cell + x + y * WidthInCells
    ///
    /// 由此可推出并已被实现印证：**格 Y 轴与 Unity 世界 Y 轴同向，向上为正，未翻转**。
    /// 因为 <c>CellToPos</c> 用的是 <c>CellSizeInMeters * (cell / Width)</c> 作为世界 y。
    ///
    /// 本类型不持有宽度状态，所有方法显式接收 width/height，便于离散测试与多世界假设。
    /// </summary>
    public static class GridGeometry
    {
        /// <summary>
        /// (x, y) → 线性索引。**不做边界检查**，与游戏保持一致。
        /// 越界输入会得到"看起来合法但指向别处"的索引，调用方应先用 <see cref="IsInBounds"/> 判定。
        /// </summary>
        public static int ToIndex(int x, int y, int width)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "宽度必须为正。");
            }

            return x + (y * width);
        }

        /// <summary>线性索引 → (x, y)。列优先取模，行取整除。</summary>
        public static void ToXY(int index, int width, out int x, out int y)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "宽度必须为正。");
            }

            x = index % width;
            y = index / width;
        }

        /// <summary>线性索引 → 格坐标。</summary>
        public static Point2i ToPoint(int index, int width)
        {
            ToXY(index, width, out int x, out int y);
            return new Point2i(x, y);
        }

        /// <summary>格坐标是否落在 [0,width) × [0,height) 内。</summary>
        public static bool IsInBounds(int x, int y, int width, int height)
        {
            return width > 0 && height > 0
                && x >= 0 && x < width
                && y >= 0 && y < height;
        }

        /// <summary>线性索引是否落在 [0, cellCount) 内。镜像游戏 <c>Grid.IsValidCell</c>。</summary>
        public static bool IsValidIndex(int index, int cellCount)
        {
            return cellCount > 0 && index >= 0 && index < cellCount;
        }

        /// <summary>
        /// 按偏移移动索引。**不做边界检查**，镜像游戏 <c>Grid.OffsetCell</c>。
        ///
        /// 危险点：从最后一列向右偏移会**绕回下一行**，产生一个"合法但错误"的索引，
        /// 而不是失败。任何构造性使用之前必须调用 <see cref="IsOffsetInBounds"/>。
        /// </summary>
        public static int Offset(int index, int dx, int dy, int width)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "宽度必须为正。");
            }

            return index + dx + (dy * width);
        }

        /// <summary>
        /// 带边界检查的偏移，镜像游戏 <c>Grid.IsCellOffsetValid</c>。
        /// 这是唯一应被用于"建筑占位是否越界"判定的方法。
        /// </summary>
        public static bool IsOffsetInBounds(int index, int dx, int dy, int width, int height)
        {
            if (!IsInBounds(0, 0, width, height))
            {
                return false;
            }

            ToXY(index, width, out int x, out int y);
            return IsInBounds(x + dx, y + dy, width, height);
        }

        /// <summary>
        /// 把相对锚点的偏移换算成绝对格坐标（数学意义上的，不含世界边界判定）。
        /// </summary>
        public static Point2i OffsetPoint(Point2i anchor, Point2i offset) => anchor + offset;
    }
}
