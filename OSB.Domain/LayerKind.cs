namespace OSB.Domain
{
    /// <summary>
    /// 蓝图条目所在的游戏图层。与 ONI 的 ObjectLayer 是两套东西：
    /// 这是规划层的抽象，映射关系由 GameAdapter 负责。
    /// </summary>
    public enum LayerKind
    {
        Building = 0,
        Backwall = 1,
        GasConduit = 2,
        LiquidConduit = 3,
        SolidConduit = 4,
        Power = 5,
        Automation = 6,
        Tile = 7
    }

    /// <summary>
    /// 建筑朝向。取值集合与已核实的 ONI <c>Orientation</c> 枚举一致
    /// （Neutral / R90 / R180 / R270 / FlipH / FlipV），但本枚举属于领域层，
    /// 不直接引用游戏类型。
    /// </summary>
    public enum BuildingOrientation
    {
        Neutral = 0,
        R90 = 1,
        R180 = 2,
        R270 = 3,
        FlipH = 4,
        FlipV = 5
    }

    /// <summary>校验问题级别。Blocker 阻止放置，Warning 允许但需提示，Info 仅建议。</summary>
    public enum IssueSeverity
    {
        Info = 0,
        Warning = 1,
        Blocker = 2
    }

    /// <summary>
    /// 一条可解释的校验结果。Code 稳定，可被 UI 与测试断言引用；
    /// Message 面向玩家，可本地化。
    /// </summary>
    public sealed class ValidationIssue
    {
        public ValidationIssue(string code, IssueSeverity severity, string message, Point2i position)
        {
            Code = code;
            Severity = severity;
            Message = message;
            Position = position;
        }

        public string Code { get; }
        public IssueSeverity Severity { get; }
        public string Message { get; }
        public Point2i Position { get; }

        public bool BlocksPlacement => Severity == IssueSeverity.Blocker;

        public override string ToString() => Severity + " " + Code + " @" + Position + ": " + Message;
    }
}
