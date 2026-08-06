using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class CombatStatusTests
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

            _players = new List<CombatUnit> { CombatUnit.CreatePlayer(45, 6) };
            _enemies = new List<CombatUnit> { CombatUnit.CreateEnemy("EN01", "路匪", 28) };
            _deck = new List<string>(GameStartParameters.StartingDeck);
        }

        [TearDown]
        public void TearDown()
        {
            CombatManager.End();
            RunSession.Reset();
            ContentRegistry.Clear();
            RunRecord.Clear();
        }

        // ---- 流血 ----

        [Test]
        public void Bleed_ApplyAndStack_ClampedToMax()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 50);
            CombatStatus.AddBleed(u, 3);
            CombatStatus.AddBleed(u, 3);
            Assert.AreEqual(5, u.Bleed, "流血上限 5");
        }

        [Test]
        public void Bleed_TurnStartTick_TrueDamageAndDecrement()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 50);
            u.Armor = 10;
            CombatStatus.AddBleed(u, 2);

            string text = CombatStatus.TriggerTurnStartBleed(u);

            Assert.AreEqual(48, u.CurrentHp, "流血无视护甲（真实伤害）");
            Assert.AreEqual(10, u.Armor, "流血不消耗护甲");
            Assert.AreEqual(1, u.Bleed, "伤害后层数 -1");
            StringAssert.Contains("2 生命", text);
        }

        [Test]
        public void Bleed_KillsUnit_TriggersEndCheck()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var enemy = CombatManager.EnemyTeam[0];
            enemy.CurrentHp = 2;
            CombatStatus.AddBleed(enemy, 3);

            CombatManager.EndPlayerTurn(); // 敌方回合开始 → 流血结算

            Assert.AreEqual(CombatPhase.Victory, CombatManager.Phase, "流血致死应触发胜利");
        }

        [Test]
        public void Bleed_RemoveAll_Clears()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 50);
            CombatStatus.AddBleed(u, 3);
            CombatStatus.RemoveAllBleed(u);
            Assert.AreEqual(0, u.Bleed);
        }

        [Test]
        public void Bleed_NotAppliedToDeadUnit()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 1);
            u.TakeTrueDamage(1);
            CombatStatus.AddBleed(u, 3);
            Assert.AreEqual(0, u.Bleed);
        }

        // ---- 疾病 ----

        [Test]
        public void Disease_LowersEffectiveMaxHp()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 40);
            CombatStatus.AddDisease(u, 2);
            Assert.AreEqual(40 - 8, u.EffectiveMaxHp, "每层 -4");
            Assert.AreEqual(40 - 8, u.CurrentHp, "当前生命高于新上限时应降至新上限");
        }

        [Test]
        public void Disease_ClampsCurrentHpToNewMax()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 40);
            u.CurrentHp = 40;
            CombatStatus.AddDisease(u, 2); // 上限 32
            Assert.AreEqual(32, u.CurrentHp, "当前生命高于新上限应降至新上限");
        }

        [Test]
        public void Disease_HealClampsToEffectiveMax()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 40);
            CombatStatus.AddDisease(u, 2); // 上限 32
            u.CurrentHp = 30;
            u.Heal(20);
            Assert.AreEqual(32, u.CurrentHp);
        }

        [Test]
        public void Disease_Remove_RecoversMax()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 40);
            CombatStatus.AddDisease(u, 2);
            CombatStatus.RemoveDisease(u, 2);
            Assert.AreEqual(40, u.EffectiveMaxHp);
        }

        // ---- 疲劳 ----

        [Test]
        public void Fatigue_LowersArmorCapAndCommandDamage()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 40);
            u.CommandDamage = 6;
            CombatStatus.AddFatigue(u, 2);

            Assert.AreEqual(30 - 10, u.EffectiveArmorCap, "每层护甲上限 -5");
            Assert.AreEqual(6 - 2, u.EffectiveCommandDamage, "每层指令伤害 -1");
        }

        [Test]
        public void Fatigue_ClampsArmorToCap()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 40);
            u.AddArmor(25);
            CombatStatus.AddFatigue(u, 3); // 上限 15
            Assert.AreEqual(15, u.Armor, "护甲应降至新上限");
        }

        [Test]
        public void Fatigue_AddArmor_RespectsCap()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 40);
            CombatStatus.AddFatigue(u, 2); // 上限 20
            u.AddArmor(50);
            Assert.AreEqual(20, u.Armor);
        }

        [Test]
        public void Fatigue_Remove_RecoversCap()
        {
            var u = CombatUnit.CreateEnemy("E", "敌", 40);
            CombatStatus.AddFatigue(u, 2);
            CombatStatus.RemoveFatigue(u, 1);
            Assert.AreEqual(30 - 5, u.EffectiveArmorCap);
        }

        // ---- 士气 ----

        [Test]
        public void Morale_ApplyAndStack_ClampedToMax()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.AddMorale(2);
            CombatManager.AddMorale(2);
            Assert.AreEqual(3, CombatManager.Morale, "士气上限 3");
        }

        [Test]
        public void Morale_FirstDamageGetsBonus_ThenCleared()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.AddMorale(2);
            var enemy = CombatManager.EnemyTeam[0];

            CombatResolver.ApplyDamage(enemy, 5);

            Assert.AreEqual(28 - (5 + 4), enemy.CurrentHp, "首次伤害应 +2×层数");
            Assert.AreEqual(0, CombatManager.Morale, "触发后清空");
            Assert.IsTrue(CombatManager.MoraleUsedThisTurn);

            // 第二次伤害无加成
            CombatResolver.ApplyDamage(enemy, 5);
            Assert.AreEqual(28 - 9 - 5, enemy.CurrentHp);
        }

        [Test]
        public void Morale_ResetsEachTurn()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.AddMorale(2);
            CombatResolver.ApplyDamage(CombatManager.EnemyTeam[0], 5);
            Assert.IsTrue(CombatManager.MoraleUsedThisTurn);

            CombatManager.EndPlayerTurn(); // 进入第 2 回合
            Assert.IsFalse(CombatManager.MoraleUsedThisTurn, "新回合应重置士气使用标记");
        }

        // ---- 多状态共存与结算顺序 ----

        [Test]
        public void MultipleStatuses_OneTurn_OrderConsistent()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var enemy = CombatManager.EnemyTeam[0];

            // 同一单位同时拥有：护甲 + 流血 + 疾病 + 疲劳 + 士气（队伍）
            enemy.AddArmor(10);
            CombatStatus.AddBleed(enemy, 2);
            CombatStatus.AddDisease(enemy, 1);
            CombatStatus.AddFatigue(enemy, 1);
            CombatManager.AddMorale(2);

            // 玩家回合出牌：士气在玩家回合先结算（伤害 +4）；疾病已把生命钳到 24
            CombatResolver.PlayTestCard(1, TargetType.SingleEnemy, 6); // 6+4=10，全部被护甲吸收

            // 结束回合 → 敌方回合开始：流血 2 点真实伤害 → 22，层数 1
            CombatManager.EndPlayerTurn();

            Assert.AreEqual(24 - 2, enemy.CurrentHp, "护甲吸收普通伤害，流血造成真实伤害");
            Assert.AreEqual(1, enemy.Bleed);
            Assert.AreEqual(0, enemy.Armor, "护甲已被普通伤害消耗");
            Assert.AreEqual(1, enemy.Disease);
            Assert.AreEqual(1, enemy.Fatigue);
            Assert.AreEqual(CombatPhase.Running, CombatManager.Phase);
        }
    }
}
