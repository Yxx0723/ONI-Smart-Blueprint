using System;

namespace OSB.Domain
{
    /// <summary>一次性初始化器的生命周期状态。</summary>
    public enum InitState
    {
        /// <summary>尚未成功，也尚未失败；可以被再次尝试。</summary>
        Idle = 0,

        /// <summary>正在执行中（防重入）。</summary>
        InProgress = 1,

        /// <summary>已成功，后续调用一律短路。</summary>
        Completed = 2,

        /// <summary>已达最大尝试次数仍失败，终止，不再自愈。</summary>
        Failed = 3
    }

    /// <summary>一次 <see cref="OneShotInitializer.TryRun"/> 调用的结果。</summary>
    public enum InitOutcome
    {
        /// <summary>前置条件未满足，本次未执行（不计入尝试次数）。</summary>
        NotReady = 0,

        /// <summary>本次执行成功。</summary>
        Completed = 1,

        /// <summary>此前已成功，本次直接短路。</summary>
        AlreadyCompleted = 2,

        /// <summary>本次执行抛异常且未达上限，可继续重试。</summary>
        Failed = 3,

        /// <summary>已达上限，终止。</summary>
        Terminal = 4,

        /// <summary>重入调用被拒绝。</summary>
        Busy = 5
    }

    /// <summary>
    /// 可重入保护的一次性初始化器。
    ///
    /// 放在领域层而不是适配层，是为了让"重复初始化""对象尚未就绪""初始化失败"这三类
    /// 边界行为能在无游戏环境的情况下被单元测试覆盖。适配层只负责提供
    /// "是否就绪"与"实际要做什么"两个委托。
    ///
    /// 设计取舍：
    ///   * 就绪检查失败**不**计入尝试次数——否则启动早期（游戏数据库没建好）就会被
    ///     白白耗光重试额度。
    ///   * 失败有上限（默认 3 次），到顶后进入 <see cref="InitState.Failed"/> 终态，
    ///     不再每帧重试刷日志。需要人工恢复时调用 <see cref="Reset"/>。
    ///   * 成功后连就绪检查都不再执行，满足"避免每帧重复采集"。
    /// </summary>
    public sealed class OneShotInitializer
    {
        public const int DefaultMaxAttempts = 3;

        private readonly object _gate = new object();
        private readonly string _name;
        private readonly int _maxAttempts;

        private InitState _state = InitState.Idle;
        private int _attempts;
        private string _lastReason = "尚未尝试";

        public OneShotInitializer(string name, int maxAttempts = DefaultMaxAttempts)
        {
            if (maxAttempts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "最大尝试次数必须为正。");
            }

            _name = string.IsNullOrEmpty(name) ? "unnamed" : name;
            _maxAttempts = maxAttempts;
        }

        public string Name => _name;

        public int MaxAttempts => _maxAttempts;

        public InitState State
        {
            get
            {
                lock (_gate)
                {
                    return _state;
                }
            }
        }

        public bool IsCompleted => State == InitState.Completed;

        public bool IsTerminal => State == InitState.Failed;

        public int Attempts
        {
            get
            {
                lock (_gate)
                {
                    return _attempts;
                }
            }
        }

        public string LastReason
        {
            get
            {
                lock (_gate)
                {
                    return _lastReason;
                }
            }
        }

        /// <summary>
        /// 尝试执行一次初始化。
        /// </summary>
        /// <param name="isReady">前置条件检查。返回 false 表示游戏对象尚未就绪。</param>
        /// <param name="run">实际初始化动作。抛异常即视为一次失败尝试。</param>
        public InitOutcome TryRun(Func<bool> isReady, Action run)
        {
            if (isReady == null)
            {
                throw new ArgumentNullException(nameof(isReady));
            }

            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }

            lock (_gate)
            {
                // 快路径：已完成则连就绪检查都不做
                if (_state == InitState.Completed)
                {
                    return InitOutcome.AlreadyCompleted;
                }

                if (_state == InitState.Failed)
                {
                    return InitOutcome.Terminal;
                }

                if (_state == InitState.InProgress)
                {
                    return InitOutcome.Busy;
                }

                bool ready;
                try
                {
                    ready = isReady();
                }
                catch (Exception e)
                {
                    // 就绪检查本身炸了，视为"未就绪"：不计次，但记录原因
                    _lastReason = "就绪检查抛异常：" + Describe(e);
                    return InitOutcome.NotReady;
                }

                if (!ready)
                {
                    _lastReason = "前置条件未满足，等待中";
                    return InitOutcome.NotReady;
                }

                _attempts++;
                _state = InitState.InProgress;
            }

            // 在锁外执行实际初始化，避免长时间持锁
            try
            {
                run();
            }
            catch (Exception e)
            {
                lock (_gate)
                {
                    string detail = "第 " + _attempts + " 次尝试失败：" + Describe(e);
                    if (_attempts >= _maxAttempts)
                    {
                        _state = InitState.Failed;
                        _lastReason = detail + "（已达上限 " + _maxAttempts + "，终止）";
                        return InitOutcome.Terminal;
                    }

                    _state = InitState.Idle;
                    _lastReason = detail;
                    return InitOutcome.Failed;
                }
            }

            lock (_gate)
            {
                _state = InitState.Completed;
                _lastReason = "第 " + _attempts + " 次尝试成功";
                return InitOutcome.Completed;
            }
        }

        /// <summary>人工恢复：重置为初始状态。已成功者不应调用。</summary>
        public void Reset()
        {
            lock (_gate)
            {
                _state = InitState.Idle;
                _attempts = 0;
                _lastReason = "已重置";
            }
        }

        public override string ToString() =>
            _name + "[" + State + " attempts=" + Attempts + "/" + _maxAttempts + "] " + LastReason;

        private static string Describe(Exception e) => e.GetType().Name + ": " + e.Message;
    }
}
