using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    /// <summary>营地与一阶建筑（A2-21）：目录完整性、建造校验、建筑效果。</summary>
    public class BuildingTests
    {
        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.EnterTestPage(GameState.Camp); // 起始：粮 14 / 财 30 / 建材 0 / 声望 0
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

        // ---- 目录完整性 ----

        [Test]
        public void Catalog_Has5Buildings_IdsUnique()
        {
            Assert.AreEqual(5, BuildingCatalog.All.Length);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var b in BuildingCatalog.All)
            {
                Assert.IsTrue(ids.Add(b.Id), "建筑 ID 唯一：" + b.Id);
                Assert.IsFalse(string.IsNullOrEmpty(b.DisplayName));
                Assert.IsFalse(string.IsNullOrEmpty(b.EffectText));
                Assert.GreaterOrEqual(b.CostWealth, 0);
                Assert.GreaterOrEqual(b.CostMaterial, 0);
                Assert.GreaterOrEqual(b.CostReputation, 0);
            }
        }

        [Test]
        public void Catalog_TownBuildingsRequireBoss()
        {
            foreach (var b in BuildingCatalog.All)
            {
                if (b.Type == BuildingType.Town)
                    Assert.IsTrue(b.RequiresBossDefeated, b.Id + " 城镇建筑需首领前置");
            }
        }

        // ---- 建造校验 ----

        [Test]
        public void Build_CampBuilding_CostsDeductedAndRecorded()
        {
            RunSession.SetMaterialsForTest(10);
            int materialBefore = RunSession.Materials;
            int recordBefore = RunSession.Records.Count;

            string result = RunSession.TryBuildBuilding("B01");

            StringAssert.Contains("建成", result);
            Assert.IsTrue(RunSession.HasBuilding("B01"));
            Assert.AreEqual(materialBefore - 3, RunSession.Materials, "建材扣除 3");
            Assert.Greater(RunSession.Records.Count, recordBefore, "写入结算记录");
            Assert.AreEqual("建筑", RunSession.LastResolution.Value.Source);
        }

        [Test]
        public void Build_NotEnoughMaterial_BlockedResourcesUnchanged()
        {
            RunSession.SetMaterialsForTest(2); // B01 需要 3 建材
            int materialBefore = RunSession.Materials;

            string result = RunSession.TryBuildBuilding("B01");

            StringAssert.Contains("建造失败", result);
            Assert.IsFalse(RunSession.HasBuilding("B01"));
            Assert.AreEqual(materialBefore, RunSession.Materials, "拒绝后建材不变");
        }

        [Test]
        public void Build_NotEnoughWealth_Blocked()
        {
            RunSession.SetWealthForTest(10); // B02 需要 20 财 + 3 建材
            RunSession.SetMaterialsForTest(10);

            string result = RunSession.TryBuildBuilding("B02");

            StringAssert.Contains("建造失败", result);
            Assert.IsFalse(RunSession.HasBuilding("B02"));
            Assert.AreEqual(10, RunSession.Wealth, "拒绝后财富不变");
        }

        [Test]
        public void Build_TownBuilding_WithoutBoss_Blocked()
        {
            RunSession.SetWealthForTest(999);
            RunSession.SetMaterialsForTest(99);
            RunSession.SetReputationForTest(100);

            string result = RunSession.TryBuildBuilding("B03");

            StringAssert.Contains("首领", result);
            Assert.IsFalse(RunSession.HasBuilding("B03"));
        }

        [Test]
        public void Build_SameBuildingTwice_Rejected()
        {
            RunSession.SetMaterialsForTest(10);
            RunSession.TryBuildBuilding("B01");
            int materials = RunSession.Materials;

            string second = RunSession.TryBuildBuilding("B01");

            StringAssert.Contains("已建造", second);
            Assert.AreEqual(materials, RunSession.Materials, "重复建造不重复扣费");
        }

        [Test]
        public void Build_BossDefeated_TownBuildingBuildable()
        {
            RunSession.SetWealthForTest(999);
            RunSession.SetMaterialsForTest(99);
            RunSession.SetReputationForTest(100);
            RunSession.MarkGrasslandBossDefeated();

            string result = RunSession.TryBuildBuilding("B03");

            StringAssert.Contains("建成", result);
            Assert.IsTrue(RunSession.HasBuilding("B03"));
            Assert.AreEqual(999 - 30, RunSession.Wealth);
            Assert.AreEqual(99 - 5, RunSession.Materials);
            Assert.AreEqual(100 - 5, RunSession.Reputation);
        }

        // ---- 建筑效果 ----

        [Test]
        public void B01_EnterCamp_FirstTimeGrantsFood4()
        {
            RunSession.SetMaterialsForTest(10);
            RunSession.TryBuildBuilding("B01");
            int foodBefore = RunSession.Food;

            string result = RunSession.EnterCampNode();

            StringAssert.Contains("粮食 +4", result);
            Assert.AreEqual(foodBefore + 4, RunSession.Food);

            // 同区域第二次进入不再给
            RunSession.EnterCampNode();
            Assert.AreEqual(foodBefore + 4, RunSession.Food, "每区域首次进入营地才给粮");
        }

        [Test]
        public void EnterCamp_ReducesRiskBy2()
        {
            RunSession.SetRiskForTest(7);

            RunSession.EnterCampNode();

            Assert.AreEqual(5, RunSession.Risk, "营地风险 -2");
        }

        [Test]
        public void CampfireRest_RemovesPlayerFatigue()
        {
            RunSession.SetPlayerFatigueForTest(2);

            string result = RunSession.CampfireRest("PLAYER");

            StringAssert.Contains("主角", result);
            Assert.AreEqual(1, RunSession.PlayerFatigue);
        }

        [Test]
        public void CampfireRest_NoFatigue_Rejected()
        {
            string result = RunSession.CampfireRest("PLAYER");

            StringAssert.Contains("没有疲劳", result);
        }

        [Test]
        public void B02_CampClinic_RemovesDisease()
        {
            RunSession.SetMaterialsForTest(10);
            RunSession.SetWealthForTest(50);
            RunSession.TryBuildBuilding("B02");
            RunSession.SetPlayerDiseaseForTest(1);

            string result = RunSession.CampClinic("PLAYER");

            StringAssert.Contains("移除 1 层疾病", result);
            Assert.AreEqual(0, RunSession.PlayerDisease);
        }

        [Test]
        public void B03_Build_GrantsFreeUpgrade()
        {
            RunSession.SetWealthForTest(999);
            RunSession.SetMaterialsForTest(99);
            RunSession.SetReputationForTest(100);
            RunSession.MarkGrasslandBossDefeated();
            RunSession.TryBuildBuilding("B03");

            Assert.IsTrue(RunSession.FreeUpgradePending, "建成后可免费升级");

            // 升级起始牌组里的 C01
            string result = RunSession.FreeUpgradeCard("C01");

            StringAssert.Contains("已升级", result);
            Assert.IsFalse(RunSession.FreeUpgradePending, "升级后清除待用标记");
        }

        [Test]
        public void B03_B04_AddCardsToRewardPool()
        {
            RunSession.SetWealthForTest(999);
            RunSession.SetMaterialsForTest(99);
            RunSession.SetReputationForTest(100);
            RunSession.MarkGrasslandBossDefeated();
            RunSession.TryBuildBuilding("B03");
            RunSession.TryBuildBuilding("B04");

            // 建筑卡跨区域进池：C04/C11 本属草原池，建 B03 后进入密林池；
            // C34/C37/C40 本属密林池，建 B04 后进入草原池
            Assert.IsTrue(RewardResolver.RewardPoolContains("密林", "C04"), "铁匠铺卡 C04 进密林池");
            Assert.IsTrue(RewardResolver.RewardPoolContains("密林", "C11"), "铁匠铺卡 C11 进密林池");
            Assert.IsTrue(RewardResolver.RewardPoolContains("草原", "C34"), "医馆卡 C34 进草原池");
            Assert.IsTrue(RewardResolver.RewardPoolContains("草原", "C37"), "医馆卡 C37 进草原池");
            Assert.IsTrue(RewardResolver.RewardPoolContains("草原", "C40"), "医馆卡 C40 进草原池");
        }

        [Test]
        public void RewardPool_WithoutBuildings_ExcludesBuildingCards()
        {
            Assert.IsFalse(RewardResolver.RewardPoolContains("密林", "C04"), "未建铁匠铺时 C04 不在密林池");
            Assert.IsFalse(RewardResolver.RewardPoolContains("草原", "C34"), "未建医馆时 C34 不在草原池");
        }

        [Test]
        public void B05_EventWealthGain_Extra5FirstTime()
        {
            // SetUp 在 Camp 状态（Camp→Event 非法转移），Reset 后从主菜单进事件页
            RunSession.Reset();
            RunSession.EnterTestPage(GameState.Event);
            RunSession.SetWealthForTest(999);
            RunSession.SetMaterialsForTest(99);
            RunSession.SetReputationForTest(100);
            RunSession.MarkGrasslandBossDefeated();
            RunSession.TryBuildBuilding("B05");

            // E05 取货：财富 +12
            Assert.IsTrue(RunSession.StartEvent("E05"), "进入 E05");
            int wealthBefore = RunSession.Wealth;
            string result = RunSession.ChooseEventOption(1);

            StringAssert.Contains("财富 +17", result, "B05 加成 5（12+5）");
            Assert.AreEqual(wealthBefore + 17, RunSession.Wealth);
        }

        // ---- Reset 清理 ----

        [Test]
        public void Reset_ClearsBuildings()
        {
            RunSession.SetMaterialsForTest(10);
            RunSession.TryBuildBuilding("B01");
            RunSession.MarkGrasslandBossDefeated();

            RunSession.Reset();

            Assert.AreEqual(0, RunSession.BuiltBuildings.Count);
            Assert.IsTrue(RunSession.GrasslandBossDefeated, "首领击败标记跨测试入口保留（新游戏 StartNewGame 时清零）");
        }
    }
}
