using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    /// <summary>8 件 MVP 遗物（A2-22，配置表 §7）：获得、触发时点、战斗/区域级一次性。</summary>
    public class RelicTests
    {
        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.Relics.Clear(); // Reset 保留遗物（跨测试入口），测试间需显式隔离
            RunSession.EnterTestPage(GameState.Combat);
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
        public void Catalog_Has8Relics_IdsUnique()
        {
            Assert.AreEqual(8, RelicCatalog.All.Length);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var r in RelicCatalog.All)
            {
                Assert.IsTrue(ids.Add(r.Id), "遗物 ID 唯一：" + r.Id);
                Assert.IsFalse(string.IsNullOrEmpty(r.DisplayName));
                Assert.IsFalse(string.IsNullOrEmpty(r.EffectText));
            }
        }

        [Test]
        public void Catalog_OnlyR08BossOnly()
        {
            foreach (var r in RelicCatalog.All)
            {
                Assert.AreEqual(r.Id == "R08", r.BossOnly, r.Id + " BossOnly 标志");
            }
        }

        [Test]
        public void AddRelic_Deduplicates()
        {
            RunSession.AddRelicForTest("R01");
            RunSession.AddRelicForTest("R01");

            Assert.AreEqual(1, RunSession.Relics.Count);
            Assert.IsTrue(RunSession.HasRelic("R01"));
        }

        // ---- R02 铁锅：每区域首次进营地粮食 +4 ----

        [Test]
        public void R02_EnterCamp_FirstTimeGrantsFood4()
        {
            RunSession.AddRelicForTest("R02");
            int foodBefore = RunSession.Food;

            string result = RunSession.EnterCampNode();

            StringAssert.Contains("铁锅：粮食 +4", result);
            Assert.AreEqual(foodBefore + 4, RunSession.Food);

            RunSession.EnterCampNode();
            Assert.AreEqual(foodBefore + 4, RunSession.Food, "同区域第二次进营地不再给粮");
        }

        [Test]
        public void R02_WithoutRelic_NoFoodBonus()
        {
            int foodBefore = RunSession.Food;

            RunSession.EnterCampNode();

            Assert.AreEqual(foodBefore, RunSession.Food, "无铁锅不给粮");
        }

        [Test]
        public void R02_AndB01_StackTo8()
        {
            RunSession.SetMaterialsForTest(10);
            RunSession.TryBuildBuilding("B01");
            RunSession.AddRelicForTest("R02");
            int foodBefore = RunSession.Food;

            RunSession.EnterCampNode();

            Assert.AreEqual(foodBefore + 8, RunSession.Food, "铁锅 + 储粮帐篷叠加 +8");
        }

        // ---- R03 琥珀护符：每场战斗开始全队 +3 护甲 ----

        [Test]
        public void R03_CombatStart_AllTeamGain3Armor()
        {
            RunSession.AddRelicForTest("R03");
            RunSession.RelaunchTestCombat();

            foreach (var u in CombatManager.PlayerTeam)
            {
                Assert.AreEqual(3, u.Armor, u.DisplayName + " 战斗开始获得 3 护甲");
            }
        }

        [Test]
        public void R03_WithoutRelic_NoArmor()
        {
            RunSession.RelaunchTestCombat();

            foreach (var u in CombatManager.PlayerTeam)
            {
                Assert.AreEqual(0, u.Armor, "无琥珀护符无初始护甲");
            }
        }

        // ---- R04 医师药箱：每区域首次进营地移除疾病/疲劳 ----

        [Test]
        public void R04_ClinicRemovesDisease()
        {
            RunSession.AddRelicForTest("R04");
            RunSession.SetPlayerDiseaseForTest(1);

            Assert.IsTrue(RunSession.RelicClinicAvailable);
            string result = RunSession.RelicClinic("PLAYER", true);

            StringAssert.Contains("医师药箱", result);
            Assert.AreEqual(0, RunSession.PlayerDisease);
            Assert.IsFalse(RunSession.RelicClinicAvailable, "本区域已使用");

            string second = RunSession.RelicClinic("PLAYER", true);
            StringAssert.Contains("已使用", second);
        }

        // ---- R05 商队印记：每区域首次事件财富 +5 ----

        [Test]
        public void R05_EventWealth_Extra5FirstTime()
        {
            RunSession.Reset();
            RunSession.EnterTestPage(GameState.Event);
            RunSession.AddRelicForTest("R05");

            Assert.IsTrue(RunSession.StartEvent("E05"), "进入 E05");
            int wealthBefore = RunSession.Wealth;
            string result = RunSession.ChooseEventOption(1); // 取货：财富 +12

            StringAssert.Contains("财富 +17", result, "商队印记 +5（12+5）");
            Assert.AreEqual(wealthBefore + 17, RunSession.Wealth);
        }

        [Test]
        public void R05_AndB05_StackTo10()
        {
            RunSession.Reset();
            RunSession.EnterTestPage(GameState.Event);
            RunSession.SetWealthForTest(999);
            RunSession.SetMaterialsForTest(99);
            RunSession.SetReputationForTest(100);
            RunSession.MarkGrasslandBossDefeated();
            RunSession.TryBuildBuilding("B05");
            RunSession.AddRelicForTest("R05");

            Assert.IsTrue(RunSession.StartEvent("E05"), "进入 E05");
            int wealthBefore = RunSession.Wealth;
            string result = RunSession.ChooseEventOption(1); // 取货：财富 +12

            StringAssert.Contains("财富 +22", result, "商队印记 +5 + 市集 +5（12+10）");
            Assert.AreEqual(wealthBefore + 22, RunSession.Wealth);
        }

        // ---- R06 狼牙坠饰：每场战斗首次普通伤害 +3 ----

        [Test]
        public void R06_FirstPlayerDamage_Plus3()
        {
            RunSession.AddRelicForTest("R06");
            RunSession.RelaunchTestCombat();
            var enemy = CombatManager.EnemyTeam[0];

            string first = CombatResolver.ApplyDamage(enemy, 6);
            StringAssert.Contains("受到 9 点伤害", first, "首次伤害 +3");
            Assert.IsTrue(CombatManager.RelicWolfUsedThisCombat);

            int hp = enemy.CurrentHp;
            CombatResolver.ApplyDamage(enemy, 6);
            Assert.AreEqual(hp - 6, enemy.CurrentHp, "第二次伤害无加成");
        }

        [Test]
        public void R06_WithoutRelic_NoBonus()
        {
            RunSession.RelaunchTestCombat();
            var enemy = CombatManager.EnemyTeam[0];

            CombatResolver.ApplyDamage(enemy, 6);

            Assert.AreEqual(6, enemy.MaxHp - enemy.CurrentHp, "无狼牙坠饰伤害 6");
        }

        // ---- R07 指挥旗：每场战斗首张战术卡费用 -1 ----

        [Test]
        public void R07_FirstTacticalCard_CostReduced1()
        {
            RunSession.AddRelicForTest("R07");
            RunSession.RelaunchTestCombat();
            // 固定手牌为战术卡 C25（1 费）
            CombatManager.Deck.Hand.Clear();
            CombatManager.Deck.Hand.Add("C25");

            int energyBefore = CombatManager.Energy;
            string result = CombatResolver.PlayCard(0);

            Assert.AreEqual(energyBefore - 0, CombatManager.Energy, "C25 1 费被减为 0 费");
            StringAssert.Contains("0费", result);
            Assert.IsTrue(CombatManager.RelicFlagUsedThisCombat);
        }

        [Test]
        public void R07_SecondTacticalCard_NoReduction()
        {
            RunSession.AddRelicForTest("R07");
            RunSession.RelaunchTestCombat();
            CombatManager.Deck.Hand.Clear();
            CombatManager.Deck.Hand.Add("C25");
            CombatManager.Deck.Hand.Add("C26");

            CombatResolver.PlayCard(0); // 首次触发，0 费

            int energyBefore = CombatManager.Energy;
            CombatResolver.PlayCard(0); // 第二张战术卡无减费（C26 默认 1 费？以目录为准）
            Assert.AreEqual(energyBefore - 1, CombatManager.Energy, "第二张战术卡正常扣费");
        }

        [Test]
        public void R07_NonTacticalCard_NoReduction()
        {
            RunSession.AddRelicForTest("R07");
            RunSession.RelaunchTestCombat();
            CombatManager.Deck.Hand.Clear();
            CombatManager.Deck.Hand.Add("C01"); // 攻击卡，1 费

            int energyBefore = CombatManager.Energy;
            CombatResolver.PlayCard(0);

            Assert.AreEqual(energyBefore - 1, CombatManager.Energy, "非战术卡不减费");
            Assert.IsFalse(CombatManager.RelicFlagUsedThisCombat, "非战术卡不消耗触发标记");
        }

        // ---- R08 不熄灯：首领战开始队伍 +2 士气 ----

        [Test]
        public void R08_BossCombat_Grants2Morale()
        {
            RunSession.AddRelicForTest("R08");
            // 从第 0 组（普通）翻页 2 次到第 3 组（草原首领），再重开战斗
            RunSession.NextEncounter();
            RunSession.NextEncounter();
            RunSession.RelaunchTestCombat();

            Assert.AreEqual(EncounterConfig.EncounterType.Boss, CombatManager.CurrentEncounterType, "当前为首领遭遇");
            Assert.AreEqual(2, CombatManager.Morale, "首领战开始士气 +2");
        }

        [Test]
        public void R08_NormalCombat_NoMorale()
        {
            RunSession.AddRelicForTest("R08");
            RunSession.RelaunchTestCombat(); // 默认第 0 组普通遭遇

            Assert.AreEqual(0, CombatManager.Morale, "普通战斗不触发不熄灯");
        }

        // ---- 战斗级标记随战斗结束重置 ----

        [Test]
        public void CombatRelicFlags_ResetOnEnd()
        {
            RunSession.AddRelicForTest("R06");
            RunSession.RelaunchTestCombat();
            CombatResolver.ApplyDamage(CombatManager.EnemyTeam[0], 1);
            Assert.IsTrue(CombatManager.RelicWolfUsedThisCombat);

            CombatManager.End();

            Assert.IsFalse(CombatManager.RelicWolfUsedThisCombat, "战斗结束标记重置");
            Assert.IsFalse(CombatManager.RelicFlagUsedThisCombat);
        }

        // ---- 遗物奖励生成与领取（配置表 §5.1）----

        [Test]
        public void Reward_Elite_OffersRelicOptions()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Elite, "草原");

            int relicCount = 0;
            foreach (var opt in RewardResolver.PendingOptions)
                if (!string.IsNullOrEmpty(opt.RelicId)) relicCount++;
            Assert.AreEqual(2, relicCount, "精英展示 2 件遗物");
        }

        [Test]
        public void Reward_Boss_Offers3Relics_NoBossOnlyInElite()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Boss, "草原");
            int bossRelicCount = 0;
            foreach (var opt in RewardResolver.PendingOptions)
                if (!string.IsNullOrEmpty(opt.RelicId)) bossRelicCount++;
            Assert.AreEqual(3, bossRelicCount, "首领展示 3 件遗物");

            // 精英奖励不出现 BossOnly（R08）
            RewardResolver.Clear();
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Elite, "草原");
            foreach (var opt in RewardResolver.PendingOptions)
                Assert.IsTrue(string.IsNullOrEmpty(opt.RelicId) || opt.RelicId != "R08", "精英奖励不含 R08");
        }

        [Test]
        public void Reward_ClaimRelic_AddsToRunSession()
        {
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Boss, "草原");
            int relicIdx = -1;
            for (int i = 0; i < RewardResolver.PendingOptions.Count; i++)
            {
                if (!string.IsNullOrEmpty(RewardResolver.PendingOptions[i].RelicId)) { relicIdx = i; break; }
            }

            Assert.GreaterOrEqual(relicIdx, 0, "存在遗物选项");
            string claimed = RewardResolver.ClaimRelic(relicIdx);

            Assert.IsNotNull(claimed);
            Assert.IsTrue(RunSession.HasRelic(claimed), "领取后持有遗物");
            Assert.IsFalse(RewardResolver.HasPendingRewards, "领取后清空选项");
        }

        [Test]
        public void Reward_OwnedRelic_NotOfferedAgain()
        {
            RunSession.AddRelicForTest("R01");
            RewardResolver.GenerateRewards(EncounterConfig.EncounterType.Boss, "草原");

            foreach (var opt in RewardResolver.PendingOptions)
                Assert.IsTrue(string.IsNullOrEmpty(opt.RelicId) || opt.RelicId != "R01", "已持有遗物不重复出现");
        }

        // ---- Reset 保留遗物（跨测试入口）----

        [Test]
        public void Reset_KeepsRelics_NewGameClears()
        {
            RunSession.AddRelicForTest("R01");

            RunSession.Reset();

            Assert.AreEqual(1, RunSession.Relics.Count, "Reset 保留遗物（测试入口共享战役进度）");

            RunSession.StartNewGame(1);

            Assert.AreEqual(0, RunSession.Relics.Count, "新游戏清空遗物");
        }
    }
}
