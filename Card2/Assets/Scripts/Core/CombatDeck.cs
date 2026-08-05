using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>
    /// 战斗内独立牌堆：从战役牌组复制而来，不与原始牌组共享。
    /// 战斗结束后丢弃，仅持久化变更（添加/移除/升级卡）带回战役。
    /// </summary>
    public sealed class CombatDeck
    {
        public readonly List<string> DrawPile = new List<string>();
        public readonly List<string> Hand = new List<string>();
        public readonly List<string> DiscardPile = new List<string>();
        public readonly List<string> ExhaustZone = new List<string>();

        public int DrawPileCount => DrawPile.Count;
        public int DiscardPileCount => DiscardPile.Count;
        public int HandSize => Hand.Count;
        public int ExhaustedCount => ExhaustZone.Count;

        private GameRandom _rng;

        /// <summary>
        /// 从战役牌组初始化：复制所有卡 ID 到抽牌堆并洗牌。
        /// </summary>
        public void InitFromCampaign(IReadOnlyList<string> cardIds, GameRandom rng)
        {
            DrawPile.Clear();
            Hand.Clear();
            DiscardPile.Clear();
            ExhaustZone.Clear();

            _rng = rng;
            DrawPile.AddRange(cardIds);
            _rng.Shuffle(DrawPile);
        }

        /// <summary>
        /// 从抽牌堆抽牌到手牌。抽牌堆空时自动弃牌堆洗回。
        /// 两堆皆空时停止抽取。受手牌上限限制（超出直接进入弃牌堆）。
        /// </summary>
        public int DrawToHand(int count, int maxHandSize)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (Hand.Count >= maxHandSize)
                {
                    break;
                }

                if (DrawPile.Count == 0)
                {
                    if (DiscardPile.Count == 0)
                    {
                        break; // 两堆皆空，停止
                    }

                    DrawPile.AddRange(DiscardPile);
                    DiscardPile.Clear();
                    _rng.Shuffle(DrawPile);
                    RunRecord.Log(RecordCategory.Draw, "弃牌堆洗回抽牌堆，" + DrawPile.Count + " 张");
                }

                string cardId = DrawPile[DrawPile.Count - 1];
                DrawPile.RemoveAt(DrawPile.Count - 1);

                if (Hand.Count < maxHandSize)
                {
                    Hand.Add(cardId);
                    RunRecord.Log(RecordCategory.Draw, "抽到 " + cardId);
                    drawn++;
                }
                else
                {
                    // 超出手牌上限，直接进入弃牌堆
                    DiscardPile.Add(cardId);
                    RunRecord.Log(RecordCategory.Draw, "手牌已满，" + cardId + " 直接进入弃牌堆");
                }
            }

            return drawn;
        }

        /// <summary>将手牌全部移入弃牌堆（临时卡进消耗区）。</summary>
        public void DiscardHand()
        {
            for (int i = Hand.Count - 1; i >= 0; i--)
            {
                if (Hand[i].StartsWith("TEMP_"))
                {
                    ExhaustZone.Add(Hand[i]);
                    Hand.RemoveAt(i);
                }
            }

            DiscardPile.AddRange(Hand);
            Hand.Clear();
        }

        /// <summary>将指定卡从手牌消耗移出（进入消耗区）。</summary>
        public void ExhaustFromHand(string cardId)
        {
            if (Hand.Remove(cardId))
            {
                ExhaustZone.Add(cardId);
            }
        }

        /// <summary>将指定卡从手牌置入弃牌堆。</summary>
        public void DiscardFromHand(string cardId)
        {
            if (Hand.Remove(cardId))
            {
                DiscardPile.Add(cardId);
            }
        }

        /// <summary>创建深拷贝（用于快照/验证）。</summary>
        public CombatDeck Clone()
        {
            var clone = new CombatDeck();
            clone.DrawPile.AddRange(DrawPile);
            clone.Hand.AddRange(Hand);
            clone.DiscardPile.AddRange(DiscardPile);
            clone.ExhaustZone.AddRange(ExhaustZone);
            return clone;
        }
    }
}
