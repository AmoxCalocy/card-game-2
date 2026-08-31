using UnityEngine;

namespace OneJourney.Core
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            GameConfigProvider.Initialize();
            ContentRegistry.LoadAll();
            RunSession.Reset();
            CampaignSaveService.Initialize();
        }
    }
}
