using System;
using UnityEngine;

namespace OSB.GameAdapter
{
    /// <summary>
    /// P0 只读探针。
    ///
    /// 目的：把 docs/API-Audit.md 中那些"通过反编译确认存在"的 API，换成
    /// "运行时在真实游戏内确认存在且签名一致"的证据。探针只读取信息并写日志，
    /// 不创建任何建筑、不修改世界、不写存档。
    ///
    /// 调用点：OSB.Mod 的 OnAllModsLoaded（见 OSB.Mod/OsbMod.cs）。
    ///
    /// 两条设计纪律（首轮实测后补上）：
    ///   1. 日志一律用 Debug.Log/LogWarning，**不用 Debug.LogError**。
    ///      ONI 会扫描异常堆栈并把命中的程序集登记进"疑似肇事 MOD"列表
    ///      （Player.log: "declared the method ... in the stack trace, adding to referenced mods list"），
    ///      一旦游戏因任何原因崩溃，被登记过的 MOD 会背上 crash_count。
    ///      探针是只读诊断，不该有任何机会被算作崩溃源。
    ///   2. 所有游戏对象访问前先判空。OnAllModsLoaded 时游戏数据库尚未初始化，
    ///      ClusterManager.Instance / Assets.BuildingDefs 都可能是 null。
    /// </summary>
    public static class GameProbe
    {
        private const string Prefix = "[OSB][P0] ";

        public static void Run()
        {
            Log("---- probe start ----");
            RunSection("ReportEnvironment", ReportEnvironment);
            RunSection("ReportApiSurface", ReportApiSurface);
            RunSection("ReportDatabaseState", ReportDatabaseState);
            Log("---- probe end ----");
        }

        /// <summary>逐段隔离：任一段失败都不影响其余段，且不会抛出到加载器。</summary>
        private static void RunSection(string section, System.Action body)
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                LogWarning(section + " 未完成：" + e.GetType().Name + ": " + e.Message);
            }
        }

        private static void ReportEnvironment()
        {
            string build;
            try
            {
                build = BuildWatermark.GetBuildText();
            }
            catch (Exception e)
            {
                build = "<BuildWatermark 不可用: " + e.GetType().Name + ">";
            }

            Log("GameBuild=" + build);
            Log("OS=" + Environment.OSVersion + " 64bit=" + Environment.Is64BitProcess);

            // 此阶段 Grid 尚未初始化，读到 0x0 是正常的，记录下来作为"探针跑得过早"的证据
            Log("Grid=" + Grid.WidthInCells + "x" + Grid.HeightInCells + " cells=" + Grid.CellCount
                + (Grid.CellCount == 0 ? "  <-- 尚未初始化，符合预期" : string.Empty));

            ClusterManager cluster = ClusterManager.Instance;
            Log("ClusterManager.Instance=" + (cluster == null ? "<null，尚未初始化>" : "ok, activeWorldId=" + cluster.activeWorldId));
            Log("Game.Instance=" + (Game.Instance == null ? "<null>" : "ok"));
        }

        /// <summary>
        /// 用反射核对关键成员的签名。编译期已能证明类型存在；
        /// 这里额外核对参数个数，防止运行期程序集与编译期引用不一致。
        /// 参数个数取自 docs/API-Audit.md 的反编译结果，不是猜的。
        /// </summary>
        private static void ReportApiSurface()
        {
            const System.Reflection.BindingFlags Instance =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

            // 放置路径（A-005 / A-006 / A-008）
            CheckMethod(typeof(BuildingDef), "TryPlace", Instance, 6);          // (src_go, pos, orient, elements, facadeID, layer)
            CheckMethod(typeof(BuildingDef), "TryReplaceTile", Instance, 5);    // (src_go, pos, orient, elements, layer)
            CheckMethod(typeof(BuildingDef), "Instantiate", Instance, 4);       // (pos, orient, elements, layer)
            CheckMethod(typeof(BuildingDef), "IsValidPlaceLocation", Instance, 6); // (src_go, cell, orient, replace, fail, restrict)
            CheckMethod(typeof(BuildingDef), "RunOnArea", Instance, 3);         // (cell, orient, callback)
            CheckMethod(typeof(BuildingDef), "MarkArea", Instance, 4);          // (cell, orient, layer, go)

            // 瞬间建成路径（A-007）——产品禁用，此处只做签名留存
            CheckMethod(typeof(BuildingDef), "Build", Instance, 7);             // (cell, orient, storage, elements, temp, playsound, timeBuilt)

            // 加载器契约（A-001 / A-003）
            CheckMethod(typeof(KMod.UserMod2), "OnLoad", Instance, 1);
            CheckMethod(typeof(KMod.UserMod2), "OnAllModsLoaded", Instance, 2);

            // KMod.DLLLoader 是 internal static，跨程序集无法直接 typeof()，只能按名字反射。
            Type dllLoader = typeof(KMod.UserMod2).Assembly.GetType("KMod.DLLLoader", false);
            if (dllLoader == null)
            {
                LogWarning("KMod.DLLLoader 不存在（加载器实现可能已变更）");
                return;
            }

            const System.Reflection.BindingFlags Static =
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

            CheckMethod(dllLoader, "LoadDLLs", Static, 4);
            CheckMethod(dllLoader, "PostLoadDLLs", Static, 3);
        }

        private static void CheckMethod(Type type, string name, System.Reflection.BindingFlags flags, int expectedParamCount)
        {
            System.Reflection.MethodInfo[] candidates = type.GetMethods(flags);
            int matched = 0;
            int total = 0;

            foreach (System.Reflection.MethodInfo method in candidates)
            {
                if (method.Name != name)
                {
                    continue;
                }

                total++;
                if (method.GetParameters().Length == expectedParamCount)
                {
                    matched++;
                }
            }

            if (matched > 0)
            {
                Log("OK " + type.Name + "." + name + "(" + expectedParamCount + " 参) 命中 " + matched + "/" + total);
            }
            else
            {
                LogWarning("签名不匹配：" + type.Name + "." + name + " 未找到 " + expectedParamCount + " 参重载（该类共 " + total + " 个同名重载）");
            }
        }

        /// <summary>此时数据库通常尚未初始化；如实记录当前状态，不当作失败。</summary>
        private static void ReportDatabaseState()
        {
            int count = Assets.BuildingDefs == null ? -1 : Assets.BuildingDefs.Count;
            Log("Assets.BuildingDefs=" + (count < 0 ? "<null>" : count.ToString()));

            if (count <= 0)
            {
                Log("数据库尚未初始化 —— 建筑几何采样推迟到 P1（需在 Db 初始化后执行）");
            }
        }

        private static void Log(string message) => Debug.Log(Prefix + message);

        /// <summary>刻意不用 Debug.LogError：见类注释第 1 条纪律。</summary>
        private static void LogWarning(string message) => Debug.LogWarning(Prefix + message);
    }
}
