using System;
using System.Collections.Generic;
using System.Linq;

namespace OSB.Domain.Tests
{
    /// <summary>建筑几何模型与几何库的测试，重点是负面路径与异常码覆盖。</summary>
    internal static class GeometryTableTests
    {
        private static BuildingGeometry Geo(
            string prefabId,
            int width,
            int height,
            Point2i[] offsets,
            PermittedRotation rotations = PermittedRotation.Unrotatable,
            string displayName = null,
            IReadOnlyList<string> materials = null,
            bool deprecated = false)
        {
            return new BuildingGeometry(
                prefabId,
                displayName,
                width,
                height,
                offsets,
                rotations,
                "Building",
                "Building",
                "OnFloor",
                materials ?? new[] { "Buildable" },
                new[] { "SandStone" },
                deprecated);
        }

        private static Point2i[] Rect(int w, int h)
        {
            var list = new List<Point2i>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    list.Add(new Point2i(x, y));
                }
            }

            return list.ToArray();
        }

        private static bool HasCode(IEnumerable<GeometryAnomaly> anomalies, string code) =>
            anomalies.Any(a => a.Code == code);

        // ---------------- 构造与不变量 ----------------

        internal static void ConstructionBasics()
        {
            BuildingGeometry g = Geo("Tile", 1, 1, new[] { new Point2i(0, 0) }, displayName: "砖块");

            Check.Equal("Tile", g.PrefabId, "PrefabId");
            Check.Equal("砖块", g.DisplayName, "DisplayName");
            Check.Equal(1, g.Width, "Width");
            Check.Equal(1, g.Height, "Height");
            Check.Equal(1, g.CellCount, "CellCount");
            Check.Equal(1, g.PlacementOffsets.Count, "Offsets 数量");
            Check.False(g.IsRotatable, "Unrotatable 不可旋转");
            Check.Equal(1, g.ValidOrientations.Count, "Unrotatable 合法朝向 1 种");
        }

        internal static void DisplayNameFallsBackToPrefabId()
        {
            BuildingGeometry g = Geo("Door", 1, 2, Rect(1, 2), displayName: null);
            Check.Equal("Door", g.DisplayName, "DisplayName 为空时回退为 PrefabId");

            BuildingGeometry empty = Geo("Wall", 1, 1, Rect(1, 1), displayName: "   ");
            Check.Equal("Wall", empty.DisplayName, "只有空白字符时也应回退");
        }

        internal static void EmptyPrefabIdRejected()
        {
            Check.Throws<ArgumentException>(
                () => Geo(string.Empty, 1, 1, Rect(1, 1)),
                "空 PrefabId 必须拒绝");
        }

        internal static void OffsetsAreDefensivelyCopied()
        {
            Point2i[] source = Rect(2, 1);
            BuildingGeometry g = Geo("Ladder", 2, 1, source, PermittedRotation.R90);
            IReadOnlyList<Point2i> before = g.OffsetsFor(BuildingOrientation.Neutral);

            source[0] = new Point2i(99, 99);

            IReadOnlyList<Point2i> after = g.OffsetsFor(BuildingOrientation.Neutral);
            Check.Equal(before[0], after[0], "外部修改源数组不得影响已构造的几何");
            Check.False(after[0].X == 99, "不应出现被改写后的值");
        }

        // ---------------- 朝向与绝对格 ----------------

        internal static void AbsoluteCellsNeutral()
        {
            BuildingGeometry g = Geo("Battery", 2, 2, Rect(2, 2));
            IReadOnlyList<Point2i> cells = g.AbsoluteCells(new Point2i(10, 5), BuildingOrientation.Neutral);

            Check.Equal(4, cells.Count, "2x2 有 4 格");
            Check.True(cells.Contains(new Point2i(10, 5)), "含锚点格");
            Check.True(cells.Contains(new Point2i(11, 6)), "含对角格");
        }

        internal static void AbsoluteCellsRotated()
        {
            // 2x1 水平，转 90° 后应占 (0,0) 与 (0,-1)
            BuildingGeometry g = Geo("Door", 2, 1, Rect(2, 1), PermittedRotation.R360);
            IReadOnlyList<Point2i> cells = g.AbsoluteCells(new Point2i(0, 0), BuildingOrientation.R90);

            Check.Equal(2, cells.Count, "旋转后格数不变");
            Check.True(cells.Contains(new Point2i(0, 0)), "含锚点");
            Check.True(cells.Contains(new Point2i(0, -1)), "含旋转后的第二格");
        }

        internal static void InvalidOrientationRejected()
        {
            BuildingGeometry r90 = Geo("Door", 1, 2, Rect(1, 2), PermittedRotation.R90);

            Check.Throws<ArgumentOutOfRangeException>(
                () => r90.AbsoluteCells(Point2i.Zero, BuildingOrientation.R180),
                "R90 形态不允许 R180");
            Check.Throws<ArgumentOutOfRangeException>(
                () => r90.AbsoluteCells(Point2i.Zero, BuildingOrientation.NumRotations),
                "哨兵值不允许作为朝向");
            Check.Throws<ArgumentOutOfRangeException>(
                () => r90.BoundsFor(BuildingOrientation.FlipH),
                "R90 形态不允许 FlipH");
        }

        internal static void BoundsForOrientation()
        {
            BuildingGeometry g = Geo("Door", 2, 1, Rect(2, 1), PermittedRotation.R360);

            Rect2i neutral = g.BoundsFor(BuildingOrientation.Neutral);
            Check.Equal(2, neutral.Width, "Neutral 宽 2");
            Check.Equal(1, neutral.Height, "Neutral 高 1");

            Rect2i r90 = g.BoundsFor(BuildingOrientation.R90);
            Check.Equal(1, r90.Width, "R90 宽 1");
            Check.Equal(2, r90.Height, "R90 高 2");
        }

        // ---------------- 异常码 ----------------

        internal static void AnomalyInvalidSize()
        {
            BuildingGeometry g = Geo("Broken", 0, 1, Rect(1, 1));
            Check.True(HasCode(g.Validate(), "GEO004"), "零宽应触发 GEO004");
        }

        internal static void AnomalyEmptyOffsets()
        {
            BuildingGeometry g = Geo("Empty", 1, 1, new Point2i[0]);
            Check.True(HasCode(g.Validate(), "GEO001"), "空占位应触发 GEO001");
        }

        internal static void AnomalyCountMismatch()
        {
            // 声明 2x2=4 格，实际只给 3 个偏移
            BuildingGeometry g = Geo("Mismatch", 2, 2, new[] { new Point2i(0, 0), new Point2i(1, 0), new Point2i(0, 1) });
            Check.True(HasCode(g.Validate(), "GEO002"), "占位格数与宽×高不符应触发 GEO002");
        }

        internal static void CenteredAnchoringIsAccepted()
        {
            // ONI 的建筑水平居中锚定：3 宽建筑 x ∈ [-1,1]，y ∈ [0,2]
            // 反编译依据：BuildingDef.GenerateOffsets 的 num = width/2 - width + 1
            var centered3 = new List<Point2i>();
            for (int y = 0; y < 3; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    centered3.Add(new Point2i(x, y));
                }
            }

            BuildingGeometry g3 = Geo("AdvancedApothecary", 3, 3, centered3.ToArray());
            Check.Equal(0, g3.Validate().Count(), "3 宽居中锚定的建筑不应被判为异常");
            Check.Equal(new Rect2i(-1, 0, 3, 3), g3.NeutralBounds, "Neutral 包围盒应为 (-1,0) 起 3x3");
            Check.Equal(new Point2i(1, 0), g3.AnchorInFootprint, "锚点在占位矩形内偏移 (1,0)");

            // 4 宽：num = 2-4+1 = -1，x ∈ [-1,2]
            var centered4 = new List<Point2i>();
            for (int y = 0; y < 4; y++)
            {
                for (int x = -1; x <= 2; x++)
                {
                    centered4.Add(new Point2i(x, y));
                }
            }

            BuildingGeometry g4 = Geo("ArtifactAnalysisStation", 4, 4, centered4.ToArray());
            Check.Equal(0, g4.Validate().Count(), "4 宽居中锚定的建筑不应被判为异常");
            Check.Equal(new Point2i(1, 0), g4.AnchorInFootprint, "4 宽锚点同样偏移 (1,0)");

            // 2 宽：num = 1-2+1 = 0，x ∈ [0,1]，锚点不偏移
            BuildingGeometry g2 = Geo("ManualGenerator", 2, 2, Rect(2, 2));
            Check.Equal(new Point2i(0, 0), g2.AnchorInFootprint, "2 宽建筑锚点不偏移");
            Check.Equal(new Rect2i(0, 0, 2, 2), g2.NeutralBounds, "2 宽建筑包围盒从原点起");
        }

        internal static void AnchorMatchesGenerateOffsetsFormula()
        {
            // 逐宽度验证 AnchorInFootprint.X == -(width/2 - width + 1)
            for (int width = 1; width <= 8; width++)
            {
                int expectedAnchorX = -(width / 2 - width + 1);
                var offsets = new Point2i[width * 2];
                int index = 0;
                for (int y = 0; y < 2; y++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        offsets[index++] = new Point2i(j + (width / 2 - width + 1), y);
                    }
                }

                BuildingGeometry g = Geo("W" + width, width, 2, offsets);
                Check.Equal(expectedAnchorX, g.AnchorInFootprint.X, "宽度 " + width + " 的锚点 X 偏移");
                Check.Equal(0, g.AnchorInFootprint.Y, "宽度 " + width + " 的锚点 Y 偏移恒为 0（底部对齐）");
                Check.Equal(0, g.Validate().Count(), "宽度 " + width + " 生成的合法占位不应报异常");
            }
        }

        internal static void DeclaredSizeMayDifferFromActual()
        {
            // 真实案例：FlowerVaseHanging 声明 1x2 却 GenerateOffsets(1,1)，实际只占 1 格
            BuildingGeometry hanging = Geo("FlowerVaseHanging", 1, 2, new[] { new Point2i(0, 0) });

            Check.Equal(new Point2i(1, 1), hanging.ActualSize, "实际占位应为 1x1");
            Check.False(hanging.SizeMatchesDeclaration, "声明 1x2 与实际 1x1 应判定为不一致");

            // 不一致要如实报告，但不是"程序错误"——这是游戏允许的设计
            bool reported = HasCode(hanging.Validate(), "GEO002");
            Check.True(reported, "声明与实际不符应如实上报 GEO002 供数据审查");

            // 常规建筑：一致
            BuildingGeometry normal = Geo("ManualGenerator", 2, 2, Rect(2, 2));
            Check.True(normal.SizeMatchesDeclaration, "2x2 常规建筑声明与实际一致");
            Check.Equal(new Point2i(2, 2), normal.ActualSize, "2x2 实际占位");
        }

        internal static void AnomalyOutOfDeclaredSize()
        {
            // 声明 1x1，却有一条偏移跑到 (3,0)
            BuildingGeometry g = Geo("OutOfSize", 1, 1, new[] { new Point2i(0, 0), new Point2i(3, 0) });
            Check.True(HasCode(g.Validate(), "GEO003"), "Neutral 占位超出声明尺寸应触发 GEO003");
        }

        internal static void AnomalyRotationExceedsSwappedSize()
        {
            // 2x1 占位，但旋转 90° 后应装进 1x2；给它一个 x=2 的偏移使旋转后越界
            var offsets = new[] { new Point2i(0, 0), new Point2i(2, 0) };
            BuildingGeometry g = Geo("RotBroken", 2, 1, offsets, PermittedRotation.R90);

            Check.True(HasCode(g.Validate(), "GEO005"), "R90 后占位超出互换尺寸应触发 GEO005");
        }

        internal static void CleanGeometryHasNoAnomaly()
        {
            BuildingGeometry g = Geo("Tile", 1, 1, Rect(1, 1));
            Check.Equal(0, g.Validate().Count(), "正常建筑不应产生异常");

            BuildingGeometry multi = Geo("Battery", 2, 2, Rect(2, 2), PermittedRotation.R360);
            Check.Equal(0, multi.Validate().Count(), "2x2 可旋转建筑不应产生异常");
        }

        // ---------------- 几何库 ----------------

        internal static void TableAddAndLookup()
        {
            var table = new BuildingGeometryTable();
            Check.Equal(0, table.Count, "初始为空");
            Check.False(table.IsSealed, "初始未封存");

            Check.True(table.TryAdd(Geo("Tile", 1, 1, Rect(1, 1))), "加入 Tile 成功");
            Check.True(table.TryAdd(Geo("Door", 1, 2, Rect(1, 2))), "加入 Door 成功");
            Check.Equal(2, table.Count, "共 2 条");

            Check.True(table.Contains("Tile"), "Contains Tile");
            Check.False(table.Contains("Nope"), "不含不存在的 ID");
            Check.False(table.Contains(null), "null 查询返回 false 而不是抛异常");

            Check.True(table.TryGet("Door", out BuildingGeometry door), "TryGet Door");
            Check.Equal(1, door.Width, "Door 宽 1");
            Check.False(table.TryGet("Nope", out _), "TryGet 不存在项返回 false");
            Check.Throws<KeyNotFoundException>(() => table.Get("Nope"), "Get 不存在项应抛异常");
        }

        internal static void TableRejectsDuplicateWithAnomaly()
        {
            var table = new BuildingGeometryTable();
            table.TryAdd(Geo("Tile", 1, 1, Rect(1, 1)));
            bool second = table.TryAdd(Geo("Tile", 2, 2, Rect(2, 2)));

            Check.False(second, "重复 PrefabId 应返回 false");
            Check.Equal(1, table.Count, "重复项不增加计数");
            Check.True(HasCode(table.Anomalies, "GEO007"), "重复项应记 GEO007");
            Check.Equal(1, table.Get("Tile").Width, "应保留先加入的记录");
        }

        internal static void TableCollectsChildAnomalies()
        {
            var table = new BuildingGeometryTable();
            table.TryAdd(Geo("Empty", 1, 1, new Point2i[0]));

            Check.True(HasCode(table.Anomalies, "GEO001"), "子项异常应汇总到库");
        }

        internal static void TableSealBlocksWrites()
        {
            var table = new BuildingGeometryTable();
            table.TryAdd(Geo("Tile", 1, 1, Rect(1, 1)));
            table.Seal();

            Check.True(table.IsSealed, "封存标志");
            Check.Throws<InvalidOperationException>(
                () => table.TryAdd(Geo("Door", 1, 1, Rect(1, 1))),
                "封存后写入必须抛异常");

            table.Seal(); // 重复封存应幂等而不是抛异常
            Check.True(table.IsSealed, "重复封存后仍为已封存");
        }
    }
}
