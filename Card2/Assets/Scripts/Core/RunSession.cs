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

    /// <summary>事件选项的待定子选择类型（A2-19：移除卡 / 升级 / 状态移除）。</summary>
    public enum EventOptionChoiceKind
    {
        None = 0,
        RemoveCard = 1,               // 从牌组选择一张可移除卡
        UpgradeCard = 2,              // 从牌组选择一张卡升级（E07）
        StatusFatigue = 3,            // 选择 1 名存活单位移除疲劳（E10）
        StatusDiseaseOrFatigue = 4    // 选择 1 名存活单位移除疾病或疲劳（E14）
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
        public static int PlayerDisease { get; private set; }     // 主角战役疾病（A2-19，事件/战斗同步）
        public static bool AmbushPending { get; private set; }    // 危机伏击待触发标记（风险达阈值时置位）

        // ---- 事件（A2-19）----
        /// <summary>当前进行中的事件；非事件状态为 null。</summary>
        public static EventDef CurrentEvent { get; private set; }

        /// <summary>事件选项待定子选择类型（None 表示无待定选择）。</summary>
        public static EventOptionChoiceKind PendingEventChoice { get; private set; }

        /// <summary>待定子选择对应的选项索引（PendingEventChoice != None 时有效）。</summary>
        public static int PendingEventOptionIndex { get; private set; }

        /// <summary>本局已持有的遗物 ID（去重，A2-19 事件授予；遗物效果 A2-22 接入）。</summary>
        public static readonly List<string> Relics = new List<string>();

        /// <summary>事件战斗胜利额外奖励（触发事件战斗时暂存，胜利后结算一次）。</summary>
        private static EventOptionDef _pendingEventCombatReward;

        /// <summary>最近一次战斗胜利的资源奖励文本（A2-20，结算页展示用；无奖励时为 null）。</summary>
        public static string LastCombatRewardText { get; private set; }

        // ---- 建筑（A2-21，配置表 §8）----
        /// <summary>本局已建造的建筑 ID（去重，一栋只能建一次）。</summary>
        public static readonly List<string> BuiltBuildings = new List<string>();

        /// <summary>草原首领已击败（城镇建筑前置；首领遭遇胜利时置位）。</summary>
        public static bool GrasslandBossDefeated { get; private set; }

        private static bool _campBonusUsedThisRegion;        // B01：本区域首次进营地奖励已用
        private static bool _eventWealthBonusUsedThisRegion; // B05：本区域首次事件财富加成已用
        private static bool _freeUpgradePending;             // B03：首次建成后的免费升级待用

        // 遗物区域级触发标记（A2-22，配置表 §7）
        private static bool _relicCampFoodUsedThisRegion;    // R02：本区域首次进营地粮 +4 已用
        private static bool _relicClinicUsedThisRegion;      // R04：本区域首次进营地治疗已用
        private static bool _relicEventWealthUsedThisRegion; // R05：本区域首次事件财富 +5 已用

        /// <summary>营地页最近一次结算文本（展示用）。</summary>
        public static string LastCampResult { get; private set; }

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
            MarkSessionStart();
            RunRecord.Clear();
            RecordsList.Clear();
            InitCampaignResources();
            GrasslandBossDefeated = false; // 新局首领击败标记清零（测试入口间保留的进度在新局不继承）
            Relics.Clear(); // 新局遗物清零（测试入口间保留的遗物在新局不继承）
            if (CampaignDeck == null)
                CampaignDeck = new CampaignDeck(GameStartParameters.StartingDeck);

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
            MarkSessionStart();
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
            else if (page == GameState.Event)
            {
                // 测试入口：从 E01 开始，可用「上一组/下一组」翻页逐个查看 E01-E20
                if (CampaignDeck == null)
                    CampaignDeck = new CampaignDeck(GameStartParameters.StartingDeck);
                SetTestEvent(_testEventIndex);
                RecordResolution("事件初始化", "测试入口进入事件", CurrentEvent != null ? CurrentEvent.Id : "无");
            }
            else if (page == GameState.Camp)
            {
                // 测试入口营地页：补齐建造用调试资源（建材/财富/声望），便于 Play 验证建筑
                if (CampaignDeck == null)
                    CampaignDeck = new CampaignDeck(GameStartParameters.StartingDeck);
                Materials = System.Math.Max(Materials, 10);
                Wealth = System.Math.Max(Wealth, 50);
                Reputation = System.Math.Max(Reputation, 10);
                RecordResolution("测试入口", "进入营地测试",
                    "调试资源：建材 " + Materials + " / 财富 " + Wealth + " / 声望 " + Reputation);
            }
            else if (page == GameState.Camp)
            {
                // 测试入口：营地页需要战役牌组（牌组管理）
                if (CampaignDeck == null)
                    CampaignDeck = new CampaignDeck(GameStartParameters.StartingDeck);
                RecordResolution("营地初始化", "测试入口进入营地", "起始资源：粮食" + Food + " 财富" + Wealth + " 建材" + Materials + " 声望" + Reputation);
            }
        }

        private static int _testEventIndex;

        /// <summary>设置测试入口当前事件（按 EventCatalog 顺序，索引模 20）。</summary>
        private static void SetTestEvent(int index)
        {
            int i = ((index % EventCatalog.All.Length) + EventCatalog.All.Length) % EventCatalog.All.Length;
            _testEventIndex = i;
            var evt = EventCatalog.All[i];
            CurrentEvent = evt;
            PendingEventChoice = EventOptionChoiceKind.None;
            PendingEventOptionIndex = -1;
            RunRecord.Log(RecordCategory.EventChoice, "进入事件 " + evt.DisplayName + "（" + evt.Id + "）");
        }

        /// <summary>事件测试入口翻页：下一个事件（E01-E20 循环）。</summary>
        public static void NextEvent()
        {
            SetTestEvent(_testEventIndex + 1);
        }

        /// <summary>事件测试入口翻页：上一个事件（E01-E20 循环）。</summary>
        public static void PrevEvent()
        {
            SetTestEvent(_testEventIndex - 1);
        }

        /// <summary>当前测试事件名（供测试页显示）。</summary>
        public static string CurrentEventLabel()
        {
            return CurrentEvent != null ? CurrentEvent.DisplayName + "（" + CurrentEvent.Id + "）" : "无";
        }

        private static int _testEncounterIndex;

        private static void InitTestCombat()
        {
            PartnerRoster.InitTestRoster();
            var player = CombatUnit.CreatePlayer(45, 6);
            var team = PartnerRoster.BuildCombatTeam(player);

            var cfg = EncounterConfig.All[((_testEncounterIndex % EncounterConfig.All.Length) + EncounterConfig.All.Length) % EncounterConfig.All.Length];
            var enemies = new List<CombatUnit>(cfg.Enemies);

            // 使用战役牌组（首次自动初始化）
            if (CampaignDeck == null)
                CampaignDeck = new CampaignDeck(GameStartParameters.StartingDeck);
            var deck = CampaignDeck.CloneCardList();
            // 先设遭遇类型（战斗内遗物/奖励按此判断），再初始化战斗
            CombatManager.CurrentEncounterType = cfg.Type;
            CombatManager.Init(team, enemies, deck);

            RecordResolution(
                "战斗初始化",
                "测试战斗：" + cfg.Label,
                CombatManager.IsActive
                    ? "玩家队伍 " + team.Count + " 人 / 敌人 " + enemies.Count + " 个"
                    : "初始化失败（检查日志）");
        }

        /// <summary>测试入口翻页重开战斗：状态已在 Combat（Combat→Combat 为非法转移），直接重新初始化。</summary>
        public static void RelaunchTestCombat()
        {
            CombatManager.End();
            InitTestCombat();
            RecordResolution("战斗初始化", "翻页重开：" + CurrentEncounterLabel(),
                CombatManager.IsActive ? "已切换到新遭遇" : "初始化失败（检查日志）");
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
            CampaignDeck = null; // 下一局重建初始牌组（A2-19：事件获得/移除卡后测试与重开隔离）
            Food = 0;
            Wealth = 0;
            Reputation = 0;
            Materials = 0;
            Risk = 0;
            PlayerFatigue = 0;
            PlayerDisease = 0;
            AmbushPending = false;
            CurrentEvent = null;
            PendingEventChoice = EventOptionChoiceKind.None;
            PendingEventOptionIndex = -1;
            _testEventIndex = 0;
            _testEncounterIndex = 0; // 翻页索引随重置归零，避免跨测试/跨局泄漏
            // 遗物跨测试入口保留（与首领击败标记一致，测试入口共享战役进度；新游戏 StartNewGame 时清零）
            _pendingEventCombatReward = null;
            LastCombatRewardText = null;
            BuiltBuildings.Clear();
            // 首领击败标记跨测试入口保留（测试入口共享战役进度；新游戏 StartNewGame 时清零）
            _campBonusUsedThisRegion = false;
            _eventWealthBonusUsedThisRegion = false;
            _freeUpgradePending = false;
            _relicCampFoodUsedThisRegion = false;
            _relicClinicUsedThisRegion = false;
            _relicEventWealthUsedThisRegion = false;
            LastCampResult = null;
            LastSettlement = null;
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
            PlayerDisease = 0;
            AmbushPending = false;
            CurrentEvent = null;
            PendingEventChoice = EventOptionChoiceKind.None;
            PendingEventOptionIndex = -1;
            // 遗物不清空：测试入口之间共享战役进度（新游戏 StartNewGame 时统一清零）
            _pendingEventCombatReward = null;
            _relicCampFoodUsedThisRegion = false;
            _relicClinicUsedThisRegion = false;
            _relicEventWealthUsedThisRegion = false;
        }

        // ---- 事件（A2-19，配置表 §6）----

        /// <summary>从地图事件节点进入事件：从节点事件池按种子抽取一个事件并进入 Event 状态。</summary>
        public static bool StartEventFromNode(RegionMapNode node)
        {
            if (node == null || node.EventPoolIds == null || node.EventPoolIds.Length == 0) return false;
            string eventId = node.EventPoolIds[Random.Next(node.EventPoolIds.Length)];
            return StartEvent(eventId);
        }

        /// <summary>进入指定事件（Event 状态）。返回是否成功。</summary>
        public static bool StartEvent(string eventId)
        {
            var evt = EventCatalog.Find(eventId);
            if (evt == null)
            {
                CurrentEvent = null;
                RecordResolution("事件", "进入事件失败", "找不到事件：" + eventId);
                return false;
            }

            // 已在事件状态（测试入口直接设置）时跳过转移
            if (GameFlow.CurrentState != GameState.Event
                && !GameFlow.TryTransition(GameState.Event, "进入事件：" + evt.DisplayName))
            {
                return false;
            }

            CurrentEvent = evt;
            PendingEventChoice = EventOptionChoiceKind.None;
            PendingEventOptionIndex = -1;
            RunRecord.Log(RecordCategory.EventChoice, "进入事件 " + evt.DisplayName + "（" + evt.Id + "）");
            return true;
        }

        /// <summary>当前事件选项是否可选中（条件检查，返回 null 表示可选中，否则返回原因）。</summary>
        public static string EventOptionBlockReason(EventOptionDef opt)
        {
            if (opt == null) return "选项不存在";

            // 通用规则（配置表 §6）：招募目标已死亡/已离队时招募选项禁用
            if (!string.IsNullOrEmpty(opt.RecruitPartnerId))
            {
                var recruit = PartnerRoster.Find(opt.RecruitPartnerId);
                if (recruit == null) return "伙伴不存在：" + opt.RecruitPartnerId;
                if (!recruit.IsAlive) return recruit.Def.DisplayName + " 已阵亡，无法招募";
            }

            switch (opt.Condition)
            {
                case EventOptionCondition.PayResource:
                    if (Food < opt.CostFood) return "粮食不足（需要 " + opt.CostFood + "，当前 " + Food + "）";
                    if (Wealth < opt.CostWealth) return "财富不足（需要 " + opt.CostWealth + "，当前 " + Wealth + "）";
                    if (Reputation < opt.CostReputation) return "声望不足（需要 " + opt.CostReputation + "，当前 " + Reputation + "）";
                    return null;

                case EventOptionCondition.HasPartner:
                    return IsPartnerAvailable(opt.RequirePartnerId) ? null : PartnerUnavailableReason(opt.RequirePartnerId);

                case EventOptionCondition.HasPartnerAndReputation:
                    if (Reputation < opt.RequireReputation) return "声望不足（需要 " + opt.RequireReputation + "）";
                    return IsPartnerAvailable(opt.RequirePartnerId) ? null : PartnerUnavailableReason(opt.RequirePartnerId);

                case EventOptionCondition.HasPartnerOrReputation:
                    if (Reputation >= opt.RequireReputation) return null;
                    return IsPartnerAvailable(opt.RequirePartnerId) ? null : PartnerUnavailableReason(opt.RequirePartnerId);

                case EventOptionCondition.HasPartnerOrCard:
                    if (HasCard(opt.RequireCardId)) return null;
                    return IsPartnerAvailable(opt.RequirePartnerId) ? null
                        : "需要已招募 " + PartnerName(opt.RequirePartnerId) + " 或牌组拥有 " + CardName(opt.RequireCardId);

                case EventOptionCondition.HasPartnerOrPartner:
                    if (IsPartnerAvailable(opt.RequirePartnerId)) return null;
                    if (!string.IsNullOrEmpty(opt.RequirePartnerId2) && IsPartnerAvailable(opt.RequirePartnerId2)) return null;
                    return "需要已招募 " + PartnerName(opt.RequirePartnerId)
                        + (string.IsNullOrEmpty(opt.RequirePartnerId2) ? "" : " 或 " + PartnerName(opt.RequirePartnerId2));

                case EventOptionCondition.ReputationAtLeast:
                    return Reputation >= opt.RequireReputation ? null : "声望不足（需要 " + opt.RequireReputation + "）";

                case EventOptionCondition.HasRemoveableCard:
                    return CampaignDeck != null && CampaignDeck.HasRemoveableCard() ? null : "没有可移除的卡牌";
            }

            return null;
        }

        /// <summary>选择事件选项：条件校验→支付→即时结果→待定子选择或事件战斗。</summary>
        public static string ChooseEventOption(int optionIndex)
        {
            if (CurrentEvent == null) return "当前没有进行中的事件";
            if (optionIndex < 0 || optionIndex >= CurrentEvent.Options.Length) return "选项索引无效";

            var opt = CurrentEvent.Options[optionIndex];
            string block = EventOptionBlockReason(opt);
            if (block != null) return "选项不可用：" + block;

            PendingEventChoice = EventOptionChoiceKind.None;
            PendingEventOptionIndex = -1;

            // 支付资源
            string payText = "";
            if (opt.CostFood > 0) { Food -= opt.CostFood; payText += "粮食 -" + opt.CostFood + " "; }
            if (opt.CostWealth > 0) { Wealth -= opt.CostWealth; payText += "财富 -" + opt.CostWealth + " "; }
            if (opt.CostReputation > 0) { Reputation -= opt.CostReputation; payText += "声望 -" + opt.CostReputation + " "; }

            // 触发事件战斗
            if (opt.CombatEnemyIds != null && opt.CombatEnemyIds.Length > 0)
            {
                _pendingEventCombatReward = opt;
                StartEventCombat(opt);
                RecordResolution("事件", CurrentEvent.DisplayName + "：" + opt.Label,
                    (payText.Length > 0 ? payText.Trim() + "；" : "") + "触发战斗：" + opt.CombatLabel);
                return "战斗开始：" + opt.CombatLabel + "（胜利后结算事件奖励）";
            }

            // 需要子选择：先记录待定，不立即应用其余结果
            var choice = NeedChoice(opt);
            if (choice != EventOptionChoiceKind.None)
            {
                PendingEventChoice = choice;
                PendingEventOptionIndex = optionIndex;
                RecordResolution("事件", CurrentEvent.DisplayName + "：" + opt.Label,
                    (payText.Length > 0 ? payText.Trim() + "；" : "") + "需要选择");
                return ChoicePrompt(choice);
            }

            // 立即结算（含全队状态移除）
            string evtName = CurrentEvent.DisplayName;
            string result = ApplyEventOptionEffects(opt, null, null);
            FinishEvent(opt);
            RecordResolution("事件", evtName + "：" + opt.Label,
                (payText.Length > 0 ? payText.Trim() + "；" : "") + result);
            return result;
        }

        /// <summary>完成事件子选择：移除卡 / 升级卡。</summary>
        public static string ChooseEventCard(string cardId)
        {
            if (CurrentEvent == null || PendingEventChoice == EventOptionChoiceKind.None)
                return "当前没有待定的卡牌选择";
            if (PendingEventOptionIndex < 0 || PendingEventOptionIndex >= CurrentEvent.Options.Length)
                return "事件选项索引无效";

            var opt = CurrentEvent.Options[PendingEventOptionIndex];
            if (CampaignDeck == null) return "战役牌组未初始化";

            if (PendingEventChoice == EventOptionChoiceKind.RemoveCard)
            {
                if (CampaignDeck.IsInitialLockedCard(cardId) || !CampaignDeck.Cards.Contains(cardId))
                    return "该卡不可移除";
                if (!CampaignDeck.RemoveCard(cardId)) return "牌组已达下限，不能移除";
            }
            else if (PendingEventChoice == EventOptionChoiceKind.UpgradeCard)
            {
                if (!CampaignDeck.UpgradeCard(cardId)) return "该卡不能升级（不在牌组或已升级）";
            }
            else
            {
                return "当前待定选择不是卡牌";
            }

            string result = ApplyEventOptionEffects(opt, cardId, null);
            string detail = PendingEventChoice == EventOptionChoiceKind.RemoveCard
                ? "移除卡 " + cardId + "（" + CardName(cardId) + "）；" + result
                : "升级卡 " + CardName(cardId) + "；" + result;
            string evtName = CurrentEvent.DisplayName;
            PendingEventChoice = EventOptionChoiceKind.None;
            PendingEventOptionIndex = -1;
            FinishEvent(opt);
            RecordResolution("事件", evtName + "：" + opt.Label, detail);
            return detail;
        }

        /// <summary>完成事件子选择：选择存活单位移除疲劳/疾病（E10/E14）。</summary>
        public static string ChooseEventStatusUnit(string unitId, bool removeDisease)
        {
            if (CurrentEvent == null || PendingEventChoice == EventOptionChoiceKind.None)
                return "当前没有待定的单位选择";
            if (PendingEventOptionIndex < 0 || PendingEventOptionIndex >= CurrentEvent.Options.Length)
                return "事件选项索引无效";

            var opt = CurrentEvent.Options[PendingEventOptionIndex];
            string targetName;

            if (unitId == "PLAYER")
            {
                if (removeDisease)
                {
                    if (PlayerDisease <= 0) return "主角没有疾病";
                    PlayerDisease--;
                    targetName = "主角";
                }
                else
                {
                    if (PlayerFatigue <= 0) return "主角没有疲劳";
                    PlayerFatigue--;
                    targetName = "主角";
                }
            }
            else
            {
                var p = PartnerRoster.Find(unitId);
                if (p == null || !p.IsRecruited || !p.IsAlive) return "该单位不可用";
                if (removeDisease)
                {
                    if (p.Disease <= 0) return p.Def.DisplayName + " 没有疾病";
                    p.Disease--;
                }
                else
                {
                    if (p.Fatigue <= 0) return p.Def.DisplayName + " 没有疲劳";
                    p.Fatigue--;
                }

                targetName = p.Def.DisplayName;
            }

            string result = ApplyEventOptionEffects(opt, null, unitId);
            string detail = targetName + " 移除" + (removeDisease ? "疾病" : "疲劳") + "；" + result;
            string evtName = CurrentEvent.DisplayName;
            PendingEventChoice = EventOptionChoiceKind.None;
            PendingEventOptionIndex = -1;
            FinishEvent(opt);
            RecordResolution("事件", evtName + "：" + opt.Label, detail);
            return detail;
        }

        /// <summary>取消事件子选择（无可选项时由界面调用，结算记录后回到地图）。</summary>
        public static void CancelEventChoice()
        {
            if (CurrentEvent == null || PendingEventChoice == EventOptionChoiceKind.None) return;
            PendingEventChoice = EventOptionChoiceKind.None;
            PendingEventOptionIndex = -1;
            CurrentEvent = null;
            if (RegionMap.IsGenerated)
            {
                GameFlow.TryTransition(GameState.Map, "事件无可选项，返回地图");
            }
        }

        /// <summary>事件战斗胜利后结算额外奖励（由 CombatManager 胜利时调用，仅结算一次）。</summary>
        public static void ApplyPendingEventCombatRewards()
        {
            var opt = _pendingEventCombatReward;
            _pendingEventCombatReward = null;
            if (opt == null) return;

            var effects = new System.Collections.Generic.List<string>();

            if (opt.VictoryBonusWealth != 0)
            {
                Wealth = Clamp(Wealth + opt.VictoryBonusWealth, 0, GameStartParameters.MaxWealth);
                effects.Add("财富 " + Signed(opt.VictoryBonusWealth) + "（当前 " + Wealth + "）");
            }

            if (opt.VictoryBonusMaterial != 0)
            {
                Materials = Clamp(Materials + opt.VictoryBonusMaterial, 0, GameStartParameters.MaxBuildingMaterials);
                effects.Add("建材 " + Signed(opt.VictoryBonusMaterial) + "（当前 " + Materials + "）");
            }

            if (opt.VictoryBonusReputation != 0)
            {
                Reputation = Clamp(Reputation + opt.VictoryBonusReputation, 0, GameStartParameters.MaxReputation);
                effects.Add("声望 " + Signed(opt.VictoryBonusReputation) + "（当前 " + Reputation + "）");
            }

            if (!string.IsNullOrEmpty(opt.VictoryBonusCardId))
            {
                bool added = CampaignDeck != null && CampaignDeck.AddCard(opt.VictoryBonusCardId);
                effects.Add("获得卡 " + CardName(opt.VictoryBonusCardId) + (added ? "（已加入牌组）" : "（牌组已满）"));
            }

            if (!string.IsNullOrEmpty(opt.VictoryBonusRelicId) && !Relics.Contains(opt.VictoryBonusRelicId))
            {
                Relics.Add(opt.VictoryBonusRelicId);
                effects.Add("获得遗物 " + opt.VictoryBonusRelicId);
            }

            if (!string.IsNullOrEmpty(opt.VictoryBonusPartnerId))
            {
                var p = PartnerRoster.Find(opt.VictoryBonusPartnerId);
                if (p != null)
                {
                    if (p.IsRecruited)
                    {
                        p.Loyalty = System.Math.Min(100, p.Loyalty + 10);
                        effects.Add(p.Def.DisplayName + " 忠诚度 +10");
                    }
                    else
                    {
                        PartnerRoster.Recruit(opt.VictoryBonusPartnerId);
                        effects.Add("招募 " + p.Def.DisplayName);
                    }
                }
            }

            CurrentEvent = null;
            string result = effects.Count > 0 ? string.Join("；", effects) : opt.ResultText;
            RecordResolution("事件", "事件战斗胜利", result);
        }

        /// <summary>清除待结算的事件战斗奖励（战斗失败时调用，避免残留到下一场战斗）。</summary>
        public static void ClearPendingEventCombatRewards()
        {
            _pendingEventCombatReward = null;
        }

        /// <summary>
        /// 战斗胜利资源入账（A2-20，配置表 §2.4/§10）：把 RewardResolver 生成的资源奖励
        /// 钳制入账并集中记录（来源/变化量/变化后总量）。重复调用不会重复入账（Pending 已清零）。
        /// </summary>
        public static string ApplyCombatRewards()
        {
            var effects = new System.Collections.Generic.List<string>();

            if (RewardResolver.PendingWealth != 0)
            {
                Wealth = Clamp(Wealth + RewardResolver.PendingWealth, 0, GameStartParameters.MaxWealth);
                effects.Add("财富 " + Signed(RewardResolver.PendingWealth) + "（当前 " + Wealth + "）");
            }

            if (RewardResolver.PendingFood != 0)
            {
                Food = Clamp(Food + RewardResolver.PendingFood, 0, GameStartParameters.MaxFood);
                effects.Add("粮食 " + Signed(RewardResolver.PendingFood) + "（当前 " + Food + "）");
            }

            if (RewardResolver.PendingMaterials != 0)
            {
                Materials = Clamp(Materials + RewardResolver.PendingMaterials, 0, GameStartParameters.MaxBuildingMaterials);
                effects.Add("建材 " + Signed(RewardResolver.PendingMaterials) + "（当前 " + Materials + "）");
            }

            RewardResolver.PendingWealth = 0;
            RewardResolver.PendingFood = 0;
            RewardResolver.PendingMaterials = 0;

            string result = effects.Count > 0 ? string.Join("；", effects) : "无资源奖励";
            LastCombatRewardText = effects.Count > 0 ? result : null;
            RecordResolution("战斗奖励", "战斗胜利资源入账", result);
            return result;
        }

        // === 建筑（A2-21，配置表 §8）===

        public static bool HasBuilding(string id)
        {
            return BuiltBuildings.Contains(id);
        }

        // === 遗物（A2-22，配置表 §7）===

        /// <summary>获得遗物（去重；事件授予与测试入口共用）。</summary>
        public static void AddRelic(string id)
        {
            if (!Relics.Contains(id)) Relics.Add(id);
        }

        public static bool HasRelic(string id)
        {
            return Relics.Contains(id);
        }

        /// <summary>测试辅助：直接获得指定遗物（去重）。</summary>
        public static void AddRelicForTest(string id)
        {
            AddRelic(id);
        }

        /// <summary>测试辅助：重置首领击败标记（BuildingTests 隔离用；正式流程由 MarkGrasslandBossDefeated 控制）。</summary>
        public static void SetBossDefeatedForTest(bool value)
        {
            GrasslandBossDefeated = value;
        }

        /// <summary>首领遭遇胜利时调用（城镇建筑前置解锁）。</summary>
        public static void MarkGrasslandBossDefeated()
        {
            if (GrasslandBossDefeated) return;
            GrasslandBossDefeated = true;
            RecordResolution("建筑", "解锁城镇建筑", "草原首领已击败，可建造城镇建筑");
        }

        /// <summary>建造前置校验：返回禁用原因；可建造时返回 null。</summary>
        public static string BuildBlockReason(string id)
        {
            var b = BuildingCatalog.Find(id);
            if (b == null) return "建筑不存在：" + id;
            if (HasBuilding(id)) return "已建造";
            if (b.RequiresBossDefeated && !GrasslandBossDefeated) return "需要先击败草原首领";
            if (Wealth < b.CostWealth) return "财富不足（需要 " + b.CostWealth + "）";
            if (Materials < b.CostMaterial) return "建材不足（需要 " + b.CostMaterial + "）";
            if (Reputation < b.CostReputation) return "声望不足（需要 " + b.CostReputation + "）";
            return null;
        }

        /// <summary>建造一栋建筑：前置校验→扣资源→登记→记录；失败时资源不变。</summary>
        public static string TryBuildBuilding(string id)
        {
            var b = BuildingCatalog.Find(id);
            if (b == null) return "建筑不存在：" + id;
            string block = BuildBlockReason(id);
            if (block != null) return "建造失败：" + block;

            Wealth -= b.CostWealth;
            Materials -= b.CostMaterial;
            Reputation -= b.CostReputation;
            BuiltBuildings.Add(id);
            if (id == "B03") _freeUpgradePending = true; // 铁匠铺首次建成可免费升级 1 张卡

            RecordResolution("建筑", "建造 " + b.DisplayName,
                "支付 财富" + b.CostWealth + " 建材" + b.CostMaterial + " 声望" + b.CostReputation
                + "（当前 财富" + Wealth + " 建材" + Materials + " 声望" + Reputation + "）；" + b.EffectText);
            LastCampResult = "建成 " + b.DisplayName + "：" + b.EffectText;
            return LastCampResult;
        }

        /// <summary>进入营地节点结算（A2-21/A2-22）：风险 -2；B01 建成时本区域首次进营地粮食 +4；R02 铁锅同理。</summary>
        public static string EnterCampNode()
        {
            var effects = new System.Collections.Generic.List<string>();
            Risk = Clamp(Risk - GameStartParameters.CampRiskReduction, 0, GameStartParameters.RiskThreshold);
            effects.Add("风险 -" + GameStartParameters.CampRiskReduction + "（当前 " + Risk + "）");

            if (HasBuilding("B01") && !_campBonusUsedThisRegion)
            {
                _campBonusUsedThisRegion = true;
                Food = Clamp(Food + 4, 0, GameStartParameters.MaxFood);
                effects.Add("储粮帐篷：粮食 +4（当前 " + Food + "）");
            }

            if (HasRelic("R02") && !_relicCampFoodUsedThisRegion)
            {
                _relicCampFoodUsedThisRegion = true;
                Food = Clamp(Food + 4, 0, GameStartParameters.MaxFood);
                effects.Add("铁锅：粮食 +4（当前 " + Food + "）");
            }

            string result = string.Join("；", effects);
            LastCampResult = result;
            RecordResolution("营地", "进入营地", result);
            return result;
        }

        /// <summary>营地基础服务 S01 篝火休整：选择 1 名存活单位移除 1 层疲劳。
        /// 配置表的「恢复至最大生命 25%」部分留待战役生命系统（MVP 未实现战役 HP）。</summary>
        public static string CampfireRest(string unitId)
        {
            string name = UnitDisplayName(unitId);
            if (name == null) return "该单位不可用";
            if (unitId == "PLAYER")
            {
                if (PlayerFatigue <= 0) return "主角没有疲劳";
                PlayerFatigue--;
            }
            else
            {
                var p = PartnerRoster.Find(unitId);
                if (p.Fatigue <= 0) return name + " 没有疲劳";
                p.Fatigue--;
            }

            string result = name + " 移除 1 层疲劳";
            LastCampResult = result;
            RecordResolution("营地", "篝火休整", result);
            return result;
        }

        /// <summary>B02 野战医棚：选择 1 名存活单位移除受伤或 1 层疾病（MVP：移除 1 层疾病）。</summary>
        public static string CampClinic(string unitId)
        {
            return BuildingStatusService(unitId, true, "野战医棚");
        }

        /// <summary>B04 医馆：选择 1 名存活单位移除 1 层疾病或疲劳。</summary>
        public static string TownClinic(string unitId, bool removeDisease)
        {
            return BuildingStatusService(unitId, removeDisease, "医馆");
        }

        /// <summary>R04 医师药箱：每区域首次进营地时选择 1 名存活单位移除 1 层疾病或疲劳。</summary>
        public static bool RelicClinicAvailable => HasRelic("R04") && !_relicClinicUsedThisRegion;

        public static string RelicClinic(string unitId, bool removeDisease)
        {
            if (!RelicClinicAvailable) return "医师药箱本区域已使用";
            _relicClinicUsedThisRegion = true;
            string result = BuildingStatusService(unitId, removeDisease, "医师药箱");
            LastCampResult = "医师药箱：" + result;
            return LastCampResult;
        }

        private static string BuildingStatusService(string unitId, bool removeDisease, string source)
        {
            string name = UnitDisplayName(unitId);
            if (name == null) return "该单位不可用";
            if (unitId == "PLAYER")
            {
                if (removeDisease)
                {
                    if (PlayerDisease <= 0) return "主角没有疾病";
                    PlayerDisease--;
                }
                else
                {
                    if (PlayerFatigue <= 0) return "主角没有疲劳";
                    PlayerFatigue--;
                }
            }
            else
            {
                var p = PartnerRoster.Find(unitId);
                if (removeDisease)
                {
                    if (p.Disease <= 0) return name + " 没有疾病";
                    p.Disease--;
                }
                else
                {
                    if (p.Fatigue <= 0) return name + " 没有疲劳";
                    p.Fatigue--;
                }
            }

            string result = name + " 移除 1 层" + (removeDisease ? "疾病" : "疲劳");
            LastCampResult = result;
            RecordResolution("建筑", source, result);
            return result;
        }

        /// <summary>B03 铁匠铺首次建成后的免费升级待用标记。</summary>
        public static bool FreeUpgradePending => _freeUpgradePending;

        /// <summary>B03 免费升级：选择 1 张卡升级（一次性，升级后清除待用标记）。</summary>
        public static string FreeUpgradeCard(string cardId)
        {
            if (!_freeUpgradePending) return "没有待用的免费升级";
            if (CampaignDeck == null) return "战役牌组未初始化";
            if (!CampaignDeck.UpgradeCard(cardId)) return "该卡不能升级（不在牌组或已升级）";
            _freeUpgradePending = false;
            var c = CardCatalog.Find(cardId);
            string name = c != null ? c.DisplayName : cardId;
            LastCampResult = "铁匠铺免费升级：" + name + " 已升级";
            RecordResolution("建筑", "铁匠铺免费升级", name + " 已升级");
            return LastCampResult;
        }

        private static string UnitDisplayName(string unitId)
        {
            if (unitId == "PLAYER") return "主角";
            var p = PartnerRoster.Find(unitId);
            return p != null && p.IsRecruited && p.IsAlive ? p.Def.DisplayName : null;
        }

        // === 事件内部 ===

        private static bool IsPartnerAvailable(string partnerId)
        {
            if (string.IsNullOrEmpty(partnerId)) return false;
            var p = PartnerRoster.Find(partnerId);
            return p != null && p.IsRecruited && p.IsAlive;
        }

        private static string PartnerUnavailableReason(string partnerId)
        {
            var p = PartnerRoster.Find(partnerId);
            if (p == null) return "伙伴不存在：" + partnerId;
            if (!p.IsRecruited) return "需要已招募 " + p.Def.DisplayName;
            if (!p.IsAlive) return p.Def.DisplayName + " 已阵亡";
            return p.Def.DisplayName + " 不可用";
        }

        private static string PartnerName(string partnerId)
        {
            var p = PartnerRoster.Find(partnerId);
            return p != null ? p.Def.DisplayName : partnerId;
        }

        private static string CardName(string cardId)
        {
            var c = CardCatalog.Find(cardId);
            return c != null ? c.DisplayName : cardId;
        }

        private static bool HasCard(string cardId)
        {
            return CampaignDeck != null && CampaignDeck.Cards.Contains(cardId);
        }

        /// <summary>选项是否需要玩家子选择。</summary>
        private static EventOptionChoiceKind NeedChoice(EventOptionDef opt)
        {
            if (opt.RemoveCard) return EventOptionChoiceKind.RemoveCard;
            if (opt.UpgradeCard) return EventOptionChoiceKind.UpgradeCard;
            switch (opt.StatusChoice)
            {
                case EventStatusChoice.FatigueSingle: return EventOptionChoiceKind.StatusFatigue;
                case EventStatusChoice.DiseaseOrFatigueSingle: return EventOptionChoiceKind.StatusDiseaseOrFatigue;
            }

            return EventOptionChoiceKind.None;
        }

        private static string ChoicePrompt(EventOptionChoiceKind choice)
        {
            switch (choice)
            {
                case EventOptionChoiceKind.RemoveCard: return "请选择一张要移除的卡牌";
                case EventOptionChoiceKind.UpgradeCard: return "请选择一张要升级的卡牌";
                case EventOptionChoiceKind.StatusFatigue: return "请选择一名存活单位移除疲劳";
                case EventOptionChoiceKind.StatusDiseaseOrFatigue: return "请选择一名存活单位移除疾病或疲劳";
                default: return "请做出选择";
            }
        }

        /// <summary>应用选项即时结果（资源/风险/招募/获得卡/遗物/全队状态）。</summary>
        private static string ApplyEventOptionEffects(EventOptionDef opt, string removedCardId, string statusUnitId)
        {
            var effects = new System.Collections.Generic.List<string>();

            if (opt.FoodDelta != 0) { Food = Clamp(Food + opt.FoodDelta, 0, GameStartParameters.MaxFood); effects.Add("粮食 " + Signed(opt.FoodDelta) + "（当前 " + Food + "）"); }
            if (opt.WealthDelta != 0)
            {
                int wealthGain = opt.WealthDelta;
                // B05 市集 + R05 商队印记：每个区域首次通过事件获得财富时各额外 +5（可叠加）
                if (wealthGain > 0 && HasBuilding("B05") && !_eventWealthBonusUsedThisRegion)
                {
                    _eventWealthBonusUsedThisRegion = true;
                    wealthGain += 5;
                }

                if (wealthGain > 0 && HasRelic("R05") && !_relicEventWealthUsedThisRegion)
                {
                    _relicEventWealthUsedThisRegion = true;
                    wealthGain += 5;
                }

                Wealth = Clamp(Wealth + wealthGain, 0, GameStartParameters.MaxWealth);
                effects.Add("财富 " + Signed(wealthGain) + "（当前 " + Wealth + "）");
            }
            if (opt.ReputationDelta != 0) { Reputation = Clamp(Reputation + opt.ReputationDelta, 0, GameStartParameters.MaxReputation); effects.Add("声望 " + Signed(opt.ReputationDelta) + "（当前 " + Reputation + "）"); }
            if (opt.MaterialDelta != 0) { Materials = Clamp(Materials + opt.MaterialDelta, 0, GameStartParameters.MaxBuildingMaterials); effects.Add("建材 " + Signed(opt.MaterialDelta) + "（当前 " + Materials + "）"); }
            if (opt.RiskDelta != 0) { Risk = Clamp(Risk + opt.RiskDelta, 0, GameStartParameters.RiskThreshold); effects.Add("风险 " + Signed(opt.RiskDelta) + "（当前 " + Risk + "）"); }

            if (!string.IsNullOrEmpty(opt.RecruitPartnerId))
            {
                var p = PartnerRoster.Find(opt.RecruitPartnerId);
                if (p != null)
                {
                    if (p.IsRecruited)
                    {
                        // 已招募：忠诚度 +10（配置表 §6 通用规则）
                        p.Loyalty = System.Math.Min(100, p.Loyalty + 10);
                        effects.Add(p.Def.DisplayName + " 忠诚度 +10（当前 " + p.Loyalty + "）");
                    }
                    else
                    {
                        PartnerRoster.Recruit(opt.RecruitPartnerId);
                        if (opt.RecruitLoyalty >= 0) p.Loyalty = opt.RecruitLoyalty;
                        effects.Add("招募 " + p.Def.DisplayName);
                    }
                }
            }

            if (!string.IsNullOrEmpty(opt.GrantCardId))
            {
                bool added = CampaignDeck != null && CampaignDeck.AddCard(opt.GrantCardId);
                effects.Add("获得卡 " + CardName(opt.GrantCardId) + (added ? "（已加入牌组）" : "（牌组已满，未加入）"));
            }

            if (!string.IsNullOrEmpty(opt.GrantRelicId) && !Relics.Contains(opt.GrantRelicId))
            {
                Relics.Add(opt.GrantRelicId);
                effects.Add("获得遗物 " + opt.GrantRelicId);
            }

            // 全队状态移除（E11 救治）
            if (opt.StatusChoice == EventStatusChoice.DiseaseAll)
            {
                if (PlayerDisease > 0) { PlayerDisease--; effects.Add("主角 移除疾病"); }
                foreach (var p in PartnerRoster.All)
                {
                    if (p.IsRecruited && p.IsAlive && p.Disease > 0)
                    {
                        p.Disease--;
                        effects.Add(p.Def.DisplayName + " 移除疾病");
                    }
                }
            }

            if (!string.IsNullOrEmpty(opt.ResultText) && effects.Count == 0)
            {
                effects.Add(opt.ResultText);
            }

            return string.Join("；", effects);
        }

        /// <summary>事件结算完成：回到地图（若地图存在）。</summary>
        private static void FinishEvent(EventOptionDef opt)
        {
            string evtName = CurrentEvent != null ? CurrentEvent.DisplayName : "事件";
            CurrentEvent = null;
            RunRecord.Log(RecordCategory.EventChoice, "事件 " + evtName + " 结算完成");
            if (RegionMap.IsGenerated)
            {
                GameFlow.TryTransition(GameState.Map, "事件结算完成，返回地图");
            }
        }

        private static void StartEventCombat(EventOptionDef opt)
        {
            // 使用当前上阵队伍；无上阵伙伴时初始化测试队伍保证可战
            if (PartnerRoster.ActiveCount == 0) PartnerRoster.InitTestRoster();
            var player = CombatUnit.CreatePlayer(45, 6);
            var team = PartnerRoster.BuildCombatTeam(player);

            var enemies = new System.Collections.Generic.List<CombatUnit>();
            foreach (var id in opt.CombatEnemyIds)
            {
                var e = EnemyUnit.CreateById(id);
                if (e != null) enemies.Add(e);
            }

            if (CampaignDeck == null)
                CampaignDeck = new CampaignDeck(GameStartParameters.StartingDeck);
            var deck = CampaignDeck.CloneCardList();
            // 先设遭遇类型（事件战斗按普通遭遇奖励），再初始化战斗
            CombatManager.CurrentEncounterType = EncounterConfig.EncounterType.Normal;
            CombatManager.Init(team, enemies, deck);
            GameFlow.TryTransition(GameState.Combat, "事件触发战斗：" + opt.CombatLabel);
        }

        // ---- 地图节点战斗（A2-23，配置表 §9）----

        /// <summary>当前区域枚举（CombatManager 结算分流用）。</summary>
        public static ContentRegion RegionMapRegion()
        {
            return RegionMap.Region;
        }

        // ---- 结局与结算（A2-24）----

        /// <summary>结算摘要：最终牌组/伙伴/资源/建筑/遗物等（进入结算页时快照）。</summary>
        public static SettlementSummary LastSettlement { get; private set; }

        public class SettlementSummary
        {
            public string Result;           // 胜利（垂直切片）/ 失败
            public string Reason;           // 胜负原因
            public string RegionProgress;   // 区域进度（如 密林 · 第 4 层）
            public string Deck;             // 最终牌组摘要
            public string Partners;         // 伙伴摘要
            public string Resources;        // 资源摘要
            public string Buildings;        // 已建建筑
            public string Relics;           // 持有遗物
            public int Seed;                // 随机种子
            public long ElapsedSeconds;     // 本局用时（秒）
        }

        /// <summary>密林首领胜利：进入 Victory 状态（奖励结算后进结算页）。</summary>
        public static void EnterVictoryState()
        {
            if (GameFlow.CurrentState != GameState.Combat) return;
            GameFlow.TryTransition(GameState.Victory, "击败密林首领，垂直切片胜利");
        }

        /// <summary>普通/精英战斗胜利：进入奖励状态（奖励页继续后分流）。</summary>
        public static void EnterRewardState()
        {
            if (GameFlow.CurrentState != GameState.Combat) return;
            GameFlow.TryTransition(GameState.Reward, "战斗胜利，进入奖励");
        }

        /// <summary>失败：进入 Defeat 状态（战斗页/结算页处理）。</summary>
        public static void EnterDefeatState()
        {
            if (GameFlow.CurrentState != GameState.Combat) return;
            GameFlow.TryTransition(GameState.Defeat, "主角阵亡");
        }

        /// <summary>进入结算页：快照战役摘要并转移 Settlement 状态。</summary>
        public static void EnterSettlement(bool victory, string reason)
        {
            int alivePartners = 0;
            foreach (var p in PartnerRoster.All)
            {
                if (p.IsRecruited && p.IsAlive) alivePartners++;
            }

            LastSettlement = new SettlementSummary
            {
                Result = victory ? "胜利（垂直切片）" : "失败",
                Reason = reason,
                RegionProgress = RegionMap.Region == ContentRegion.Jungle
                    ? "密林 · 第 " + (RegionMap.CurrentLayer > 0 ? RegionMap.CurrentLayer : 1) + " 层"
                    : "草原 · 第 " + (RegionMap.CurrentLayer > 0 ? RegionMap.CurrentLayer : 1) + " 层",
                Deck = CampaignDeck != null ? CampaignDeck.Count + " 张" : "无",
                Partners = alivePartners + " 名存活伙伴",
                Resources = "粮食" + Food + " 财富" + Wealth + " 建材" + Materials + " 声望" + Reputation,
                Buildings = BuiltBuildings.Count + " 座",
                Relics = Relics.Count + " 件",
                Seed = Seed,
                ElapsedSeconds = (long)(UnityEngine.Time.realtimeSinceStartup - _sessionStartTime)
            };

            if (GameFlow.CurrentState == GameState.Victory || GameFlow.CurrentState == GameState.Defeat)
                GameFlow.TryTransition(GameState.Settlement, "进入结算页：" + reason);
        }

        private static float _sessionStartTime;

        private static void MarkSessionStart()
        {
            _sessionStartTime = UnityEngine.Time.realtimeSinceStartup;
        }

        /// <summary>当前区域名称（奖励池等按区域区分）。</summary>
        public static string RegionDisplayName()
        {
            return RegionMap.Region == ContentRegion.Jungle ? "密林" : "草原";
        }

        /// <summary>从地图战斗/精英/首领节点进入战斗：按种子从节点敌人池抽取敌人并初始化。</summary>
        public static bool StartNodeCombat(RegionMapNode node)
        {
            if (node == null || node.EnemyPoolIds == null || node.EnemyPoolIds.Length == 0) return false;
            if (node.Type != NodeType.Combat && node.Type != NodeType.Elite && node.Type != NodeType.Boss) return false;
            if (CombatManager.IsActive) return false;

            // 危机伏击优先：移动到普通/精英/事件节点时若标记置位则触发强制伏击（§9.1）
            if (AmbushPending && node.Type != NodeType.Boss)
            {
                return StartAmbushCombat();
            }

            if (PartnerRoster.ActiveCount == 0) PartnerRoster.InitTestRoster();
            var player = CombatUnit.CreatePlayer(45, 6);
            var team = PartnerRoster.BuildCombatTeam(player);

            var enemies = new System.Collections.Generic.List<CombatUnit>();
            string enemyId = node.EnemyPoolIds[Random.Next(node.EnemyPoolIds.Length)];
            var e = EnemyUnit.CreateById(enemyId);
            if (e != null) enemies.Add(e);

            if (CampaignDeck == null)
                CampaignDeck = new CampaignDeck(GameStartParameters.StartingDeck);
            var deck = CampaignDeck.CloneCardList();

            CombatManager.CurrentEncounterType = node.Type == NodeType.Boss
                ? EncounterConfig.EncounterType.Boss
                : (node.Type == NodeType.Elite ? EncounterConfig.EncounterType.Elite : EncounterConfig.EncounterType.Normal);
            CombatManager.Init(team, enemies, deck);
            // 状态转移：奖励/地图等状态进入 Combat（失败则不应返回成功）
            if (!GameFlow.TryTransition(GameState.Combat, "进入" + RegionMapNode.NodeTypeName(node.Type) + "节点"))
            {
                CombatManager.End();
                return false;
            }

            RecordResolution("战斗", "进入节点战斗",
                CombatManager.IsActive
                    ? CombatManager.EnemyTeam[0].DisplayName + "（" + RegionMapNode.NodeTypeName(node.Type) + "）"
                    : "初始化失败（检查日志）");
            return true;
        }

        /// <summary>危机伏击战斗（§9.1）：草原 EN01+EN02 / 密林 EN06+EN08，按精英奖励结算；触发后风险已重置 5。</summary>
        public static bool StartAmbushCombat()
        {
            if (CombatManager.IsActive) return false;
            string[] ids = RegionMap.Region == ContentRegion.Jungle
                ? new[] { "EN06", "EN08" }
                : new[] { "EN01", "EN02" };

            if (PartnerRoster.ActiveCount == 0) PartnerRoster.InitTestRoster();
            var player = CombatUnit.CreatePlayer(45, 6);
            var team = PartnerRoster.BuildCombatTeam(player);

            var enemies = new System.Collections.Generic.List<CombatUnit>();
            foreach (var id in ids)
            {
                var e = EnemyUnit.CreateById(id);
                if (e != null) enemies.Add(e);
            }

            if (CampaignDeck == null)
                CampaignDeck = new CampaignDeck(GameStartParameters.StartingDeck);
            var deck = CampaignDeck.CloneCardList();

            AmbushPending = false; // 伏击已触发
            CombatManager.CurrentEncounterType = EncounterConfig.EncounterType.Elite; // 按精英奖励结算
            CombatManager.Init(team, enemies, deck);
            if (!GameFlow.TryTransition(GameState.Combat, "危机伏击：" + ids.Length + " 名敌人"))
            {
                CombatManager.End();
                return false;
            }

            RecordResolution("战斗", "危机伏击",
                CombatManager.IsActive ? "触发伏击战斗（按精英奖励结算）" : "初始化失败（检查日志）");
            return true;
        }

        /// <summary>区域切换（A2-23）：草原首领胜利后进入密林。保留牌组/伙伴/资源/遗物/建筑，
        /// 重置区域风险与区域级一次性标记（配置表 §9 区域初始风险 0）。由胜利分支调用（Combat→Reward）。</summary>
        public static void AdvanceToNextRegion()
        {
            if (RegionMap.Region != ContentRegion.Plains) return; // 仅草原 → 密林；密林首领结局 A2-24
            if (GameFlow.CurrentState != GameState.Combat && GameFlow.CurrentState != GameState.Reward) return;

            GameFlow.TryTransition(GameState.Reward, "区域切换：进入密林");
            RegionMap.Generate(ContentRegion.Jungle, Random);
            Risk = 0;
            AmbushPending = false;
            _campBonusUsedThisRegion = false;
            _eventWealthBonusUsedThisRegion = false;
            _relicCampFoodUsedThisRegion = false;
            _relicClinicUsedThisRegion = false;
            _relicEventWealthUsedThisRegion = false;
            RecordResolution("区域切换", "进入密林",
                "保留牌组 " + (CampaignDeck != null ? CampaignDeck.Count : 0) + " 张 / 资源：粮食" + Food
                + " 财富" + Wealth + " 建材" + Materials + "；风险重置为 0；密林地图已生成");
        }

        private static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        private static string Signed(int v)
        {
            return v > 0 ? "+" + v : v.ToString();
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

            // 移动成功：进入移动状态（Map→Move；已处于 Move 时幂等，支持连续移动/测试）
            if (CurrentState == GameState.Map
                && !GameFlow.TryTransition(GameState.Move, "移动结算：" + RegionMap.Nodes[nodeIndex].DisplayName))
            {
                return "状态机拒绝移动结算";
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

        public static void SetWealthForTest(int value)
        {
            Wealth = System.Math.Clamp(value, 0, GameStartParameters.MaxWealth);
        }

        public static void SetReputationForTest(int value)
        {
            Reputation = System.Math.Clamp(value, 0, GameStartParameters.MaxReputation);
        }

        public static void SetMaterialsForTest(int value)
        {
            Materials = System.Math.Clamp(value, 0, GameStartParameters.MaxBuildingMaterials);
        }

        public static void SetPlayerFatigueForTest(int value)
        {
            PlayerFatigue = System.Math.Clamp(value, 0, CombatStatus.MaxFatigue);
        }

        public static void SetPlayerDiseaseForTest(int value)
        {
            PlayerDisease = System.Math.Clamp(value, 0, CombatStatus.MaxDisease);
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
