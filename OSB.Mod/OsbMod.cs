using System.Collections.Generic;
using HarmonyLib;
using KMod;
using OSB.GameAdapter;
using UnityEngine;

namespace OSB.Mod
{
    /// <summary>
    /// MOD 加载入口。
    ///
    /// 关于多程序集的已核实事实（KMod.DLLLoader.LoadDLLs，见 docs/API-Audit.md A-001）：
    ///   * 加载器会把 MOD 目录下每个 .dll 都 Assembly.LoadFrom 一遍；
    ///   * 每个程序集各自成为一个 UserMod2 实例：含子类则实例化它，
    ///     不含则（Dev 模式）自动 new 一个空的 UserMod2；
    ///   * 同一程序集内出现两个及以上 UserMod2 子类 → LogError 并中止整个 MOD 加载。
    ///
    /// 因此本解决方案的部署形态是安全的：
    ///   OSB.Domain.dll       —— 无 UserMod2 子类、无 Harmony 特性 → 空实例，PatchAll 无事发生
    ///   OSB.GameAdapter.dll  —— 无 UserMod2 子类，但可以承载 Harmony 特性类
    ///   OSB.Mod.dll          —— 恰好一个 UserMod2 子类（本类）
    ///
    /// 注意：基类 OnLoad 的默认实现是 harmony.PatchAll(assembly)，所以
    /// GameAdapter 里的 [HarmonyPatch] 类会在自动创建的实例上被应用。
    /// P0 阶段刻意不放任何补丁，避免未验证的 patch 影响正常游戏。
    /// </summary>
    public sealed class OsbMod : UserMod2
    {
        private const string Prefix = "[OSB] ";

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            Debug.Log(Prefix + "OnLoad, assembly=" + typeof(OsbMod).Assembly.GetName().Name);

            // P1：显式挂载数据管线补丁。
            // 刻意不用 [HarmonyPatch] 特性 —— 那条路径由加载器补出的空 UserMod2 实例执行
            // PatchAll，没有 try/catch，特性写错就是整包 MOD 加载失败。
            try
            {
                GameDatabaseReady.ApplyPatches(harmony);
            }
            catch (System.Exception e)
            {
                // 补丁挂载失败不允许影响 MOD 加载
                Debug.LogWarning(Prefix + "ApplyPatches 异常：" + e);
            }
        }

        public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<KMod.Mod> mods)
        {
            base.OnAllModsLoaded(harmony, mods);

            try
            {
                GameProbe.Run();
            }
            catch (System.Exception e)
            {
                // 探针失败绝不允许影响游戏启动
                Debug.LogError(Prefix + "GameProbe 异常：" + e);
            }

            Debug.Log(Prefix + "OnAllModsLoaded 完成，P0 探针已执行");
        }
    }
}
