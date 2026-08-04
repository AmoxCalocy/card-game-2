using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class RunSessionTests
    {
        [TearDown]
        public void TearDown()
        {
            RunSession.Reset();
        }

        [Test]
        public void StartNewGame_SpecifiedSeed_SeedAndStateMatch()
        {
            RunSession.StartNewGame(12345);

            Assert.AreEqual(12345, RunSession.Seed);
            Assert.AreEqual(GameState.Map, RunSession.CurrentState);
            Assert.IsTrue(RunSession.LastResolution.HasValue);
        }

        [Test]
        public void EnterTestPage_SpecifiedSeed_PageStateAndRecordSet()
        {
            RunSession.EnterTestPage(GameState.Combat);

            Assert.AreEqual(GameState.Combat, RunSession.CurrentState);
            Assert.AreEqual("测试入口", RunSession.LastResolution.Value.Source);
        }

        [Test]
        public void RecordResolution_TwoEntries_LastEntryIsNewest()
        {
            RunSession.RecordResolution("A", "描述", "结果 1");
            RunSession.RecordResolution("B", "描述", "结果 2");

            Assert.AreEqual(2, RunSession.Records.Count);
            Assert.AreEqual("结果 2", RunSession.LastResolution.Value.Result);
        }

        [Test]
        public void Reset_ClearsSession_ReturnsToMainMenu()
        {
            RunSession.StartNewGame(7);

            RunSession.Reset();

            Assert.AreEqual(0, RunSession.Seed);
            Assert.AreEqual(GameState.MainMenu, RunSession.CurrentState);
            Assert.IsFalse(RunSession.LastResolution.HasValue);
        }
    }
}
