using System.Collections.Generic;

namespace OSB.Domain
{
    /// <summary>
    /// 蓝图中的单个建筑记录。字段语义见 docs/PRD.md 4.1。
    /// 序列化时必须携带 sourceGameBuild，以便诊断跨版本兼容问题。
    /// </summary>
    public sealed class BuildingRecord
    {
        /// <summary>蓝图内唯一标识，用于表达建筑之间的引用关系，不得重复。</summary>
        public string RecordId { get; set; }

        /// <summary>ONI 建筑 Prefab ID。必须是经适配层校验过的真实 ID，不允许猜测。</summary>
        public string PrefabId { get; set; }

        /// <summary>相对于蓝图原点的局部栅格坐标（建筑锚点格）。</summary>
        public Point2i LocalPosition { get; set; }

        public BuildingOrientation Orientation { get; set; }

        public LayerKind Layer { get; set; }

        /// <summary>建造材料 Tag。空字符串表示"使用游戏默认材料"。</summary>
        public string MaterialTag { get; set; }

        /// <summary>建筑占位形状（多格）。</summary>
        public Footprint Footprint { get; set; }

        /// <summary>
        /// 可复制的建筑设置。键为设置类型名，值为序列化文本。
        /// 未知键必须原样保留（opaqueSettings 语义），不得静默丢弃。
        /// </summary>
        public IDictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        /// <summary>该建筑在世界坐标下占据的矩形。</summary>
        public Rect2i WorldRect() => Footprint.ToWorldRect(LocalPosition);

        public override string ToString() =>
            (RecordId ?? "?") + ":" + (PrefabId ?? "?") + "@" + LocalPosition;
    }
}
