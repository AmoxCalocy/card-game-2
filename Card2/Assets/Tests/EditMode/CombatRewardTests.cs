using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    /// <summary>战斗胜利资源入账（A2-20）：奖励生成→钳制入账→结算记录→防重复。</summary>
    public class CombatRewardTests
    {
        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.EnterTestPage(GameState.Combat); // 起始：粮 14 / 财 30 / 建材 0（测试入口初始化资源）
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

        // ---- 入账数值 ----

        [Test]
        public void Victory_Normal_ResourcesCredited()
        {
            RunSession.EnterTestPage(GameState.Combat);
            int wealth = RunSession.Wealth;
            int food = RunSession.Food;

            KillAllEnemies();

            Assert.AreEqual(CombatPhase.Victory, CombatManager.Phase);
            Assert.AreEqual(wealth + 5, RunSession.Wealth, "普通遭遇胜利财富 +5");
            Assert.AreEqual(food + 2, RunSession.Food, "普通遭遇胜利粮食 +2");
            Assert.AreEqual(0, RunSession.Materials, "普通遭遇无建材奖励");
        }

        [Test]
        public void GenerateAndApply_Elite_Credited()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Elite, "草原");
            RunSession.ApplyCombatRewards();

            Assert.AreEqual(30 + 10, RunSession.Wealth, "精英财富 +10");
            Assert.AreEqual(14 + 3, RunSession.Food, "精英粮食 +3");
            Assert.AreEqual(0 + 2, RunSession.Materials, "精英建材 +2");
        }

        [Test]
        public void GenerateAndApply_BossPlains_Credited()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Boss, "草原");
            RunSession.ApplyCombatRewards();

            Assert.AreEqual(30 + 20, RunSession.Wealth, "首领（草原）财富 +20");
            Assert.AreEqual(14 + 5, RunSession.Food, "首领（草原）粮食 +5");
            Assert.AreEqual(0 + 3, RunSession.Materials, "首领（草原）建材 +3");
        }

        // ---- 上限钳制 ----

        [Test]
        public void Rewards_ClampedAtCap()
        {
            RunSession.SetWealthForTest(GameStartParameters.MaxWealth - 15);
            RunSession.SetFoodForTest(GameStartParameters.MaxFood - 2);
            RunSession.SetMaterialsForTest(GameStartParameters.MaxBuildingMaterials - 1);

            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Boss, "草原");
            RunSession.ApplyCombatRewards();

            Assert.AreEqual(GameStartParameters.MaxWealth, RunSession.Wealth, "财富不超上限");
            Assert.AreEqual(GameStartParameters.MaxFood, RunSession.Food, "粮食不超上限");
            Assert.AreEqual(GameStartParameters.MaxBuildingMaterials, RunSession.Materials, "建材不超上限");
        }

        // ---- 跳过卡牌不丢资源 ----

        [Test]
        public void SkipCardReward_KeepsResources()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Normal, "草原");
            RunSession.ApplyCombatRewards();
            int wealth = RunSession.Wealth;
            int food = RunSession.Food;

            RewardResolver.SkipReward();

            Assert.IsFalse(RewardResolver.HasPendingRewards, "跳过清空卡牌选项");
            Assert.AreEqual(0, RewardResolver.PendingWealth, "Pending 已清零");
            Assert.AreEqual(wealth, RunSession.Wealth, "跳过卡牌不丢财富");
            Assert.AreEqual(food, RunSession.Food, "跳过卡牌不丢粮食");
        }

        // ---- 防重复入账 ----

        [Test]
        public void ApplyCombatRewards_SecondCall_NoDoubleCredit()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Normal, "草原");
            RunSession.ApplyCombatRewards();
            int wealth = RunSession.Wealth;
            int food = RunSession.Food;

            string second = RunSession.ApplyCombatRewards();

            Assert.AreEqual("无资源奖励", second, "第二次调用无资源可入账");
            Assert.AreEqual(wealth, RunSession.Wealth, "不重复加财富");
            Assert.AreEqual(food, RunSession.Food, "不重复加粮食");
        }

        [Test]
        public void Victory_RecordsRewardOnce()
        {
            RunSession.EnterTestPage(GameState.Combat);
            KillAllEnemies();

            int count = 0;
            foreach (var r in RunSession.Records)
                if (r.Source == "战斗奖励") count++;
            Assert.AreEqual(1, count, "一场胜利只记录一次资源入账");
        }

        // ---- 结算记录 ----

        [Test]
        public void RewardRecord_ContainsSourceAndTotal()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Normal, "草原");
            RunSession.ApplyCombatRewards();

            Assert.IsTrue(RunSession.LastResolution.HasValue);
            Assert.AreEqual("战斗奖励", RunSession.LastResolution.Value.Source);
            StringAssert.Contains("财富 +5", RunSession.LastResolution.Value.Result, "记录变化量");
            StringAssert.Contains("当前 35", RunSession.LastResolution.Value.Result, "记录变化后总量");
        }

        // ---- 事件战斗叠加 ----

        [Test]
        public void EventCombatVictory_AddsNormalAndEventRewards()
        {
            // SetUp 已进入 Combat 测试页；Reset 强制回主菜单（与 UI 返回路径一致），再进事件页
            RunSession.Reset();
            RunSession.EnterTestPage(GameState.Event);
            Assert.IsTrue(RunSession.StartEvent("E03"), "进入 E03");
            int wealthBefore = RunSession.Wealth;

            string result = RunSession.ChooseEventOption(2);
            StringAssert.Contains("战斗开始", result);
            KillAllEnemies();

            Assert.AreEqual(CombatPhase.Victory, CombatManager.Phase);
            Assert.AreEqual(wealthBefore + 10, RunSession.Wealth, "事件奖励 5 + 普通遭遇奖励 5");
            Assert.IsTrue(!string.IsNullOrEmpty(RunSession.LastCombatRewardText), "结算页可显示资源奖励");
        }

        // ---- Reset 清理 ----

        [Test]
        public void Reset_ClearsCombatRewardState()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Elite, "草原");
            RunSession.ApplyCombatRewards();
            Assert.IsFalse(string.IsNullOrEmpty(RunSession.LastCombatRewardText));

            RunSession.Reset();

            Assert.IsNull(RunSession.LastCombatRewardText, "Reset 清空结算文本");
            Assert.AreEqual(0, RunSession.Wealth);
            Assert.AreEqual(0, RunSession.Food);
            Assert.AreEqual(0, RunSession.Materials);
            Assert.AreEqual(0, RewardResolver.PendingWealth);
        }

        private static void KillAllEnemies()
        {
            foreach (var e in CombatManager.EnemyTeam)
                e.TakeDamage(e.CurrentHp + e.Armor);
            CombatManager.CheckEndCondition();
        }
    }
}
