using System;
using UnityEngine;

namespace OneJourney.Core
{
    public static class GameConfigProvider
    {
        public static GameMode Mode { get; private set; } = GameMode.Development;

        public static GameConfig Active { get; private set; }

        public static bool IsReleaseLocked { get; private set; }

        public static event Action Changed;

        public static void Initialize()
        {
            IsReleaseLocked = HasArgument("-releaseMode");

            GameMode startupMode;
            if (IsReleaseLocked)
            {
                startupMode = GameMode.Release;
            }
            else if (HasArgument("-testMode"))
            {
                startupMode = GameMode.Testing;
            }
            else if (Application.isEditor)
            {
                startupMode = GameMode.Development;
            }
            else
            {
                startupMode = GameMode.Testing;
            }

            ApplyMode(startupMode);
        }

        public static void ApplyMode(GameMode mode)
        {
            if (IsReleaseLocked && mode != GameMode.Release)
            {
                Debug.LogWarning("[GameConfigProvider] 已以 Release 模式锁定启动，忽略切换到 " + mode);
                return;
            }

            var config = Resources.Load<GameConfig>("Configs/GameConfig_" + mode);
            if (config == null)
            {
                Debug.LogWarning("[GameConfigProvider] 未找到配置资产 Configs/GameConfig_" + mode + "，使用代码默认值");
                config = GameConfig.Create(mode, true, mode != GameMode.Release);
            }

            Mode = mode;
            Active = config;
            Changed?.Invoke();
        }

        private static bool HasArgument(string argument)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
