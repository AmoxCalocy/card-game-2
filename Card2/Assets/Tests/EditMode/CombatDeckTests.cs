using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class CombatDeckTests
    {
        private List<string> _fiveCards;

        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.StartNewGame(42);
            ContentRegistry.Clear();
            _fiveCards = new List<string> { "C01", "C02", "C03", "C04", "C05" };
        }

        [TearDown]
        public void TearDown()
        {
            RunSession.Reset();
            ContentRegistry.Clear();
            RunRecord.Clear();
        }

        [Test]
        public void Init_CopiesCards_NotReferences()
        {
            var original = new List<string> { "C01", "C02" };
            var deck = new CombatDeck();
            deck.InitFromCampaign(original, RunSession.Random);

            original.Clear();
            Assert.AreEqual(2, deck.DrawPileCount + deck.HandSize, "牌堆应是独立副本");
        }

        [Test]
        public void Init_ShufflesIntoDrawPile_HandEmpty()
        {
            var deck = new CombatDeck();
            deck.InitFromCampaign(_fiveCards, RunSession.Random);

            Assert.AreEqual(5, deck.DrawPileCount + deck.HandSize);
            Assert.AreEqual(0, deck.DiscardPileCount);
            Assert.AreEqual(0, deck.ExhaustedCount);
        }

        [Test]
        public void Draw_RemovesFromDrawPile_AddsToHand()
        {
            var deck = new CombatDeck();
            deck.InitFromCampaign(_fiveCards, RunSession.Random);

            int before = deck.DrawPileCount;
            deck.DrawToHand(1, 10);
            Assert.AreEqual(before - 1, deck.DrawPileCount);
            Assert.AreEqual(1, deck.HandSize);
        }

        [Test]
        public void Draw_SameSeed_SameOrder()
        {
            var a = new CombatDeck();
            a.InitFromCampaign(_fiveCards, new GameRandom(99));
            a.DrawToHand(3, 10);

            var b = new CombatDeck();
            b.InitFromCampaign(_fiveCards, new GameRandom(99));
            b.DrawToHand(3, 10);

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(a.Hand[i], b.Hand[i], "同种子第 " + i + " 张应相同");
            }
        }

        [Test]
        public void Draw_ReshufflesDiscardWhenDrawPileEmpty()
        {
            var deck = new CombatDeck();
            deck.InitFromCampaign(_fiveCards, RunSession.Random);

            // 抽光
            deck.DrawToHand(5, 10);
            Assert.AreEqual(0, deck.DrawPileCount);
            Assert.AreEqual(5, deck.HandSize);

            // 弃掉手牌，弃牌堆有 5 张
            deck.DiscardHand();
            Assert.AreEqual(5, deck.DiscardPileCount);

            // 再抽：空抽牌堆 → 弃牌堆洗回
            int drawn = deck.DrawToHand(3, 10);
            Assert.AreEqual(3, drawn);
            Assert.AreEqual(2, deck.DrawPileCount, "洗回后应剩 2 张在抽牌堆");
        }

        [Test]
        public void Draw_StopsWhenBothPilesEmpty()
        {
            var deck = new CombatDeck();
            deck.InitFromCampaign(_fiveCards, RunSession.Random);

            // 把所有牌抽到手上并全部消耗，使抽牌堆和弃牌堆均为空
            deck.DrawToHand(5, 10);
            var handCopy = new List<string>(deck.Hand);
            foreach (var c in handCopy) deck.ExhaustFromHand(c);

            Assert.AreEqual(5, deck.ExhaustedCount);
            Assert.AreEqual(0, deck.DrawPileCount);
            Assert.AreEqual(0, deck.DiscardPileCount);
            Assert.AreEqual(0, deck.HandSize);

            int drawn = deck.DrawToHand(1, 10);
            Assert.AreEqual(0, drawn, "两堆皆空时应抽到 0");
        }

        [Test]
        public void Draw_HandAtMax_StopsDrawing()
        {
            var deck = new CombatDeck();
            deck.InitFromCampaign(_fiveCards, RunSession.Random);

            deck.DrawToHand(5, 3);
            Assert.AreEqual(3, deck.HandSize, "手牌不应超过上限");
            Assert.AreEqual(2, deck.DrawPileCount + deck.DiscardPileCount, "未抽的牌留在抽牌堆");
        }

        [Test]
        public void DiscardHand_MovesAllToDiscard()
        {
            var deck = new CombatDeck();
            deck.InitFromCampaign(_fiveCards, RunSession.Random);

            deck.DrawToHand(3, 10);
            deck.DiscardHand();

            Assert.AreEqual(0, deck.HandSize);
            Assert.AreEqual(3, deck.DiscardPileCount);
        }

        [Test]
        public void ExhaustFromHand_RemovesPermanently()
        {
            var deck = new CombatDeck();
            deck.InitFromCampaign(_fiveCards, RunSession.Random);

            deck.DrawToHand(3, 10);
            string card = deck.Hand[0];
            deck.ExhaustFromHand(card);

            Assert.AreEqual(2, deck.HandSize);
            Assert.AreEqual(1, deck.ExhaustedCount);
            Assert.IsTrue(deck.ExhaustZone.Contains(card));
            Assert.IsFalse(deck.Hand.Contains(card));
            Assert.IsFalse(deck.DiscardPile.Contains(card), "消耗卡不应在弃牌堆");
        }

        [Test]
        public void DiscardFromHand_MovesToDiscard()
        {
            var deck = new CombatDeck();
            deck.InitFromCampaign(_fiveCards, RunSession.Random);

            deck.DrawToHand(3, 10);
            string card = deck.Hand[0];
            deck.DiscardFromHand(card);

            Assert.AreEqual(2, deck.HandSize);
            Assert.IsTrue(deck.DiscardPile.Contains(card));
            Assert.AreEqual(0, deck.ExhaustedCount, "弃牌不应进消耗区");
        }

        [Test]
        public void Clone_IndependentCopy()
        {
            var deck = new CombatDeck();
            deck.InitFromCampaign(_fiveCards, RunSession.Random);
            deck.DrawToHand(2, 10);

            var clone = deck.Clone();
            clone.DrawPile.Clear();

            Assert.AreNotEqual(deck.DrawPileCount, clone.DrawPileCount, "clone 修改不应影响原对象");
        }
    }
}
