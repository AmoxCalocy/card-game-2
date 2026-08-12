using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    /// <summary>A2-18 移动消耗与粮食耗尽处理（配置表 §2.4/§9）。</summary>
    public class MoveCostTests
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
            RegionMap.Clear();
            RunSession.Reset();
            ContentRegistry.Clear();
        }

        [Test]
        public void Move_EnoughFood_ConsumesOneNoPenalty()
        {
            RegionMap.Clear();
            RegionMap.Generate(ContentRegion.Plains, new GameRandom(42));
            int foodBefore = RunSession.Food;
            var reachable = RegionMap.ReachableNext();
            Assert.GreaterOrEqual(reachable.Count, 1);
            string result = RunSession.TryMoveToNode(reachable[0]);

            Assert.AreEqual(foodBefore - 1, RunSession.Food, "粮食应消耗 1");
            Assert.AreEqual(0, RunSession.PlayerFatigue, "粮食充足不应加疲劳");
            Assert.AreEqual(1, RunSession.Risk, "草原移动风险 +1");
            StringAssert.Contains("粮食 -1", result);
        }

        [Test]
        public void Move_ExactlyEnoughFood_ConsumesToZeroNoPenalty()
        {
            RegionMap.Clear();
            RegionMap.Generate(ContentRegion.Plains, new GameRandom(42));
            RunSession.SetFoodForTest(1);
            var reachable = RegionMap.ReachableNext();
            RunSession.TryMoveToNode(reachable[0]);

            Assert.AreEqual(0, RunSession.Food, "粮食应消耗至 0");
            Assert.AreEqual(0, RunSession.PlayerFatigue, "刚好足够不应惩罚");
            Assert.AreEqual(1, RunSession.Risk);
        }

        [Test]
        public void Move_NoFood_ConsumesToZeroWithPenalty()
        {
            RegionMap.Clear();
            RegionMap.Generate(ContentRegion.Plains, new GameRandom(42));
            RunSession.SetFoodForTest(0);
            var reachable = RegionMap.ReachableNext();
            string result = RunSession.TryMoveToNode(reachable[0]);

            Assert.AreEqual(0, RunSession.Food, "粮食不能为负");
            Assert.AreEqual(1, RunSession.PlayerFatigue, "主角疲劳 +1");
            Assert.AreEqual(3, RunSession.Risk, "粮食不足风险 +2 再加移动 +1");
            StringAssert.Contains("粮食不足", result);
        }

        [Test]
        public void Move_NoFoodTwice_PenaltyAppliedEachTime()
        {
            RegionMap.Clear();
            RegionMap.Generate(ContentRegion.Plains, new GameRandom(42));
            RunSession.SetFoodForTest(0);
            var reachable = RegionMap.ReachableNext();
            RunSession.TryMoveToNode(reachable[0]);
            int fatigueAfterFirst = RunSession.PlayerFatigue;
            int riskAfterFirst = RunSession.Risk;

            var reachable2 = RegionMap.ReachableNext();
            Assert.GreaterOrEqual(reachable2.Count, 1);
            RunSession.TryMoveToNode(reachable2[0]);

            Assert.AreEqual(fatigueAfterFirst + 1, RunSession.PlayerFatigue, "每次粮食不足都应 +1 疲劳");
            Assert.Greater(RunSession.Risk, riskAfterFirst, "风险应继续增加");
            Assert.AreEqual(0, RunSession.Food);
        }

        [Test]
        public void Move_EliteNode_ExtraRisk()
        {
            // 用多个种子找一条能到达精英的路径
            int[] seeds = { 7, 77, 777, 2024 };
            int elite = -1;
            bool moved = false;
            foreach (int seed in seeds)
            {
                RunSession.Reset();
                RunSession.StartNewGame(1);
                RegionMap.Clear();
                RegionMap.Generate(ContentRegion.Plains, new GameRandom(seed));

                var first = RegionMap.ReachableNext();
                if (first.Count == 0) continue;
                RunSession.TryMoveToNode(first[0]);

                var reachable2 = RegionMap.ReachableNext();
                foreach (int idx in reachable2)
                {
                    if (RegionMap.Nodes[idx].Type == NodeType.Elite)
                    {
                        elite = idx;
                        moved = true;
                        break;
                    }
                }
                if (moved) break;
            }
            Assert.IsTrue(moved, "应能找到精英节点路径");

            int riskBefore = RunSession.Risk;
            RunSession.TryMoveToNode(elite);
            Assert.AreEqual(riskBefore + 2, RunSession.Risk, "精英节点风险 +2（基础 1+额外 1）");
        }

        [Test]
        public void Move_Rejected_ResourcesUnchanged()
        {
            RegionMap.Clear();
            RegionMap.Generate(ContentRegion.Plains, new GameRandom(42));
            int foodBefore = RunSession.Food;
            int riskBefore = RunSession.Risk;
            int fatigueBefore = RunSession.PlayerFatigue;

            // 从起点直接移动到第 2 层（跨层，应被拒绝）
            int layer2 = -1;
            for (int i = 0; i < RegionMap.Nodes.Count; i++)
            {
                if (RegionMap.Nodes[i].Layer == 2) { layer2 = i; break; }
            }
            string result = RunSession.TryMoveToNode(layer2);
            StringAssert.Contains("被拒绝", result);
            Assert.AreEqual(foodBefore, RunSession.Food, "拒绝后粮食不变");
            Assert.AreEqual(riskBefore, RunSession.Risk, "拒绝后风险不变");
            Assert.AreEqual(fatigueBefore, RunSession.PlayerFatigue, "拒绝后疲劳不变");
        }

        [Test]
        public void Move_FullPathToBoss_TotalFoodConsumedEqualsLayers()
        {
            RegionMap.Clear();
            RegionMap.Generate(ContentRegion.Plains, new GameRandom(777));
            int foodBefore = RunSession.Food;
            for (int layer = 1; layer <= RegionMap.LayerCount; layer++)
            {
                var reachable = RegionMap.ReachableNext();
                Assert.GreaterOrEqual(reachable.Count, 1);
                RunSession.TryMoveToNode(reachable[0]);
            }
            Assert.AreEqual(foodBefore - 4, RunSession.Food, "4 层移动共消耗 4 粮食");
            Assert.AreEqual(0, RunSession.PlayerFatigue, "初始 14 粮食足够全程，无惩罚");
            Assert.AreEqual(4, RunSession.Risk, "4 次移动风险 +4");
        }

        [Test]
        public void Move_RiskReachesThreshold_ResetsToFiveAndFlagsAmbush()
        {
            RegionMap.Clear();
            RegionMap.Generate(ContentRegion.Plains, new GameRandom(999983));
            RunSession.SetRiskForTest(GameStartParameters.RiskThreshold - 1);
            var reachable = RegionMap.ReachableNext();
            Assert.GreaterOrEqual(reachable.Count, 1);
            string result = RunSession.TryMoveToNode(reachable[0]);

            Assert.AreEqual(GameStartParameters.RiskAfterCrisis, RunSession.Risk, "达到阈值后重置为 5");
            Assert.IsTrue(RunSession.AmbushPending, "应标记危机伏击待触发");
            StringAssert.Contains("危机伏击", result);
        }

        [Test]
        public void Move_StartNewGame_ResourcesInitialized()
        {
            Assert.AreEqual(GameStartParameters.StartFood, RunSession.Food);
            Assert.AreEqual(GameStartParameters.StartWealth, RunSession.Wealth);
            Assert.AreEqual(GameStartParameters.StartReputation, RunSession.Reputation);
            Assert.AreEqual(GameStartParameters.StartBuildingMaterials, RunSession.Materials);
            Assert.AreEqual(0, RunSession.Risk);
            Assert.AreEqual(0, RunSession.PlayerFatigue);
            Assert.IsFalse(RunSession.AmbushPending);
        }
    }
}
