using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>A3-27：无障碍提示与确认对话框使用的统一文案。</summary>
    public static class AccessibilityCopy
    {
        public static string FormatCosts(int food, int wealth, int reputation, int materials)
        {
            var costs = new List<string>();
            if (food > 0) costs.Add("粮食 " + food);
            if (wealth > 0) costs.Add("财富 " + wealth);
            if (reputation > 0) costs.Add("声望 " + reputation);
            if (materials > 0) costs.Add("建材 " + materials);
            return costs.Count > 0 ? string.Join(" / ", costs) : "无资源消耗";
        }

        public static string BuildingConfirmation(BuildingDef building)
        {
            if (building == null) return "建筑信息不可用。";
            return "建设项目：" + building.DisplayName
                + "\n最终成本：" + FormatCosts(0, building.CostWealth, building.CostReputation, building.CostMaterial)
                + "\n建成结果：" + building.EffectText
                + "\n\n确认后会立即扣除资源并完成建设，本局内无法撤销。";
        }

        public static string EventCardConfirmation(EventOptionDef option, CardDef card, bool remove)
        {
            string cardName = card != null ? card.DisplayName : "未知卡牌";
            string action = remove ? "永久移除《" + cardName + "》" : "升级《" + cardName + "》";
            string result = option != null && !string.IsNullOrEmpty(option.ResultText)
                ? option.ResultText
                : (remove ? "该卡将从本局牌组中移除" : "该卡将在本局中标记为已升级");
            string cost = option != null
                ? FormatCosts(option.CostFood, option.CostWealth, option.CostReputation, 0)
                : "无资源消耗";
            return "最终操作：" + action
                + "\n最终成本：" + cost
                + "\n结算结果：" + result
                + "\n\n只有确认后才会扣除资源并修改牌组；取消不会改变战役状态。";
        }

        public static string RewardSkipConfirmation(int cardCount, int relicCount)
        {
            var parts = new List<string>();
            if (cardCount > 0) parts.Add(cardCount + " 个卡牌选项");
            if (relicCount > 0) parts.Add(relicCount + " 个遗物选项");
            string pending = parts.Count > 0 ? string.Join("和", parts) : "当前剩余奖励";
            return "将放弃" + pending + "。\n\n已领取的奖励会保留，未领取的奖励将永久消失。";
        }
    }
}
