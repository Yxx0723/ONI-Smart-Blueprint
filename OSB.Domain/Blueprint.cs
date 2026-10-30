using System;
using System.Collections.Generic;
using System.Linq;

namespace OSB.Domain
{
    /// <summary>
    /// 蓝图根对象。Schema 版本化：任何破坏性字段变更都必须提升 SchemaVersion
    /// 并提供显式迁移，禁止在同一版本内改变语义。
    /// </summary>
    public sealed class Blueprint
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }

        /// <summary>蓝图局部原点（通常为 (0,0)，由捕获时归一化得到）。</summary>
        public Point2i Origin { get; set; }

        public IList<BuildingRecord> Buildings { get; set; } = new List<BuildingRecord>();

        /// <summary>捕获该蓝图时的游戏 Build 号，用于诊断兼容性。</summary>
        public int SourceGameBuild { get; set; }

        /// <summary>捕获时的 DLC 集合，例如 "ALL" 或 "EXPANSION1_ID"。</summary>
        public string SourceDlcSet { get; set; }

        /// <summary>全部建筑的局部包围盒；为空时返回 Empty。</summary>
        public Rect2i LocalBounds()
        {
            Rect2i bounds = Rect2i.Empty;
            foreach (BuildingRecord record in Buildings)
            {
                if (record == null)
                {
                    continue;
                }

                bounds = bounds.Union(record.WorldRect());
            }

            return bounds;
        }

        /// <summary>
        /// 结构化校验。返回的问题集不含玩家策略（材料是否够、科技是否解锁），
        /// 那些属于 GameAdapter 的放置期校验。
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();

            if (string.IsNullOrWhiteSpace(Id))
            {
                issues.Add(new ValidationIssue("OSB001", IssueSeverity.Blocker, "蓝图缺少 Id。", Origin));
            }

            if (SchemaVersion <= 0)
            {
                issues.Add(new ValidationIssue("OSB002", IssueSeverity.Blocker, "SchemaVersion 非法。", Origin));
            }
            else if (SchemaVersion > CurrentSchemaVersion)
            {
                issues.Add(new ValidationIssue(
                    "OSB003",
                    IssueSeverity.Blocker,
                    "蓝图版本高于本 MOD 支持的版本，只能只读打开。",
                    Origin));
            }

            if (Buildings == null || Buildings.Count == 0)
            {
                issues.Add(new ValidationIssue("OSB004", IssueSeverity.Blocker, "蓝图不含任何建筑。", Origin));
                return issues;
            }

            var seenRecordIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (BuildingRecord record in Buildings)
            {
                if (record == null)
                {
                    issues.Add(new ValidationIssue("OSB005", IssueSeverity.Blocker, "存在空的建筑记录。", Origin));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(record.RecordId))
                {
                    issues.Add(new ValidationIssue(
                        "OSB006",
                        IssueSeverity.Blocker,
                        "建筑记录缺少 RecordId。",
                        record.LocalPosition));
                }
                else if (!seenRecordIds.Add(record.RecordId))
                {
                    issues.Add(new ValidationIssue(
                        "OSB007",
                        IssueSeverity.Blocker,
                        "RecordId 重复：" + record.RecordId,
                        record.LocalPosition));
                }

                if (string.IsNullOrWhiteSpace(record.PrefabId))
                {
                    issues.Add(new ValidationIssue(
                        "OSB008",
                        IssueSeverity.Blocker,
                        "建筑记录缺少 PrefabId。",
                        record.LocalPosition));
                }

                if (!record.Footprint.IsValid)
                {
                    issues.Add(new ValidationIssue(
                        "OSB009",
                        IssueSeverity.Blocker,
                        "建筑 footprint 尺寸非法：" + record.Footprint,
                        record.LocalPosition));
                }

                if (record.LocalPosition.X < 0 || record.LocalPosition.Y < 0)
                {
                    issues.Add(new ValidationIssue(
                        "OSB010",
                        IssueSeverity.Blocker,
                        "建筑局部坐标出现负值，蓝图未正确归一化到原点。",
                        record.LocalPosition));
                }
            }

            issues.AddRange(FindLayerOverlaps());

            return issues;
        }

        /// <summary>同图层 footprint 重叠检测。不同图层允许叠放（如电线穿过建筑格）。</summary>
        private IEnumerable<ValidationIssue> FindLayerOverlaps()
        {
            var valid = Buildings
                .Where(r => r != null && r.Footprint.IsValid)
                .ToList();

            for (int i = 0; i < valid.Count; i++)
            {
                for (int j = i + 1; j < valid.Count; j++)
                {
                    BuildingRecord a = valid[i];
                    BuildingRecord b = valid[j];

                    if (a.Layer != b.Layer)
                    {
                        continue;
                    }

                    Rect2i overlap = a.WorldRect().Intersect(b.WorldRect());
                    if (overlap.IsEmpty)
                    {
                        continue;
                    }

                    yield return new ValidationIssue(
                        "OSB011",
                        IssueSeverity.Blocker,
                        "同图层建筑重叠：" + a.RecordId + " 与 " + b.RecordId,
                        new Point2i(overlap.MinX, overlap.MinY));
                }
            }
        }

        /// <summary>
        /// 归一化：整体平移使包围盒左下角落在原点。坐标本身是整数，平移不损失精度。
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

                record.LocalPosition = new Point2i(record.LocalPosition.X - dx, record.LocalPosition.Y - dy);
            }
        }
    }
}
