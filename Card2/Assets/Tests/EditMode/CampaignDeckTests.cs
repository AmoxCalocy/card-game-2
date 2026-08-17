using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class CampaignDeckTests
    {
        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.StartNewGame(1);
            ContentRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CombatManager.End();
            RewardResolver.Clear();
            RunSession.Reset();
            ContentRegistry.Clear();
        }

        // ---- CampaignDeck ----

        [Test]
        public void Deck_InitializesFromStartingDeck()
        {
            var deck = new CampaignDeck(GameStartParameters.StartingDeck);
            Assert.AreEqual(10, deck.Count);
        }

        [Test]
        public void AddCard_WithinLimit_ReturnsTrue()
        {
            var deck = new CampaignDeck(new[] { "C01", "C09" });
            Assert.IsTrue(deck.AddCard("C17"));
            Assert.AreEqual(3, deck.Count);
        }

        [Test]
        public void AddCard_AtMaxLimit_ReturnsFalse()
        {
            var cards = new string[GameStartParameters.MaxDeckSize];
            for (int i = 0; i < cards.Length; i++) cards[i] = "C01";
            var deck = new CampaignDeck(cards);
            Assert.IsFalse(deck.AddCard("C17"));
        }

        [Test]
        public void RemoveCard_AboveMin_ReturnsTrue()
        {
            var deck = new CampaignDeck(new[] { "C01", "C09", "C17", "C33", "C36", "C02", "C03", "C04", "C05", "C06", "C07" });
            // 11 cards, min is 10, so can remove 1
            Assert.IsTrue(deck.RemoveCard("C01"));
            Assert.AreEqual(10, deck.Count);
        }

        [Test]
        public void RemoveCard_AtMinLimit_ReturnsFalse()
        {
            var cards = new string[GameStartParameters.MinDeckSize];
            for (int i = 0; i < cards.Length; i++) cards[i] = "C01";
            var deck = new CampaignDeck(cards);
            Assert.IsFalse(deck.RemoveCard("C01"));
        }

        [Test]
        public void CloneCardList_IsIndependent()
        {
            var deck = new CampaignDeck(new[] { "C01", "C09" });
            var clone = deck.CloneCardList();
            clone.Add("C17");
            Assert.AreEqual(2, deck.Count);
            Assert.AreEqual(3, clone.Count);
        }

        // ---- RewardResolver ----

        [Test]
        public void GenerateRewards_Normal_CreatesOptions()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Normal, "草原");
            Assert.GreaterOrEqual(RewardResolver.PendingOptions.Count, 1);
            Assert.AreEqual(5, RewardResolver.PendingWealth);
            Assert.AreEqual(2, RewardResolver.PendingFood);
        }

        [Test]
        public void GenerateRewards_Elite_HasRareCard()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Elite, "草原");
            Assert.AreEqual(3, RewardResolver.PendingOptions.Count);
            Assert.AreEqual(10, RewardResolver.PendingWealth);
        }

        [Test]
        public void ClaimCard_ValidIndex_ReturnsCardId()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Normal, "草原");
            string claimed = RewardResolver.ClaimCard(0);
            Assert.IsNotNull(claimed);
            Assert.IsTrue(CardCatalog.Exists(claimed));
            Assert.IsFalse(RewardResolver.HasPendingRewards);
        }

        [Test]
        public void ClaimCard_InvalidIndex_ReturnsNull()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Normal, "草原");
            Assert.IsNull(RewardResolver.ClaimCard(99));
        }

        [Test]
        public void SkipReward_ClearsCardOptions_KeepsResources()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Normal, "草原");
            RewardResolver.SkipReward();
            Assert.IsFalse(RewardResolver.HasPendingRewards, "跳过清空卡牌选项");
            Assert.AreEqual(5, RewardResolver.PendingWealth, "资源奖励不受跳过影响（由 ApplyCombatRewards 在胜利时入账）");
        }
    }
}
