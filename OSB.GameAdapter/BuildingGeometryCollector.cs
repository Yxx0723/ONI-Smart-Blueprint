using System;
using System.Collections.Generic;
using OSB.Domain;
using UnityEngine;

namespace OSB.GameAdapter
{
    /// <summary>
    /// 把游戏 <c>BuildingDef</c> 转成领域层 <see cref="BuildingGeometry"/>。
    /// 纯读取，不创建、不修改、不放置任何东西。
    ///
    /// 对每个建筑单独 try/catch：单个建筑的异常（例如某个 Mod 建筑缺材料定义）
    /// 不允许拖垮整批采集。
    /// </summary>
    internal static class BuildingGeometryCollector
    {
        internal static BuildingGeometryTable Collect()
        {
            var table = new BuildingGeometryTable();

            List<BuildingDef> defs = Assets.BuildingDefs;
            if (defs == null)
            {
                OsbLog.Warn("采集中止：Assets.BuildingDefs 为 null");
                return table;
            }

            int skippedNull = 0;
            int failed = 0;

            for (int i = 0; i < defs.Count; i++)
            {
                BuildingDef def = defs[i];
                if (def == null)
                {
                    skippedNull++;
                    continue;
                }

                try
                {
                    table.TryAdd(Convert(def));
                }
                catch (Exception e)
                {
                    failed++;
                    OsbLog.Warn("建筑转换失败 [" + SafePrefabId(def) + "]：" + e.GetType().Name + ": " + e.Message);
                }
            }

            if (skippedNull > 0)
            {
                OsbLog.Warn("跳过 null 建筑定义 " + skippedNull + " 个");
            }

            if (failed > 0)
            {
                OsbLog.Warn("转换失败 " + failed + " 个建筑");
            }

            return table;
        }

        private static BuildingGeometry Convert(BuildingDef def)
        {
            string prefabId = def.PrefabID;

            var offsets = new List<Point2i>();
            if (def.PlacementOffsets != null)
            {
                offsets.Capacity = def.PlacementOffsets.Length;
                for (int i = 0; i < def.PlacementOffsets.Length; i++)
                {
                    CellOffset offset = def.PlacementOffsets[i];
                    offsets.Add(new Point2i(offset.x, offset.y));
                }
            }

            string displayName;
            try
            {
                displayName = def.Name;
            }
            catch (Exception)
            {
                // 本地化未就绪或词条缺失时回退，不视为失败
                displayName = prefabId;
            }

            if (string.IsNullOrEmpty(displayName))
            {
                displayName = prefabId;
            }

            var defaultMaterials = new List<string>();
            try
            {
                List<Tag> elements = def.DefaultElements();
                if (elements != null)
                {
                    for (int i = 0; i < elements.Count; i++)
                    {
                        defaultMaterials.Add(elements[i].Name);
                    }
                }
            }
            catch (Exception)
            {
                // 材料大类未映射到元素时 DefaultElements 可能抛异常，允许为空
            }

            return new BuildingGeometry(
                prefabId,
                displayName,
                def.WidthInCells,
                def.HeightInCells,
                offsets,
                PermittedRotationInfo.FromGameValue((int)def.PermittedRotations),
                def.ObjectLayer.ToString(),
                def.SceneLayer.ToString(),
                def.BuildLocationRule.ToString(),
                def.MaterialCategory ?? Array.Empty<string>(),
                defaultMaterials,
                def.Deprecated);
        }

        private static string SafePrefabId(BuildingDef def)
        {
            try
            {
                return def.PrefabID;
            }
            catch (Exception)
            {
                return "<unknown>";
            }
        }
    }
}
