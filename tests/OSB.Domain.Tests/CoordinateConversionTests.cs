using System.Collections.Generic;
using System.Linq;

namespace OSB.Domain.Tests
{
    /// <summary>
    /// P2-A 几何转换测试：锚点 ⇄ 矩形角、局部坐标 ⇄ 世界坐标、朝向变换。
    ///
    /// 这些是蓝图落位的数学基础，任何一条不过都会导致建筑整体偏移。
    /// </summary>
    internal static class CoordinateConversionTests
    {
        private static List<Point2i> GameOffsets(int width, int height)
        {
            var result = new List<Point2i>(width * height);
            int startX = (width / 2) - width + 1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result.Add(new Point2i(x + startX, y));
                }
            }

            return result;
        }

        private static BuildingRecord Make(
            int width,
            int height,
            Point2i localPosition = default,
            BuildingOrientation orientation = BuildingOrientation.Neutral,
            IList<Point2i> offsets = null)
        {
            return new BuildingRecord
            {
                RecordId = "r",
                PrefabId = "P",
                LocalPosition = localPosition,
                DeclaredSize = new Point2i(width, height),
                PlacementOffsets = offsets ?? GameOffsets(width, height),
                Orientation = orientation
            };
        }

        // ---------------- 世界 ⇄ 局部 ----------------

        internal static void WorldLocalRoundtrip()
        {
            var origin = new Point2i(120, 45);
            var world = new Point2i(137, 88);

            Point2i local = Blueprint.WorldToLocal(world, origin);
            Check.Equal(new Point2i(17, 43), local, "世界 → 局部");
            Check.Equal(world, Blueprint.LocalToWorld(local, origin), "局部 → 世界 必须还原");

            // 负方向同样成立
            var below = new Point2i(100, 20);
            Check.Equal(new Point2i(-20, -25), Blueprint.WorldToLocal(below, origin), "负局部坐标");
            Check.Equal(below, Blueprint.LocalToWorld(new Point2i(-20, -25), origin), "负坐标往返");
        }

        internal static void GameCellsMatchLocalPlusOrigin()
        {
            var origin = new Point2i(200, 100);
            BuildingRecord record = Make(3, 2, new Point2i(5, 7));

            IReadOnlyList<Point2i> game = record.GameCells(origin);
            IReadOnlyList<Point2i> local = record.OccupiedCells();

            Check.Equal(local.Count, game.Count, "世界格与局部格数量一致");
            for (int i = 0; i < local.Count; i++)
            {
                Check.Equal(local[i] + origin, game[i], "第 " + i + " 格：世界 = 局部 + 原点");
            }
        }

        // ---------------- 锚点 ⇄ 矩形角 ----------------

        internal static void FootprintCornerConversionRoundtrip()
        {
            foreach (int width in new[] { 1, 2, 3, 4, 5, 6, 7, 8 })
            {
                foreach (int height in new[] { 1, 2, 3 })
                {
                    foreach (BuildingOrientation orientation in PermittedRotationInfo.ValidOrientations(PermittedRotation.R360))
                    {
                        BuildingRecord record = Make(width, height, new Point2i(10, 20));
                        var anchor = new Point2i(50, 60);

                        Point2i corner = record.FootprintCorner(anchor, orientation);
                        Point2i back = record.AnchorForFootprintCorner(corner, orientation);

                        Check.Equal(anchor, back,
                            "锚点⇄矩形角往返失败：w=" + width + " h=" + height + " " + orientation);
                    }
                }
            }
        }

        internal static void FootprintCornerMatchesBounds()
        {
            BuildingRecord record = Make(3, 3, new Point2i(0, 0));
            var anchor = new Point2i(100, 100);

            Rect2i bounds = record.OccupiedBoundsAt(anchor, BuildingOrientation.Neutral);
            Point2i corner = record.FootprintCorner(anchor, BuildingOrientation.Neutral);

            Check.Equal(new Point2i(bounds.MinX, bounds.MinY), corner, "矩形角应等于包围盒最小角");
            Check.Equal(new Point2i(99, 100), corner, "3 宽建筑锚点 (100,100) 时左下角应为 (99,100)");
        }

        internal static void AnchorInFootprintPerWidth()
        {
            // 与游戏 GenerateOffsets 公式一致：锚点偏移 = -(width/2 - width + 1)
            for (int width = 1; width <= 10; width++)
            {
                BuildingRecord record = Make(width, 1);
                int expected = -(width / 2 - width + 1);
                Check.Equal(expected, record.AnchorInFootprint.X, "宽度 " + width + " 的锚点 X 偏移");
                Check.Equal(0, record.AnchorInFootprint.Y, "宽度 " + width + " 的锚点 Y 偏移恒为 0");
            }
        }

        internal static void AnchorInFootprintRotatesWithOrientation()
        {
            // 3x1 水平：Neutral 锚点偏移 (1,0)；R90 后变竖直，锚点偏移应变为 (0,1)
            BuildingRecord record = Make(3, 1);

            Check.Equal(new Point2i(1, 0), record.AnchorInFootprintFor(BuildingOrientation.Neutral), "Neutral 锚点偏移");
            Check.Equal(new Point2i(0, 1), record.AnchorInFootprintFor(BuildingOrientation.R90), "R90 锚点偏移");

            // R180：x 反向
            Check.Equal(new Point2i(1, 0), record.AnchorInFootprintFor(BuildingOrientation.R180), "R180 锚点偏移");
        }

        // ---------------- 朝向变换与占用格 ----------------

        internal static void RotatedCellsPreserveCountAndSwapBounds()
        {
            BuildingRecord record = Make(3, 2, new Point2i(0, 0));

            foreach (BuildingOrientation orientation in PermittedRotationInfo.ValidOrientations(PermittedRotation.R360))
            {
                IReadOnlyList<Point2i> cells = record.OccupiedCells(orientation);
                Check.Equal(6, cells.Count, orientation + " 朝向占位格数不变");

                Rect2i bounds = record.OccupiedBounds(orientation);
                bool swaps = orientation == BuildingOrientation.R90 || orientation == BuildingOrientation.R270;
                Check.Equal(swaps ? 2 : 3, bounds.Width, orientation + " 朝向包围盒宽");
                Check.Equal(swaps ? 3 : 2, bounds.Height, orientation + " 朝向包围盒高");
            }
        }

        internal static void RotationNeverMutatesRawOffsets()
        {
            BuildingRecord record = Make(3, 2);
            List<Point2i> before = record.PlacementOffsets.ToList();

            record.OccupiedCells(BuildingOrientation.R90);
            record.OccupiedCells(BuildingOrientation.R270);
            record.FootprintCorner(new Point2i(5, 5), BuildingOrientation.FlipH);

            Check.Equal(before.Count, record.PlacementOffsets.Count, "旋转不得改变原始偏移数量");
            for (int i = 0; i < before.Count; i++)
            {
                Check.Equal(before[i], record.PlacementOffsets[i], "旋转不得修改第 " + i + " 条原始偏移");
            }
        }

        internal static void RotatedOffsetsAreFreshArrays()
        {
            BuildingRecord record = Make(2, 1);
            IReadOnlyList<Point2i> rotated = record.RotatedOffsets(BuildingOrientation.R90);

            Check.Equal(2, rotated.Count, "旋转后条目数");
            Check.Equal(new Point2i(0, 0), rotated[0], "原始 (0,0) 旋转后仍为 (0,0)");
            Check.Equal(new Point2i(0, -1), rotated[1], "原始 (1,0) 旋转后为 (0,-1)");
        }

        internal static void AsymmetricFootprintRotatesCorrectly()
        {
            // 吊兰：声明 1x2，实际 1x1，锚点无偏移
            var hanging = new BuildingRecord
            {
                RecordId = "h",
                PrefabId = "FlowerVaseHanging",
                LocalPosition = new Point2i(4, 4),
                DeclaredSize = new Point2i(1, 2),
                PlacementOffsets = new List<Point2i> { new Point2i(0, 0) }
            };

            Check.Equal(1, hanging.OccupiedCells().Count, "实占 1 格");
            Check.Equal(new Point2i(1, 1), hanging.ActualSize, "实际尺寸 1x1");
            Check.Equal(new Point2i(1, 2), hanging.DeclaredSize, "声明尺寸 1x2");
            Check.False(hanging.SizeMatchesDeclaration, "二者应当不一致");

            Rect2i bounds = hanging.OccupiedBounds();
            Check.Equal(new Rect2i(4, 4, 1, 1), bounds, "包围盒只覆盖真实占位，不含声明高度");
        }

        internal static void ZeroOffsetRecordIsSafe()
        {
            var empty = new BuildingRecord
            {
                RecordId = "e",
                PrefabId = "P",
                LocalPosition = new Point2i(3, 3),
                DeclaredSize = new Point2i(1, 1),
                PlacementOffsets = new List<Point2i>()
            };

            Check.Equal(0, empty.OccupiedCells().Count, "无偏移时占用格为空");
            Check.True(empty.OccupiedBounds().IsEmpty, "无偏移时包围盒为空");
            Check.True(empty.NeutralBounds.IsEmpty, "无偏移时 Neutral 包围盒为空");
            Check.Equal(Point2i.Zero, empty.AnchorInFootprint, "无偏移时锚点偏移为零");
            Check.Equal(new Point2i(3, 3), empty.FootprintCorner(new Point2i(3, 3), BuildingOrientation.Neutral),
                "无偏移时矩形角回退为锚点本身");
        }
    }
}
