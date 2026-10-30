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
    /// 建筑朝向。
    ///
    /// **数值必须与 ONI 的 <c>Orientation</c> 枚举逐一对应**（已反编译核实）：
    /// <c>Neutral=0, R90=1, R180=2, R270=3, NumRotations=4, FlipH=5, FlipV=6</c>。
    /// 这样适配层可以直接整数转换，避免映射表出错。
    ///
    /// <see cref="NumRotations"/> 不是可用朝向，而是"旋转形态数量"的哨兵值：
    /// 游戏 <c>Rotatable.GetRotatedCellOffset</c> 的 default 分支会把它当恒等变换静默处理，
    /// 这是最容易埋雷的地方。本领域层把它建模出来，并让 <see cref="GeometryOrientation"/>
    /// 在收到该值时**抛异常而不是静默放行**。
    /// </summary>
    public enum BuildingOrientation
    {
        Neutral = 0,
        R90 = 1,
        R180 = 2,
        R270 = 3,
        NumRotations = 4,
        FlipH = 5,
        FlipV = 6
    }

    /// <summary>
    /// 建筑可用的旋转形态。与 ONI 的 <c>PermittedRotations</c> 枚举逐一对应（已反编译核实）。
    /// 注意它与 <see cref="BuildingOrientation"/> 不是一回事：前者是"能怎么转"，后者是"当前转到了哪"。
    /// </summary>
    public enum PermittedRotation
    {
        Unrotatable = 0,
        R90 = 1,
        R360 = 2,
        FlipH = 3,
        FlipV = 4
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
