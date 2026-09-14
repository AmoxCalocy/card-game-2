using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    /// <summary>A3-27：确认文案、延迟扣费与取消不产生副作用。</summary>
    public class AccessibilityTests
    {
        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.Relics.Clear();
            RunSession.EventFlags.Clear();
            RunSession.BuiltBuildings.Clear();
            RunSession.EnterTestPage(GameState.Event);
            ContentRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CombatManager.End();
            RewardResolver.Clear();
            RegionMap.Clear();
            RunSession.Reset();
            ContentRegistry.Clear();
        }

        [Test]
        public void BuildingConfirmation_ContainsFinalCostAndResult()
        {
            BuildingDef building = BuildingCatalog.Find("B03");
            string text = AccessibilityCopy.BuildingConfirmation(building);

            StringAssert.Contains("最终成本", text);
            StringAssert.Contains("财富 30", text);
            StringAssert.Contains("声望 5", text);
            StringAssert.Contains("建材 5", text);
            StringAssert.Contains(building.EffectText, text);
            StringAssert.Contains("无法撤销", text);
        }

        [Test]
        public void EventUpgrade_CancelLeavesResourcesDeckAndEventUnchanged()
        {
            Assert.IsTrue(RunSession.StartEvent("E07"));
            int wealthBefore = RunSession.Wealth;
            var cardsBefore = new List<string>(RunSession.CampaignDeck.Cards);
            var upgradedBefore = new HashSet<string>(RunSession.CampaignDeck.UpgradedCards);
            int recordsBefore = RunSession.Records.Count;

            string pending = RunSession.ChooseEventOption(0);
            StringAssert.Contains("最终确认后支付", pending);
            Assert.AreEqual(wealthBefore, RunSession.Wealth, "进入子选择不应预扣财富");

            string cancelled = RunSession.CancelEventChoice();

            StringAssert.Contains("均未改变", cancelled);
            Assert.AreEqual("E07", RunSession.CurrentEvent.Id, "取消后仍停留在原事件");
            Assert.AreEqual(EventOptionChoiceKind.None, RunSession.PendingEventChoice);
            Assert.AreEqual(wealthBefore, RunSession.Wealth);
            CollectionAssert.AreEqual(cardsBefore, RunSession.CampaignDeck.Cards);
            CollectionAssert.AreEquivalent(upgradedBefore, RunSession.CampaignDeck.UpgradedCards);
            Assert.AreEqual(recordsBefore, RunSession.Records.Count, "取消前后不应新增规则结算记录");
        }

        [Test]
        public void EventRemove_CancelKeepsCardAndPendingRewardsUnapplied()
        {
            Assert.IsTrue(RunSession.CampaignDeck.AddCard("C02"));
            Assert.IsTrue(RunSession.StartEvent("E06"));
            int materialsBefore = RunSession.Materials;
            int reputationBefore = RunSession.Reputation;
            int deckBefore = RunSession.CampaignDeck.Count;

            RunSession.ChooseEventOption(0);
            string cancelled = RunSession.CancelEventChoice();

            StringAssert.Contains("未改变", cancelled);
            Assert.AreEqual(deckBefore, RunSession.CampaignDeck.Count);
            Assert.IsTrue(RunSession.CampaignDeck.Cards.Contains("C02"));
            Assert.AreEqual(materialsBefore, RunSession.Materials, "取消后不发放事件建材奖励");
            Assert.AreEqual(reputationBefore, RunSession.Reputation, "取消后不发放事件声望奖励");
            Assert.AreEqual("E06", RunSession.CurrentEvent.Id);
        }

        [Test]
        public void EventUpgrade_FinalChoiceChargesExactlyOnce()
        {
            Assert.IsTrue(RunSession.StartEvent("E07"));
            int wealthBefore = RunSession.Wealth;
            string cardId = RunSession.CampaignDeck.Cards[0];

            RunSession.ChooseEventOption(0);
            Assert.AreEqual(wealthBefore, RunSession.Wealth);

            string result = RunSession.ChooseEventCard(cardId);

            StringAssert.Contains("财富 -15", result);
            Assert.AreEqual(wealthBefore - 15, RunSession.Wealth);
            Assert.IsTrue(RunSession.CampaignDeck.UpgradedCards.Contains(cardId));
            Assert.AreEqual(EventOptionChoiceKind.None, RunSession.PendingEventChoice);
        }

        [Test]
        public void EventCardConfirmation_StatesDeferredCostAndSafeCancel()
        {
            EventOptionDef option = EventCatalog.Find("E07").Options[0];
            CardDef card = CardCatalog.Find("C01");
            string text = AccessibilityCopy.EventCardConfirmation(option, card, false);

            StringAssert.Contains("最终成本：财富 15", text);
            StringAssert.Contains("升级《剑击》", text);
            StringAssert.Contains("只有确认后", text);
            StringAssert.Contains("取消不会改变战役状态", text);
        }
    }
}
