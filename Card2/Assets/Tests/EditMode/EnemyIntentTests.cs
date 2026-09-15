using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class EnemyIntentTests
    {
        private List<CombatUnit> _players;
        private List<CombatUnit> _enemies;
        private List<string> _deck;

        [SetUp]
        public void SetUp()
        {
            CombatManager.End();
            RunSession.Reset();
            RunSession.StartNewGame(7);
            ContentRegistry.Clear();
            RunRecord.Clear();

            _players = new List<CombatUnit>
            {
                CombatUnit.CreatePlayer(45, 6),
                CombatUnit.CreateCompanion("P01", "阿德里安", 42, 5)
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
            RunRecord.Clear();
        }

        // ---- 意图抽取 ----

        [Test]
        public void RollIntent_SameSeed_SameIntent()
        {
            var a = EnemyUnit.CreateBandit();
            a.RollIntent(new GameRandom(99));
            var b = EnemyUnit.CreateBandit();
            b.RollIntent(new GameRandom(99));

            Assert.AreEqual(a.CurrentIntent.Name, b.CurrentIntent.Name, "同种子应抽到同一意图");
        }

        [Test]
        public void RollIntent_ZeroWeightIntent_NeverPicked()
        {
            var enemy = EnemyUnit.CreateBandit();
            enemy.Intents[0].Weight = 0; // 砍击权重清零

            var rng = new GameRandom(123);
            for (int i = 0; i < 50; i++)
            {
                enemy.RollIntent(rng);
                Assert.AreNotEqual("砍击", enemy.CurrentIntent.Name);
            }
        }

        [Test]
        public void RollIntent_AllZeroWeights_ReportsIssue()
        {
            var enemy = EnemyUnit.CreateBandit();
            foreach (var i in enemy.Intents) i.Weight = 0;

            enemy.RollIntent(new GameRandom(5));

            Assert.IsNull(enemy.CurrentIntent, "权重全零时不应有意图");
        }

        [Test]
        public void BeginPlayerTurn_RevealsIntents_ForAllAliveEnemies()
        {
            CombatManager.Init(_players, _enemies, _deck);

            foreach (var e in CombatManager.EnemyTeam)
            {
                Assert.IsNotNull(((EnemyUnit)e).CurrentIntent, "第 1 回合开始应已揭示意图");
            }
        }

        [TestCase("EN05", 62)]
        [TestCase("EN10", 68)]
        public void BossFactory_UsesReducedMaxHp(string enemyId, int expectedMaxHp)
        {
            var enemy = EnemyUnit.CreateById(enemyId);

            Assert.IsNotNull(enemy);
            Assert.AreEqual(expectedMaxHp, enemy.MaxHp);
            Assert.AreEqual(expectedMaxHp, enemy.CurrentHp);
        }

        [TestCase("EN01", "架盾", 5)]
        [TestCase("EN03", "架势", 5)]
        [TestCase("EN04", "蛰伏", 5)]
        [TestCase("EN05", "号令", 8)]
        [TestCase("EN07", "菌壳", 4)]
        [TestCase("EN08", "伏守", 6)]
        [TestCase("EN09", "蓄势", 5)]
        [TestCase("EN10", "树皮", 9)]
        public void DefenseIntentFactory_UsesReducedArmor(string enemyId, string intentName, int expectedArmor)
        {
            var enemy = EnemyUnit.CreateById(enemyId);
            var defense = enemy.Intents.Find(intent => intent.Kind == IntentKind.Defense);

            Assert.IsNotNull(defense, enemyId + " 应包含防御意图");
            Assert.AreEqual(intentName, defense.Name);
            Assert.AreEqual(expectedArmor, defense.ArmorGain);
        }

        // ---- 意图执行 ----

        [Test]
        public void AttackIntent_DamagesDefaultTarget()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var bandit = (EnemyUnit)CombatManager.EnemyTeam[0];
            bandit.CurrentIntent = new EnemyIntentExec("砍击", IntentKind.Attack, 50) { Damage = 6 };
            ((EnemyUnit)CombatManager.EnemyTeam[1]).CurrentIntent = null;
            int before = CombatManager.PlayerTeam[0].CurrentHp + CombatManager.PlayerTeam[1].CurrentHp;

            CombatManager.EndPlayerTurn(); // 触发敌方行动

            Assert.AreEqual(before - 6, CombatManager.PlayerTeam[0].CurrentHp + CombatManager.PlayerTeam[1].CurrentHp, "单体攻击应造成 6 伤害");
        }

        [Test]
        public void DefenseIntent_GainsArmor()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var bandit = (EnemyUnit)CombatManager.EnemyTeam[0];
            bandit.CurrentIntent = bandit.Intents.Find(intent => intent.Kind == IntentKind.Defense);

            CombatManager.EndPlayerTurn();

            Assert.AreEqual(5, bandit.Armor, "路匪防御意图应获得调整后的 5 护甲");
        }

        [Test]
        public void PlunderIntent_DamagesAndAddsPlunder()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var bandit = (EnemyUnit)CombatManager.EnemyTeam[0];
            bandit.CurrentIntent = new EnemyIntentExec("勒索", IntentKind.Plunder, 30) { Damage = 4, PlunderStacks = 1 };
            ((EnemyUnit)CombatManager.EnemyTeam[1]).CurrentIntent = null; // 禁用野犬
            int before = CombatManager.PlayerTeam[0].CurrentHp + CombatManager.PlayerTeam[1].CurrentHp;

            CombatManager.EndPlayerTurn();

            Assert.AreEqual(before - 4, CombatManager.PlayerTeam[0].CurrentHp + CombatManager.PlayerTeam[1].CurrentHp);
            Assert.AreEqual(1, CombatManager.Plunder, "应施加 1 层掠夺");
        }

        [Test]
        public void DeadEnemy_SkippedInAction()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.EnemyTeam[0].TakeDamage(999);
            ((EnemyUnit)CombatManager.EnemyTeam[1]).CurrentIntent = null; // 禁用野犬，只验证死亡路匪被跳过
            int before = CombatManager.PlayerTeam[0].CurrentHp + CombatManager.PlayerTeam[1].CurrentHp;

            CombatManager.EndPlayerTurn();

            Assert.AreEqual(before, CombatManager.PlayerTeam[0].CurrentHp + CombatManager.PlayerTeam[1].CurrentHp, "死亡敌人不应行动");
            Assert.AreEqual(CombatPhase.Running, CombatManager.Phase);
        }

        [Test]
        public void NoAlivePlayerTarget_DefaultBehavior_NoCrash()
        {
            CombatManager.Init(_players, _enemies, _deck);
            foreach (var p in CombatManager.PlayerTeam)
            {
                if (!p.IsPlayerCharacter) p.TakeDamage(999);
            }

            // 只剩主角且把主角打死后，敌人无目标
            CombatManager.PlayerTeam[0].TakeDamage(999);

            CombatManager.EndPlayerTurn(); // 不应崩溃；主角死亡 → 失败

            Assert.AreEqual(CombatPhase.Defeat, CombatManager.Phase);
        }

        [Test]
        public void PickDefaultTarget_ReturnsFirstAliveUnit()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var p1 = CombatManager.PlayerTeam[0];
            p1.CurrentHp = 0; // 第一位阵亡

            var target = CombatManager.PickDefaultTarget(CombatManager.PlayerTeam);

            Assert.AreEqual(CombatManager.PlayerTeam[1], target, "应选择第一位存活单位");
        }

        [Test]
        public void PickDefaultTarget_FirstAlive_WhenAllFullHp()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var target = CombatManager.PickDefaultTarget(CombatManager.PlayerTeam);
            Assert.AreEqual(CombatManager.PlayerTeam[0], target, "满血时选择队伍第一位");
        }

        [Test]
        public void EnemyDamage_DoesNotTriggerMorale()
        {
            CombatManager.Init(_players, _enemies, _deck);
            CombatManager.AddMorale(2);
            var bandit = (EnemyUnit)CombatManager.EnemyTeam[0];
            bandit.CurrentIntent = new EnemyIntentExec("砍击", IntentKind.Attack, 50) { Damage = 6 };

            CombatManager.EndPlayerTurn();

            Assert.AreEqual(2, CombatManager.Morale, "敌方伤害不应消耗士气");
            Assert.IsFalse(CombatManager.MoraleUsedThisTurn);
        }

        [Test]
        public void AoeIntent_DamagesAllPlayers()
        {
            CombatManager.Init(_players, _enemies, _deck);
            var hound = (EnemyUnit)CombatManager.EnemyTeam[1];
            hound.CurrentIntent = new EnemyIntentExec("咆哮", IntentKind.AoeAttack, 10) { Damage = 5 };
            ((EnemyUnit)CombatManager.EnemyTeam[0]).CurrentIntent = null; // 禁用路匪

            CombatManager.EndPlayerTurn();

            Assert.AreEqual(45 - 5, CombatManager.PlayerTeam[0].CurrentHp);
            Assert.AreEqual(42 - 5, CombatManager.PlayerTeam[1].CurrentHp);
        }
    }
}

// recompile-marker-1402
