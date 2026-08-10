using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class PartnerRosterTests
    {
        [SetUp]
        public void SetUp()
        {
            RunSession.Reset();
            RunSession.StartNewGame(1);
            ContentRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CombatManager.End();
            RunSession.Reset();
            ContentRegistry.Clear();
        }

        // ---- 数据完整性 ----

        [Test]
        public void AllPartners_CountIs8()
        {
            Assert.AreEqual(8, PartnerRoster.All.Count);
        }

        [Test]
        public void Find_ValidId_ReturnsPartner()
        {
            var p = PartnerRoster.Find("P01");
            Assert.IsNotNull(p);
            Assert.AreEqual("阿德里安", p.Def.DisplayName);
            Assert.AreEqual(42, p.Def.MaxHp);
            Assert.AreEqual(5, p.Def.CommandDamage);
        }

        [Test]
        public void Find_InvalidId_ReturnsNull()
        {
            Assert.IsNull(PartnerRoster.Find("NOPE"));
        }

        // ---- 招募 ----

        [Test]
        public void Recruit_SetsIsRecruited()
        {
            Assert.IsTrue(PartnerRoster.Recruit("P03"));
            var p = PartnerRoster.Find("P03");
            Assert.IsTrue(p.IsRecruited);
        }

        [Test]
        public void Recruit_Duplicate_ReturnsFalse()
        {
            PartnerRoster.Recruit("P02");
            Assert.IsFalse(PartnerRoster.Recruit("P02"));
        }

        [Test]
        public void Recruit_InvalidId_ReturnsFalse()
        {
            Assert.IsFalse(PartnerRoster.Recruit("UNKNOWN"));
        }

        // ---- 上阵管理 ----

        [Test]
        public void SetActiveTeam_Valid_ReturnsNull()
        {
            PartnerRoster.Recruit("P01");
            PartnerRoster.Recruit("P02");
            Assert.IsNull(PartnerRoster.SetActiveTeam(new[] { "P01", "P02" }));
            Assert.AreEqual(2, PartnerRoster.ActiveCount);
            Assert.IsTrue(PartnerRoster.Find("P01").IsInActiveTeam);
            Assert.IsTrue(PartnerRoster.Find("P02").IsInActiveTeam);
        }

        [Test]
        public void SetActiveTeam_ExceedsMax_ReturnsError()
        {
            PartnerRoster.Recruit("P01"); PartnerRoster.Recruit("P02");
            PartnerRoster.Recruit("P03"); PartnerRoster.Recruit("P04");
            string err = PartnerRoster.SetActiveTeam(new[] { "P01", "P02", "P03", "P04" });
            Assert.IsNotNull(err);
            Assert.IsTrue(err.Contains("最多") || err.Contains("3"));
        }

        [Test]
        public void SetActiveTeam_NotRecruited_ReturnsError()
        {
            string err = PartnerRoster.SetActiveTeam(new[] { "P05" });
            Assert.IsNotNull(err);
            Assert.IsTrue(err.Contains("尚未招募"));
        }

        [Test]
        public void SetActiveTeam_Dead_ReturnsError()
        {
            PartnerRoster.Recruit("P06");
            PartnerRoster.Find("P06").CurrentHp = 0;
            string err = PartnerRoster.SetActiveTeam(new[] { "P06" });
            Assert.IsNotNull(err);
            Assert.IsTrue(err.Contains("阵亡"));
        }

        [Test]
        public void SetActiveTeam_ReplacesOldTeam()
        {
            PartnerRoster.Recruit("P01"); PartnerRoster.Recruit("P02");
            PartnerRoster.SetActiveTeam(new[] { "P01" });
            Assert.AreEqual(1, PartnerRoster.ActiveCount);
            PartnerRoster.SetActiveTeam(new[] { "P02" });
            Assert.AreEqual(1, PartnerRoster.ActiveCount);
            Assert.IsTrue(PartnerRoster.Find("P02").IsInActiveTeam);
            Assert.IsFalse(PartnerRoster.Find("P01").IsInActiveTeam);
        }

        // ---- BuildCombatTeam ----

        [Test]
        public void BuildCombatTeam_IncludesPlayerAndActivePartners()
        {
            PartnerRoster.Recruit("P01"); PartnerRoster.Recruit("P03");
            PartnerRoster.SetActiveTeam(new[] { "P01", "P03" });
            var player = CombatUnit.CreatePlayer(45, 6);
            var team = PartnerRoster.BuildCombatTeam(player);
            Assert.AreEqual(3, team.Count); // 2 partners + player
            Assert.AreEqual("P01", team[0].Id);
            Assert.IsTrue(team[1].IsPlayerCharacter, "旅人应在第二位");
            Assert.AreEqual("P03", team[2].Id);
        }

        [Test]
        public void BuildCombatTeam_SkipsDeadPartners()
        {
            PartnerRoster.Recruit("P01"); PartnerRoster.Recruit("P02");
            PartnerRoster.SetActiveTeam(new[] { "P01", "P02" });
            PartnerRoster.Find("P01").CurrentHp = 0;
            var player = CombatUnit.CreatePlayer(45, 6);
            var team = PartnerRoster.BuildCombatTeam(player);
            Assert.AreEqual(2, team.Count); // 1 alive partner + player
            Assert.IsTrue(team[1].IsPlayerCharacter, "旅人应在第二位");
        }

        // ---- SyncFromCombat ----

        [Test]
        public void SyncFromCombat_UpdatesHpAndStatus()
        {
            PartnerRoster.Recruit("P01");
            PartnerRoster.SetActiveTeam(new[] { "P01" });
            var player = CombatUnit.CreatePlayer(45, 6);
            var team = PartnerRoster.BuildCombatTeam(player);
            team[0].CurrentHp = 30; // team[0] = P01（旅人第二位）
            team[0].Disease = 1;
            team[0].Fatigue = 2;
            PartnerRoster.SyncFromCombat(team);
            var p01 = PartnerRoster.Find("P01");
            Assert.AreEqual(30, p01.CurrentHp);
            Assert.AreEqual(1, p01.Disease);
            Assert.AreEqual(2, p01.Fatigue);
        }

        [Test]
        public void SyncFromCombat_DeathSetsHpToZero()
        {
            PartnerRoster.Recruit("P01");
            PartnerRoster.SetActiveTeam(new[] { "P01" });
            var player = CombatUnit.CreatePlayer(45, 6);
            var team = PartnerRoster.BuildCombatTeam(player);
            team[0].CurrentHp = 0; // team[0] = P01
            PartnerRoster.SyncFromCombat(team);
            Assert.AreEqual(0, PartnerRoster.Find("P01").CurrentHp);
            Assert.IsFalse(PartnerRoster.Find("P01").IsAlive);
        }

        // ---- Clear ----

        [Test]
        public void Clear_ResetsAll()
        {
            PartnerRoster.Recruit("P01"); PartnerRoster.Recruit("P02");
            PartnerRoster.SetActiveTeam(new[] { "P01" });
            PartnerRoster.Clear();
            Assert.AreEqual(0, PartnerRoster.ActiveCount);
            Assert.IsFalse(PartnerRoster.Find("P01").IsRecruited);
            Assert.IsFalse(PartnerRoster.Find("P02").IsRecruited);
            Assert.AreEqual(42, PartnerRoster.Find("P01").CurrentHp); // reset to max
        }
    }
}
