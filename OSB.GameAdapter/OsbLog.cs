using UnityEngine;

namespace OSB.GameAdapter
{
    /// <summary>
    /// 统一日志出口。
    ///
    /// **硬规则：这里只有 Log 和 LogWarning，没有 LogError。**
    /// P0 实测教训：MOD 加载期调用 <c>Debug.LogError</c> 会让 ONI 扫描异常堆栈、
    /// 把命中的程序集登记进"疑似肇事 MOD"列表，游戏随后任何一次崩溃都会记到本 MOD 头上，
    /// 并触发安全模式禁用全部 MOD。详见 AGENTS.md 第 4 节。
    /// </summary>
    internal static class OsbLog
    {
        private const string P1Prefix = "[OSB][P1] ";
        private const string P0Prefix = "[OSB][P0] ";

        internal static void Info(string message) => Debug.Log(P1Prefix + message);

        internal static void Warn(string message) => Debug.LogWarning(P1Prefix + message);

        internal static void P0Info(string message) => Debug.Log(P0Prefix + message);

        internal static void P0Warn(string message) => Debug.LogWarning(P0Prefix + message);

        internal static void Bare(string message) => Debug.Log("[OSB] " + message);
    }
}
