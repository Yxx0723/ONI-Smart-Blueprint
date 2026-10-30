using System.Collections.Generic;
using System.Linq;

namespace OSB.Domain.Tests
{
    /// <summary>Blueprint 结构与校验规则测试，重点是"负面路径"（非法输入必须被拦下）。</summary>
    internal static class BlueprintTests
    {
        private static BuildingRecord Record(
            string recordId,
            string prefabId = "Tile",
            int x = 0,
            int y = 0,
            int w = 1,
            int h = 1,
            LayerKind layer = LayerKind.Building)
        {
            return new BuildingRecord
            {
                RecordId = recordId,
                PrefabId = prefabId,
                LocalPosition = new Point2i(x, y),
                Footprint = new Footprint(w, h),
                Layer = layer,
                Orientation = BuildingOrientation.Neutral,
                MaterialTag = "SandStone"
            };
        }

        private static Blueprint Sample(params BuildingRecord[] records)
        {
            return new Blueprint
            {
                Id = "sample.test.v1",
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

            Rect2i bounds = bp.LocalBounds();
            Check.Equal(new Rect2i(3, 1, 3, 4), bounds, "包围盒应为 x3..5 y1..4");

            Blueprint empty = new Blueprint();
            Check.True(empty.LocalBounds().IsEmpty, "空蓝图包围盒应为 Empty");
        }

        internal static void Normalize()
        {
            Blueprint bp = Sample(
                Record("r1", "Tile", 3, 4, 1, 1),
                Record("r2", "Door", 5, 1, 1, 1));

            bp.Normalize();

            Check.Equal(new Point2i(0, 3), bp.Buildings[0].LocalPosition, "归一化后 r1 应在 (0,3)");
            Check.Equal(new Point2i(2, 0), bp.Buildings[1].LocalPosition, "归一化后 r2 应在 (2,0)");
            Check.Equal(0, bp.LocalBounds().MinX, "归一化后 MinX 应为 0");
            Check.Equal(0, bp.LocalBounds().MinY, "归一化后 MinY 应为 0");

            // 幂等性：再次归一化不应改变任何东西
            bp.Normalize();
            Check.Equal(new Point2i(0, 3), bp.Buildings[0].LocalPosition, "归一化必须幂等");
        }

        internal static void DifferentLayerAllowed()
        {
            // 同一格，不同图层（电线穿过建筑）是合法的
            Blueprint bp = Sample(
                Record("r1", "Tile", 0, 0, 2, 2, LayerKind.Building),
                Record("r2", "Wire", 0, 0, 2, 2, LayerKind.Power));

            Check.False(HasCode(bp, "OSB011"), "不同图层重叠不应报错；实际：" + string.Join(" / ", bp.Validate()));
        }

        // ---------------- 负面 ----------------

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
            Check.True(HasCode(bp, "OSB003"), "高于当前版本应触发 OSB003 且仅可只读");

            bp.SchemaVersion = 0;
            Check.True(HasCode(bp, "OSB002"), "非法版本号应触发 OSB002");
        }

        internal static void DuplicateRecordId()
        {
            Blueprint bp = Sample(Record("dup"), Record("dup", "Door", 1, 0));
            Check.True(HasCode(bp, "OSB007"), "重复 RecordId 应触发 OSB007");
        }

        internal static void MissingPrefabId()
        {
            Blueprint bp = Sample(Record("r1", prefabId: ""));
            Check.True(HasCode(bp, "OSB008"), "缺 PrefabId 应触发 OSB008");
        }

        internal static void InvalidFootprint()
        {
            Blueprint bp = Sample(Record("r1", "Tile", 0, 0, 0, 0));
            Check.True(HasCode(bp, "OSB009"), "非法 footprint 应触发 OSB009");
        }

        internal static void NegativeLocalPosition()
        {
            Blueprint bp = Sample(Record("r1", "Tile", -1, 2));
            Check.True(HasCode(bp, "OSB010"), "负坐标应触发 OSB010（未归一化）");
        }

        internal static void SameLayerOverlap()
        {
            // 3x3 与 2x2，起点 (0,0) 与 (2,2) → 覆盖 (2,2) 一格重叠
            Blueprint bp = Sample(
                Record("a", "Tile", 0, 0, 3, 3),
                Record("b", "Door", 2, 2, 2, 2));

            Check.True(HasCode(bp, "OSB011"), "同图层重叠应触发 OSB011");

            // 相邻但不重叠必须放行
            Blueprint adjacent = Sample(
                Record("a", "Tile", 0, 0, 3, 3),
                Record("b", "Door", 3, 0, 2, 2));
            Check.False(HasCode(adjacent, "OSB011"), "相邻建筑不应判定为重叠");
        }
    }
}
