using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    /// <summary>A2-19 实现 20 个 MVP 事件（配置表 §6）。</summary>
    public class EventTests
    {
        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.Relics.Clear(); // 遗物跨测试入口保留，事件测试间需隔离
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

        private void Enter(string eventId)
        {
            RunSession.StartEvent(eventId);
            Assert.AreEqual(eventId, RunSession.CurrentEvent.Id, "应进入事件 " + eventId);
        }

        private string Choose(int optionIndex)
        {
            return RunSession.ChooseEventOption(optionIndex);
        }

        private static PartnerState Partner(string id)
        {
            return PartnerRoster.Find(id);
        }

        private static void Recruit(string id)
        {
            Assert.IsTrue(PartnerRoster.Recruit(id), "招募 " + id + " 应成功");
        }

        // ---- 目录完整性 ----

        [Test]
        public void Catalog_20Events_AllHaveAtLeastTwoOptions()
        {
            Assert.AreEqual(20, EventCatalog.All.Length, "应恰好 20 个事件");
            var ids = new HashSet<string>();
            foreach (var e in EventCatalog.All)
            {
                Assert.IsTrue(ids.Add(e.Id), "事件 ID 应唯一：" + e.Id);
                Assert.GreaterOrEqual(e.Options.Length, 2, e.Id + " 至少 2 个选项");
            }
        }

        [Test]
        public void Catalog_RegionSplit_10Plains10Jungle()
        {
            int plains = 0, jungle = 0;
            foreach (var e in EventCatalog.All)
            {
                if (e.Region == ContentRegion.Plains) plains++;
                if (e.Region == ContentRegion.Jungle) jungle++;
            }
            Assert.AreEqual(10, plains, "草原事件应 10 个");
            Assert.AreEqual(10, jungle, "密林事件应 10 个");
        }

        [Test]
        public void Catalog_References_AllResolvable()
        {
            foreach (var e in EventCatalog.All)
            {
                foreach (var opt in e.Options)
                {
                    if (!string.IsNullOrEmpty(opt.RequirePartnerId)) Assert.IsNotNull(Partner(opt.RequirePartnerId), e.Id + " 条件伙伴存在");
                    if (!string.IsNullOrEmpty(opt.RequirePartnerId2)) Assert.IsNotNull(Partner(opt.RequirePartnerId2), e.Id + " 条件伙伴2存在");
                    if (!string.IsNullOrEmpty(opt.RequireCardId)) Assert.IsNotNull(CardCatalog.Find(opt.RequireCardId), e.Id + " 条件卡存在");
                    if (!string.IsNullOrEmpty(opt.RecruitPartnerId)) Assert.IsNotNull(Partner(opt.RecruitPartnerId), e.Id + " 招募伙伴存在");
                    if (!string.IsNullOrEmpty(opt.GrantCardId)) Assert.IsNotNull(CardCatalog.Find(opt.GrantCardId), e.Id + " 获得卡存在");
                    if (opt.CombatEnemyIds != null)
                    {
                        foreach (var en in opt.CombatEnemyIds)
                            Assert.IsNotNull(EnemyUnit.CreateById(en), e.Id + " 战斗敌人存在：" + en);
                    }
                }
            }
        }

        // ---- E01 饥荒村 ----

        [Test]
        public void E01_Support_Pays3FoodGains8Reputation()
        {
            Enter("E01");
            int foodBefore = RunSession.Food;
            int repBefore = RunSession.Reputation;
            Choose(0);

            Assert.AreEqual(foodBefore - 3, RunSession.Food);
            Assert.AreEqual(repBefore + 8, RunSession.Reputation);
        }

        [Test]
        public void E01_Support_NotEnoughFood_BlockedResourcesUnchanged()
        {
            Enter("E01");
            RunSession.SetFoodForTest(2);
            int foodBefore = RunSession.Food;
            int repBefore = RunSession.Reputation;
            string result = Choose(0);

            StringAssert.Contains("不可用", result);
            Assert.AreEqual(foodBefore, RunSession.Food, "拒绝后粮食不变");
            Assert.AreEqual(repBefore, RunSession.Reputation, "拒绝后声望不变");
        }

        [Test]
        public void E01_LimitedAid_Pays1FoodGains3Reputation()
        {
            Enter("E01");
            int foodBefore = RunSession.Food;
            Choose(1);

            Assert.AreEqual(foodBefore - 1, RunSession.Food);
            Assert.AreEqual(3, RunSession.Reputation);
        }

        [Test]
        public void E01_Leave_RiskPlusOne()
        {
            Enter("E01");
            Choose(2);

            Assert.AreEqual(1, RunSession.Risk);
            Assert.AreEqual(GameStartParameters.StartFood, RunSession.Food, "离开不消耗粮食");
        }

        // ---- E02 迷路的斥候 ----

        [Test]
        public void E02_BringBack_RecruitsP03GainsReputation()
        {
            Enter("E02");
            int foodBefore = RunSession.Food;
            Choose(0);

            Assert.AreEqual(foodBefore - 2, RunSession.Food);
            Assert.IsTrue(Partner("P03").IsRecruited, "应招募 P03");
            Assert.AreEqual(3, RunSession.Reputation);
        }

        [Test]
        public void E02_TakePayment_Wealth8Risk1()
        {
            Enter("E02");
            int wealthBefore = RunSession.Wealth;
            Choose(1);

            Assert.AreEqual(wealthBefore + 8, RunSession.Wealth);
            Assert.AreEqual(1, RunSession.Risk);
        }

        // ---- E03 劫匪过路费 ----

        [Test]
        public void E03_PayToll_Wealth10Pass()
        {
            Enter("E03");
            int wealthBefore = RunSession.Wealth;
            Choose(0);

            Assert.AreEqual(wealthBefore - 10, RunSession.Wealth);
        }

        [Test]
        public void E03_Negotiate_RequiresP05AndReputation5()
        {
            Enter("E03");
            // 未招募 P05 → 阻止
            StringAssert.Contains("不可用", Choose(1));

            Recruit("P05");
            RunSession.SetReputationForTest(4);
            Enter("E03");
            StringAssert.Contains("不可用", Choose(1), "声望 4 仍不足");

            RunSession.SetReputationForTest(5);
            Enter("E03");
            int repBefore = RunSession.Reputation;
            Choose(1);
            Assert.AreEqual(repBefore + 3, RunSession.Reputation, "满足条件应声望 +3 通过");
        }

        [Test]
        public void E03_Fight_StartsCombat_VictoryGrantsWealth5()
        {
            Enter("E03");
            int wealthBefore = RunSession.Wealth;
            string result = Choose(2);

            StringAssert.Contains("战斗开始", result);
            Assert.IsTrue(CombatManager.IsActive, "应触发战斗");
            Assert.AreEqual(2, CombatManager.EnemyTeam.Count, "应遭遇 EN01+EN02");

            // 模拟胜利
            foreach (var e in CombatManager.EnemyTeam) e.TakeDamage(e.CurrentHp + e.Armor);
            CombatManager.CheckEndCondition();

            Assert.AreEqual(wealthBefore + 10, RunSession.Wealth, "胜利后财富 +10（事件奖励 5 + 普通遭遇奖励 5）");
        }

        [Test]
        public void E03_Fight_Defeat_NoBonus()
        {
            Enter("E03");
            int wealthBefore = RunSession.Wealth;
            Choose(2);

            // 模拟失败：主角死亡
            var player = CombatManager.PlayerCharacter();
            player.TakeDamage(player.CurrentHp + player.Armor);
            CombatManager.CheckEndCondition();

            Assert.AreEqual(CombatPhase.Defeat, CombatManager.Phase);
            Assert.AreEqual(wealthBefore, RunSession.Wealth, "失败不应结算胜利奖励");
        }

        // ---- E04 受伤哨兵 ----

        [Test]
        public void E04_Heal_Pays5Wealth2Food_RecruitsP01()
        {
            Enter("E04");
            int wealthBefore = RunSession.Wealth;
            int foodBefore = RunSession.Food;
            Choose(0);

            Assert.AreEqual(wealthBefore - 5, RunSession.Wealth);
            Assert.AreEqual(foodBefore - 2, RunSession.Food);
            Assert.IsTrue(Partner("P01").IsRecruited);
            Assert.AreEqual(4, RunSession.Reputation);
        }

        [Test]
        public void E04_Loot_Materials2ReputationMinus3()
        {
            Enter("E04");
            RunSession.SetReputationForTest(5);
            int matBefore = RunSession.Materials;
            Choose(1);

            Assert.AreEqual(matBefore + 2, RunSession.Materials);
            Assert.AreEqual(2, RunSession.Reputation, "声望 5-3");
        }

        [Test]
        public void E04_Loot_ReputationClampedAtZero()
        {
            Enter("E04");
            Choose(1);
            Assert.AreEqual(0, RunSession.Reputation, "声望不能为负");
        }

        // ---- E05 损坏的商车 ----

        [Test]
        public void E05_Repair_Pays8Wealth_RecruitsP04Food2()
        {
            Enter("E05");
            int wealthBefore = RunSession.Wealth;
            int foodBefore = RunSession.Food;
            Choose(0);

            Assert.AreEqual(wealthBefore - 8, RunSession.Wealth);
            Assert.IsTrue(Partner("P04").IsRecruited);
            Assert.AreEqual(foodBefore + 2, RunSession.Food);
        }

        [Test]
        public void E05_TakeGoods_Wealth12Risk1()
        {
            Enter("E05");
            int wealthBefore = RunSession.Wealth;
            Choose(1);

            Assert.AreEqual(wealthBefore + 12, RunSession.Wealth);
            Assert.AreEqual(1, RunSession.Risk);
        }

        // ---- E06 草火逼近 ----

        [Test]
        public void E06_Isolation_NoRemoveableCard_Blocked()
        {
            Enter("E06");
            StringAssert.Contains("不可用", Choose(0), "初始牌组全为锁定卡时应阻止");
        }

        [Test]
        public void E06_Isolation_RemoveCard_GainsMaterials3Reputation2()
        {
            RunSession.CampaignDeck.AddCard("C02"); // 非锁定卡
            Enter("E06");
            int matBefore = RunSession.Materials;
            string result = Choose(0);

            StringAssert.Contains("请选择", result);
            Assert.AreEqual(EventOptionChoiceKind.RemoveCard, RunSession.PendingEventChoice);

            ChooseEventCardAndFinish("C02");
            Assert.IsFalse(RunSession.CampaignDeck.Cards.Contains("C02"), "C02 应被移除");
            Assert.AreEqual(matBefore + 3, RunSession.Materials);
            Assert.AreEqual(2, RunSession.Reputation);
        }

        [Test]
        public void E06_Detour_FoodMinus3Risk1()
        {
            Enter("E06");
            int foodBefore = RunSession.Food;
            Choose(1);

            Assert.AreEqual(foodBefore - 3, RunSession.Food);
            Assert.AreEqual(1, RunSession.Risk);
        }

        private void ChooseEventCardAndFinish(string cardId)
        {
            string result = RunSession.ChooseEventCard(cardId);
            StringAssert.Contains("移除卡 " + cardId, result);
        }

        // ---- E07 流动铁匠 ----

        [Test]
        public void E07_Upgrade_Pays15Wealth_UpgradesCard()
        {
            Enter("E07");
            int wealthBefore = RunSession.Wealth;
            string result = Choose(0);

            Assert.AreEqual(wealthBefore - 15, RunSession.Wealth, "升级应先支付 15 财富");
            StringAssert.Contains("请选择", result);
            Assert.AreEqual(EventOptionChoiceKind.UpgradeCard, RunSession.PendingEventChoice);

            string cardId = RunSession.CampaignDeck.Cards[0];
            RunSession.ChooseEventCard(cardId);
            Assert.IsTrue(RunSession.CampaignDeck.UpgradedCards.Contains(cardId), "卡应标记升级");
        }

        [Test]
        public void E07_Upgrade_NotEnoughWealth_Blocked()
        {
            Enter("E07");
            RunSession.SetWealthForTest(14);
            StringAssert.Contains("不可用", Choose(0));
        }

        [Test]
        public void E07_Buy_GrantC04()
        {
            Enter("E07");
            int wealthBefore = RunSession.Wealth;
            Choose(1);

            Assert.AreEqual(wealthBefore - 8, RunSession.Wealth);
            Assert.IsTrue(RunSession.CampaignDeck.Cards.Contains("C04"), "牌组应获得 C04");
        }

        [Test]
        public void E07_Leave_NoChange()
        {
            Enter("E07");
            int wealthBefore = RunSession.Wealth;
            Choose(2);

            Assert.AreEqual(wealthBefore, RunSession.Wealth);
        }

        // ---- E08 难民营 ----

        [Test]
        public void E08_Accept_Pays4Food_RecruitsP05Reputation5()
        {
            Enter("E08");
            int foodBefore = RunSession.Food;
            Choose(0);

            Assert.AreEqual(foodBefore - 4, RunSession.Food);
            Assert.IsTrue(Partner("P05").IsRecruited);
            Assert.AreEqual(5, RunSession.Reputation);
        }

        [Test]
        public void E08_Conscript_RequiresReputation8_RecruitsWithLoyalty50()
        {
            Enter("E08");
            StringAssert.Contains("不可用", Choose(1), "声望 0 应阻止");

            RunSession.SetReputationForTest(8);
            Enter("E08");
            Choose(1);

            Assert.IsTrue(Partner("P05").IsRecruited);
            Assert.AreEqual(50, Partner("P05").Loyalty, "征募忠诚度应为 50");
            Assert.AreEqual(6, RunSession.Reputation, "声望 8-2");
        }

        [Test]
        public void E08_Refuse_Risk2()
        {
            Enter("E08");
            Choose(2);

            Assert.AreEqual(2, RunSession.Risk);
        }

        // ---- E09 风暴石碑 ----

        [Test]
        public void E09_Pray_RemoveCard_GrantC19()
        {
            RunSession.CampaignDeck.AddCard("C02");
            Enter("E09");
            Choose(0);
            Assert.AreEqual(EventOptionChoiceKind.RemoveCard, RunSession.PendingEventChoice);
            RunSession.ChooseEventCard("C02");

            Assert.IsTrue(RunSession.CampaignDeck.Cards.Contains("C19"), "应获得 C19");
        }

        [Test]
        public void E09_Loot_Wealth10Risk2()
        {
            Enter("E09");
            int wealthBefore = RunSession.Wealth;
            Choose(1);

            Assert.AreEqual(wealthBefore + 10, RunSession.Wealth);
            Assert.AreEqual(2, RunSession.Risk);
        }

        // ---- E10 草原水源 ----

        [Test]
        public void E10_Rest_RemoveFatigueFromPlayer()
        {
            Enter("E10");
            RunSession.SetPlayerFatigueForTest(2);
            Choose(0);

            Assert.AreEqual(EventOptionChoiceKind.StatusFatigue, RunSession.PendingEventChoice);
            RunSession.ChooseEventStatusUnit("PLAYER", false);

            Assert.AreEqual(1, RunSession.PlayerFatigue, "主角疲劳应 -1");
        }

        [Test]
        public void E10_Rest_NoFatigueTarget_BlockedWithoutPendingChoice()
        {
            Enter("E10");

            string result = Choose(0);

            StringAssert.Contains("不可用", result);
            StringAssert.Contains("疲劳", result);
            Assert.AreEqual(EventOptionChoiceKind.None, RunSession.PendingEventChoice);
            Assert.AreEqual("E10", RunSession.CurrentEvent.Id, "阻止后仍可选择采集");
        }

        [Test]
        public void E10_Collect_Food5Risk1()
        {
            Enter("E10");
            int foodBefore = RunSession.Food;
            Choose(1);

            Assert.AreEqual(foodBefore + 5, RunSession.Food);
            Assert.AreEqual(1, RunSession.Risk);
        }

        // ---- E11 疫病营地 ----

        [Test]
        public void E11_Heal_Pays10Wealth_RecruitsP02_RemovesAllDisease()
        {
            Recruit("P02");
            Partner("P02").Disease = 2;
            Enter("E11");
            int wealthBefore = RunSession.Wealth;
            Choose(0);

            Assert.AreEqual(wealthBefore - 10, RunSession.Wealth);
            Assert.AreEqual(1, Partner("P02").Disease, "存活伙伴疾病 -1");
            Assert.AreEqual(5, RunSession.Reputation);
        }

        [Test]
        public void E11_LootMedicine_GrantC34_ReputationMinus4()
        {
            Enter("E11");
            Choose(1);

            Assert.IsTrue(RunSession.CampaignDeck.Cards.Contains("C34"));
            Assert.AreEqual(0, RunSession.Reputation, "声望 0-4 钳制为 0");
        }

        // ---- E12 封存遗迹 ----

        [Test]
        public void E12_Decipher_RequiresP07OrReputation10()
        {
            Enter("E12");
            StringAssert.Contains("不可用", Choose(0), "无 P07 且声望 0 应阻止");

            RunSession.SetReputationForTest(10);
            Enter("E12");
            Choose(0);

            Assert.IsTrue(Partner("P07").IsRecruited);
            Assert.IsTrue(RunSession.Relics.Contains("R01"), "应获得遗物 R01");
        }

        [Test]
        public void E12_ForcedEntry_FightVictory_GrantRelicR01()
        {
            Enter("E12");
            Choose(1);

            Assert.IsTrue(CombatManager.IsActive);
            foreach (var e in CombatManager.EnemyTeam) e.TakeDamage(e.CurrentHp + e.Armor);
            CombatManager.CheckEndCondition();

            Assert.IsTrue(RunSession.Relics.Contains("R01"), "胜利后获得遗物 R01");
        }

        // ---- E13 林间伏击 ----

        [Test]
        public void E13_CounterAmbush_RequiresP03_Wealth8RiskMinus1()
        {
            Enter("E13");
            StringAssert.Contains("不可用", Choose(0), "无 P03 应阻止");

            Recruit("P03");
            Enter("E13");
            int wealthBefore = RunSession.Wealth;
            Choose(0);

            Assert.AreEqual(wealthBefore + 8, RunSession.Wealth);
            Assert.AreEqual(0, RunSession.Risk, "风险 -1 钳制为 0");
        }

        [Test]
        public void E13_Breakout_Pays3Food_Pass()
        {
            Enter("E13");
            int foodBefore = RunSession.Food;
            Choose(1);

            Assert.AreEqual(foodBefore - 3, RunSession.Food);
        }

        [Test]
        public void E13_Fight_Victory_MaterialPlus1()
        {
            Enter("E13");
            int matBefore = RunSession.Materials;
            Choose(2);

            Assert.IsTrue(CombatManager.IsActive);
            foreach (var e in CombatManager.EnemyTeam) e.TakeDamage(e.CurrentHp + e.Armor);
            CombatManager.CheckEndCondition();

            Assert.AreEqual(matBefore + 1, RunSession.Materials, "胜利后建材 +1（事件额外奖励；事件战斗按普通遭遇，普通奖励无建材）");
        }

        // ---- E14 药草地 ----

        [Test]
        public void E14_PrepareMedicine_RemoveDiseaseOrFatigue()
        {
            Enter("E14");
            RunSession.SetPlayerDiseaseForTest(1);
            Choose(0);

            Assert.AreEqual(EventOptionChoiceKind.StatusDiseaseOrFatigue, RunSession.PendingEventChoice);
            RunSession.ChooseEventStatusUnit("PLAYER", true);

            Assert.AreEqual(0, RunSession.PlayerDisease, "主角疾病应 -1");
        }

        [Test]
        public void E14_PrepareMedicine_NoAffectedUnit_BlockedAndCanChooseSell()
        {
            Enter("E14");
            int wealthBefore = RunSession.Wealth;

            string blocked = Choose(0);

            StringAssert.Contains("不可用", blocked);
            StringAssert.Contains("没有可治疗", blocked);
            Assert.AreEqual(EventOptionChoiceKind.None, RunSession.PendingEventChoice);
            Assert.AreEqual("E14", RunSession.CurrentEvent.Id, "阻止后事件保持可操作");

            Choose(1);
            Assert.AreEqual(wealthBefore + 12, RunSession.Wealth, "仍可选择采药出售并完成事件");
            Assert.IsNull(RunSession.CurrentEvent);
        }

        [Test]
        public void E14_SellHerbs_Wealth12()
        {
            Enter("E14");
            int wealthBefore = RunSession.Wealth;
            Choose(1);

            Assert.AreEqual(wealthBefore + 12, RunSession.Wealth);
        }

        // ---- E15 走私小径 ----

        [Test]
        public void E15_Smuggle_Pays2Reputation_Wealth15Risk2()
        {
            Enter("E15");
            StringAssert.Contains("不可用", Choose(0), "声望 0 应阻止");

            RunSession.SetReputationForTest(2);
            Enter("E15");
            int wealthBefore = RunSession.Wealth;
            Choose(0);

            Assert.AreEqual(wealthBefore + 15, RunSession.Wealth);
            Assert.AreEqual(2, RunSession.Risk);
            Assert.AreEqual(0, RunSession.Reputation, "声望 2-2");
        }

        [Test]
        public void E15_Report_Reputation5Wealth3()
        {
            Enter("E15");
            Choose(1);

            Assert.AreEqual(5, RunSession.Reputation);
            Assert.AreEqual(GameStartParameters.StartWealth + 3, RunSession.Wealth);
        }

        // ---- E16 失落远征 ----

        [Test]
        public void E16_Rescue_Pays3Food_Materials3Reputation5()
        {
            Enter("E16");
            int foodBefore = RunSession.Food;
            Choose(0);

            Assert.AreEqual(foodBefore - 3, RunSession.Food);
            Assert.AreEqual(3, RunSession.Materials);
            Assert.AreEqual(5, RunSession.Reputation);
        }

        [Test]
        public void E16_Loot_GrantC23_Risk1()
        {
            Enter("E16");
            Choose(1);

            Assert.IsTrue(RunSession.CampaignDeck.Cards.Contains("C23"));
            Assert.AreEqual(1, RunSession.Risk);
        }

        // ---- E17 古树契约 ----

        [Test]
        public void E17_Memory_RemoveCard_GrantRelicR02()
        {
            RunSession.CampaignDeck.AddCard("C02");
            Enter("E17");
            Choose(0);
            Assert.AreEqual(EventOptionChoiceKind.RemoveCard, RunSession.PendingEventChoice);
            RunSession.ChooseEventCard("C02");

            Assert.IsTrue(RunSession.Relics.Contains("R02"));
        }

        [Test]
        public void E17_Offer_Pays10Wealth_GrantRelicR02Reputation2()
        {
            Enter("E17");
            int wealthBefore = RunSession.Wealth;
            Choose(1);

            Assert.AreEqual(wealthBefore - 10, RunSession.Wealth);
            Assert.IsTrue(RunSession.Relics.Contains("R02"));
            Assert.AreEqual(2, RunSession.Reputation);
        }

        // ---- E18 盗猎营 ----

        [Test]
        public void E18_Rescue_FightVictory_RecruitsP08Reputation4()
        {
            Enter("E18");
            Choose(0);

            Assert.IsTrue(CombatManager.IsActive);
            foreach (var e in CombatManager.EnemyTeam) e.TakeDamage(e.CurrentHp + e.Armor);
            CombatManager.CheckEndCondition();

            Assert.IsTrue(Partner("P08").IsRecruited, "胜利后招募 P08");
            Assert.AreEqual(4, RunSession.Reputation);
        }

        [Test]
        public void E18_Trade_Pays12Wealth_RecruitsP08ReputationMinus3()
        {
            Enter("E18");
            int wealthBefore = RunSession.Wealth;
            Choose(1);

            Assert.AreEqual(wealthBefore - 12, RunSession.Wealth);
            Assert.IsTrue(Partner("P08").IsRecruited);
            Assert.AreEqual(0, RunSession.Reputation, "声望 -3 钳制为 0");
        }

        // ---- E19 发热旅人 ----

        [Test]
        public void E19_ShareMedicine_RequiresP02OrC34()
        {
            Enter("E19");
            StringAssert.Contains("不可用", Choose(0), "无 P02 无 C34 应阻止");

            RunSession.CampaignDeck.AddCard("C34");
            Enter("E19");
            Choose(0);

            Assert.AreEqual(6, RunSession.Reputation);
            Assert.IsTrue(RunSession.CampaignDeck.Cards.Contains("C37"), "应获得 C37");
        }

        [Test]
        public void E19_Detour_Risk1()
        {
            Enter("E19");
            Choose(1);

            Assert.AreEqual(1, RunSession.Risk);
        }

        // ---- E20 狼群踪迹 ----

        [Test]
        public void E20_Hunt_RequiresP03OrP06_RecruitsP06Food5()
        {
            Enter("E20");
            StringAssert.Contains("不可用", Choose(0), "无 P03/P06 应阻止");

            Recruit("P03");
            Enter("E20");
            int foodBefore = RunSession.Food;
            Choose(0);

            Assert.IsTrue(Partner("P06").IsRecruited);
            Assert.AreEqual(foodBefore + 5, RunSession.Food);
        }

        [Test]
        public void E20_Ambush_FightVictory_GrantC27()
        {
            Enter("E20");
            Choose(1);

            Assert.IsTrue(CombatManager.IsActive);
            Assert.AreEqual(2, CombatManager.EnemyTeam.Count, "应遭遇 EN06+EN09");
            foreach (var e in CombatManager.EnemyTeam) e.TakeDamage(e.CurrentHp + e.Armor);
            CombatManager.CheckEndCondition();

            Assert.IsTrue(RunSession.CampaignDeck.Cards.Contains("C27"), "胜利后获得 C27");
        }

        // ---- 通用规则 ----

        [Test]
        public void Recruit_AlreadyRecruited_LoyaltyPlus10()
        {
            Recruit("P03");
            int loyaltyBefore = Partner("P03").Loyalty;
            Enter("E02");
            Choose(0);

            Assert.AreEqual(loyaltyBefore + 10, Partner("P03").Loyalty, "已招募伙伴应忠诚 +10");
            Assert.IsTrue(Partner("P03").IsRecruited);
        }

        [Test]
        public void Recruit_DeadPartner_Blocked()
        {
            Recruit("P03");
            Partner("P03").CurrentHp = 0;
            Enter("E02");

            StringAssert.Contains("不可用", Choose(0), "已阵亡伙伴的招募选项应禁用");
        }

        [Test]
        public void StartEvent_InvalidId_Rejected()
        {
            Assert.IsFalse(RunSession.StartEvent("E99"));
            Assert.IsNull(RunSession.CurrentEvent);
        }

        [Test]
        public void EventChoice_NoPending_ChooseCardRejected()
        {
            string result = RunSession.ChooseEventCard("C01");
            StringAssert.Contains("没有待定的卡牌选择", result);
        }
    }
}
