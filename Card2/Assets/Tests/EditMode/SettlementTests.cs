using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    /// <summary>A2-24 定义 MVP 单局结局与结算页：密林首领胜利（垂直切片）、主角死亡失败、结算摘要。</summary>
    public class SettlementTests
    {
        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.Relics.Clear();
            RunSession.BuiltBuildings.Clear();
            RunSession.SetBossDefeatedForTest(false);
            RunSession.StartNewGame(42);
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

        // ---- 密林首领胜利 → Victory → 结算 ----

        [Test]
        public void JungleBossVictory_EntersVictoryAndSettlement()
        {
            // 切到密林并走到首领
            RunSession.AdvanceToNextRegion(); // 草原 → 密林（当前在草原状态也可？需在 Combat 态）
            // 若转移失败则直接生成密林地图（测试直通）
            if (RegionMap.Region != ContentRegion.Jungle)
            {
                RegionMap.Clear();
                RegionMap.Generate(ContentRegion.Jungle, new GameRandom(42));
            }

            var boss = WalkToBossNode();
            Assert.IsNotNull(boss, "密林首领节点");
            Assert.IsTrue(RunSession.StartNodeCombat(boss), "进入首领战");
            Assert.AreEqual(EncounterConfig.EncounterType.Boss, CombatManager.CurrentEncounterType);

            foreach (var e in CombatManager.EnemyTeam) e.TakeDamage(e.CurrentHp + e.Armor);
            CombatManager.CheckEndCondition();
            CombatManager.End();

            // 密林首领胜利 → Victory 状态
            Assert.AreEqual(GameState.Victory, RunSession.CurrentState, "密林首领进入 Victory");

            RunSession.EnterSettlement(true, "击败密林首领（垂直切片）");

            Assert.AreEqual(GameState.Settlement, RunSession.CurrentState, "进入结算状态");
            var s = RunSession.LastSettlement;
            Assert.IsNotNull(s);
            Assert.AreEqual("胜利（垂直切片）", s.Result);
            StringAssert.Contains("密林", s.RegionProgress);
            Assert.AreEqual(42, s.Seed);
            Assert.GreaterOrEqual(s.ElapsedSeconds, 0, "记录用时（测试瞬时可为 0）");
            Assert.IsFalse(string.IsNullOrEmpty(s.Deck));
            Assert.IsFalse(string.IsNullOrEmpty(s.Resources));
        }

        [Test]
        public void PlainsBossVictory_AdvancesRegion_NotVictoryState()
        {
            // 草原首领胜利 → 区域切换（Reward 态），不是 Victory
            var boss = WalkToBossNode();
            Assert.IsNotNull(boss);
            Assert.IsTrue(RunSession.StartNodeCombat(boss), "进入首领战");
            Assert.AreEqual(EncounterConfig.EncounterType.Boss, CombatManager.CurrentEncounterType);

            foreach (var e in CombatManager.EnemyTeam) e.TakeDamage(e.CurrentHp + e.Armor);
            CombatManager.CheckEndCondition();
            CombatManager.End();

            Assert.AreEqual(ContentRegion.Jungle, RegionMap.Region, "切到密林");
            Assert.AreNotEqual(GameState.Victory, RunSession.CurrentState, "草原首领不进入 Victory");
            Assert.AreEqual(GameState.Reward, RunSession.CurrentState);
        }

        // ---- 主角死亡 → Defeat → 结算 ----

        [Test]
        public void PlayerDeath_EntersDefeatAndSettlement()
        {
            RunSession.StartNodeCombat(WalkToFirstCombat());
            var player = CombatManager.PlayerCharacter();
            Assert.IsNotNull(player, "主角存在");

            player.TakeDamage(player.CurrentHp + player.Armor + 1);
            CombatManager.CheckEndCondition(); // 主角死亡：Phase=Defeat + GameFlow→Defeat

            Assert.AreEqual(CombatPhase.Defeat, CombatManager.Phase);
            Assert.AreEqual(GameState.Defeat, RunSession.CurrentState, "CheckEndCondition 已进入 Defeat");
            CombatManager.End();

            RunSession.EnterSettlement(false, "主角阵亡");
            Assert.AreEqual(GameState.Settlement, RunSession.CurrentState, "失败进入结算");
            Assert.AreEqual("失败", RunSession.LastSettlement.Result);
            StringAssert.Contains("阵亡", RunSession.LastSettlement.Reason);
        }

        // ---- 结算摘要 ----

        [Test]
        public void Settlement_SummarizesCampaignState()
        {
            // 准备战役状态
            RunSession.SetFoodForTest(12);
            RunSession.SetWealthForTest(60);
            RunSession.SetMaterialsForTest(9);
            RunSession.SetReputationForTest(30);
            RunSession.AddRelicForTest("R03");
            RunSession.TryBuildBuilding("B01");
            int deckCount = RunSession.CampaignDeck.Count;

            // 构造失败结算
            RunSession.EnterDefeatState();
            RunSession.EnterSettlement(false, "主角阵亡");
            var s = RunSession.LastSettlement;

            Assert.IsNotNull(s);
            StringAssert.Contains("12", s.Resources, "资源含粮食");
            StringAssert.Contains("60", s.Resources, "资源含财富");
            Assert.AreEqual(deckCount + " 张", s.Deck, "牌组摘要");
            Assert.AreEqual("1 座", s.Buildings, "建筑摘要");
            Assert.AreEqual("1 件", s.Relics, "遗物摘要");
            Assert.AreEqual(42, s.Seed, "种子");
        }

        [Test]
        public void Reset_ClearsSettlement()
        {
            RunSession.EnterDefeatState();
            RunSession.EnterSettlement(false, "主角阵亡");
            Assert.IsNotNull(RunSession.LastSettlement);

            RunSession.Reset();

            Assert.IsNull(RunSession.LastSettlement, "Reset 清结算摘要");
        }

        // ---- 辅助 ----

        private static RegionMapNode WalkToBossNode()
        {
            int guard = 0;
            while (RegionMap.CurrentLayer < 3 && guard++ < 20)
            {
                var reachable = RegionMap.ReachableNext();
                if (reachable.Count == 0) return null;
                RunSession.TryMoveToNode(reachable[0]);
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

        private static RegionMapNode WalkToFirstCombat()
        {
            foreach (int idx in RegionMap.ReachableNext())
            {
                if (RegionMap.Nodes[idx].Type == NodeType.Combat)
                {
                    RunSession.TryMoveToNode(idx);
                    return RegionMap.Nodes[idx];
                }
            }

            return null;
        }
    }
}
