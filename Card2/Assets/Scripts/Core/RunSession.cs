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

        // ---- 战役资源与风险（配置表 §2.4，A2-18）----
        public static int Food { get; private set; }
        public static int Wealth { get; private set; }
        public static int Reputation { get; private set; }
        public static int Materials { get; private set; }
        public static int Risk { get; private set; }              // 当前区域风险 0-10
        public static int PlayerFatigue { get; private set; }     // 主角战役疲劳（粮食不足惩罚，0-3）
        public static bool AmbushPending { get; private set; }    // 危机伏击待触发标记（风险达阈值时置位）

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
            InitCampaignResources();

            bool mapOk = RegionMap.Generate(ContentRegion.Plains, Random);
            RecordResolution(
                "会话初始化",
                "新游戏开始",
                "随机种子 " + Seed + "，进入地图；起始资源：粮食" + GameStartParameters.StartFood
                + " 财富" + GameStartParameters.StartWealth
                + " 声望" + GameStartParameters.StartReputation
                + " 建材" + GameStartParameters.StartBuildingMaterials
                + "；起始牌组 " + GameStartParameters.StartingDeck.Length + " 张"
                + (mapOk ? "；草原地图已生成" : "；地图生成失败"));
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
            InitCampaignResources();
            RecordResolution("测试入口", "直接进入" + DisplayName(page), "随机种子 " + Seed);

            if (page == GameState.Combat)
            {
                InitTestCombat();
            }
            else if (page == GameState.Map)
            {
                RegionMap.Generate(ContentRegion.Plains, Random);
                RecordResolution("地图初始化", "草原地图已生成", RegionMap.Nodes.Count + " 个节点 / 4 层");
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
            RewardResolver.Clear();
            RegionMap.Clear();
            Food = 0;
            Wealth = 0;
            Reputation = 0;
            Materials = 0;
            Risk = 0;
            PlayerFatigue = 0;
            AmbushPending = false;
            Changed?.Invoke();
        }

        /// <summary>初始化战役资源与风险（配置表 §2.4）。新游戏与测试入口共用。</summary>
        private static void InitCampaignResources()
        {
            Food = GameStartParameters.StartFood;
            Wealth = GameStartParameters.StartWealth;
            Reputation = GameStartParameters.StartReputation;
            Materials = GameStartParameters.StartBuildingMaterials;
            Risk = 0;
            PlayerFatigue = 0;
            AmbushPending = false;
        }

        /// <summary>
        /// 移动结算（A2-18，配置表 §2.4/§9）：先执行地图移动，成功后结算粮食消耗、
        /// 粮食不足惩罚（主角疲劳 +1、风险 +2）、风险增长（区域基础 +1，精英额外 +1）。
        /// 风险达到阈值 10 时重置为 5 并标记危机伏击待触发（实际伏击战斗留待节点内容接入）。
        /// 返回可读结算文本；移动被拒绝时返回原因且资源不变。
        /// </summary>
        public static string TryMoveToNode(int nodeIndex)
        {
            if (!RegionMap.TryMoveTo(nodeIndex, out string reason))
            {
                return "移动被拒绝：" + reason;
            }

            var node = RegionMap.Nodes[nodeIndex];
            int foodBefore = Food;

            // 粮食消耗：草原 1 / 密林 2（密林地图 A2-23 接入）
            int foodCost = RegionMap.Region == ContentRegion.Plains
                ? GameStartParameters.GrasslandMoveFoodCost
                : GameStartParameters.ForestMoveFoodCost;

            var effects = new System.Collections.Generic.List<string>();
            if (Food >= foodCost)
            {
                Food -= foodCost;
                effects.Add("粮食 -" + foodCost);
            }
            else
            {
                // 粮食不足：消耗至 0，主角疲劳 +1，风险 +2，仍可移动
                Food = 0;
                PlayerFatigue = System.Math.Min(PlayerFatigue + GameStartParameters.StarvationFatigueGain, CombatStatus.MaxFatigue);
                Risk = System.Math.Min(Risk + GameStartParameters.StarvationRiskGain, GameStartParameters.RiskThreshold);
                effects.Add("粮食不足！粮食降至 0，主角疲劳 +1（当前 " + PlayerFatigue + "），风险 +2");
            }

            // 风险增长：区域基础 + 精英额外
            int riskGain = RegionMap.Region == ContentRegion.Plains
                ? GameStartParameters.GrasslandMoveRisk
                : GameStartParameters.ForestMoveRisk;
            if (node.Type == NodeType.Elite) riskGain += GameStartParameters.EliteMoveRiskExtra;
            Risk = System.Math.Min(Risk + riskGain, GameStartParameters.RiskThreshold);
            effects.Add("风险 +" + riskGain + "（当前 " + Risk + "/" + GameStartParameters.RiskThreshold + "）");

            // 风险阈值：达到 10 → 重置 5 + 危机伏击标记（伏击战斗在节点内容接入后触发）
            bool crisis = Risk >= GameStartParameters.RiskThreshold;
            if (crisis)
            {
                Risk = GameStartParameters.RiskAfterCrisis;
                AmbushPending = true;
                effects.Add("风险达到阈值！重置为 " + GameStartParameters.RiskAfterCrisis + "，危机伏击待触发");
            }

            string result = "移动到 " + node.DisplayName + "（第 " + node.Layer + " 层 / "
                + RegionMapNode.NodeTypeName(node.Type) + "）；" + string.Join("；", effects)
                + "。粮食 " + foodBefore + " → " + Food;
            RecordResolution("地图移动", "移动到 " + node.DisplayName + "（第 " + node.Layer + " 层）", result);
            return result;
        }

        /// <summary>战斗胜利后同步主角战役疲劳（粮食不足惩罚的长期效果）。</summary>
        public static void SyncPlayerFromCombat(System.Collections.Generic.IReadOnlyList<CombatUnit> team)
        {
            foreach (var u in team)
            {
                if (u.IsPlayerCharacter)
                {
                    PlayerFatigue = u.IsAlive ? u.Fatigue : PlayerFatigue;
                    return;
                }
            }
        }

        // ---- 测试辅助（仅 EditMode 测试使用）----

        public static void SetFoodForTest(int value)
        {
            Food = System.Math.Max(0, value);
        }

        public static void SetRiskForTest(int value)
        {
            Risk = System.Math.Clamp(value, 0, GameStartParameters.RiskThreshold);
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
