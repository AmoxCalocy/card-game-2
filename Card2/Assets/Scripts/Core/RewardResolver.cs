using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>单次可选奖励项。</summary>
    public class RewardOption
    {
        public string Label;
        public string Detail;
        public string CardId;    // 卡牌奖励（可空）
    }

    /// <summary>
    /// 战斗奖励结算（A2-16）：按遭遇类型生成可选奖励，管理待领取队列。
    /// </summary>
    public static class RewardResolver
    {
        /// <summary>当前待领取的奖励选项列表。</summary>
        public static IReadOnlyList<RewardOption> PendingOptions => _pending;
        public static bool HasPendingRewards => _pending.Count > 0;
        public static int PendingWealth;
        public static int PendingFood;
        public static int PendingMaterials;

        private static readonly List<RewardOption> _pending = new List<RewardOption>();

        /// <summary>为指定遭遇生成奖励并存入待领取队列。</summary>
        public static void GenerateRewards(EncounterConfig.EncounterType type, string region)
        {
            _pending.Clear();
            PendingWealth = 0; PendingFood = 0; PendingMaterials = 0;

            switch (type)
            {
                case EncounterConfig.EncounterType.Normal:
                    PendingWealth = 5; PendingFood = 2;
                    GenerateCardOptions(region, rareCount: 0, totalCount: 3);
                    break;

                case EncounterConfig.EncounterType.Elite:
                    PendingWealth = 10; PendingFood = 3; PendingMaterials = 2;
                    GenerateCardOptions(region, rareCount: 1, totalCount: 3);
                    break;

                case EncounterConfig.EncounterType.Boss:
                    PendingWealth = region == "草原" ? 20 : 25;
                    PendingFood = 5;
                    PendingMaterials = region == "草原" ? 3 : 4;
                    GenerateCardOptions(region, rareCount: 1, totalCount: 3);
                    break;
            }
        }

        /// <summary>领取指定索引的卡牌奖励（0-based）。返回被领取的卡 ID，失败返回 null。</summary>
        public static string ClaimCard(int optionIndex)
        {
            if (optionIndex < 0 || optionIndex >= _pending.Count) return null;
            var opt = _pending[optionIndex];
            if (string.IsNullOrEmpty(opt.CardId)) return null;
            _pending.Clear(); // 领取后清空（一个奖励只能选一次）
            return opt.CardId;
        }

        /// <summary>跳过卡牌奖励（资源奖励已在战斗胜利时由 RunSession.ApplyCombatRewards 入账，此处只放弃卡牌选项）。</summary>
        public static void SkipReward()
        {
            _pending.Clear();
        }

        public static void Clear()
        {
            _pending.Clear();
            PendingWealth = 0; PendingFood = 0; PendingMaterials = 0;
        }

        // === 内部 ===

        private static void AddToPool(List<CardDef> pool, string[] cardIds)
        {
            foreach (var id in cardIds)
            {
                var c = CardCatalog.Find(id);
                if (c != null && !pool.Contains(c)) pool.Add(c);
            }
        }

        /// <summary>构建区域战斗奖励卡池：区域来源卡（可选仅普通）+ 建筑奖励卡（B03/B04，A2-21）。</summary>
        private static List<CardDef> BuildRegionPool(string region, bool commonOnly)
        {
            var pool = new List<CardDef>();
            foreach (var card in CardCatalog.All)
            {
                bool inPool = false;

                if (card.SourceText.Contains(region + "奖励") || card.SourceText.Contains("初始"))
                    inPool = true;
                else if (region == "草原" && card.SourceText.Contains("草原"))
                    inPool = true;
                else if (region == "密林" && card.SourceText.Contains("密林"))
                    inPool = true;
                else if (card.SourceText.Contains("草原奖励") && region == "草原")
                    inPool = true;
                else if (card.SourceText.Contains("密林奖励") && region == "密林")
                    inPool = true;

                if (!inPool) continue;
                if (commonOnly && card.Rarity != CardRarity.Common) continue;
                pool.Add(card);
            }

            // 建筑奖励池（B03 铁匠铺：C04/C11；B04 医馆：C34/C37/C40）加入所有后续战斗奖励
            if (RunSession.HasBuilding("B03"))
                AddToPool(pool, new[] { "C04", "C11" });
            if (RunSession.HasBuilding("B04"))
                AddToPool(pool, new[] { "C34", "C37", "C40" });

            return pool;
        }

        /// <summary>检查指定卡是否在当前区域战斗奖励池中（A2-21 建筑奖励池验证）。</summary>
        public static bool RewardPoolContains(string region, string cardId)
        {
            var pool = BuildRegionPool(region, commonOnly: false);
            foreach (var c in pool)
            {
                if (c.Id == cardId) return true;
            }

            return false;
        }

        private static void GenerateCardOptions(string region, int rareCount, int totalCount)
        {
            var pool = BuildRegionPool(region, commonOnly: rareCount == 0);

            var rng = RunSession.Random;
            var selected = new List<CardDef>();

            // 先选稀有卡
            var rarePool = new List<CardDef>();
            foreach (var c in pool) if (c.Rarity != CardRarity.Common) rarePool.Add(c);
            for (int i = 0; i < rareCount && rarePool.Count > 0; i++)
            {
                int idx = rng.Next(rarePool.Count);
                selected.Add(rarePool[idx]);
                rarePool.RemoveAt(idx);
            }

            // 普通卡填满
            var commonPool = new List<CardDef>();
            foreach (var c in pool) if (c.Rarity == CardRarity.Common) commonPool.Add(c);
            while (selected.Count < totalCount && commonPool.Count > 0)
            {
                int idx = rng.Next(commonPool.Count);
                selected.Add(commonPool[idx]);
                commonPool.RemoveAt(idx);
            }

            // 如果还不够（池不够大），从 selected 中补充
            Shuffle(rng, pool);
            foreach (var c in pool)
            {
                if (selected.Count >= totalCount) break;
                if (!selected.Contains(c)) selected.Add(c);
            }

            foreach (var c in selected)
            {
                _pending.Add(new RewardOption
                {
                    Label = c.DisplayName + "（" + c.Cost + "费）",
                    Detail = c.EffectText,
                    CardId = c.Id
                });
            }
        }

        private static void Shuffle<T>(GameRandom rng, List<T> list)
        {
            // Fallback if rng is null
            if (rng == null) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
