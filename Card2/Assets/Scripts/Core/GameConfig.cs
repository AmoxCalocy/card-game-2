using UnityEngine;

namespace OneJourney.Core
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "一人旅途/GameConfig", order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField] private GameMode _mode = GameMode.Development;
        [SerializeField] private bool _showTestHud = true;
        [SerializeField] private bool _enableTestEntries = true;

        public GameMode Mode => _mode;

        public bool ShowTestHud => _showTestHud;

        public bool EnableTestEntries => _enableTestEntries;

        public static GameConfig Create(GameMode mode, bool showTestHud, bool enableTestEntries)
        {
            var config = CreateInstance<GameConfig>();
            config._mode = mode;
            config._showTestHud = showTestHud;
            config._enableTestEntries = enableTestEntries;
            return config;
        }
    }
}
