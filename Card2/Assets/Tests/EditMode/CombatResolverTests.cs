using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class CombatResolverTests
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

        private void StartCombat()
        {
            CombatManager.Init(_players, _enemies, _deck);
        }

        [Test]
        public void SingleEnemy_ResolvesAllAliveEnemies()
        {
            StartCombat();
            var targets = CombatResolver.ResolveTargets(TargetType.SingleEnemy, out string issue);
            Assert.IsNull(issue);
            Assert.AreEqual(2, targets.Count);
        }

        [Test]
        public void AllEnemies_SameAsSingleEnemyList()
        {
            StartCombat();
            var targets = CombatResolver.ResolveTargets(TargetType.AllEnemies, out string issue);
            Assert.IsNull(issue);
            Assert.AreEqual(2, targets.Count);
        }

        [Test]
        public void Self_ResolvesPlayerCharacterOnly()
        {
            StartCombat();
            var targets = CombatResolver.ResolveTargets(TargetType.Self, out string issue);
            Assert.IsNull(issue);
            Assert.AreEqual(1, targets.Count);
            Assert.IsTrue(targets[0].IsPlayerCharacter);
        }

        [Test]
        public void SingleAlly_ResolvesAllAlivePlayerUnits()
        {
            StartCombat();
            var targets = CombatResolver.ResolveTargets(TargetType.SingleAlly, out string issue);
            Assert.IsNull(issue);
            Assert.AreEqual(2, targets.Count);
        }

        [Test]
        public void AllAllies_SameAsSingleAllyList()
        {
            StartCombat();
            var targets = CombatResolver.ResolveTargets(TargetType.AllAllies, out string issue);
            Assert.IsNull(issue);
            Assert.AreEqual(2, targets.Count);
        }

        [Test]
        public void None_ResolvesEmpty()
        {
            StartCombat();
            var targets = CombatResolver.ResolveTargets(TargetType.None, out string issue);
            Assert.IsNull(issue);
            Assert.AreEqual(0, targets.Count);
        }

        [Test]
        public void DeadUnit_ExcludedFromTargets()
        {
            StartCombat();
            CombatManager.EnemyTeam[0].TakeDamage(999);

            var targets = CombatResolver.ResolveTargets(TargetType.SingleEnemy, out string issue);
            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual("EN02", targets[0].Id);
        }

        [Test]
        public void AllEnemiesDead_IssueReported()
        {
            StartCombat();
            CombatManager.EnemyTeam[0].TakeDamage(999);
            CombatManager.EnemyTeam[1].TakeDamage(999);

            var targets = CombatResolver.ResolveTargets(TargetType.SingleEnemy, out string issue);
            Assert.AreEqual(0, targets.Count);
            Assert.IsNotNull(issue);
        }

        [Test]
        public void Damage_EqualToArmor_HpUnchanged()
        {
            StartCombat();
            var enemy = CombatManager.EnemyTeam[0];
            enemy.Armor = 6;

            string text = CombatResolver.ApplyDamage(enemy, 6);

            Assert.AreEqual(28, enemy.CurrentHp, "护甲恰好吸收，生命不应变化");
            Assert.AreEqual(0, enemy.Armor);
            StringAssert.Contains("护甲吸收 6", text);
        }

        [Test]
        public void Damage_ArmorPlusOne_LosesOneHp()
        {
            StartCombat();
            var enemy = CombatManager.EnemyTeam[0];
            enemy.Armor = 6;

            string text = CombatResolver.ApplyDamage(enemy, 7);

            Assert.AreEqual(27, enemy.CurrentHp, "伤害刚好多 1，应扣 1 生命");
            Assert.AreEqual(0, enemy.Armor);
            StringAssert.Contains("27", text);
        }

        [Test]
        public void KillLastEnemy_TriggersVictory()
        {
            StartCombat();
            // 先杀第一个
            CombatManager.EnemyTeam[0].TakeDamage(999);
            Assert.AreEqual(CombatPhase.Running, CombatManager.Phase);

            // 杀最后一个
            string text = CombatResolver.ApplyDamage(CombatManager.EnemyTeam[1], 999);

            Assert.AreEqual(CombatPhase.Victory, CombatManager.Phase, "最后一个敌人死亡应触发胜利");
            StringAssert.Contains("死亡", text);
        }

        [Test]
        public void AoeKillsLastEnemy_InSameBatch_TriggersVictory()
        {
            StartCombat();
            // 同一效果（全体攻击）在一批结算中击杀全部敌人 → 应在批处理内触发胜利
            string text = CombatResolver.PlayTestCard(2, TargetType.AllEnemies, 99);

            Assert.AreEqual(CombatPhase.Victory, CombatManager.Phase, "AOE 批内击杀最后敌人应触发胜利");
            Assert.IsFalse(CombatManager.EnemyTeam[0].IsAlive);
            Assert.IsFalse(CombatManager.EnemyTeam[1].IsAlive);
            StringAssert.Contains("死亡", text);
        }

        [Test]
        public void Aoe_DeadTargetInBatch_SkippedSafely()
        {
            StartCombat();
            // 预先击杀第一个敌人；AOE 只应命中存活目标
            CombatManager.EnemyTeam[0].TakeDamage(999);

            string text = CombatResolver.PlayTestCard(2, TargetType.AllEnemies, 5);

            Assert.AreEqual(22 - 5, CombatManager.EnemyTeam[1].CurrentHp, "只有存活目标被结算");
            Assert.IsFalse(text.Contains("路匪 受到"), "已死亡目标不应被结算");
            Assert.AreEqual(CombatPhase.Running, CombatManager.Phase);
        }

        [Test]
        public void PlayTestCard_InsufficientEnergy_NoSpend()
        {
            StartCombat();
            CombatManager.SpendEnergy(2); // 3 → 1

            string text = CombatResolver.PlayTestCard(2, TargetType.SingleEnemy, 6);

            StringAssert.Contains("能量不足", text);
            Assert.AreEqual(1, CombatManager.Energy, "失败时不应扣能量");
        }

        [Test]
        public void PlayTestCard_NoValidTarget_RefundsEnergy()
        {
            StartCombat();
            CombatManager.EnemyTeam[0].TakeDamage(999);
            CombatManager.EnemyTeam[1].TakeDamage(999);
            int before = CombatManager.Energy;

            string text = CombatResolver.PlayTestCard(1, TargetType.SingleEnemy, 6);

            StringAssert.Contains("无合法目标", text);
            Assert.AreEqual(before, CombatManager.Energy, "无目标时应退还能量");
        }

        [Test]
        public void PlayTestCard_SingleEnemy_DamagesOne()
        {
            StartCombat();
            string text = CombatResolver.PlayTestCard(1, TargetType.SingleEnemy, 6);

            // 两个敌人都可能被结算（作为可选目标列表）；这里验证至少一个受到伤害且能量已扣
            Assert.AreEqual(2, CombatManager.Energy);
            int totalHp = CombatManager.EnemyTeam[0].CurrentHp + CombatManager.EnemyTeam[1].CurrentHp;
            Assert.AreEqual(28 + 22 - 6, totalHp, "应恰好有一个敌人受到 6 伤害");
        }

        [Test]
        public void PlayTestCard_AllEnemies_DamagesBoth()
        {
            StartCombat();
            string text = CombatResolver.PlayTestCard(2, TargetType.AllEnemies, 5);

            Assert.AreEqual(1, CombatManager.Energy);
            Assert.AreEqual(28 - 5, CombatManager.EnemyTeam[0].CurrentHp);
            Assert.AreEqual(22 - 5, CombatManager.EnemyTeam[1].CurrentHp);
        }
    }
}
