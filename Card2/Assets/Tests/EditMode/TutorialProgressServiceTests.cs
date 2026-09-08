using System;
using System.Linq;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class TutorialProgressServiceTests
    {
        private string _storageKey;

        [SetUp]
        public void SetUp()
        {
            _storageKey = "OneJourney.TutorialTests." + Guid.NewGuid().ToString("N");
            TutorialProgressService.SetStorageKeyForTests(_storageKey);
            TutorialProgressService.ResetProgress();
            RunSession.Reset();
            RunSession.Relics.Clear();
            RunSession.EventFlags.Clear();
            RunSession.BuiltBuildings.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CombatManager.End();
            RewardResolver.Clear();
            RunSession.Reset();
            TutorialProgressService.ResetStorageKeyForTests();
        }

        [Test]
        public void TutorialCatalog_HasRequiredEightTopicsInPlannedOrder()
        {
            Assert.AreEqual(8, TutorialContent.TutorialCount);
            CollectionAssert.AreEqual(new[]
            {
                TutorialTopic.MapMovement,
                TutorialTopic.Food,
                TutorialTopic.PlayCards,
                TutorialTopic.SharedEnergy,
                TutorialTopic.EnemyIntent,
                TutorialTopic.Reward,
                TutorialTopic.EventChoice,
                TutorialTopic.CampBuildings
            }, TutorialContent.AllTutorials.Select(x => x.Topic));
        }

        [Test]
        public void Complete_MarksOnlySpecifiedTopic_AndPersistsAcrossReload()
        {
            Assert.IsTrue(TutorialProgressService.Complete(TutorialTopic.MapMovement));
            Assert.IsFalse(TutorialProgressService.Complete(TutorialTopic.MapMovement));
            Assert.IsTrue(TutorialProgressService.IsCompleted(TutorialTopic.MapMovement));
            Assert.IsFalse(TutorialProgressService.IsCompleted(TutorialTopic.Food));
            Assert.AreEqual(1, TutorialProgressService.CompletedCount);

            TutorialProgressService.ReloadForTests();

            Assert.IsTrue(TutorialProgressService.IsCompleted(TutorialTopic.MapMovement));
            Assert.AreEqual(1, TutorialProgressService.CompletedCount);
        }

        [Test]
        public void BeginNewRun_ClearsPreviousRunProgress()
        {
            TutorialProgressService.Complete(TutorialTopic.MapMovement);
            TutorialProgressService.Complete(TutorialTopic.Food);
            Assert.AreEqual(2, TutorialProgressService.CompletedCount);

            TutorialProgressService.BeginNewRun();
            TutorialProgressService.ReloadForTests();

            Assert.AreEqual(0, TutorialProgressService.CompletedCount);
            Assert.IsFalse(TutorialProgressService.IsCompleted(TutorialTopic.MapMovement));
            Assert.IsFalse(TutorialProgressService.IsCompleted(TutorialTopic.Food));
        }

        [Test]
        public void SkipAll_MarksEveryTopicComplete()
        {
            TutorialProgressService.SkipAll();

            foreach (TutorialEntry entry in TutorialContent.AllTutorials)
                Assert.IsTrue(TutorialProgressService.IsCompleted(entry.Topic), entry.Topic.ToString());
            Assert.IsTrue(TutorialProgressService.AllCompleted);
            Assert.AreEqual(TutorialContent.TutorialCount, TutorialProgressService.CompletedCount);
        }

        [Test]
        public void ResetProgress_ClearsPersistedCompletion()
        {
            TutorialProgressService.SkipAll();
            TutorialProgressService.ResetProgress();
            TutorialProgressService.ReloadForTests();

            Assert.AreEqual(0, TutorialProgressService.CompletedMask);
            Assert.AreEqual(0, TutorialProgressService.CompletedCount);
            Assert.IsFalse(TutorialProgressService.AllCompleted);
        }

        [Test]
        public void CompleteAndSkip_DoNotChangeCampaignMechanics()
        {
            RunSession.StartNewGame(24680);
            int seed = RunSession.Seed;
            int food = RunSession.Food;
            int wealth = RunSession.Wealth;
            int reputation = RunSession.Reputation;
            int materials = RunSession.Materials;
            int risk = RunSession.Risk;
            GameState state = RunSession.CurrentState;
            int nodeCount = RegionMap.Nodes.Count;

            TutorialProgressService.Complete(TutorialTopic.MapMovement);
            TutorialProgressService.SkipAll();

            Assert.AreEqual(seed, RunSession.Seed);
            Assert.AreEqual(food, RunSession.Food);
            Assert.AreEqual(wealth, RunSession.Wealth);
            Assert.AreEqual(reputation, RunSession.Reputation);
            Assert.AreEqual(materials, RunSession.Materials);
            Assert.AreEqual(risk, RunSession.Risk);
            Assert.AreEqual(state, RunSession.CurrentState);
            Assert.AreEqual(nodeCount, RegionMap.Nodes.Count);
        }

        [Test]
        public void HelpCatalog_ContainsAllRequiredSections()
        {
            CollectionAssert.AreEqual(new[]
            {
                HelpSection.Overview,
                HelpSection.CardTypes,
                HelpSection.Statuses,
                HelpSection.Resources,
                HelpSection.Icons
            }, TutorialContent.AllHelpSections.Select(x => x.Section));
        }
    }
}
