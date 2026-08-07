using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class CardCatalogTests
    {
        private List<CombatUnit> _players;
        private List<CombatUnit> _enemies;
        private List<string> _deck;

        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.StartNewGame(1);
            ContentRegistry.Clear();
            _players = new List<CombatUnit>
            {
                CombatUnit.CreatePlayer(45, 6),
                CombatUnit.CreateCompanion("P01", "阿德里安(测试)", 42, 5)
            };
            _enemies = new List<CombatUnit>
            {
                EnemyUnit.CreateBandit(),
                EnemyUnit.CreateHound()
            };
            _deck = new List<string>(GameStartParameters.StartingDeck);
        }

        [TearDown]
        public void TearDown()
        {
            CombatManager.End();
            RunSession.Reset();
            ContentRegistry.Clear();
        }

        // ---- 目录完整性 ----

        [Test]
        public void Catalog_Has40Cards()
        {
            Assert.AreEqual(40, CardCatalog.All.Count, "目录应有 40 张卡");
        }

        [Test]
        public void Catalog_AllIdsUnique()
        {
            var seen = new HashSet<string>();
            foreach (var c in CardCatalog.All)
            {
                Assert.IsFalse(seen.Contains(c.Id), "重复 ID：" + c.Id);
                seen.Add(c.Id);
            }
        }

        [Test]
        public void Catalog_FindByValidId_ReturnsCard()
        {
            Assert.IsNotNull(CardCatalog.Find("C01"));
            Assert.IsNotNull(CardCatalog.Find("C40"));
            Assert.AreEqual("剑击", CardCatalog.Find("C01").DisplayName);
        }

        [Test]
        public void Catalog_FindByInvalidId_ReturnsNull()
        {
            Assert.IsNull(CardCatalog.Find("NONEXISTENT"));
        }

        [Test]
        public void Catalog_Exists_TrueForValidId()
        {
            Assert.IsTrue(CardCatalog.Exists("C17"));
            Assert.IsFalse(CardCatalog.Exists("NOPE"));
        }

        [Test]
        public void Catalog_StartingDeckIds_AllResolvable()
        {
            foreach (var id in GameStartParameters.StartingDeck)
                Assert.IsNotNull(CardCatalog.Find(id), "起始牌组 ID " + id + " 应在目录中");
        }

        // ---- 出牌基础流程 ----

        [Test]
        public void PlayCard_FromHand_ConsumesEnergy()
        {
            CombatManager.Init(_players, _enemies, _deck);
            int energyBefore = CombatManager.Energy;
            // 手牌第一张应为 C01 剑击（1费）
            string result = CombatResolver.PlayCard(0);
            Assert.IsTrue(result.Contains("剑击"), "应打出剑击，结果：" + result);
            Assert.AreEqual(energyBefore - 1, CombatManager.Energy, "能量应扣 1");
        }

        [Test]
        public void PlayCard_HandIndexInvalid_ReturnsError()
        {
            CombatManager.Init(_players, _enemies, _deck);
            string result = CombatResolver.PlayCard(999);
            Assert.IsTrue(result.Contains("无效") || result.Contains("索引"), "应报错：" + result);
        }

        [Test]
        public void PlayCard_NotEnoughEnergy_ReturnsError()
        {
            CombatManager.Init(_players, _enemies, _deck);
            // 出完所有能量（每张 1 费 ×3）
            for (int i = 0; i < 3; i++) CombatResolver.PlayCard(0);
            Assert.AreEqual(0, CombatManager.Energy);
            // 第四张应失败
            string result = CombatResolver.PlayCard(0);
            Assert.IsTrue(result.Contains("能量不足"), "应报能量不足：" + result);
        }

        [Test]
        public void PlayCard_RemovesFromHand_MovesToDiscard()
        {
            CombatManager.Init(_players, _enemies, _deck);
            int handBefore = CombatManager.Deck.HandSize;
            int discardBefore = CombatManager.Deck.DiscardPileCount;
            CombatResolver.PlayCard(0);
            Assert.AreEqual(handBefore - 1, CombatManager.Deck.HandSize, "手牌应减少 1");
            Assert.AreEqual(discardBefore + 1, CombatManager.Deck.DiscardPileCount, "弃牌堆应增加 1");
        }

        [Test]
        public void PlayCard_Exhaust_MovesToExhaustZone()
        {
            // C07 突袭是 0 费消耗卡，不在起始牌组中。手动加入手牌。
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.Deck.Hand.Add("C07"); // 0 费 消耗攻击
            int exhaustBefore = CombatManager.Deck.ExhaustedCount;
            // 找出 C07 的索引
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx);
            Assert.AreEqual(exhaustBefore + 1, CombatManager.Deck.ExhaustedCount, "消耗卡应进入消耗区");
        }

        // ---- 攻击卡 ----

        [Test]
        public void C01_SwordStrike_Deals6Damage()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var enemy = CombatManager.EnemyTeam[0];
            int before = enemy.CurrentHp;
            // 手牌中有 C01×4，打出其中一张
            int idx = CombatManager.Deck.Hand.IndexOf("C01");
            CombatResolver.PlayCard(idx);
            Assert.AreEqual(before - 6, enemy.CurrentHp, "应造成 6 伤害");
        }

        [Test]
        public void C05_Sweep_DamagesAllEnemies()
        {
            CombatManager.Init(_players, _enemies, _deck);
            // 手动加入 C05 横扫
            CombatManager.Deck.Hand.Add("C05");
            int idx = CombatManager.Deck.HandSize - 1;
            int hp0 = CombatManager.EnemyTeam[0].CurrentHp;
            int hp1 = CombatManager.EnemyTeam[1].CurrentHp;
            CombatResolver.PlayCard(idx);
            Assert.AreEqual(hp0 - 5, CombatManager.EnemyTeam[0].CurrentHp, "敌人 1 应受 5 伤");
            Assert.AreEqual(hp1 - 5, CombatManager.EnemyTeam[1].CurrentHp, "敌人 2 应受 5 伤");
        }

        [Test]
        public void C04_ArmorBreak_RemovesArmorThenDamages()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var enemy = CombatManager.EnemyTeam[0];
            enemy.Armor = 10;
            CombatManager.Deck.Hand.Add("C04");
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx);
            Assert.AreEqual(0, enemy.Armor, "10护甲→移除5余5→伤害吸收5归零");
            Assert.AreEqual(28, enemy.CurrentHp, "伤害被剩余护甲全部吸收，HP不变");
        }

        [Test]
        public void C03_DoubleStab_HitsTwice()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var enemy = CombatManager.EnemyTeam[0];
            enemy.Armor = 1; // 第一次破甲 1，余 2 伤扣血；第二次全部扣血 3
            int before = enemy.CurrentHp;
            CombatManager.Deck.Hand.Add("C03");
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx);
            // 两次 3 伤害，第一次护甲吸收 1 → 扣 2 血，第二次全扣 3 血
            Assert.AreEqual(before - 5, enemy.CurrentHp, "两段伤害共扣 5 血");
        }

        // ---- 防御卡 ----

        [Test]
        public void C09_Block_Gains5Armor()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var player = CombatManager.PlayerTeam[0];
            int before = player.Armor;
            int idx = CombatManager.Deck.Hand.IndexOf("C09");
            CombatResolver.PlayCard(idx);
            Assert.AreEqual(before + 5, player.Armor, "应获得 5 护甲");
        }

        [Test]
        public void C12_Guard_GrantsAllyArmor()
        {
            CombatManager.Init(_players, _enemies, _deck);
            // C12 是 SingleAlly，需手动加入手牌
            CombatManager.Deck.Hand.Add("C12");
            var ally = CombatManager.PlayerTeam[1]; // 阿德里安
            int before = ally.Armor;
            int idx = CombatManager.Deck.HandSize - 1;
            // selectedTarget = ally
            CombatResolver.PlayCard(idx, ally);
            Assert.AreEqual(before + 7, ally.Armor, "目标伙伴应获得 7 护甲");
        }

        // ---- 策略卡 ----

        [Test]
        public void C17_Scout_DrawsOneCard()
        {
            CombatManager.Init(_players, _enemies, _deck);
            // 确保手牌中有 C17（加入后再记录基准值）
            int idx = CombatManager.Deck.Hand.IndexOf("C17");
            if (idx < 0) { CombatManager.Deck.Hand.Add("C17"); idx = CombatManager.Deck.HandSize - 1; }
            int handBefore = CombatManager.Deck.HandSize;
            CombatResolver.PlayCard(idx);
            // 打出 C17（1张进入弃牌堆），抽 1 张，净变化 0
            Assert.AreEqual(handBefore, CombatManager.Deck.HandSize, "净手牌数应不变（打出→弃牌堆，抽1回手）");
        }

        [Test]
        public void C21_Inspire_AddsMorale()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.Deck.Hand.Add("C21");
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx);
            Assert.AreEqual(2, CombatManager.Morale, "应获得 2 层士气");
        }

        [Test]
        public void C22_Disrupt_ReducesEnemyIntent()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.Deck.Hand.Add("C22");
            var enemy = (EnemyUnit)CombatManager.EnemyTeam[0];
            enemy.CurrentIntent = new EnemyIntentExec("测试砍击", IntentKind.Attack, 1) { Damage = 8 };
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx);
            Assert.AreEqual(3, enemy.CurrentIntent.Damage, "意图伤害应从 8 降为 3（-5）");
        }

        // ---- 战术卡 ----

        [Test]
        public void C29_Rally_AddsMoraleAndPartnerArmor()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.Deck.Hand.Add("C29");
            var ally = CombatManager.PlayerTeam[1];
            int armorBefore = ally.Armor;
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx);
            Assert.AreEqual(1, CombatManager.Morale, "应获得 1 层士气");
            Assert.AreEqual(armorBefore + 2, ally.Armor, "伙伴应获得 2 护甲");
        }

        // ---- 后勤卡 ----

        [Test]
        public void C33_Bandage_Heals()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var ally = CombatManager.PlayerTeam[1];
            ally.CurrentHp = 30; // 扣点血
            CombatManager.Deck.Hand.Add("C33");
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx, ally);
            Assert.AreEqual(36, ally.CurrentHp, "应恢复 6 生命");
        }

        [Test]
        public void C34_Cleanse_RemovesBleed()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var ally = CombatManager.PlayerTeam[1];
            ally.Bleed = 3;
            CombatManager.Deck.Hand.Add("C34");
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx, ally);
            Assert.AreEqual(0, ally.Bleed, "应移除全部流血");
        }

        [Test]
        public void C36_FirstAid_HealsAndGrantsArmorThenExhausts()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var player = CombatManager.PlayerTeam[0];
            player.CurrentHp = 30;
            CombatManager.Deck.Hand.Add("C36");
            int idx = CombatManager.Deck.HandSize - 1;
            int exhaustBefore = CombatManager.Deck.ExhaustedCount;
            CombatResolver.PlayCard(idx, player);
            Assert.AreEqual(33, player.CurrentHp, "应恢复 3 生命");
            Assert.AreEqual(3, player.Armor, "应获得 3 护甲");
            Assert.AreEqual(exhaustBefore + 1, CombatManager.Deck.ExhaustedCount, "应进入消耗区");
        }

        // ---- 边界条件 ----

        [Test]
        public void PlayCard_NoValidTarget_RefundsEnergy()
        {
            // 清空所有敌人，打出单体攻击卡
            CombatManager.Init(_players, _enemies, _deck);
            foreach (var e in CombatManager.EnemyTeam) e.CurrentHp = 0;
            CombatManager.CheckEndCondition(); // 触发胜利
            // 战斗已结束，CanPlayerAct 为 false
            Assert.IsFalse(CombatManager.CanPlayerAct);
        }

        [Test]
        public void PlayCard_AfterCombatEnd_ReturnsError()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.End();
            string result = CombatResolver.PlayCard(0);
            Assert.IsTrue(result.Contains("不能出牌") || result.Contains("无效"), "战斗结束后不能出牌：" + result);
        }

        [Test]
        public void C06_Execution_BonusDamageWhenBleedGE2()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var enemy = CombatManager.EnemyTeam[0];
            enemy.Bleed = 3; // >= 2 触发额外伤害
            int before = enemy.CurrentHp;
            CombatManager.Deck.Hand.Add("C06");
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx);
            Assert.AreEqual(before - 14, enemy.CurrentHp, "应造成 8+6=14 伤害（流血≥2）");
        }

        [Test]
        public void C06_Execution_NoBonusWhenBleedLow()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var enemy = CombatManager.EnemyTeam[0];
            enemy.Bleed = 1; // < 2，不触发
            int before = enemy.CurrentHp;
            CombatManager.Deck.Hand.Add("C06");
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx);
            Assert.AreEqual(before - 8, enemy.CurrentHp, "应只造成 8 伤害（流血<2）");
        }

        [Test]
        public void C25_FocusFire_AddsExtraDamageOnHit()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var enemy = CombatManager.EnemyTeam[0];
            CombatManager.Deck.Hand.Add("C25");
            int idx1 = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx1); // 施加集火 +2

            // 再用普通攻击
            int idx2 = CombatManager.Deck.Hand.IndexOf("C01");
            if (idx2 < 0) { CombatManager.Deck.Hand.Add("C01"); idx2 = CombatManager.Deck.HandSize - 1; }
            int before = enemy.CurrentHp;
            CombatResolver.PlayCard(idx2); // 应造成 6+2=8
            Assert.AreEqual(before - 8, enemy.CurrentHp, "伤害应含集火 +2");
        }

        [Test]
        public void C30_Taunt_RedirectsIntentToPlayer()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var enemy = (EnemyUnit)CombatManager.EnemyTeam[0];
            enemy.CurrentIntent = new EnemyIntentExec("砍击", IntentKind.Attack, 1) { Damage = 8 };
            CombatManager.Deck.Hand.Add("C30");
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx);
            Assert.IsTrue(enemy.CurrentIntent.TargetsPlayer, "意图应标记为攻击主角");
            Assert.AreEqual(5, enemy.CurrentIntent.Damage, "意图伤害应从 8 降为 5（-3）");
        }

        [Test]
        public void C40_HealerHand_Heals10AndRemovesInjury()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var ally = CombatManager.PlayerTeam[1];
            ally.CurrentHp = 10;
            CombatManager.Deck.Hand.Add("C40");
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx, ally);
            Assert.AreEqual(20, ally.CurrentHp, "应恢复 10 生命");
        }

        [Test]
        public void C32_TotalAssault_PlayerAndPartnersDealDamage()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var enemy = CombatManager.EnemyTeam[0];
            int before = enemy.CurrentHp;
            CombatManager.Deck.Hand.Add("C32");
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx);
            // 主角 5 + 阿德里安指令伤害 5 = 10
            Assert.AreEqual(before - 10, enemy.CurrentHp, "应造成主角 5 + 伙伴 5 = 10 伤害");
        }

        // ---- 减费 ----

        [Test]
        public void C19_EnergySave_ReducesNextCardCost()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.Deck.Hand.Add("C19"); // 0 费，下张牌费用 -1，消耗
            int idx19 = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx19); // 打出节能
            Assert.AreEqual(1, CombatManager.CostReductionRemaining, "应有 1 点减费");

            // 打出下一张（C01 剑击 1 费 → 实际 0 费）
            int idx01 = CombatManager.Deck.Hand.IndexOf("C01");
            if (idx01 < 0) { CombatManager.Deck.Hand.Add("C01"); idx01 = CombatManager.Deck.HandSize - 1; }
            int energyBefore = CombatManager.Energy;
            CombatResolver.PlayCard(idx01);
            Assert.AreEqual(energyBefore, CombatManager.Energy, "能量不应减少（减费 1）");
        }

        // ---- 手牌操作卡 ----

        [Test]
        public void C18_Plan_Draws2ThenDiscards1()
        {
            CombatManager.Init(_players, _enemies, _deck);
            int handBefore = CombatManager.Deck.HandSize;
            CombatManager.Deck.Hand.Add("C18");
            int idx = CombatManager.Deck.HandSize - 1;
            CombatResolver.PlayCard(idx);
            // 打出 C18（1 张入弃牌堆），抽 2 弃 1 → 净 +0
            Assert.AreEqual(handBefore, CombatManager.Deck.HandSize, "净手牌数应不变");
        }

        // ---- 起始牌组测试 ----

        [Test]
        public void StartingDeck_AllCardsPlayable()
        {
            CombatManager.Init(_players, _enemies, _deck);
            Assert.GreaterOrEqual(CombatManager.Deck.HandSize, 3, "起始应有至少 3 张手牌");

            // 打出所有手牌，不应报错
            int count = CombatManager.Deck.HandSize;
            for (int i = 0; i < count; i++)
            {
                int idx = 0; // 每次打出第一张
                string result = CombatResolver.PlayCard(idx);
                Assert.IsFalse(result.Contains("找不到卡牌定义"), "打出失败：" + result);
                if (CombatManager.Energy <= 0) break;
            }
        }
    }
}
