using System;

namespace OSB.Domain.Tests
{
    /// <summary>
    /// 一次性初始化器的状态机测试。覆盖 P1 要求的四类边界：
    /// 尚未就绪、重复初始化、初始化失败、失败到顶后的终止行为。
    /// </summary>
    internal static class InitializerTests
    {
        internal static void NotReadyDoesNotConsumeAttempts()
        {
            var init = new OneShotInitializer("t");
            int runs = 0;

            InitOutcome outcome = init.TryRun(() => false, () => runs++);

            Check.Equal(InitOutcome.NotReady, outcome, "未就绪应返回 NotReady");
            Check.Equal(0, runs, "未就绪不得执行初始化动作");
            Check.Equal(0, init.Attempts, "未就绪不得消耗尝试次数");
            Check.Equal(InitState.Idle, init.State, "未就绪后状态仍为 Idle");
            Check.True(init.LastReason.Contains("等待"), "应记录等待原因");
        }

        internal static void ReadyThenSucceeds()
        {
            var init = new OneShotInitializer("t");
            int runs = 0;

            InitOutcome outcome = init.TryRun(() => true, () => runs++);

            Check.Equal(InitOutcome.Completed, outcome, "就绪且成功应返回 Completed");
            Check.Equal(1, runs, "应执行一次");
            Check.Equal(1, init.Attempts, "尝试次数为 1");
            Check.Equal(InitState.Completed, init.State, "状态为 Completed");
            Check.True(init.IsCompleted, "IsCompleted 为真");
        }

        internal static void RepeatedCallsShortCircuit()
        {
            var init = new OneShotInitializer("t");
            int runs = 0;
            int readinessChecks = 0;

            init.TryRun(() => { readinessChecks++; return true; }, () => runs++);

            InitOutcome second = init.TryRun(() => { readinessChecks++; return true; }, () => runs++);
            InitOutcome third = init.TryRun(() => { readinessChecks++; return true; }, () => runs++);

            Check.Equal(InitOutcome.AlreadyCompleted, second, "第二次应短路返回 AlreadyCompleted");
            Check.Equal(InitOutcome.AlreadyCompleted, third, "第三次同样短路");
            Check.Equal(1, runs, "初始化动作只允许执行一次（避免每帧重复采集）");
            Check.Equal(1, readinessChecks, "完成后连就绪检查都不应再执行");
        }

        internal static void FailureIsRetriableUntilMax()
        {
            var init = new OneShotInitializer("t", maxAttempts: 2);
            int runs = 0;

            void FailingRun()
            {
                runs++;
                throw new InvalidOperationException("boom");
            }

            InitOutcome first = init.TryRun(() => true, FailingRun);
            Check.Equal(InitOutcome.Failed, first, "首次失败返回 Failed");
            Check.Equal(InitState.Idle, init.State, "未达上限时应回到 Idle 以便重试");
            Check.Equal(1, init.Attempts, "尝试次数 1");

            InitOutcome second = init.TryRun(() => true, FailingRun);
            Check.Equal(InitOutcome.Terminal, second, "达到上限返回 Terminal");
            Check.Equal(InitState.Failed, init.State, "状态进入 Failed 终态");
            Check.Equal(2, runs, "共执行两次");

            InitOutcome third = init.TryRun(() => true, FailingRun);
            Check.Equal(InitOutcome.Terminal, third, "终态后继续返回 Terminal");
            Check.Equal(2, runs, "终态后不得再次执行");
        }

        internal static void ReadinessExceptionIsNotCounted()
        {
            var init = new OneShotInitializer("t");
            int runs = 0;

            InitOutcome outcome = init.TryRun(
                () => throw new InvalidOperationException("readiness broke"),
                () => runs++);

            Check.Equal(InitOutcome.NotReady, outcome, "就绪检查抛异常应视为未就绪");
            Check.Equal(0, runs, "不得执行初始化");
            Check.Equal(0, init.Attempts, "不得消耗尝试次数");
            Check.True(init.LastReason.Contains("就绪检查抛异常"), "应记录就绪检查异常原因");
        }

        internal static void ReentrancyIsRejected()
        {
            var init = new OneShotInitializer("t");
            InitOutcome inner = InitOutcome.Completed;

            init.TryRun(() => true, () => { inner = init.TryRun(() => true, () => { }); });

            Check.Equal(InitOutcome.Busy, inner, "初始化过程中重入应被拒绝");
            Check.Equal(InitState.Completed, init.State, "外层执行完成后仍应正常结束");
        }

        internal static void ResetAllowsRetry()
        {
            var init = new OneShotInitializer("t", maxAttempts: 1);
            init.TryRun(() => true, () => throw new InvalidOperationException("once"));
            Check.Equal(InitState.Failed, init.State, "一次失败即终态");

            init.Reset();
            Check.Equal(InitState.Idle, init.State, "Reset 后回到 Idle");
            Check.Equal(0, init.Attempts, "Reset 后尝试次数归零");

            int runs = 0;
            InitOutcome outcome = init.TryRun(() => true, () => runs++);
            Check.Equal(InitOutcome.Completed, outcome, "Reset 后可以成功");
            Check.Equal(1, runs, "实际执行一次");
        }

        internal static void MaxAttemptsValidated()
        {
            Check.Throws<ArgumentOutOfRangeException>(() => new OneShotInitializer("t", 0), "maxAttempts 为 0 应拒绝");
            Check.Throws<ArgumentOutOfRangeException>(() => new OneShotInitializer("t", -1), "maxAttempts 为负应拒绝");
            Check.Throws<ArgumentNullException>(() => new OneShotInitializer("t").TryRun(null, () => { }), "null 就绪检查应拒绝");
            Check.Throws<ArgumentNullException>(() => new OneShotInitializer("t").TryRun(() => true, null), "null 初始化动作应拒绝");
        }

        internal static void SimulatesGameStartupSequence()
        {
            // 模拟 P0 遇到的真实时序：前两帧数据库为空，第三帧就绪
            var init = new OneShotInitializer("geometry");
            int databaseState = 0; // 0/1 = 未就绪，2 = 就绪
            int collections = 0;

            InitOutcome frame1 = init.TryRun(() => databaseState >= 2, () => collections++);
            InitOutcome frame2 = init.TryRun(() => databaseState >= 2, () => collections++);

            Check.Equal(InitOutcome.NotReady, frame1, "第 1 帧：数据库未就绪");
            Check.Equal(InitOutcome.NotReady, frame2, "第 2 帧：仍未就绪");
            Check.Equal(0, collections, "未就绪期间不得采集");

            databaseState = 2;
            InitOutcome frame3 = init.TryRun(() => databaseState >= 2, () => collections++);
            InitOutcome frame4 = init.TryRun(() => databaseState >= 2, () => collections++);

            Check.Equal(InitOutcome.Completed, frame3, "第 3 帧：就绪并完成采集");
            Check.Equal(1, collections, "只采集一次");
            Check.Equal(InitOutcome.AlreadyCompleted, frame4, "第 4 帧：短路，不重复采集");
        }
    }
}
