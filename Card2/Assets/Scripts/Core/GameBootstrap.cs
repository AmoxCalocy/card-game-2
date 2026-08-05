using UnityEngine;

namespace OneJourney.Core
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            GameConfigProvider.Initialize();
            RunSession.Reset();
        }
    }
}
