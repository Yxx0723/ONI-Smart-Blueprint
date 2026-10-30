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
        /// 统计已采集建筑的材料覆盖情况：有多少建筑的默认材料在元素表里查不到。
        /// 这是"蓝图能否落地"的前置健康度指标。
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
            int missing = 0;
            var missingSamples = new List<string>();

            foreach (BuildingGeometry geometry in table.All)
            {
                IReadOnlyList<string> tags = geometry.DefaultMaterialTags;
                if (tags.Count == 0)
                {
                    continue;
                }

                buildingsWithMaterials++;

                foreach (string tagName in tags)
                {
                    try
                    {
                        if (ElementLoader.GetElement(new Tag(tagName)) == null)
                        {
                            missing++;
                            if (missingSamples.Count < 10)
                            {
                                missingSamples.Add(geometry.PrefabId + ":" + tagName);
                            }

                            break;
                        }
                    }
                    catch (Exception)
                    {
                        missing++;
                        break;
                    }
                }
            }

            OsbLog.Info("材料覆盖：有默认材料的建筑=" + buildingsWithMaterials
                + "，其中含无法解析材料的=" + missing);

            if (missingSamples.Count > 0)
            {
                OsbLog.Info("无法解析材料样本：" + string.Join(", ", missingSamples.ToArray()));
            }
        }
    }
}
