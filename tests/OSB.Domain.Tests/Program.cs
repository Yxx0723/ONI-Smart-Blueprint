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
                // ================= P0：基础几何 =================
                ("Point2i.EqualAndOperators", GeometryTests.PointEquality),
                ("Point2i.HashStability", GeometryTests.PointHash),
                ("Point2i.RotateClockwise90", GeometryTests.PointRotate),
                ("Point2i.RotateFourTimesIsIdentity", GeometryTests.PointRotateFourTimes),
                ("Point2i.MirrorTwiceIsIdentity", GeometryTests.PointMirrorTwice),

                ("Rect2i.Contains", GeometryTests.RectContains),
                ("Rect2i.Intersect", GeometryTests.RectIntersect),
                ("Rect2i.IntersectDisjointIsEmpty", GeometryTests.RectIntersectDisjoint),
                ("Rect2i.Union", GeometryTests.RectUnion),
                ("Rect2i.UnionWithEmpty", GeometryTests.RectUnionWithEmpty),
                ("Rect2i.FromMinMaxInvalid", GeometryTests.RectFromMinMaxInvalid),
                ("Rect2i.EmptyAreaZero", GeometryTests.RectEmptyArea),

                ("Footprint.CellCount", GeometryTests.FootprintCellCount),
                ("Footprint.CellsEnumeration", GeometryTests.FootprintCells),
                ("Footprint.ToWorldRect", GeometryTests.FootprintWorldRect),
                ("Footprint.InvalidYieldsNothing", GeometryTests.FootprintInvalid),

                // ================= P2-A：蓝图结构（新语义） =================
                ("Blueprint.ValidPasses", BlueprintTests.ValidBlueprintPasses),
                ("Blueprint.EmptyIdBlocked", BlueprintTests.EmptyIdBlocked),
                ("Blueprint.NoBuildingsBlocked", BlueprintTests.NoBuildingsBlocked),
                ("Blueprint.FutureSchemaBlocked", BlueprintTests.FutureSchemaBlocked),
                ("Blueprint.DuplicateRecordId", BlueprintTests.DuplicateRecordId),
                ("Blueprint.MissingPrefabId", BlueprintTests.MissingPrefabId),
                ("Blueprint.MissingOffsetsBlocked", BlueprintTests.MissingOffsetsBlocked),
                ("Blueprint.InvalidDeclaredSize", BlueprintTests.InvalidDeclaredSize),
                ("Blueprint.SentinelOrientationBlocked", BlueprintTests.SentinelOrientationBlocked),
                ("Blueprint.CenteredAnchorNormalization", BlueprintTests.CenteredAnchorNeedsOccupiedCellNormalization),
                ("Blueprint.WideBuildingsNoShift", BlueprintTests.WideBuildingsDoNotShiftAfterPlacement),
                ("Blueprint.DeclaredSizeMismatchIsWarning", BlueprintTests.DeclaredSizeMismatchIsWarningNotBlocker),
                ("Blueprint.SameLayerCellOverlap", BlueprintTests.SameLayerOccupiedCellOverlap),
                ("Blueprint.AdjacentNoOverlap", BlueprintTests.AdjacentBuildingsDoNotOverlap),
                ("Blueprint.BoundsOverlapCellsDisjoint", BlueprintTests.BoundingBoxOverlapButCellsDisjoint),
                ("Blueprint.DifferentLayerNoOverlap", BlueprintTests.DifferentLayerAllowed),
                ("Blueprint.LocalBounds", BlueprintTests.LocalBounds),
                ("Blueprint.Normalize", BlueprintTests.Normalize),
                ("Blueprint.OccupancyMap", BlueprintTests.OccupancyMapReflectsOccupiedCellsOnly),

                // ================= P2-A：几何转换 =================
                ("Conversion.WorldLocalRoundtrip", CoordinateConversionTests.WorldLocalRoundtrip),
                ("Conversion.GameCellsMatchOrigin", CoordinateConversionTests.GameCellsMatchLocalPlusOrigin),
                ("Conversion.FootprintCornerRoundtrip", CoordinateConversionTests.FootprintCornerConversionRoundtrip),
                ("Conversion.FootprintCornerMatchesBounds", CoordinateConversionTests.FootprintCornerMatchesBounds),
                ("Conversion.AnchorInFootprintPerWidth", CoordinateConversionTests.AnchorInFootprintPerWidth),
                ("Conversion.AnchorInFootprintRotates", CoordinateConversionTests.AnchorInFootprintRotatesWithOrientation),
                ("Conversion.RotatedCellsSwapBounds", CoordinateConversionTests.RotatedCellsPreserveCountAndSwapBounds),
                ("Conversion.RotationImmutable", CoordinateConversionTests.RotationNeverMutatesRawOffsets),
                ("Conversion.RotatedOffsetsFresh", CoordinateConversionTests.RotatedOffsetsAreFreshArrays),
                ("Conversion.AsymmetricFootprint", CoordinateConversionTests.AsymmetricFootprintRotatesCorrectly),
                ("Conversion.ZeroOffsetSafe", CoordinateConversionTests.ZeroOffsetRecordIsSafe),

                // ================= P1：朝向变换 =================
                ("Orientation.RotateMatchesGameFormulas", OrientationTests.RotateMatchesGameFormulas),
                ("Orientation.SentinelIsRejected", OrientationTests.SentinelIsRejectedNotSilentlyIdentity),
                ("Orientation.CompositionInvariants", OrientationTests.RotationCompositionInvariants),
                ("Orientation.ValidOrientationSets", OrientationTests.ValidOrientationSets),
                ("Orientation.NextMirrorsGame", OrientationTests.NextOrientationMirrorsGame),
                ("Orientation.RotateAllOrder", OrientationTests.RotateAllPreservesOrderAndCount),
                ("Orientation.BoundsOf", OrientationTests.BoundsOfRotatedOffsets),
                ("Orientation.FitsInSize", OrientationTests.FitsInSizeChecks),
                ("Orientation.GameValueConversion", OrientationTests.GameValueConversion),
                ("Orientation.EnumMatchesGameNumbers", OrientationTests.OrientationEnumMatchesGameNumbers),

                // ================= P1：栅格索引 =================
                ("Grid.IndexFormula", GridGeometryTests.IndexFormula),
                ("Grid.InverseMapping", GridGeometryTests.InverseMapping),
                ("Grid.RoundtripFullGrid", GridGeometryTests.RoundtripOverFullGrid),
                ("Grid.BoundsChecking", GridGeometryTests.BoundsChecking),
                ("Grid.ValidIndex", GridGeometryTests.ValidIndex),
                ("Grid.OffsetWraparoundHazard", GridGeometryTests.OffsetWraparoundHazard),
                ("Grid.OffsetVerticalDiagonal", GridGeometryTests.OffsetVerticalAndDiagonal),
                ("Grid.InvalidWidthRejected", GridGeometryTests.InvalidWidthRejected),
                ("Grid.OffsetPoint", GridGeometryTests.OffsetPoint),

                // ================= P1：建筑几何与几何库 =================
                ("Geometry.ConstructionBasics", GeometryTableTests.ConstructionBasics),
                ("Geometry.DisplayNameFallback", GeometryTableTests.DisplayNameFallsBackToPrefabId),
                ("Geometry.EmptyPrefabIdRejected", GeometryTableTests.EmptyPrefabIdRejected),
                ("Geometry.OffsetsDefensivelyCopied", GeometryTableTests.OffsetsAreDefensivelyCopied),
                ("Geometry.AbsoluteCellsNeutral", GeometryTableTests.AbsoluteCellsNeutral),
                ("Geometry.AbsoluteCellsRotated", GeometryTableTests.AbsoluteCellsRotated),
                ("Geometry.InvalidOrientationRejected", GeometryTableTests.InvalidOrientationRejected),
                ("Geometry.BoundsForOrientation", GeometryTableTests.BoundsForOrientation),
                ("Geometry.CenteredAnchoring", GeometryTableTests.CenteredAnchoringIsAccepted),
                ("Geometry.AnchorMatchesGameFormula", GeometryTableTests.AnchorMatchesGenerateOffsetsFormula),
                ("Geometry.DeclaredSizeMayDiffer", GeometryTableTests.DeclaredSizeMayDifferFromActual),
                ("Geometry.AnomalyInvalidSize", GeometryTableTests.AnomalyInvalidSize),
                ("Geometry.AnomalyEmptyOffsets", GeometryTableTests.AnomalyEmptyOffsets),
                ("Geometry.AnomalyCountMismatch", GeometryTableTests.AnomalyCountMismatch),
                ("Geometry.AnomalyOutOfDeclaredSize", GeometryTableTests.AnomalyOutOfDeclaredSize),
                ("Geometry.AnomalyRotationExceeds", GeometryTableTests.AnomalyRotationExceedsSwappedSize),
                ("Geometry.CleanHasNoAnomaly", GeometryTableTests.CleanGeometryHasNoAnomaly),
                ("GeometryTable.AddAndLookup", GeometryTableTests.TableAddAndLookup),
                ("GeometryTable.RejectsDuplicate", GeometryTableTests.TableRejectsDuplicateWithAnomaly),
                ("GeometryTable.CollectsChildAnomalies", GeometryTableTests.TableCollectsChildAnomalies),
                ("GeometryTable.SealBlocksWrites", GeometryTableTests.TableSealBlocksWrites),

                // ================= P1：初始化时序 =================
                ("Initializer.NotReadyNoAttempt", InitializerTests.NotReadyDoesNotConsumeAttempts),
                ("Initializer.ReadyThenSucceeds", InitializerTests.ReadyThenSucceeds),
                ("Initializer.RepeatedShortCircuits", InitializerTests.RepeatedCallsShortCircuit),
                ("Initializer.FailureRetriableUntilMax", InitializerTests.FailureIsRetriableUntilMax),
                ("Initializer.ReadinessExceptionNotCounted", InitializerTests.ReadinessExceptionIsNotCounted),
                ("Initializer.ReentrancyRejected", InitializerTests.ReentrancyIsRejected),
                ("Initializer.ResetAllowsRetry", InitializerTests.ResetAllowsRetry),
                ("Initializer.MaxAttemptsValidated", InitializerTests.MaxAttemptsValidated),
                ("Initializer.SimulatesGameStartup", InitializerTests.SimulatesGameStartupSequence),
            };

            Console.WriteLine("OSB.Domain.Tests —— 共 " + cases.Count + " 个用例");
            Console.WriteLine(new string('-', 72));

            int failedCases = 0;

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

                bool passed = Check.Failed == failedBefore;
                if (!passed)
                {
                    failedCases++;
                }

                Console.WriteLine((passed ? "PASS " : "FAIL ") + name);
            }

            Console.WriteLine(new string('-', 72));
            Console.WriteLine("用例 " + (cases.Count - failedCases) + "/" + cases.Count + " 通过"
                + "；断言通过 " + Check.Passed + " 条，失败 " + Check.Failed + " 条");

            if (Check.Failed == 0)
            {
                Console.WriteLine("结果：PASS");
                return 0;
            }

            Console.WriteLine("失败明细：");
            foreach (string failure in Check.Failures)
            {
                Console.WriteLine("  - " + failure);
            }

            Console.WriteLine("结果：FAIL");
            return 1;
        }
    }
}
