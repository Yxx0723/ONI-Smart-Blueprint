using System;
using System.Collections.Generic;

namespace OSB.Domain
{
    /// <summary>
    /// 建筑几何库：PrefabId → <see cref="BuildingGeometry"/>。
    /// 采集完成后应 <see cref="Seal"/>，之后任何写入都会抛异常，
    /// 避免运行期被意外改动导致"同一建筑两种几何"。
    /// </summary>
    public sealed class BuildingGeometryTable
    {
        private readonly Dictionary<string, BuildingGeometry> _byPrefabId =
            new Dictionary<string, BuildingGeometry>(StringComparer.Ordinal);

        private readonly List<GeometryAnomaly> _anomalies = new List<GeometryAnomaly>();

        private bool _sealed;

        public int Count => _byPrefabId.Count;

        public bool IsSealed => _sealed;

        public IReadOnlyList<GeometryAnomaly> Anomalies => _anomalies;

        /// <summary>
        /// 加入一条几何记录。重复 PrefabId 不会被覆盖，而是记一条 GEO007 异常并返回 false。
        /// </summary>
        public bool TryAdd(BuildingGeometry geometry)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (_sealed)
            {
                throw new InvalidOperationException("几何库已封存，不能再写入。");
            }

            if (_byPrefabId.TryGetValue(geometry.PrefabId, out BuildingGeometry existing))
            {
                _anomalies.Add(new GeometryAnomaly(
                    "GEO007",
                    geometry.PrefabId,
                    "重复 PrefabId，已保留先加入的记录（" + existing.Width + "x" + existing.Height + "），忽略本条"));
                return false;
            }

            _byPrefabId.Add(geometry.PrefabId, geometry);

            foreach (GeometryAnomaly anomaly in geometry.Validate())
            {
                _anomalies.Add(anomaly);
            }

            return true;
        }

        public bool TryGet(string prefabId, out BuildingGeometry geometry)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                geometry = null;
                return false;
            }

            return _byPrefabId.TryGetValue(prefabId, out geometry);
        }

        public BuildingGeometry Get(string prefabId)
        {
            if (!TryGet(prefabId, out BuildingGeometry geometry))
            {
                throw new KeyNotFoundException("几何库中没有建筑：" + prefabId);
            }

            return geometry;
        }

        public bool Contains(string prefabId) =>
            !string.IsNullOrEmpty(prefabId) && _byPrefabId.ContainsKey(prefabId);

        /// <summary>全部记录，顺序为插入顺序（便于日志稳定复现）。</summary>
        public IReadOnlyCollection<BuildingGeometry> All => _byPrefabId.Values;

        /// <summary>封存后不再允许写入。重复封存是幂等的。</summary>
        public void Seal() => _sealed = true;
    }
}
