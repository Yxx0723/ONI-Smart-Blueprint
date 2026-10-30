using System;
using System.Collections.Generic;

namespace OSB.Domain.Tests
{
    /// <summary>
    /// 测试入口。无框架发现机制，用例表是显式的：
    /// 新增用例必须在这里登记，避免"写了测试但没跑"的情况。
    /// 退出码：0 = 全部通过，1 = 存在失败（供 CI / build.sh 判定）。
    /// </summary>
    internal static class Program
    {
        private static int Main()
        {
            var cases = new List<(string Name, Action Body)>
            {
                // ---- Point2i ----
                ("Point2i.EqualAndOperators", GeometryTests.PointEquality),
                ("Point2i.HashStability", GeometryTests.PointHash),
                ("Point2i.RotateClockwise90", GeometryTests.PointRotate),
                ("Point2i.RotateFourTimesIsIdentity", GeometryTests.PointRotateFourTimes),
                ("Point2i.MirrorTwiceIsIdentity", GeometryTests.PointMirrorTwice),

                // ---- Rect2i ----
                ("Rect2i.Contains", GeometryTests.RectContains),
                ("Rect2i.Intersect", GeometryTests.RectIntersect),
                ("Rect2i.IntersectDisjointIsEmpty", GeometryTests.RectIntersectDisjoint),
                ("Rect2i.Union", GeometryTests.RectUnion),
                ("Rect2i.UnionWithEmpty", GeometryTests.RectUnionWithEmpty),
                ("Rect2i.FromMinMaxInvalid", GeometryTests.RectFromMinMaxInvalid),
                ("Rect2i.EmptyAreaZero", GeometryTests.RectEmptyArea),

                // ---- Footprint ----
                ("Footprint.CellCount", GeometryTests.FootprintCellCount),
                ("Footprint.CellsEnumeration", GeometryTests.FootprintCells),
                ("Footprint.ToWorldRect", GeometryTests.FootprintWorldRect),
                ("Footprint.InvalidYieldsNothing", GeometryTests.FootprintInvalid),

                // ---- Blueprint ----
                ("Blueprint.ValidPasses", BlueprintTests.ValidBlueprintPasses),
                ("Blueprint.EmptyIdBlocked", BlueprintTests.EmptyIdBlocked),
                ("Blueprint.NoBuildingsBlocked", BlueprintTests.NoBuildingsBlocked),
                ("Blueprint.FutureSchemaReadOnly", BlueprintTests.FutureSchemaBlocked),
                ("Blueprint.DuplicateRecordId", BlueprintTests.DuplicateRecordId),
                ("Blueprint.MissingPrefabId", BlueprintTests.MissingPrefabId),
                ("Blueprint.InvalidFootprint", BlueprintTests.InvalidFootprint),
                ("Blueprint.NegativeLocalPosition", BlueprintTests.NegativeLocalPosition),
                ("Blueprint.SameLayerOverlap", BlueprintTests.SameLayerOverlap),
                ("Blueprint.DifferentLayerNoOverlap", BlueprintTests.DifferentLayerAllowed),
                ("Blueprint.LocalBounds", BlueprintTests.LocalBounds),
                ("Blueprint.Normalize", BlueprintTests.Normalize),
            };

            Console.WriteLine("OSB.Domain.Tests —— 共 " + cases.Count + " 个用例");
            Console.WriteLine(new string('-', 72));

            foreach ((string name, Action body) in cases)
            {
                Check.BeginCase(name);
                int failedBefore = Check.Failed;
                try
                {
                    body();
                }
                catch (Exception e)
                {
                    Check.True(false, "用例抛出未处理异常：" + e.GetType().Name + " " + e.Message);
                }

                string status = Check.Failed == failedBefore ? "PASS" : "FAIL";
                Console.WriteLine(status.PadRight(5) + " " + name);
            }

            Console.WriteLine(new string('-', 72));
            Console.WriteLine("断言通过 " + Check.Passed + " 条，失败 " + Check.Failed + " 条");

            if (Check.Failed == 0)
            {
                Console.WriteLine("结果：PASS");
                return 0;
            }

            Console.WriteLine("结果：FAIL");
            foreach (string failure in Check.Failures)
            {
                Console.WriteLine("  - " + failure);
            }

            return 1;
        }
    }
}
