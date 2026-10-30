using System;
using System.Collections.Generic;

namespace OSB.Domain
{
    /// <summary>
    /// 蓝图根对象。
    ///
    /// ## 坐标空间
    ///
    /// ```
    /// 世界格      WorldOrigin + record.LocalPosition + rotate(offset, orientation)
    /// 蓝图局部格  record.LocalPosition + rotate(offset, orientation)
    /// ```
    ///
    /// <see cref="WorldOrigin"/> 记录捕获时对齐的世界格，蓝图内部一切运算只用局部坐标。
    /// 局部坐标的归一化以**真实占用格**的最小角为准（不是锚点），见 <see cref="Normalize"/>。
    ///
    /// ## 校验码
    ///
    /// | 码 | 级别 | 含义 |
    /// |---|---|---|
    /// | OSB001 | Blocker | 蓝图缺少 Id |
    /// | OSB002 | Blocker | SchemaVersion 非法 |
    /// | OSB003 | Blocker | SchemaVersion 高于本 MOD 支持 |
    /// | OSB004 | Blocker | 蓝图不含任何建筑 |
    /// | OSB005 | Blocker | 存在 null 记录 |
    /// | OSB006 | Blocker | 记录缺 RecordId |
    /// | OSB007 | Blocker | RecordId 重复 |
    /// | OSB008 | Blocker | 记录缺 PrefabId |
    /// | OSB009 | Blocker | 记录缺 PlacementOffsets |
    /// | OSB010 | Blocker | 占用格出现负坐标（未归一化） |
    /// | OSB011 | Blocker | 同图层真实占用格重叠 |
    /// | OSB012 | Warning | 声明尺寸与实际占位不一致 |
    /// | OSB013 | Blocker | 声明尺寸非正 |
    /// | OSB014 | Blocker | Orientation 为哨兵值 |
    /// </summary>
    public sealed class Blueprint
    {
        /// <summary>
        /// 数据格式版本。P2-A 由 1 提升到 2：
        /// 记录结构从"声明尺寸 Footprint"改为"原始 PlacementOffsets + 声明尺寸 + 锚点"，
        /// 属破坏性变更，旧版本数据不可直接按 v2 语义解释。
        /// </summary>
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }

        /// <summary>捕获时对齐的世界格原点。蓝图内部不含世界坐标。</summary>
        public Point2i WorldOrigin { get; set; }

        public IList<BuildingRecord> Buildings { get; set; } = new List<BuildingRecord>();

        /// <summary>捕获该蓝图时的游戏 Build 号，用于诊断兼容性。</summary>
        public int SourceGameBuild { get; set; }

        /// <summary>捕获时的 DLC 集合，例如 "ALL" 或 "EXPANSION1_ID"。</summary>
        public string SourceDlcSet { get; set; }

        /// <summary>世界格 → 蓝图局部格。</summary>
        public static Point2i WorldToLocal(Point2i worldCell, Point2i worldOrigin) =>
            worldCell - worldOrigin;

        /// <summary>蓝图局部格 → 世界格。</summary>
        public static Point2i LocalToWorld(Point2i localCell, Point2i worldOrigin) =>
            localCell + worldOrigin;

        /// <summary>
        /// 全部建筑真实占用格的包围盒（蓝图局部坐标）。
        /// 基于占用格而非锚点，因此锚点靠左时会自然包含负方向。
        /// </summary>
        public Rect2i LocalBounds()
        {
            Rect2i bounds = Rect2i.Empty;
            foreach (BuildingRecord record in Buildings)
            {
                if (record == null || record.OffsetCount == 0)
                {
                    continue;
                }

                foreach (Point2i cell in record.OccupiedCells())
                {
                    bounds = bounds.Union(new Rect2i(cell.X, cell.Y, 1, 1));
                }
            }

            return bounds;
        }

        /// <summary>
        /// 结构化校验。
        ///
        /// 重叠判定依据**真实占用格集合**，不依据包围盒：
        /// 包围盒相交但实际格不重叠是合法情况（例如 L 形布局），
        /// 声明 1x2 实占 1x1 的建筑也不应虚占一格。
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();

            if (string.IsNullOrWhiteSpace(Id))
            {
                issues.Add(new ValidationIssue("OSB001", IssueSeverity.Blocker, "蓝图缺少 Id。", WorldOrigin));
            }

            if (SchemaVersion <= 0)
            {
                issues.Add(new ValidationIssue("OSB002", IssueSeverity.Blocker, "SchemaVersion 非法。", WorldOrigin));
            }
            else if (SchemaVersion > CurrentSchemaVersion)
            {
                issues.Add(new ValidationIssue(
                    "OSB003",
                    IssueSeverity.Blocker,
                    "蓝图版本 " + SchemaVersion + " 高于本 MOD 支持的 " + CurrentSchemaVersion + "，只能只读打开。",
                    WorldOrigin));
            }

            if (Buildings == null || Buildings.Count == 0)
            {
                issues.Add(new ValidationIssue("OSB004", IssueSeverity.Blocker, "蓝图不含任何建筑。", WorldOrigin));
                return issues;
            }

            var seenRecordIds = new HashSet<string>(StringComparer.Ordinal);
            var validRecords = new List<BuildingRecord>();

            foreach (BuildingRecord record in Buildings)
            {
                if (record == null)
                {
                    issues.Add(new ValidationIssue("OSB005", IssueSeverity.Blocker, "存在空的建筑记录。", WorldOrigin));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(record.RecordId) && !seenRecordIds.Add(record.RecordId))
                {
                    issues.Add(new ValidationIssue(
                        "OSB007",
                        IssueSeverity.Blocker,
                        "RecordId 重复：" + record.RecordId,
                        record.LocalPosition));
                }

                foreach (ValidationIssue issue in record.Validate())
                {
                    issues.Add(issue);
                }

                // 只有几何可安全求值的记录才参与重叠检测：
                // 缺偏移或朝向为哨兵值时无法计算占用格，强行参与会抛异常打断整份校验。
                if (record.OffsetCount > 0 && !GeometryOrientation.IsSentinel(record.Orientation))
                {
                    validRecords.Add(record);
                }
            }

            foreach (ValidationIssue overlap in FindOccupiedCellOverlaps(validRecords))
            {
                issues.Add(overlap);
            }

            return issues;
        }

        /// <summary>
        /// 同图层真实占用格重叠检测。
        /// 不同图层允许叠放（电线穿过建筑格），故按图层分组。
        /// </summary>
        private static IEnumerable<ValidationIssue> FindOccupiedCellOverlaps(List<BuildingRecord> records)
        {
            var occupancy = new Dictionary<LayerKind, Dictionary<Point2i, string>>();

            foreach (BuildingRecord record in records)
            {
                if (!occupancy.TryGetValue(record.Layer, out Dictionary<Point2i, string> cells))
                {
                    cells = new Dictionary<Point2i, string>();
                    occupancy.Add(record.Layer, cells);
                }

                foreach (Point2i cell in record.OccupiedCells())
                {
                    if (cells.TryGetValue(cell, out string otherId))
                    {
                        string a = string.IsNullOrEmpty(otherId) ? "<无 RecordId>" : otherId;
                        string b = string.IsNullOrEmpty(record.RecordId) ? "<无 RecordId>" : record.RecordId;
                        yield return new ValidationIssue(
                            "OSB011",
                            IssueSeverity.Blocker,
                            "同图层占用格重叠：" + a + " 与 " + b,
                            cell);
                        continue;
                    }

                    cells.Add(cell, record.RecordId);
                }
            }
        }

        /// <summary>
        /// 归一化：整体平移，使**真实占用格**的最小角落在原点。
        ///
        /// 平移对象是锚点 <see cref="BuildingRecord.LocalPosition"/>，
        /// 但基准取自占用格最小值——3 宽建筑锚定在 (0,0) 时占 x=-1..1，
        /// 需整体 +1 才能让最左格落到 0。只按锚点归一会漏掉这一格。
        /// </summary>
        public void Normalize()
        {
            Rect2i bounds = LocalBounds();
            if (bounds.IsEmpty)
            {
                return;
            }

            int dx = bounds.MinX;
            int dy = bounds.MinY;
            if (dx == 0 && dy == 0)
            {
                return;
            }

            foreach (BuildingRecord record in Buildings)
            {
                if (record == null)
                {
                    continue;
                }

                record.LocalPosition = new Point2i(
                    record.LocalPosition.X - dx,
                    record.LocalPosition.Y - dy);
            }
        }

        /// <summary>
        /// 按真实占用格汇总每格被哪些记录占用。供重叠检测与预览使用。
        /// </summary>
        public IReadOnlyDictionary<Point2i, IReadOnlyList<string>> OccupancyMap()
        {
            var result = new Dictionary<Point2i, List<string>>();

            foreach (BuildingRecord record in Buildings)
            {
                if (record == null || record.OffsetCount == 0)
                {
                    continue;
                }

                foreach (Point2i cell in record.OccupiedCells())
                {
                    if (!result.TryGetValue(cell, out List<string> owners))
                    {
                        owners = new List<string>();
                        result.Add(cell, owners);
                    }

                    owners.Add(record.RecordId ?? string.Empty);
                }
            }

            var frozen = new Dictionary<Point2i, IReadOnlyList<string>>();
            foreach (KeyValuePair<Point2i, List<string>> pair in result)
            {
                frozen.Add(pair.Key, pair.Value);
            }

            return frozen;
        }
    }
}
