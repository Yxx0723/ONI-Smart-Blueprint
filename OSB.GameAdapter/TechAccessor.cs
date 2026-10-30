using System;
using System.Collections.Generic;
using OSB.Domain;

namespace OSB.GameAdapter
{
    /// <summary>
    /// 科技解锁状态读取（只读审计，P1 不参与任何决策）。
    ///
    /// 已核实接口（见 docs/API-Audit.md T-001）：
    ///   <c>Database.Techs.TryGetTechForTechItem(string itemId)</c> → <c>Tech</c> 或 null
    ///   <c>Database.Techs.IsTechItemComplete(string id)</c>       → bool
    ///   <c>Tech.IsComplete()</c>                                  → bool
    ///   <c>Tech.unlockedItemIDs</c>                                → List&lt;string&gt;
    ///
    /// **三条调用限制，任何一条踩了都会得到错误但看起来正常的答案：**
    ///   1. 必须在 <c>Db.Get()</c> 之后调用。Db 在 <c>Assets.CreatePrefabs()</c> 里初始化，
    ///      因此安全时机是进入世界之后。
    ///   2. <c>Tech.IsComplete()</c> 内部依赖 <c>Research.Instance</c>；**未进入殖民地时该实例为 null，
    ///      方法返回 false**（fail-closed）。也就是说在主菜单查科技，所有科技都会显示"未解锁"，
    ///      这不是数据错误，而是上下文不成立。
    ///   3. <c>IsTechItemComplete(id)</c> 在**找不到该 item 时返回 true**（fail-open）。
    ///      判"已解锁"必须在确认 item 存在的前提下进行，否则不存在的东西会冒充已解锁。
    /// </summary>
    internal static class TechAccessor
    {
        internal static void Audit()
        {
            OsbLog.Info("=== 科技接口审计 ===");

            try
            {
                if (Research.Instance == null)
                {
                    OsbLog.Warn("Research.Instance 为 null（当前不在殖民地上下文），"
                        + "Tech.IsComplete 会一律返回 false —— 这是 fail-closed，不是数据错误");
                }

                Database.Techs techs = Db.Get().Techs;
                if (techs == null)
                {
                    OsbLog.Warn("Db.Get().Techs 为 null，审计中止");
                    return;
                }

                OsbLog.Info("科技总数=" + techs.Count);

                // 样本：确认"查所属科技"与"查是否解锁"两条路径都能走通
                string[] samples = { "Tile", "Door", "ManualGenerator", "Ladder", "Battery" };
                foreach (string itemId in samples)
                {
                    try
                    {
                        Tech tech = techs.TryGetTechForTechItem(itemId);
                        bool complete = techs.IsTechItemComplete(itemId);

                        OsbLog.Info("科技样本 " + itemId
                            + " → tech=" + (tech == null ? "<未归属任何科技>" : tech.Id)
                            + " IsComplete=" + complete);
                    }
                    catch (Exception e)
                    {
                        OsbLog.Warn("科技样本 " + itemId + " 查询失败：" + e.GetType().Name + ": " + e.Message);
                    }
                }

                // 反例：不存在的 item，用于印证 fail-open 行为
                try
                {
                    bool phantom = techs.IsTechItemComplete("OSB_ThisItemDoesNotExist");
                    OsbLog.Info("不存在的 item 调用 IsTechItemComplete → " + phantom
                        + (phantom ? "（印证 fail-open：不存在即视为已解锁，调用方必须自己判存在性）" : string.Empty));
                }
                catch (Exception e)
                {
                    OsbLog.Warn("fail-open 验证抛异常：" + e.GetType().Name + ": " + e.Message);
                }
            }
            catch (Exception e)
            {
                OsbLog.Warn("科技审计失败：" + e.GetType().Name + ": " + e.Message);
            }
        }
    }
}
