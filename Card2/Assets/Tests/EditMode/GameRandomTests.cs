using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class GameRandomTests
    {
        [Test]
        public void StateRoundTrip_ContinuesSequence()
        {
            var original = new GameRandom(20260831);
            for (int i = 0; i < 37; i++) original.Next();
            GameRandomState state = original.CaptureState();
            var expected = new int[20];
            for (int i = 0; i < expected.Length; i++) expected[i] = original.Next(1000000);

            Assert.IsTrue(GameRandom.TryCreate(state, out GameRandom restored, out string issue), issue);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], restored.Next(1000000), "恢复后第 " + i + " 次取值不一致");
        }

        [Test]
        public void Sequence_MatchesSystemRandomCompatibility()
        {
            var actual = new GameRandom(123456789);
            var expected = new System.Random(123456789);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(expected.Next(), actual.Next(), "第 " + i + " 次 System.Random 兼容序列不一致");
        }

        [Test]
        public void SameSeed_ProducesIdenticalSequence()
        {
            var a = new GameRandom(42);
            var b = new GameRandom(42);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(a.Next(100), b.Next(100), "第 " + i + " 次取值不一致");
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new GameRandom(1);
            var b = new GameRandom(2);

            bool same = true;
            for (int i = 0; i < 20; i++)
            {
                if (a.Next(100) != b.Next(100))
                {
                    same = false;
                    break;
                }
            }

            Assert.IsFalse(same, "不同种子的序列应当不同");
        }

        [Test]
        public void Shuffle_SameSeed_SameOrder()
        {
            var a = new GameRandom(100);
            var listA = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            a.Shuffle(listA);

            var b = new GameRandom(100);
            var listB = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            b.Shuffle(listB);

            CollectionAssert.AreEqual(listA, listB);
        }

        [Test]
        public void Shuffle_DifferentSeeds_UsuallyDifferent()
        {
            var a = new GameRandom(1);
            var listA = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            a.Shuffle(listA);

            var b = new GameRandom(2);
            var listB = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            b.Shuffle(listB);

            CollectionAssert.AreNotEqual(listA, listB);
        }

        [Test]
        public void WeightedPick_ValidWeights_ReturnsInRange()
        {
            var rng = new GameRandom(7);
            int[] weights = { 3, 1, 2 };

            for (int i = 0; i < 30; i++)
            {
                int idx = rng.WeightedPick(weights, out string issue);
                Assert.IsNull(issue);
                Assert.GreaterOrEqual(idx, 0);
                Assert.Less(idx, weights.Length);
            }
        }

        [Test]
        public void WeightedPick_ValidWeights_RespectsProportions()
        {
            var rng = new GameRandom(7);
            int[] weights = { 10, 0, 0 }; // 只有 0 号有非零权重
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(0, rng.WeightedPick(weights, out _));
            }
        }

        [Test]
        public void WeightedPick_EmptyPool_ReportsIssue()
        {
            var rng = new GameRandom(1);
            int idx = rng.WeightedPick(new int[0], out string issue);
            Assert.AreEqual(-1, idx);
            StringAssert.Contains("空", issue);
        }

        [Test]
        public void WeightedPick_NullPool_ReportsIssue()
        {
            var rng = new GameRandom(1);
            int idx = rng.WeightedPick((int[])null, out string issue);
            Assert.AreEqual(-1, idx);
            StringAssert.Contains("空", issue);
        }

        [Test]
        public void WeightedPick_AllZeroWeights_ReportsIssue()
        {
            var rng = new GameRandom(1);
            int idx = rng.WeightedPick(new[] { 0, 0, 0 }, out string issue);
            Assert.AreEqual(-1, idx);
            StringAssert.Contains("为零", issue);
        }

        [Test]
        public void WeightedPick_NegativeWeight_ReportsIssue()
        {
            var rng = new GameRandom(1);
            int idx = rng.WeightedPick(new[] { 1, -1, 3 }, out string issue);
            Assert.AreEqual(-1, idx);
            StringAssert.Contains("为负", issue);
        }

        [Test]
        public void WeightedPick_Typed_ValidPool_ReturnsItem()
        {
            var rng = new GameRandom(7);
            var items = new[] { "a", "b", "c" };
            string result = rng.WeightedPick(items, s => s == "a" ? 1 : s == "b" ? 10 : 0, out string issue);
            Assert.IsNull(issue);
            Assert.IsNotNull(result);
        }

        [Test]
        public void WeightedPick_Typed_EmptyPool_ReportsIssue()
        {
            var rng = new GameRandom(1);
            string result = rng.WeightedPick(new string[0], s => 1, out string issue);
            Assert.IsNull(result);
            StringAssert.Contains("空", issue);
        }
    }
}
