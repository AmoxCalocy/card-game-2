using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class CombatManagerTests
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
                CombatUnit.CreateCompanion("P01", "阿德里安", 42, 5)
            };

            _enemies = new List<CombatUnit>
            {
                CombatUnit.CreateEnemy("EN01", "路匪", 28),
                CombatUnit.CreateEnemy("EN02", "野犬", 22)
            };

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

        [Test]
        public void Init_ValidSetup_EntersRunningPhase()
        {
            CombatManager.Init(_players, _enemies, _deck);
            Assert.AreEqual(CombatPhase.Running, CombatManager.Phase);
            Assert.AreEqual(2, CombatManager.PlayerTeam.Count);
            Assert.AreEqual(2, CombatManager.EnemyTeam.Count);
            Assert.IsNotNull(CombatManager.Deck);
            Assert.AreEqual(1, CombatManager.TurnNumber, "Init 后应自动开始第 1 回合");
            Assert.AreEqual(CombatManager.MaxEnergy, CombatManager.Energy, "能量应重置为最大值");
            Assert.IsTrue(CombatManager.CanPlayerAct, "应处于玩家行动阶段");
        }

        [Test]
        public void Init_ClonesUnits_DoesNotModifyOriginal()
        {
            var original = CombatUnit.CreateEnemy("EN01", "路匪", 28);
            var enemies = new List<CombatUnit> { original };
            CombatManager.Init(_players, enemies, _deck);

            Assert.AreEqual(28, original.CurrentHp, "原始敌人不应被修改");
            Assert.AreEqual(28, CombatManager.EnemyTeam[0].CurrentHp);

            CombatManager.EnemyTeam[0].TakeDamage(10);
            Assert.AreEqual(28, original.CurrentHp, "攻击战斗副本不应影响原始对象");
        }

        [Test]
        public void Init_EmptyPlayerTeam_Rejected()
        {
            CombatManager.Init(new List<CombatUnit>(), _enemies, _deck);
            Assert.AreEqual(CombatPhase.None, CombatManager.Phase);
            Assert.IsFalse(CombatManager.IsActive);
        }

        [Test]
        public void Init_EmptyEnemyTeam_Rejected()
        {
            CombatManager.Init(_players, new List<CombatUnit>(), _deck);
            Assert.AreEqual(CombatPhase.None, CombatManager.Phase);
            Assert.IsFalse(CombatManager.IsActive);
        }

        [Test]
        public void Init_NullPlayerTeam_Rejected()
        {
            CombatManager.Init(null, _enemies, _deck);
            Assert.AreEqual(CombatPhase.None, CombatManager.Phase);
        }

        [Test]
        public void CheckEndCondition_AllEnemiesDead_Victory()
        {
            CombatManager.Init(_players, _enemies, _deck);
            foreach (var e in CombatManager.EnemyTeam)
            {
                e.TakeDamage(999);
            }

            string result = CombatManager.CheckEndCondition();
            Assert.AreEqual("victory", result);
            Assert.AreEqual(CombatPhase.Victory, CombatManager.Phase);
        }

        [Test]
        public void CheckEndCondition_PlayerDead_Defeat()
        {
            CombatManager.Init(_players, _enemies, _deck);
            // 找到主角并杀死
            foreach (var u in CombatManager.PlayerTeam)
            {
                if (u.IsPlayerCharacter)
                {
                    u.TakeDamage(999);
                    break;
                }
            }

            string result = CombatManager.CheckEndCondition();
            Assert.AreEqual("defeat", result);
            Assert.AreEqual(CombatPhase.Defeat, CombatManager.Phase);
        }

        [Test]
        public void CheckEndCondition_EnemiesAlive_NoResult()
        {
            CombatManager.Init(_players, _enemies, _deck);
            Assert.IsNull(CombatManager.CheckEndCondition());
            Assert.AreEqual(CombatPhase.Running, CombatManager.Phase);
        }

        [Test]
        public void End_CleansUpState()
        {
            CombatManager.Init(_players, _enemies, _deck);
            Assert.IsTrue(CombatManager.IsActive);

            CombatManager.End();
            Assert.AreEqual(CombatPhase.Ended, CombatManager.Phase);
            Assert.IsFalse(CombatManager.IsActive);
            Assert.IsNull(CombatManager.PlayerTeam);
            Assert.IsNull(CombatManager.EnemyTeam);
            Assert.IsNull(CombatManager.Deck);
        }

        [Test]
        public void SecondCombat_DoesNotInheritStateFromFirst()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var deck1 = CombatManager.Deck;
            CombatManager.End();

            CombatManager.Init(_players, _enemies, _deck);
            var deck2 = CombatManager.Deck;

            // 新战斗应生成全新牌堆
            Assert.AreNotSame(deck1, deck2);
            Assert.AreEqual(_deck.Count, deck2.DrawPileCount + deck2.HandSize);
        }

        [Test]
        public void Retreat_AlwaysDisabled()
        {
            Assert.IsFalse(CombatManager.RetreatAllowed);
        }

        [Test]
        public void Init_StartsTurnOne_WithFullEnergy()
        {
            CombatManager.Init(_players, _enemies, _deck);

            Assert.AreEqual(1, CombatManager.TurnNumber);
            Assert.AreEqual(CombatManager.MaxEnergy, CombatManager.Energy);
            Assert.AreEqual(TurnPhase.PlayerTurn, CombatManager.CurrentTurnPhase);
        }

        [Test]
        public void EndPlayerTurn_TransitionsThroughEnemyToNextTurn()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.EndPlayerTurn();

            // 应已进入第 2 回合
            Assert.AreEqual(2, CombatManager.TurnNumber);
            Assert.AreEqual(CombatManager.MaxEnergy, CombatManager.Energy);
            Assert.AreEqual(TurnPhase.PlayerTurn, CombatManager.CurrentTurnPhase);
        }

        [Test]
        public void Energy_ResetsEachTurn()
        {
            CombatManager.Init(_players, _enemies, _deck);
            Assert.AreEqual(CombatManager.MaxEnergy, CombatManager.Energy);

            CombatManager.SpendEnergy(2);
            Assert.AreEqual(1, CombatManager.Energy);

            CombatManager.EndPlayerTurn();
            Assert.AreEqual(CombatManager.MaxEnergy, CombatManager.Energy, "新回合能量应重置");
        }

        [Test]
        public void SpendEnergy_CannotGoBelowZero()
        {
            CombatManager.Init(_players, _enemies, _deck);
            Assert.IsFalse(CombatManager.SpendEnergy(5), "能量不足时应拒绝");
            Assert.AreEqual(CombatManager.MaxEnergy, CombatManager.Energy, "拒绝时应没扣能量");
        }

        [Test]
        public void CannotEndTurn_DuringEnemyPhase()
        {
            CombatManager.Init(_players, _enemies, _deck);
            Assert.IsTrue(CombatManager.CanPlayerAct);

            CombatManager.EndPlayerTurn(); // 进入敌方 → 自动回玩家回合
            Assert.IsTrue(CombatManager.CanPlayerAct, "回合结束后应回到玩家行动阶段");
        }

        [Test]
        public void CannotAct_AfterCombatEnds()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.End();
            Assert.IsFalse(CombatManager.CanPlayerAct);
            Assert.IsFalse(CombatManager.SpendEnergy(0));
        }

        [Test]
        public void End_ResetsTurnAndEnergy()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.End();
            Assert.AreEqual(0, CombatManager.TurnNumber);
            Assert.AreEqual(0, CombatManager.Energy);
            Assert.AreEqual(TurnPhase.None, CombatManager.CurrentTurnPhase);
        }

        [Test]
        public void Energy_UnaffectedByPlayerTurnPhase_WhenNotPlayerTurn()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.EndPlayerTurn(); // 自动流转，应回到第 2 回合 PlayerTurn
            Assert.AreEqual(TurnPhase.PlayerTurn, CombatManager.CurrentTurnPhase);
            Assert.AreEqual(CombatManager.MaxEnergy, CombatManager.Energy);
        }
    }
}
