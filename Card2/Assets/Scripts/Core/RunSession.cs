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
        Camp = 5,
        NewGame = 6,
        Move = 7,
        Reward = 8,
        Victory = 9,
        Defeat = 10,
        Settlement = 11
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

        /// <summary>本局可复现随机数生成器（仅在 StartNewGame / EnterTestPage 之后可用）。</summary>
        public static GameRandom Random { get; private set; }

        /// <summary>战役牌组（A2-16）：战斗外持久化卡牌集合。</summary>
        public static CampaignDeck CampaignDeck { get; private set; }

        public static GameState CurrentState => GameFlow.CurrentState;

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
            if (ContentRegistry.HasBlockingIssues)
            {
                RecordResolution(
                    "内容校验",
                    "新游戏被阻止",
                    "存在 " + ContentRegistry.Issues.Count + " 个内容校验问题："
                    + (ContentRegistry.Issues.Count > 0 ? ContentRegistry.Issues[0].ToString() : string.Empty));
                return;
            }

            if (!GameFlow.TryTransition(GameState.NewGame, "新游戏：会话初始化"))
            {
                return;
            }

            GameFlow.TryTransition(GameState.Map, "新游戏：初始化完成，进入地图");
            Seed = seedOverride ?? RequestedSeedFromArgs() ?? NewSeed();
            Random = new GameRandom(Seed);
            RunRecord.Clear();
            RecordsList.Clear();
            RecordResolution(
                "会话初始化",
                "新游戏开始",
                "随机种子 " + Seed + "，进入地图；起始资源：粮食" + GameStartParameters.StartFood
                + " 财富" + GameStartParameters.StartWealth
                + " 声望" + GameStartParameters.StartReputation
                + " 建材" + GameStartParameters.StartBuildingMaterials
                + "；起始牌组 " + GameStartParameters.StartingDeck.Length + " 张");
        }

        public static void EnterTestPage(GameState page)
        {
            if (page == GameState.None || page == GameState.MainMenu)
            {
                throw new ArgumentOutOfRangeException(nameof(page), page, "测试入口只接受战斗、地图、事件或营地页面");
            }

            if (!GameFlow.TryTransition(page, "测试入口：直接进入" + DisplayName(page)))
            {
                return;
            }

            Seed = RequestedSeedFromArgs() ?? NewSeed();
            Random = new GameRandom(Seed);
            RunRecord.Clear();
            RecordsList.Clear();
            RecordResolution("测试入口", "直接进入" + DisplayName(page), "随机种子 " + Seed);

            if (page == GameState.Combat)
            {
                InitTestCombat();
            }
        }

        private static int _testEncounterIndex;

        private static void InitTestCombat()
        {
            PartnerRoster.InitTestRoster();
            var player = CombatUnit.CreatePlayer(45, 6);
            var team = PartnerRoster.BuildCombatTeam(player);

            var cfg = EncounterConfig.All[_testEncounterIndex % EncounterConfig.All.Length];
            var enemies = new List<CombatUnit>(cfg.Enemies);

            // 使用战役牌组（首次自动初始化）
            if (CampaignDeck == null)
                CampaignDeck = new CampaignDeck(GameStartParameters.StartingDeck);
            var deck = CampaignDeck.CloneCardList();
            CombatManager.Init(team, enemies, deck);
            CombatManager.CurrentEncounterType = cfg.Type;

            RecordResolution(
                "战斗初始化",
                "测试战斗：" + cfg.Label,
                CombatManager.IsActive
                    ? "玩家队伍 " + team.Count + " 人 / 敌人 " + enemies.Count + " 个"
                    : "初始化失败（检查日志）");
        }

        public static string CurrentEncounterLabel()
        {
            return EncounterConfig.All[_testEncounterIndex % EncounterConfig.All.Length].Label;
        }

        public static void NextEncounter()
        {
            _testEncounterIndex++;
        }

        public static void PrevEncounter()
        {
            _testEncounterIndex--;
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
            Random = null;
            CombatManager.End();
            GameFlow.Reset();
            RunRecord.Clear();
            RecordsList.Clear();
            PartnerRoster.Clear();
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
                case GameState.NewGame:
                    return "新局初始化";
                case GameState.Move:
                    return "移动结算";
                case GameState.Reward:
                    return "奖励";
                case GameState.Victory:
                    return "胜利";
                case GameState.Defeat:
                    return "失败";
                case GameState.Settlement:
                    return "结算";
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
