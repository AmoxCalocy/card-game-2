using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    /// <summary>A2-23 构建密林区域与区域切换（配置表 §9）：密林地图、节点战斗、危机伏击、草原→密林过渡。</summary>
    public class RegionTransitionTests
    {
        private static readonly int[] FixedSeeds = { 1, 2, 3, 42, 12345, 777, 20240806, 314159, 271828, 999983 };

        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.Relics.Clear();
            RunSession.BuiltBuildings.Clear();
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

        // ---- 密林地图生成（配置表 §9）----

        [Test]
        public void Jungle_Generate_AllSeeds_FourLayersCorrectComposition()
        {
            foreach (int seed in FixedSeeds)
            {
                RegionMap.Clear();
                Assert.IsTrue(RegionMap.Generate(ContentRegion.Jungle, new GameRandom(seed)), "种子 " + seed);
                Assert.AreEqual(ContentRegion.Jungle, RegionMap.Region);
                Assert.AreEqual(10, RegionMap.Nodes.Count, "种子 " + seed + " 节点总数");

                AssertComposition(seed, 1, NodeType.Combat, NodeType.Event, NodeType.Camp);
                AssertComposition(seed, 2, NodeType.Combat, NodeType.Event, NodeType.Elite);
                AssertComposition(seed, 3, NodeType.Combat, NodeType.Event, NodeType.Camp);
                AssertComposition(seed, 4, NodeType.Boss);
            }
        }

        [Test]
        public void Jungle_Generate_AllSeeds_EnemyAndEventPoolsMatchConfig()
        {
            foreach (int seed in FixedSeeds)
            {
                RegionMap.Clear();
                RegionMap.Generate(ContentRegion.Jungle, new GameRandom(seed));

                foreach (var node in RegionMap.Nodes)
                {
                    if (node.EnemyPoolIds != null)
                    {
                        foreach (var id in node.EnemyPoolIds)
                        {
                            Assert.IsTrue(IsJungleEnemyId(id),
                                "种子 " + seed + " 节点 " + node.Id + " 敌人池非密林：" + id);
                        }
                    }

                    if (node.EventPoolIds != null)
                    {
                        foreach (var id in node.EventPoolIds)
                        {
                            Assert.IsTrue(IsJungleEventId(id),
                                "种子 " + seed + " 节点 " + node.Id + " 事件池非密林 E11-E20：" + id);
                        }
                    }
                }
            }
        }

        [Test]
        public void Jungle_Generate_AllSeeds_ReachableFromStartToBoss()
        {
            foreach (int seed in FixedSeeds)
            {
                RegionMap.Clear();
                RegionMap.Generate(ContentRegion.Jungle, new GameRandom(seed));
                Assert.IsTrue(HasPathToBoss(), "种子 " + seed + " 密林无起点到首领路径");
            }
        }

        // ---- 节点战斗（A2-23）----

        [Test]
        public void CombatNode_Start_FightsEnemy_NormalRewards()
        {
            // 移动到第 1 层战斗节点
            var combatNode = MoveToFirstLayerNode(NodeType.Combat);

            Assert.IsTrue(RunSession.StartNodeCombat(combatNode), "进入节点战斗");
            Assert.AreEqual(GameState.Combat, RunSession.CurrentState);
            Assert.IsTrue(CombatManager.IsActive);
            Assert.AreEqual(EncounterConfig.EncounterType.Normal, CombatManager.CurrentEncounterType);
            Assert.AreEqual(1, CombatManager.EnemyTeam.Count, "单个敌人");
        }

        [Test]
        public void EliteNode_Start_EliteType()
        {
            // 走到第 2 层精英节点
            MoveToFirstLayerNode(NodeType.Combat);
            var elite = MoveToLayerNode(2, NodeType.Elite);

            Assert.IsTrue(RunSession.StartNodeCombat(elite), "进入精英战斗");
            Assert.AreEqual(EncounterConfig.EncounterType.Elite, CombatManager.CurrentEncounterType);
        }

        // ---- 危机伏击（§9.1）----

        [Test]
        public void Ambush_TriggersOnPending_EliteRewards()
        {
            // 风险推到阈值：移动到战斗节点时风险 +1 达 10 → AmbushPending（草原伏击 EN01+EN02）
            RegionMap.Clear();
            RegionMap.Generate(ContentRegion.Plains, new GameRandom(5));
            RunSession.SetRiskForTest(GameStartParameters.RiskThreshold - 1);
            var combatNode = MoveToFirstLayerNode(NodeType.Combat);

            Assert.IsTrue(RunSession.AmbushPending, "风险阈值应标记伏击");
            Assert.AreEqual(GameStartParameters.RiskAfterCrisis, RunSession.Risk, "伏击后风险重置");

            bool started = RunSession.StartNodeCombat(combatNode);

            Assert.IsTrue(started, "进入战斗节点触发伏击战斗");
            Assert.IsFalse(RunSession.AmbushPending, "伏击已触发清标记");
            Assert.AreEqual(2, CombatManager.EnemyTeam.Count, "伏击 2 名敌人");
            Assert.AreEqual(EncounterConfig.EncounterType.Elite, CombatManager.CurrentEncounterType, "按精英结算");

            // 杀敌 → 精英奖励（建材 +2）
            foreach (var e in CombatManager.EnemyTeam) e.TakeDamage(e.CurrentHp + e.Armor);
            CombatManager.CheckEndCondition();
            Assert.AreEqual(CombatPhase.Victory, CombatManager.Phase);
            Assert.AreEqual(0 + 2, RunSession.Materials, "精英建材 +2");
        }

        // ---- 区域切换：草原首领胜利 → 密林（保留战役状态）----

        [Test]
        public void PlainsBossVictory_AdvancesToJungle_KeepsCampaignState()
        {
            // 准备战役状态：资源/牌组/遗物/建筑
            RunSession.SetFoodForTest(20);
            RunSession.SetWealthForTest(100);
            RunSession.SetMaterialsForTest(15);
            RunSession.AddRelicForTest("R03");
            RunSession.SetReputationForTest(100);
            RunSession.MarkGrasslandBossDefeated();
            RunSession.TryBuildBuilding("B01");
            int deckCount = RunSession.CampaignDeck.Count;

            // 走到首领节点并战斗
            var bossNode = WalkToBossNode();
            Assert.AreEqual(NodeType.Boss, bossNode.Type);
            int foodBeforeCombat = RunSession.Food; // 战斗前粮食（已含移动消耗）
            Assert.IsTrue(RunSession.StartNodeCombat(bossNode), "进入首领战斗");
            Assert.AreEqual(EncounterConfig.EncounterType.Boss, CombatManager.CurrentEncounterType);

            // 杀首领
            foreach (var e in CombatManager.EnemyTeam) e.TakeDamage(e.CurrentHp + e.Armor);
            CombatManager.CheckEndCondition();
            CombatManager.End();

            // 区域切换断言
            Assert.AreEqual(ContentRegion.Jungle, RegionMap.Region, "切换到密林");
            Assert.IsTrue(RegionMap.IsGenerated);
            Assert.AreEqual(0, RunSession.Risk, "密林风险重置 0");
            Assert.AreEqual(foodBeforeCombat + 5, RunSession.Food, "保留粮食并 +首领奖励 5 粮");
            Assert.AreEqual(deckCount, RunSession.CampaignDeck.Count, "保留牌组");
            Assert.IsTrue(RunSession.HasRelic("R03"), "保留遗物");
            Assert.IsTrue(RunSession.HasBuilding("B01"), "保留建筑");
            Assert.IsTrue(RunSession.GrasslandBossDefeated, "首领击败标记保留");
            Assert.AreEqual(GameState.Reward, RunSession.CurrentState, "首领胜利进入奖励状态（领取后回密林地图）");
        }

        // ---- 密林移动消耗（配置表 §9：粮 -2、风险 +2）----

        [Test]
        public void Jungle_Move_Consumes2Food_RiskPlus2()
        {
            RegionMap.Clear();
            RegionMap.Generate(ContentRegion.Jungle, new GameRandom(1));
            RunSession.SetRiskForTest(0);
            int foodBefore = RunSession.Food;

            RunSession.TryMoveToNode(RegionMap.ReachableNext()[0]);

            Assert.AreEqual(foodBefore - 2, RunSession.Food, "密林移动粮 -2");
            Assert.AreEqual(GameStartParameters.ForestMoveRisk, RunSession.Risk, "密林移动风险 +2");
        }

        // ---- 辅助 ----

        private static void AssertComposition(int seed, int layer, params NodeType[] expected)
        {
            var types = new System.Collections.Generic.HashSet<NodeType>();
            foreach (var n in RegionMap.Nodes)
            {
                if (n.Layer == layer) types.Add(n.Type);
            }

            Assert.AreEqual(expected.Length, types.Count, "种子 " + seed + " 第 " + layer + " 层类型数");
            foreach (var t in expected)
            {
                Assert.IsTrue(types.Contains(t), "种子 " + seed + " 第 " + layer + " 层缺 " + t);
            }
        }

        private static bool IsJungleEnemyId(string id)
        {
            return id == "EN06" || id == "EN07" || id == "EN08" || id == "EN09" || id == "EN10";
        }

        private static bool IsJungleEventId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 2) return false;
            return int.TryParse(id.Substring(1), out int n) && n >= 11 && n <= 20;
        }

        private static bool HasPathToBoss()
        {
            var startNodes = new System.Collections.Generic.List<RegionMapNode> { null }; // null = 起点
            var queue = new System.Collections.Generic.Queue<RegionMapNode>(startNodes);
            var seen = new System.Collections.Generic.HashSet<RegionMapNode> { null };
            while (queue.Count > 0)
            {
                RegionMapNode cur = queue.Dequeue();
                foreach (var n in RegionMap.Nodes)
                {
                    if (n.Layer != LayerOf(cur) + 1) continue;
                    bool connected = cur == null || cur.NextIndexes.Contains(IndexOf(n));
                    if (connected && seen.Add(n))
                    {
                        if (n.Type == NodeType.Boss) return true;
                        queue.Enqueue(n);
                    }
                }
            }

            return false;
        }

        private static int IndexOf(RegionMapNode node)
        {
            for (int i = 0; i < RegionMap.Nodes.Count; i++)
            {
                if (RegionMap.Nodes[i] == node) return i;
            }

            return -1;
        }

        private static int LayerOf(RegionMapNode node)
        {
            return node == null ? 0 : node.Layer;
        }

        private static RegionMapNode MoveToFirstLayerNode(NodeType type)
        {
            foreach (int idx in RegionMap.ReachableNext())
            {
                if (RegionMap.Nodes[idx].Type == type)
                {
                    RunSession.TryMoveToNode(idx);
                    return RegionMap.Nodes[idx];
                }
            }

            return null;
        }

        private static RegionMapNode MoveToLayerNode(int layer, NodeType type)
        {
            while (RegionMap.CurrentLayer < layer - 1)
            {
                RunSession.TryMoveToNode(RegionMap.ReachableNext()[0]);
            }

            foreach (int idx in RegionMap.ReachableNext())
            {
                if (RegionMap.Nodes[idx].Layer == layer && RegionMap.Nodes[idx].Type == type)
                {
                    RunSession.TryMoveToNode(idx);
                    return RegionMap.Nodes[idx];
                }
            }

            return null;
        }

        private static RegionMapNode WalkToBossNode()
        {
            while (RegionMap.CurrentLayer < 3)
            {
                RunSession.TryMoveToNode(RegionMap.ReachableNext()[0]);
            }

            foreach (int idx in RegionMap.ReachableNext())
            {
                if (RegionMap.Nodes[idx].Type == NodeType.Boss)
                {
                    RunSession.TryMoveToNode(idx);
                    return RegionMap.Nodes[idx];
                }
            }

            return null;
        }
    }
}
