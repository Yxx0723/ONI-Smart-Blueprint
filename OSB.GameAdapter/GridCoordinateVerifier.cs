using System;
using System.Collections.Generic;
using OSB.Domain;
using UnityEngine;

namespace OSB.GameAdapter
{
    /// <summary>
    /// 在真实游戏内核对 <c>Grid</c> 坐标规则。**只读**：不写世界、不改状态。
    ///
    /// 核对目标（对应 docs/API-Audit.md 的 UNKNOWN-1）：
    ///   1. <c>XYToCell</c> / <c>CellToXY</c> 是否为互逆；
    ///   2. 索引公式是否确为 <c>x + y * WidthInCells</c>；
    ///   3. 格 Y 轴方向是否与 Unity 世界 Y 轴同向（向上为正、未翻转）；
    ///   4. 边界行为：<c>IsValidCell</c> 与 <c>IsCellOffsetValid</c> 的返回值；
    ///   5. <c>OffsetCell</c> 的越界绕回风险是否真实存在。
    ///
    /// 任何一项不过，都必须阻止后续放置功能的开发。
    /// </summary>
    internal static class GridCoordinateVerifier
    {
        private const float Epsilon = 0.001f;

        private static int _passed;
        private static int _failed;

        internal static void Verify()
        {
            _passed = 0;
            _failed = 0;

            int width = Grid.WidthInCells;
            int height = Grid.HeightInCells;
            int cellCount = Grid.CellCount;
            float cellSize = Grid.CellSizeInMeters;

            OsbLog.Info("=== Grid 坐标核对开始 ===");
            OsbLog.Info("WidthInCells=" + width + " HeightInCells=" + height
                + " CellCount=" + cellCount + " CellSizeInMeters=" + cellSize);

            if (width <= 0 || height <= 0 || cellCount <= 0)
            {
                OsbLog.Warn("Grid 尚未初始化（尺寸非正），核对中止");
                return;
            }

            CheckFormula(width);
            CheckRoundtrip(width, height);
            CheckOffsetCells(width);
            CheckWorldSpaceMapping(width, cellSize);
            CheckYAxisDirection(width);
            CheckBoundaries(width, height, cellCount);
            CheckOffsetBoundaryGuard(width, height);

            string verdict = _failed == 0 ? "全部通过" : ("失败 " + _failed + " 项");
            OsbLog.Info("=== Grid 坐标核对结束：" + _passed + " 项通过，" + verdict + " ===");
        }

        /// <summary>核对索引公式：XYToCell(x,y) == x + y*W。</summary>
        private static void CheckFormula(int width)
        {
            int mismatches = 0;
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int actual = Grid.XYToCell(x, y);
                    int expected = GridGeometry.ToIndex(x, y, width);
                    if (actual != expected)
                    {
                        mismatches++;
                    }
                }
            }

            Assert(mismatches == 0, "索引公式 XYToCell(x,y) == x + y*WidthInCells（不符点 " + mismatches + "）");
        }

        /// <summary>核对 XYToCell ↔ CellToXY 互逆。</summary>
        private static void CheckRoundtrip(int width, int height)
        {
            var samples = new List<int>();

            // 覆盖四角与中心
            samples.Add(Grid.XYToCell(0, 0));
            samples.Add(Grid.XYToCell(width - 1, 0));
            samples.Add(Grid.XYToCell(0, height - 1));
            samples.Add(Grid.XYToCell(width - 1, height - 1));
            samples.Add(Grid.XYToCell(width / 2, height / 2));
            samples.Add(Grid.XYToCell(1, 1));

            int mismatches = 0;
            foreach (int cell in samples)
            {
                Grid.CellToXY(cell, out int x, out int y);
                Point2i expected = GridGeometry.ToPoint(cell, width);
                if (x != expected.X || y != expected.Y)
                {
                    mismatches++;
                }

                if (Grid.XYToCell(x, y) != cell)
                {
                    mismatches++;
                }
            }

            Assert(mismatches == 0, "XYToCell 与 CellToXY 互逆（采样 " + samples.Count + " 格，不符 " + mismatches + "）");
        }

        /// <summary>核对 OffsetCell 与纯数学偏移一致。</summary>
        private static void CheckOffsetCells(int width)
        {
            int origin = Grid.XYToCell(5, 7);
            int up = Grid.OffsetCell(origin, 0, 1);
            int right = Grid.OffsetCell(origin, 1, 0);
            int diagonal = Grid.OffsetCell(origin, 2, 3);

            bool ok = up == origin + width
                && right == origin + 1
                && diagonal == origin + 2 + (3 * width);

            Assert(ok, "OffsetCell 与数学偏移一致（y+1 → +Width，x+1 → +1）");
        }

        /// <summary>核对格坐标 → 世界坐标的线性关系。</summary>
        private static void CheckWorldSpaceMapping(int width, float cellSize)
        {
            if (cellSize <= 0f)
            {
                OsbLog.Warn("CellSizeInMeters 非正，跳过世界坐标映射核对");
                return;
            }

            int mismatches = 0;
            int[] probes = { Grid.XYToCell(3, 2), Grid.XYToCell(10, 5) };

            foreach (int cell in probes)
            {
                Grid.CellToXY(cell, out int x, out int y);
                Vector3 pos = Grid.CellToPos(cell);

                if (Mathf.Abs(pos.x - (cellSize * x)) > Epsilon)
                {
                    mismatches++;
                }

                if (Mathf.Abs(pos.y - (cellSize * y)) > Epsilon)
                {
                    mismatches++;
                }

                // 世界坐标 → 格索引 往返
                if (Grid.PosToCell(pos) != cell)
                {
                    mismatches++;
                }
            }

            Assert(mismatches == 0, "CellToPos 与 PosToCell 往返一致，且 pos = (cellSize*x, cellSize*y)");
        }

        /// <summary>核对 Y 轴方向：格 y 增大时世界 y 也增大（未翻转）。</summary>
        private static void CheckYAxisDirection(int width)
        {
            int lower = Grid.XYToCell(0, 1);
            int upper = Grid.XYToCell(0, 2);

            Vector3 lowerPos = Grid.CellToPos(lower);
            Vector3 upperPos = Grid.CellToPos(upper);

            Assert(upperPos.y > lowerPos.y, "格 Y 轴向上为正、未翻转（y=2 的世界 Y 大于 y=1）");
            Assert(GridGeometry.ToIndex(0, 2, width) > GridGeometry.ToIndex(0, 1, width),
                "领域层索引随 Y 增大而增大，与游戏同向");
        }

        /// <summary>核对边界判定。</summary>
        private static void CheckBoundaries(int width, int height, int cellCount)
        {
            Assert(Grid.IsValidCell(0), "IsValidCell(0) 为真");
            Assert(Grid.IsValidCell(cellCount - 1), "IsValidCell(CellCount-1) 为真");
            Assert(!Grid.IsValidCell(-1), "IsValidCell(-1) 为假");
            Assert(!Grid.IsValidCell(cellCount), "IsValidCell(CellCount) 为假");

            int corner = Grid.XYToCell(width - 1, 0);
            Assert(!Grid.IsCellOffsetValid(corner, 1, 0), "最后一列向右偏移被判为非法（不会静默绕行）");
            Assert(Grid.IsCellOffsetValid(corner, -1, 0), "最后一列向左偏移合法");

            int bottom = Grid.XYToCell(0, 0);
            Assert(!Grid.IsCellOffsetValid(bottom, 0, -1), "最底层向下偏移被判为非法");
            Assert(Grid.IsCellOffsetValid(bottom, 0, 1), "最底层向上偏移合法");

            int top = Grid.XYToCell(0, height - 1);
            Assert(!Grid.IsCellOffsetValid(top, 0, 1), "最顶层向上偏移被判为非法");
        }

        /// <summary>
        /// 证明 <c>OffsetCell</c> 本身不做边界检查：最后一列 +1 会绕到下一行首列。
        /// 这条是给后续开发者看的警示，不是缺陷。
        /// </summary>
        private static void CheckOffsetBoundaryGuard(int width, int height)
        {
            if (height < 2)
            {
                OsbLog.Warn("世界高度不足 2 行，跳过绕回验证");
                return;
            }

            int corner = Grid.XYToCell(width - 1, 0);
            int wrapped = Grid.OffsetCell(corner, 1, 0);
            Grid.CellToXY(wrapped, out int wx, out int wy);

            bool wrappedToNextRow = wx == 0 && wy == 1;
            Assert(wrappedToNextRow,
                "已证实 OffsetCell 不校验边界：最后一列 +1 绕到下一行首列（x=" + wx + ", y=" + wy + "），"
                + "因此构造性使用前必须调用 IsCellOffsetValid");
        }

        private static void Assert(bool condition, string description)
        {
            if (condition)
            {
                _passed++;
                OsbLog.Info("  ✓ " + description);
            }
            else
            {
                _failed++;
                OsbLog.Warn("  ✗ " + description);
            }
        }
    }
}
