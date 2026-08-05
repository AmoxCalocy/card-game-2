using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class GameStartParametersTests
    {
        [TearDown]
        public void TearDown()
        {
            RunSession.Reset();
        }

        [Test]
        public void StartingParameters_FirstVersion_FixedValues()
        {
            Assert.AreEqual(4, GameStartParameters.MaxPartySize);
            Assert.AreEqual(3, GameStartParameters.BaseEnergy);
            Assert.AreEqual(3, GameStartParameters.InitialHandSize);
            Assert.AreEqual(1, GameStartParameters.CardsPerTurn);
            Assert.AreEqual(5, GameStartParameters.MaxHandSize);
            Assert.AreEqual(10, GameStartParameters.MinDeckSize);
            Assert.AreEqual(30, GameStartParameters.MaxDeckSize);
            Assert.AreEqual(14, GameStartParameters.StartFood);
            Assert.AreEqual(30, GameStartParameters.StartWealth);
            Assert.AreEqual(0, GameStartParameters.StartReputation);
            Assert.AreEqual(0, GameStartParameters.StartBuildingMaterials);
            Assert.AreEqual(30, GameStartParameters.MaxFood);
            Assert.AreEqual(999, GameStartParameters.MaxWealth);
            Assert.AreEqual(100, GameStartParameters.MaxReputation);
            Assert.AreEqual(99, GameStartParameters.MaxBuildingMaterials);
        }

        [Test]
        public void StartingDeck_TenCards_WithinDeckLimitsAndConfigIds()
        {
            Assert.AreEqual(10, GameStartParameters.StartingDeck.Length);
            Assert.LessOrEqual(GameStartParameters.StartingDeck.Length, GameStartParameters.MaxDeckSize);
            Assert.GreaterOrEqual(GameStartParameters.StartingDeck.Length, GameStartParameters.MinDeckSize);

            var counts = new Dictionary<string, int>();
            foreach (var id in GameStartParameters.StartingDeck)
            {
                Assert.IsTrue(IsConfigCardId(id), "起始牌组引用了配置表外的卡 ID：" + id);
                counts[id] = counts.TryGetValue(id, out int c) ? c + 1 : 1;
            }

            // 与配置表 2.1 初始牌组一致：C01×4、C09×3、C17×1、C33×1、C36×1
            Assert.AreEqual(4, counts["C01"]);
            Assert.AreEqual(3, counts["C09"]);
            Assert.AreEqual(1, counts["C17"]);
            Assert.AreEqual(1, counts["C33"]);
            Assert.AreEqual(1, counts["C36"]);
        }

        [Test]
        public void StartNewGame_TenRuns_AllHaveSameStartingResources()
        {
            string firstRecord = null;
            var seeds = new HashSet<int>();

            for (int i = 0; i < 10; i++)
            {
                RunSession.Reset();
                RunSession.StartNewGame();

                Assert.AreEqual(GameState.Map, RunSession.CurrentState);
                Assert.IsTrue(RunSession.LastResolution.HasValue);
                Assert.IsTrue(seeds.Add(RunSession.Seed), "十局种子应各不相同");

                string record = RunSession.LastResolution.Value.Result;
                if (firstRecord == null)
                {
                    firstRecord = record;
                }
                else
                {
                    Assert.AreEqual(
                        firstRecord.Substring(firstRecord.IndexOf('；')),
                        record.Substring(record.IndexOf('；')),
                        "第 " + (i + 1) + " 局的起始资源与第一局不一致");
                }
            }
        }

        [Test]
        public void StartNewGame_ExplicitSeed_RecordContainsSeed()
        {
            RunSession.StartNewGame(20260805);

            Assert.AreEqual(20260805, RunSession.Seed);
            StringAssert.Contains("随机种子 20260805", RunSession.LastResolution.Value.Result);
        }

        private static bool IsConfigCardId(string id)
        {
            if (id == null || id.Length < 3 || id[0] != 'C')
            {
                return false;
            }

            return int.TryParse(id.Substring(1), out int n) && n >= 1 && n <= 40;
        }
    }
}
