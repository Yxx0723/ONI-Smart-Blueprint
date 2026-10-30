using System;
using System.Reflection;
using HarmonyLib;
using OSB.Domain;
using UnityEngine;

namespace OSB.GameAdapter
{
    /// <summary>
    /// P1 数据管线：在**正确的时机**一次性采集建筑几何、核对 Grid 坐标、审计科技与材料接口。
    ///
    /// ## 时机从哪来（反编译核实，见 docs/API-Audit.md L-001）
    ///
    /// <code>
    /// Assets.OnPrefabInit()
    ///   └─ CreatePrefabs()
    ///        ├─ Db.Get()                         ← 游戏数据库在此初始化
    ///        ├─ BuildingDefs = new List&lt;&gt;()      ← 清空并准备填充
    ///        ├─ foreach PrefabAssets → AddPrefab
    ///        ├─ LegacyModMain.Load()
    ///        │    └─ GeneratedBuildings.LoadGeneratedBuildings(types)
    ///        │         └─ BuildingConfigManager.RegisterBuilding(config)
    ///        │              └─ Assets.AddBuildingDef(def)   ← 建筑在此逐个入库
    ///        ├─ Db.Get().PostProcess()
    ///        └─ ComplexRecipeManager.Get().PostProcess()
    /// </code>
    ///
    /// 结论：**<c>Assets.CreatePrefabs</c> 返回之后，BuildingDefs 才是完整的**（含所有 Mod 建筑）。
    /// <see cref="KMod.UserMod2.OnAllModsLoaded"/> 在其之前，所以 P0 探针读到的是 null。
    ///
    /// ## 补丁为什么显式挂载而不是用 [HarmonyPatch] 特性
    ///
    /// 特性补丁会在加载器为 GameAdapter 自动补出的空 <c>UserMod2</c> 实例上，
    /// 由基类 <c>OnLoad</c> 执行 <c>PatchAll</c> 时应用。那条路径**没有 try/catch**——
    /// 一旦特性写错就是整包 MOD 加载失败，并可能被 ONI 记为崩溃源（P0 已踩过一次）。
    /// 显式挂载可以逐条捕获异常、逐条记录结果，失败也不影响 MOD 其余部分。
    /// </summary>
    public static class GameDatabaseReady
    {
        internal static readonly OneShotInitializer GeometryInit = new OneShotInitializer("building-geometry");
        internal static readonly OneShotInitializer GridInit = new OneShotInitializer("grid-coordinates", 5);

        private static BuildingGeometryTable _geometry;
        private static bool _loggedGeometryWaiting;
        private static bool _loggedGridWaiting;

        /// <summary>采集完成的建筑几何库。未完成时为 null。</summary>
        internal static BuildingGeometryTable Geometry => _geometry;

        internal static bool GeometryCollected => GeometryInit.IsCompleted && _geometry != null;

        internal static bool GridVerified => GridInit.IsCompleted;

        /// <summary>
        /// 由 OSB.Mod 在 <c>OnLoad</c> 中调用，显式挂载两个补丁。
        /// 每条单独 try/catch，任何一条失败都不影响其余功能与 MOD 加载。
        /// </summary>
        public static void ApplyPatches(Harmony harmony)
        {
            if (harmony == null)
            {
                OsbLog.Warn("ApplyPatches 收到 null harmony，跳过");
                return;
            }

            TryPatch(harmony, typeof(Assets), "CreatePrefabs", nameof(OnCreatePrefabs), "Assets.CreatePrefabs");
            TryPatch(harmony, typeof(Game), "Update", nameof(OnGameUpdate), "Game.Update");
        }

        private static void TryPatch(Harmony harmony, Type targetType, string targetMethod, string postfixName, string label)
        {
            try
            {
                MethodInfo target = AccessTools.Method(targetType, targetMethod);
                if (target == null)
                {
                    OsbLog.Warn("补丁目标不存在，已跳过：" + label);
                    return;
                }

                MethodInfo postfix = typeof(GameDatabaseReady).GetMethod(
                    postfixName,
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (postfix == null)
                {
                    OsbLog.Warn("找不到 postfix 方法 " + postfixName + "，已跳过：" + label);
                    return;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                OsbLog.Info("补丁已挂载：" + label + " → " + postfixName);
            }
            catch (Exception e)
            {
                OsbLog.Warn("补丁挂载失败（不影响 MOD 加载）：" + label + " | " + e.GetType().Name + ": " + e.Message);
            }
        }

        // ---------- 补丁回调 ----------

        private static void OnCreatePrefabs() => Tick("Assets.CreatePrefabs");

        private static void OnGameUpdate() => Tick("Game.Update");

        // ---------- 主循环 ----------

        /// <summary>
        /// 由两个补丁共同驱动。两个初始化器各自短路由，已完成后开销仅一次 bool 读取。
        /// </summary>
        internal static void Tick(string source)
        {
            if (!GeometryInit.IsCompleted)
            {
                TryCollectGeometry(source);
            }

            if (!GridInit.IsCompleted)
            {
                TryVerifyGrid(source);
            }
        }

        private static void TryCollectGeometry(string source)
        {
            InitOutcome outcome = GeometryInit.TryRun(
                IsBuildingDatabaseReady,
                () =>
                {
                    BuildingGeometryTable table = BuildingGeometryCollector.Collect();
                    table.Seal();
                    _geometry = table;
                    ReportGeometry(table, source);
                });

            switch (outcome)
            {
                case InitOutcome.NotReady:
                    if (!_loggedGeometryWaiting)
                    {
                        _loggedGeometryWaiting = true;
                        OsbLog.Info("建筑几何采集等待数据库就绪（触发点 " + source + "）");
                    }

                    break;

                case InitOutcome.Completed:
                    OsbLog.Info("建筑几何采集完成，来源=" + source + "，尝试次数=" + GeometryInit.Attempts);
                    break;

                case InitOutcome.Failed:
                case InitOutcome.Terminal:
                    OsbLog.Warn("建筑几何采集失败：" + GeometryInit.LastReason
                        + "（状态=" + GeometryInit.State + "，尝试=" + GeometryInit.Attempts + "）");
                    break;
            }
        }

        private static void TryVerifyGrid(string source)
        {
            InitOutcome outcome = GridInit.TryRun(
                IsGridReady,
                () =>
                {
                    GridCoordinateVerifier.Verify();
                    TechAccessor.Audit();
                    MaterialAccessor.Audit();
                });

            switch (outcome)
            {
                case InitOutcome.NotReady:
                    if (!_loggedGridWaiting)
                    {
                        _loggedGridWaiting = true;
                        OsbLog.Info("Grid 坐标核对等待进入世界（触发点 " + source + "）");
                    }

                    break;

                case InitOutcome.Completed:
                    OsbLog.Info("Grid 坐标核对完成，来源=" + source + "，尝试次数=" + GridInit.Attempts);
                    break;

                case InitOutcome.Failed:
                case InitOutcome.Terminal:
                    OsbLog.Warn("Grid 坐标核对失败：" + GridInit.LastReason);
                    break;
            }
        }

        // ---------- 就绪判定 ----------

        /// <summary>建筑数据库就绪：BuildingDefs 非 null 且非空。</summary>
        internal static bool IsBuildingDatabaseReady()
        {
            return Assets.BuildingDefs != null && Assets.BuildingDefs.Count > 0;
        }

        /// <summary>Grid 就绪：网格已按世界尺寸初始化，且世界管理器可用。</summary>
        internal static bool IsGridReady()
        {
            return Grid.CellCount > 0 && ClusterManager.Instance != null;
        }

        // ---------- 报告 ----------

        private static void ReportGeometry(BuildingGeometryTable table, string source)
        {
            OsbLog.Info("=== 建筑几何采集结果（来源 " + source + "）===");
            OsbLog.Info("建筑总数=" + table.Count + "，异常项=" + table.Anomalies.Count);

            int rotatable = 0;
            int multiCell = 0;
            int deprecated = 0;
            int maxCells = 0;
            string maxCellsId = "<none>";
            int totalOffsets = 0;
            var byRotation = new int[5];

            foreach (BuildingGeometry geometry in table.All)
            {
                if (geometry.IsRotatable)
                {
                    rotatable++;
                }

                if (geometry.CellCount > 1)
                {
                    multiCell++;
                }

                if (geometry.Deprecated)
                {
                    deprecated++;
                }

                totalOffsets += geometry.PlacementOffsets.Count;

                int rotationIndex = (int)geometry.PermittedRotations;
                if (rotationIndex >= 0 && rotationIndex < byRotation.Length)
                {
                    byRotation[rotationIndex]++;
                }

                if (geometry.CellCount > maxCells)
                {
                    maxCells = geometry.CellCount;
                    maxCellsId = geometry.PrefabId + "(" + geometry.Width + "x" + geometry.Height + ")";
                }
            }

            OsbLog.Info("可旋转=" + rotatable + "，多格=" + multiCell + "，弃用=" + deprecated
                + "，占位格总数=" + totalOffsets + "，最大占位=" + maxCellsId);

            OsbLog.Info("旋转形态分布：Unrotatable=" + byRotation[0] + " R90=" + byRotation[1]
                + " R360=" + byRotation[2] + " FlipH=" + byRotation[3] + " FlipV=" + byRotation[4]);

            // 异常明细（最多打印 20 条，避免刷屏）
            int printed = 0;
            foreach (GeometryAnomaly anomaly in table.Anomalies)
            {
                if (printed >= 20)
                {
                    OsbLog.Warn("……还有 " + (table.Anomalies.Count - printed) + " 条异常未列出");
                    break;
                }

                OsbLog.Warn("异常 " + anomaly);
                printed++;
            }

            ReportSamples(table);
        }

        private static void ReportSamples(BuildingGeometryTable table)
        {
            string[] samples = { "Tile", "Door", "ManualGenerator", "Outhouse", "RationBox", "Ladder", "Battery" };
            foreach (string prefabId in samples)
            {
                if (!table.TryGet(prefabId, out BuildingGeometry geometry))
                {
                    OsbLog.Info("样本 " + prefabId + " = <未采集到>");
                    continue;
                }

                OsbLog.Info("样本 " + prefabId
                    + " size=" + geometry.Width + "x" + geometry.Height
                    + " offsets=" + geometry.PlacementOffsets.Count
                    + " rot=" + geometry.PermittedRotations
                    + " layer=" + geometry.ObjectLayerName
                    + " rule=" + geometry.BuildLocationRuleName
                    + " materials=[" + string.Join(",", ToArray(geometry.MaterialCategories)) + "]");
            }
        }

        private static string[] ToArray(System.Collections.Generic.IReadOnlyList<string> list)
        {
            var result = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                result[i] = list[i];
            }

            return result;
        }
    }
}
