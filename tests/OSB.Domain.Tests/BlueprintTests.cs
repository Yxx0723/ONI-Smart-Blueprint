using System.Collections.Generic;
using System.Linq;

namespace OSB.Domain.Tests
{
    /// <summary>
    /// 蓝图结构校验测试（P2-A 后的新语义）。
    ///
    /// 与 P0 版的差异：记录不再持有"声明尺寸矩形"，而是持有**原始 PlacementOffsets**。
    /// 因此测试里的占位必须按游戏真实算法生成，见 <see cref="GameOffsets"/>。
    /// </summary>
    internal static class BlueprintTests
    {
        /// <summary>
        /// 复刻游戏 <c>BuildingDef.GenerateOffsets</c>：水平居中、底部对齐。
        /// x = j + (width/2 - width + 1)，y = i。宽度 ≥3 时 x 为负。
        /// </summary>
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

        private static BuildingRecord Record(
            string recordId,
            string prefabId = "Tile",
            int x = 0,
            int y = 0,
            int declaredWidth = 1,
            int declaredHeight = 1,
            LayerKind layer = LayerKind.Building,
            BuildingOrientation orientation = BuildingOrientation.Neutral,
            IList<Point2i> offsets = null)
        {
            return new BuildingRecord
            {
                RecordId = recordId,
                PrefabId = prefabId,
                LocalPosition = new Point2i(x, y),
                DeclaredSize = new Point2i(declaredWidth, declaredHeight),
                PlacementOffsets = offsets ?? GameOffsets(declaredWidth, declaredHeight),
                Layer = layer,
                Orientation = orientation,
                MaterialTag = "SandStone"
            };
        }

        private static Blueprint Sample(params BuildingRecord[] records)
        {
            return new Blueprint
            {
                Id = "sample.test.v2",
                Name = "测试蓝图",
                Author = "tests",
                SchemaVersion = Blueprint.CurrentSchemaVersion,
                SourceGameBuild = 744825,
                SourceDlcSet = "ALL",
                Buildings = records.ToList()
            };
        }

        private static bool HasCode(Blueprint blueprint, string code) =>
            blueprint.Validate().Any(i => i.Code == code);

        private static ValidationIssue Find(Blueprint blueprint, string code) =>
            blueprint.Validate().FirstOrDefault(i => i.Code == code);

        // ---------------- 正向 ----------------

        internal static void ValidBlueprintPasses()
        {
            Blueprint bp = Sample(
                Record("r1", "Tile", 0, 0, 2, 1),
                Record("r2", "Door", 2, 0, 1, 2));

            IReadOnlyList<ValidationIssue> issues = bp.Validate();
            Check.Equal(0, issues.Count, "合法蓝图不应产生任何问题，实际：" + string.Join(" / ", issues));
        }

        internal static void LocalBounds()
        {
            Blueprint bp = Sample(
                Record("r1", "Tile", 3, 4, 1, 1),
                Record("r2", "Door", 5, 1, 1, 1));

            Check.Equal(new Rect2i(3, 1, 3, 4), bp.LocalBounds(), "包围盒应为 x3..5 y1..4");

            Blueprint empty = new Blueprint();
            Check.True(empty.LocalBounds().IsEmpty, "空蓝图包围盒应为 Empty");
        }

        internal static void Normalize()
        {
            Blueprint bp = Sample(
                Record("r1", "Tile", 3, 4, 1, 1),
                Record("r2", "Door", 5, 1, 1, 1));

            bp.Normalize();

            Check.Equal(new Point2i(0, 3), bp.Buildings[0].LocalPosition, "归一化后 r1 锚点应在 (0,3)");
            Check.Equal(new Point2i(2, 0), bp.Buildings[1].LocalPosition, "归一化后 r2 锚点应在 (2,0)");
            Check.Equal(0, bp.LocalBounds().MinX, "归一化后 MinX 应为 0");
            Check.Equal(0, bp.LocalBounds().MinY, "归一化后 MinY 应为 0");

            int anchorBefore = bp.Buildings[0].LocalPosition.X;
            bp.Normalize();
            Check.Equal(anchorBefore, bp.Buildings[0].LocalPosition.X, "归一化必须幂等");
        }

        // ---------------- 居中锚定（P2-A 核心） ----------------

        internal static void CenteredAnchorNeedsOccupiedCellNormalization()
        {
            // 3 宽建筑锚定在 (0,0)：占 x ∈ [-1,1]，锚点非负但占用格已越界
            Blueprint bp = Sample(Record("wide", "AdvancedApothecary", 0, 0, 3, 3));

            Check.Equal(-1, bp.Buildings[0].NeutralBounds.MinX, "3 宽建筑包围盒最小 x 为 -1");
            Check.Equal(new Point2i(1, 0), bp.Buildings[0].AnchorInFootprint, "锚点在占位矩形内偏移 (1,0)");
            Check.True(HasCode(bp, "OSB010"), "未归一化时应报占用格负坐标");

            bp.Normalize();

            Check.Equal(new Point2i(1, 0), bp.Buildings[0].LocalPosition, "归一化后锚点应右移 1 格");
            Check.False(HasCode(bp, "OSB010"), "归一化后不应再有负坐标");
            Check.Equal(0, bp.LocalBounds().MinX, "归一化后最左占用格为 0");
            Check.Equal(3, bp.LocalBounds().Width, "3 宽建筑占 3 列");
        }

        internal static void WideBuildingsDoNotShiftAfterPlacement()
        {
            // 回归测试：宽度 1..8 的建筑在归一化后，占用格必须与锚点保持正确关系
            foreach (int width in new[] { 1, 2, 3, 4, 5, 6, 7, 8 })
            {
                Blueprint bp = Sample(Record("b", "W" + width, 0, 0, width, 1));
                bp.Normalize();

                BuildingRecord record = bp.Buildings[0];
                IReadOnlyList<Point2i> cells = record.OccupiedCells();

                Check.Equal(width, cells.Count, "宽度 " + width + " 的占位格数");
                Check.Equal(0, cells.Min(c => c.X), "宽度 " + width + " 归一化后最左格为 0");
                Check.Equal(width - 1, cells.Max(c => c.X), "宽度 " + width + " 归一化后最右格为 width-1");
                Check.Equal(record.AnchorInFootprint.X, record.LocalPosition.X,
                    "宽度 " + width + " 归一化后锚点 X 应等于 AnchorInFootprint.X");
            }
        }

        internal static void DeclaredSizeMismatchIsWarningNotBlocker()
        {
            // 真实案例：FlowerVaseHanging 声明 1x2，实际只占 1 格
            BuildingRecord hanging = Record("h", "FlowerVaseHanging", 0, 0, 1, 2,
                offsets: new List<Point2i> { new Point2i(0, 0) });

            Blueprint bp = Sample(hanging);
            ValidationIssue issue = Find(bp, "OSB012");

            Check.NotNull(issue, "声明与实际不符应报 OSB012");
            Check.Equal(IssueSeverity.Warning, issue.Severity, "OSB012 必须是 Warning 而非 Blocker");
            Check.False(issue.BlocksPlacement, "OSB012 不应阻止放置");
            Check.Equal(1, bp.Buildings[0].OffsetCount, "真实占位只有 1 格");

            List<Point2i> cells = bp.Buildings[0].OccupiedCells().ToList();
            Check.False(cells.Contains(new Point2i(0, 1)), "不得因声明尺寸而虚占上方一格");
        }

        // ---------------- 重叠判定必须基于真实占用格 ----------------

        internal static void SameLayerOccupiedCellOverlap()
        {
            // 两个 2x2 错位摆放，共享一格
            Blueprint bp = Sample(
                Record("a", "Tile", 0, 0, 2, 2),
                Record("b", "Door", 1, 1, 2, 2));

            ValidationIssue issue = Find(bp, "OSB011");
            Check.NotNull(issue, "真实占用格重叠应报 OSB011");
            Check.Equal(new Point2i(1, 1), issue.Position, "冲突位置应为重叠格 (1,1)");
        }

        internal static void AdjacentBuildingsDoNotOverlap()
        {
            Blueprint bp = Sample(
                Record("a", "Tile", 0, 0, 2, 2),
                Record("b", "Door", 2, 0, 2, 2));

            Check.False(HasCode(bp, "OSB011"), "相邻不相交的建筑不应报重叠");
        }

        internal static void BoundingBoxOverlapButCellsDisjoint()
        {
            // 互补的两组格：包围盒完全重合，但真实占用格完全不重叠。
            // 若用包围盒判定会产生误报，本用例锁死"必须按格判定"。
            var first = new List<Point2i>
            {
                new Point2i(0, 0), new Point2i(2, 0),
                new Point2i(0, 1), new Point2i(0, 2)
            };
            var second = new List<Point2i>
            {
                new Point2i(1, 0), new Point2i(2, 1), new Point2i(2, 2)
            };

            Blueprint bp = Sample(
                Record("A", "ShapeA", 0, 0, 3, 3, offsets: first),
                Record("B", "ShapeB", 0, 0, 3, 3, offsets: second));

            Check.True(bp.Buildings[0].OccupiedBounds().Intersects(bp.Buildings[1].OccupiedBounds()),
                "前提：两者包围盒确实相交");
            Check.False(HasCode(bp, "OSB011"), "包围盒相交但真实格不重叠时不得报重叠");
        }

        internal static void DifferentLayerAllowed()
        {
            Blueprint bp = Sample(
                Record("r1", "Tile", 0, 0, 2, 2, layer: LayerKind.Building),
                Record("r2", "Wire", 0, 0, 2, 2, layer: LayerKind.Power));

            Check.False(HasCode(bp, "OSB011"), "不同图层重叠不应报错");
        }

        // ---------------- 负面路径 ----------------

        internal static void EmptyIdBlocked()
        {
            Blueprint bp = Sample(Record("r1"));
            bp.Id = "  ";
            Check.True(HasCode(bp, "OSB001"), "空 Id 应触发 OSB001");
        }

        internal static void NoBuildingsBlocked()
        {
            Blueprint bp = Sample();
            Check.True(HasCode(bp, "OSB004"), "无建筑应触发 OSB004");
        }

        internal static void FutureSchemaBlocked()
        {
            Blueprint bp = Sample(Record("r1"));
            bp.SchemaVersion = Blueprint.CurrentSchemaVersion + 1;
            Check.True(HasCode(bp, "OSB003"), "高于当前版本应触发 OSB003");

            bp.SchemaVersion = 0;
            Check.True(HasCode(bp, "OSB002"), "非法版本号应触发 OSB002");
        }

        internal static void DuplicateRecordId()
        {
            Blueprint bp = Sample(Record("dup", "Tile", 0, 0), Record("dup", "Door", 5, 0));
            Check.True(HasCode(bp, "OSB007"), "重复 RecordId 应触发 OSB007");
        }

        internal static void MissingPrefabId()
        {
            Blueprint bp = Sample(Record("r1", prefabId: ""));
            Check.True(HasCode(bp, "OSB008"), "缺 PrefabId 应触发 OSB008");
        }

        internal static void MissingOffsetsBlocked()
        {
            BuildingRecord record = Record("r1");
            record.PlacementOffsets = new List<Point2i>();
            Blueprint bp = Sample(record);

            Check.True(HasCode(bp, "OSB009"), "缺 PlacementOffsets 应触发 OSB009");
        }

        internal static void InvalidDeclaredSize()
        {
            BuildingRecord record = Record("r1");
            record.DeclaredSize = new Point2i(0, 1);
            Blueprint bp = Sample(record);

            Check.True(HasCode(bp, "OSB013"), "声明尺寸非正应触发 OSB013");
        }

        internal static void SentinelOrientationBlocked()
        {
            Blueprint bp = Sample(Record("r1", orientation: BuildingOrientation.NumRotations));
            Check.True(HasCode(bp, "OSB014"), "哨兵朝向应触发 OSB014");
        }

        internal static void OccupancyMapReflectsOccupiedCellsOnly()
        {
            BuildingRecord hanging = Record("h", "FlowerVaseHanging", 0, 0, 1, 2,
                offsets: new List<Point2i> { new Point2i(0, 0) });
            Blueprint bp = Sample(hanging);

            IReadOnlyDictionary<Point2i, IReadOnlyList<string>> map = bp.OccupancyMap();
            Check.Equal(1, map.Count, "实占 1 格的建筑在占用图中只有 1 项");
            Check.True(map.ContainsKey(new Point2i(0, 0)), "占用 (0,0)");
            Check.False(map.ContainsKey(new Point2i(0, 1)), "不得因声明高度而占用 (0,1)");
        }
    }
}
