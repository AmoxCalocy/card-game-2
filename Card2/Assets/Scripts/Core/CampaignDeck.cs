using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>
    /// 战役牌组（A2-16）：存储玩家在战斗外的卡牌集合。
    /// 战斗开始时 CombatManager 从此复制独立副本，战斗结束后仅持久化变更。
    /// </summary>
    public class CampaignDeck
    {
        public readonly List<string> Cards = new List<string>();
        public int Count => Cards.Count;

        /// <summary>已升级的卡牌 ID（事件 E07 / 建筑 B03 使用；同一张卡只能升级一次）。</summary>
        public readonly HashSet<string> UpgradedCards = new HashSet<string>();

        public CampaignDeck(IEnumerable<string> initialCards)
        {
            Cards.AddRange(initialCards);
        }

        /// <summary>添加一张卡（不超上限）。返回是否成功。</summary>
        public bool AddCard(string cardId)
        {
            if (Cards.Count >= GameStartParameters.MaxDeckSize) return false;
            Cards.Add(cardId);
            return true;
        }

        /// <summary>移除一张卡（不低于下限，仅移除第一张匹配的）。返回是否成功。</summary>
        public bool RemoveCard(string cardId)
        {
            if (Cards.Count <= GameStartParameters.MinDeckSize) return false;
            return Cards.Remove(cardId);
        }

        /// <summary>移除指定索引的卡。返回被移除的卡 ID，失败返回 null。</summary>
        public string RemoveCardAt(int index)
        {
            if (Cards.Count <= GameStartParameters.MinDeckSize) return null;
            if (index < 0 || index >= Cards.Count) return null;
            string id = Cards[index];
            Cards.RemoveAt(index);
            return id;
        }

        /// <summary>是否为初始牌组锁定卡（不可被事件移除）。</summary>
        public static bool IsInitialLockedCard(string cardId)
        {
            foreach (var id in GameStartParameters.StartingDeck)
            {
                if (id == cardId) return true;
            }

            return false;
        }

        /// <summary>牌组中是否存在可移除的非初始锁定卡（事件「移除卡」选项的启用条件）。</summary>
        public bool HasRemoveableCard()
        {
            foreach (var id in Cards)
            {
                if (!IsInitialLockedCard(id)) return true;
            }

            return false;
        }

        /// <summary>牌组中所有可移除的非初始锁定卡（去重，供界面选择）。</summary>
        public List<string> RemoveableCards()
        {
            var result = new List<string>();
            foreach (var id in Cards)
            {
                if (IsInitialLockedCard(id)) continue;
                if (!result.Contains(id)) result.Add(id);
            }

            return result;
        }

        /// <summary>标记一张卡已升级（同一张卡只能升级一次）。返回是否成功。</summary>
        public bool UpgradeCard(string cardId)
        {
            if (!Cards.Contains(cardId)) return false;
            if (!UpgradedCards.Add(cardId)) return false;
            return true;
        }

        /// <summary>创建深拷贝，供战斗初始化使用。</summary>
        public List<string> CloneCardList()
        {
            return new List<string>(Cards);
        }

        public CampaignDeck Clone()
        {
            var clone = new CampaignDeck(Cards);
            foreach (var id in UpgradedCards) clone.UpgradedCards.Add(id);
            return clone;
        }
    }
}
