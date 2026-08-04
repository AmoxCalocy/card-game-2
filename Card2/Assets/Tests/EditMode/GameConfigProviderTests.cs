using NUnit.Framework;
using OneJourney.Core;
using UnityEngine;

namespace OneJourney.Tests.EditMode
{
    public class GameConfigProviderTests
    {
        [Test]
        public void ConfigAssets_AllThreeModes_LoadFromResources()
        {
            Assert.IsNotNull(Resources.Load<GameConfig>("Configs/GameConfig_Development"));
            Assert.IsNotNull(Resources.Load<GameConfig>("Configs/GameConfig_Testing"));
            Assert.IsNotNull(Resources.Load<GameConfig>("Configs/GameConfig_Release"));
        }

        [Test]
        public void ApplyMode_Development_ActiveConfigMatchesMode()
        {
            GameConfigProvider.ApplyMode(GameMode.Development);

            Assert.AreEqual(GameMode.Development, GameConfigProvider.Mode);
            Assert.AreEqual(GameMode.Development, GameConfigProvider.Active.Mode);
        }

        [Test]
        public void ApplyMode_Release_ActiveConfigHidesTestUi()
        {
            GameConfigProvider.ApplyMode(GameMode.Release);

            Assert.AreEqual(GameMode.Release, GameConfigProvider.Active.Mode);
            Assert.IsFalse(GameConfigProvider.Active.ShowTestHud);
            Assert.IsFalse(GameConfigProvider.Active.EnableTestEntries);
        }
    }
}
