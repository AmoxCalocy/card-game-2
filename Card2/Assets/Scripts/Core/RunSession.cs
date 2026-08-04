using System;
using System.Collections.Generic;

namespace OneJourney.Core
{
    public enum GameState
    {
        None = 0,
        MainMenu = 1,
        Combat = 2,
        Map = 3,
        Event = 4,
        Camp = 5
    }

    public struct ResolutionRecord
    {
        public readonly string Source;
        public readonly string Description;
        public readonly string Result;

        public ResolutionRecord(string source, string description, string result)
        {
            Source = source ?? "未知来源";
            Description = description ?? string.Empty;
            Result = result ?? string.Empty;
        }
    }

    public static class RunSession
    {
        private const int MaxRecordCount = 20;
        private static readonly List<ResolutionRecord> RecordsList = new List<ResolutionRecord>();

        public static int Seed { get; private set; }

        public static GameState CurrentState { get; private set; } = GameState.MainMenu;

        public static IReadOnlyList<ResolutionRecord> Records => RecordsList;

        public static ResolutionRecord? LastResolution
        {
            get
            {
                if (RecordsList.Count == 0)
                {
                    return null;
                }

                return RecordsList[RecordsList.Count - 1];
            }
        }

        public static event Action Changed;

        public static void StartNewGame(int? seedOverride = null)
        {
            Seed = seedOverride ?? RequestedSeedFromArgs() ?? NewSeed();
            CurrentState = GameState.Map;
            RecordsList.Clear();
            RecordResolution("会话初始化", "新游戏开始", "随机种子 " + Seed + "，进入地图");
        }

        public static void EnterTestPage(GameState page)
        {
            if (page == GameState.None || page == GameState.MainMenu)
            {
                throw new ArgumentOutOfRangeException(nameof(page), page, "测试入口只接受战斗、地图、事件或营地页面");
            }

            Seed = RequestedSeedFromArgs() ?? NewSeed();
            CurrentState = page;
            RecordsList.Clear();
            RecordResolution("测试入口", "直接进入" + DisplayName(page), "随机种子 " + Seed);
        }

        public static void RecordResolution(string source, string description, string result)
        {
            RecordsList.Add(new ResolutionRecord(source, description, result));
            if (RecordsList.Count > MaxRecordCount)
            {
                RecordsList.RemoveAt(0);
            }

            Changed?.Invoke();
        }

        public static void Reset()
        {
            Seed = 0;
            CurrentState = GameState.MainMenu;
            RecordsList.Clear();
            Changed?.Invoke();
        }

        public static string DisplayName(GameState state)
        {
            switch (state)
            {
                case GameState.None:
                    return "无";
                case GameState.MainMenu:
                    return "主菜单";
                case GameState.Combat:
                    return "战斗";
                case GameState.Map:
                    return "地图";
                case GameState.Event:
                    return "事件";
                case GameState.Camp:
                    return "营地";
                default:
                    return state.ToString();
            }
        }

        public static int? RequestedSeedFromArgs()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-seed", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(args[i + 1], out int seed))
                {
                    return seed;
                }
            }

            return null;
        }

        private static int NewSeed()
        {
            return new System.Random().Next(1, int.MaxValue);
        }
    }
}
