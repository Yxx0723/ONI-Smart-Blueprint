using System;
using System.Collections.Generic;
using System.Linq;
using OSB.Domain;
using UnityEngine;

namespace OSB.GameAdapter
{
    /// <summary>
    /// 建筑材料可用性读取（只读审计，P1 不参与任何决策）。
    ///
    /// 已核实接口（见 docs/API-Audit.md M-001）：
    ///   <c>ElementLoader.FindElementByTag(Tag)</c>   → Element 或 null
    ///   <c>ElementLoader.FindElementByName(string)</c> → Element 或 null
    ///   <c>ElementLoader.GetElement(Tag)</c>         → Element 或 null（字典直查，不做规范化）
    ///   <c>ElementLoader.FindElements(Func&lt;Element,bool&gt;)</c> → List&lt;Element&gt;
    ///   <c>BuildingDef.DefaultElements()</c> / <c>BuildingDef.MaterialCategory</c>
    ///
    /// 调用限制：
    ///   1. <c>ElementLoader.GetElement</c> 依赖元素表，元素表在 <c>Assets</c> 初始化期间建立，
    ///      因此必须在 <c>Assets.CreatePrefabs()</c> 之后调用。
    ///   2. <c>BuildingDef.DefaultElements()</c> 会遍历 <c>MaterialCategory</c> 映射到具体元素，
    ///      某个建筑材料大类配置异常时会抛异常，调用方必须逐个 try/catch。
    ///   3. "材料够不够"**不是**本类的职责。本类只回答"这个材料 Tag 在游戏里是否真实存在"，
    ///      实际库存判定属于放置期逻辑，依赖具体殖民地，P1 不做。
    /// </summary>
    internal static class MaterialAccessor
    {
        internal static void Audit()
        {
            OsbLog.Info("=== 材料接口审计 ===");

            try
            {
                var probes = new[]
                {
                    new Tag("SandStone"),
                    new Tag("Granite"),
                    new Tag("Copper"),
                    new Tag("RefinedMetal"),
                    new Tag("OSB_ThisTagDoesNotExist")
                };

                foreach (Tag tag in probes)
                {
                    try
                    {
                        Element element = ElementLoader.FindElementByTag(tag);
                        if (element == null)
                        {
                            OsbLog.Info("材料 " + tag.Name + " → <不存在>（GetElement 返回 null，不是异常）");
                            continue;
                        }

                        OsbLog.Info("材料 " + tag.Name + " → id=" + element.id
                            + " tag=" + element.tag.Name
                            + " state=" + element.state);
                    }
                    catch (Exception e)
                    {
                        OsbLog.Warn("材料 " + tag.Name + " 查询失败：" + e.GetType().Name + ": " + e.Message);
                    }
                }

                ReportMaterialCoverage();
            }
            catch (Exception e)
            {
                OsbLog.Warn("材料审计失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>
        /// 统计已采集建筑的默认材料构成。
        ///
        /// **注意语义**：`DefaultMaterialTags` 里既有真正的元素（SandStone、Copper），
        /// 也有制成品/虚拟材料（BasicFabric、PlasticGasket、RefinedMetal）。
        /// 后者查 `ElementLoader` 必然返回 null，那是**正常**的，不是数据错误。
        /// P1 实测：424 个建筑里有 19 个的主材料属于这一类。
        /// </summary>
        private static void ReportMaterialCoverage()
        {
            BuildingGeometryTable table = GameDatabaseReady.Geometry;
            if (table == null)
            {
                OsbLog.Warn("建筑几何库尚未采集，跳过材料覆盖统计");
                return;
            }

            int buildingsWithMaterials = 0;
            int elementBacked = 0;
            int fabricated = 0;
            var fabricatedSamples = new List<string>();

            foreach (BuildingGeometry geometry in table.All)
            {
                IReadOnlyList<string> tags = geometry.DefaultMaterialTags;
                if (tags.Count == 0)
                {
                    continue;
                }

                buildingsWithMaterials++;

                try
                {
                    if (ElementLoader.GetElement(new Tag(tags[0])) != null)
                    {
                        elementBacked++;
                    }
                    else
                    {
                        fabricated++;
                        if (fabricatedSamples.Count < 10)
                        {
                            fabricatedSamples.Add(geometry.PrefabId + ":" + tags[0]);
                        }
                    }
                }
                catch (Exception)
                {
                    fabricated++;
                    if (fabricatedSamples.Count < 10)
                    {
                        fabricatedSamples.Add(geometry.PrefabId + ":" + tags[0]);
                    }
                }
            }

            OsbLog.Info("材料构成：有默认材料的建筑=" + buildingsWithMaterials
                + "，主材料为元素=" + elementBacked
                + "，主材料为制成品/虚拟材料=" + fabricated + "（后者查元素表为 null 属正常）");

            if (fabricatedSamples.Count > 0)
            {
                OsbLog.Info("制成品材料样本：" + string.Join(", ", fabricatedSamples.ToArray()));
            }
        }
    }
}
