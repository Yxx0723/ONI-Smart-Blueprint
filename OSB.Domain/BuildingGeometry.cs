using System;
using System.Collections.Generic;

namespace OSB.Domain
{
    /// <summary>
    /// 单个建筑的几何与分类数据。由适配层从游戏 <c>BuildingDef</c> 采集后构造，
    /// 领域层后续所有布局、校验、序列化都基于它，不再接触游戏类型。
    ///
    /// 不可变：构造后所有集合都是防御性拷贝，避免被外部改坏。
    /// </summary>
    public sealed class BuildingGeometry
    {
        private readonly Point2i[] _placementOffsets;
        private readonly string[] _materialCategories;
        private readonly string[] _defaultMaterialTags;
        private readonly BuildingOrientation[] _validOrientations;

        public BuildingGeometry(
            string prefabId,
            string displayName,
            int width,
            int height,
            IReadOnlyList<Point2i> placementOffsets,
            PermittedRotation permittedRotations,
            string objectLayerName,
            string sceneLayerName,
            string buildLocationRuleName,
            IReadOnlyList<string> materialCategories,
            IReadOnlyList<string> defaultMaterialTags,
            bool deprecated)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                throw new ArgumentException("PrefabId 不能为空。", nameof(prefabId));
            }

            PrefabId = prefabId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? prefabId : displayName;
            Width = width;
            Height = height;
            PermittedRotations = permittedRotations;
            ObjectLayerName = objectLayerName ?? string.Empty;
            SceneLayerName = sceneLayerName ?? string.Empty;
            BuildLocationRuleName = buildLocationRuleName ?? string.Empty;
            Deprecated = deprecated;

            _placementOffsets = Copy(placementOffsets);
            _materialCategories = Copy(materialCategories);
            _defaultMaterialTags = Copy(defaultMaterialTags);
            _validOrientations = CopyPermitted(permittedRotations);
        }

        /// <summary>ONI 建筑 Prefab ID，例如 "Tile"、"ManualGenerator"。</summary>
        public string PrefabId { get; }

        /// <summary>游戏内显示名（已本地化）。可能为空字符串，此时回退为 PrefabId。</summary>
        public string DisplayName { get; }

        public int Width { get; }
        public int Height { get; }

        /// <summary>相对建筑锚点格的占位偏移序列。顺序与游戏一致，未去重。</summary>
        public IReadOnlyList<Point2i> PlacementOffsets => _placementOffsets;

        public PermittedRotation PermittedRotations { get; }

        public string ObjectLayerName { get; }
        public string SceneLayerName { get; }
        public string BuildLocationRuleName { get; }

        /// <summary>建造材料大类，例如 "Buildable"、"Metal"。</summary>
        public IReadOnlyList<string> MaterialCategories => _materialCategories;

        /// <summary>游戏给出的默认材料 Tag 列表，索引 0 为主材料。</summary>
        public IReadOnlyList<string> DefaultMaterialTags => _defaultMaterialTags;

        /// <summary>游戏标记为弃用的建筑。采集保留，但不应出现在可建造列表中。</summary>
        public bool Deprecated { get; }

        public bool IsRotatable => PermittedRotations != PermittedRotation.Unrotatable;

        /// <summary>该建筑的全部合法朝向，不含 <see cref="BuildingOrientation.NumRotations"/>。</summary>
        public IReadOnlyList<BuildingOrientation> ValidOrientations => _validOrientations;

        public int CellCount => Width > 0 && Height > 0 ? Width * Height : 0;

        /// <summary>指定朝向下的占位偏移。</summary>
        public IReadOnlyList<Point2i> OffsetsFor(BuildingOrientation orientation)
        {
            EnsureValidOrientation(orientation);
            return GeometryOrientation.RotateAll(_placementOffsets, orientation);
        }

        /// <summary>指定朝向下占位的最小包围盒（相对锚点）。</summary>
        public Rect2i BoundsFor(BuildingOrientation orientation)
        {
            EnsureValidOrientation(orientation);
            return GeometryOrientation.BoundsOf(_placementOffsets, orientation);
        }

        /// <summary>
        /// 指定朝向下，把锚点格换算成全部绝对占用格。
        /// 只做数学加法，**不做世界边界判定** —— 越界判定属于适配层职责。
        /// </summary>
        public IReadOnlyList<Point2i> AbsoluteCells(Point2i anchorCell, BuildingOrientation orientation)
        {
            EnsureValidOrientation(orientation);
            var result = new Point2i[_placementOffsets.Length];
            for (int i = 0; i < _placementOffsets.Length; i++)
            {
                result[i] = anchorCell + GeometryOrientation.Rotate(_placementOffsets[i], orientation);
            }

            return result;
        }

        /// <summary>朝向是否属于该建筑的合法集合。</summary>
        public bool IsValidOrientation(BuildingOrientation orientation) =>
            PermittedRotationInfo.IsValid(PermittedRotations, orientation);

        private void EnsureValidOrientation(BuildingOrientation orientation)
        {
            if (GeometryOrientation.IsSentinel(orientation))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(orientation),
                    orientation,
                    "NumRotations 是哨兵值，不能作为朝向使用（建筑 " + PrefabId + "）。");
            }

            if (!IsValidOrientation(orientation))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(orientation),
                    orientation,
                    "建筑 " + PrefabId + " 的 PermittedRotations=" + PermittedRotations + " 不允许朝向 " + orientation + "。");
            }
        }

        /// <summary>对该建筑做几何一致性检查，返回异常项（空表示通过）。</summary>
        public IEnumerable<GeometryAnomaly> Validate()
        {
            if (Width <= 0 || Height <= 0)
            {
                yield return new GeometryAnomaly("GEO004", PrefabId, "宽或高非正：" + Width + "x" + Height);
                yield break;
            }

            if (_placementOffsets.Length == 0)
            {
                yield return new GeometryAnomaly("GEO001", PrefabId, "PlacementOffsets 为空");
                yield break;
            }

            if (_placementOffsets.Length != CellCount)
            {
                yield return new GeometryAnomaly(
                    "GEO002",
                    PrefabId,
                    "占位格数 " + _placementOffsets.Length + " 与 宽×高 " + CellCount + " 不一致");
            }

            if (!GeometryOrientation.FitsInSize(_placementOffsets, BuildingOrientation.Neutral, Width, Height))
            {
                yield return new GeometryAnomaly(
                    "GEO003",
                    PrefabId,
                    "Neutral 朝向占位超出声明尺寸 " + Width + "x" + Height);
            }

            // 旋转 90°/270° 后，占位包围盒应为 H×W（长宽互换）。
            // 注意：**不能用"原始偏移是否落在 [0,H)×[0,W) 内"来判定** ——
            // 旋转会让偏移出现负值（(1,0) 在 R90 下变 (0,-1)），那是正常行为，
            // 游戏在落位时会重新锚定。只有包围盒的**尺寸**才是平移不变的判据。
            if (IsRotatable && (PermittedRotations == PermittedRotation.R90 || PermittedRotations == PermittedRotation.R360))
            {
                Rect2i rotated = GeometryOrientation.BoundsOf(_placementOffsets, BuildingOrientation.R90);
                if (rotated.Width != Height || rotated.Height != Width)
                {
                    yield return new GeometryAnomaly(
                        "GEO005",
                        PrefabId,
                        "R90 后占位包围盒 " + rotated.Width + "x" + rotated.Height
                        + " 与预期 " + Height + "x" + Width + " 不符");
                }
            }
        }

        public override string ToString() =>
            PrefabId + "(" + DisplayName + ") " + Width + "x" + Height
            + " offsets=" + _placementOffsets.Length + " rot=" + PermittedRotations;

        private static Point2i[] Copy(IReadOnlyList<Point2i> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<Point2i>();
            }

            var copy = new Point2i[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i] ?? string.Empty;
            }

            return copy;
        }

        private static BuildingOrientation[] CopyPermitted(PermittedRotation permitted)
        {
            IReadOnlyList<BuildingOrientation> source = PermittedRotationInfo.ValidOrientations(permitted);
            var copy = new BuildingOrientation[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }

    /// <summary>几何数据异常。Code 稳定可断言，Message 面向开发者。</summary>
    public sealed class GeometryAnomaly
    {
        public GeometryAnomaly(string code, string prefabId, string message)
        {
            Code = code;
            PrefabId = prefabId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string PrefabId { get; }
        public string Message { get; }

        public override string ToString() => Code + " [" + PrefabId + "] " + Message;
    }
}
