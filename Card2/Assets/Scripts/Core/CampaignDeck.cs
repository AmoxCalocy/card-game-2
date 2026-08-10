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

        /// <summary>创建深拷贝，供战斗初始化使用。</summary>
        public List<string> CloneCardList()
        {
            return new List<string>(Cards);
        }

        public CampaignDeck Clone()
        {
            var clone = new CampaignDeck(Cards);
            return clone;
        }
    }
}
