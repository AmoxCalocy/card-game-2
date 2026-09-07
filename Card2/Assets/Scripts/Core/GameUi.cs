using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneJourney.Core
{
    /// <summary>主界面驱动：场景只保留页面 Prefab 实例，本组件负责页面切换与运行时交互绑定。</summary>
    public sealed class GameUi : MonoBehaviour
    {
        [Header("根引用")]
        [SerializeField] private Text _hudText;
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private GameObject _menuContent;

        [Header("按运行配置显隐的元素")]
        [SerializeField] private List<GameObject> _testEntryElements = new List<GameObject>();
        [SerializeField] private List<GameObject> _modeSwitchElements = new List<GameObject>();

        [Header("按钮绑定")]
        [SerializeField] private Button _startNewGameButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Text _saveStatusText;
        [SerializeField] private InputField _seedInput;
        [SerializeField] private Button _startWithSeedButton;
        [SerializeField] private Button[] _testEntryButtons;
        [SerializeField] private GameState[] _testEntryStates;
        [SerializeField] private Button[] _modeSwitchButtons;
        [SerializeField] private GameMode[] _modeSwitchModes;
        [SerializeField] private Button _quitButton;

        [Header("地图页（A2-17 / 布局优化）")]
        [SerializeField] private MapPageView _mapPageView;

        [Header("营地页（A2-21 / 布局优化）")]
        [SerializeField] private Transform _campOptionContainer;
        [SerializeField] private GameObject _campLayoutRoot;
        [SerializeField] private Transform _campTeamContainer;
        [SerializeField] private Transform _campFacilityContainer;
        [SerializeField] private TMP_Text _campFacilityTitleText;
        [SerializeField] private CampTeamCardView _campTeamCardPrefab;
        [SerializeField] private CampFacilityCardView _campFacilityCardPrefab;

        [Header("结算页面（A2-24 / Prefab）")]
        [SerializeField] private GameObject _victoryPage;
        [SerializeField] private FailurePageView _failurePageView;

        private Button _mapMenuButton;
        private Button _eventMenuButton;
        private Button _eventPreviousButton;
        private Button _eventNextButton;
        private Button _campMenuButton;
        private TMP_Text _campTitleText;
        private TMP_Text _campResourceText;
        private TMP_Text _campFeedbackText;
        private Button _failureMenuButton;
        private TMP_Text _victoryTitleText;
        private TMP_Text _victoryReasonText;
        private TMP_Text _victoryDetailText;
        private TMP_Text _victoryRestartText;
        private Button _victoryMenuButton;
        private Button _victoryRestartButton;

        private enum CampPageMode { None, Rest, ClinicCamp, ClinicTown, ClinicRelic, FreeUpgrade, DeckView }
        private CampPageMode _campMode;

        [Header("事件页面（A2-19 / 布局优化）")]
        [SerializeField] private EventPageView _eventPageView;
        private string _eventFeedback;

        [Header("战斗界面（A1-14）")]
        [SerializeField] private BattleView _battleView;

        private void Awake()
        {
            ResolvePrefabPageRefs();
            BindButtons();

            RunSession.Changed += Refresh;
            GameConfigProvider.Changed += RefreshConfigUi;
            GameFlow.Changed += Refresh;
            CampaignSaveService.Changed += RefreshSaveUi;

            ShowMenu();
            RefreshConfigUi();
        }

        private void OnDestroy()
        {
            RunSession.Changed -= Refresh;
            GameConfigProvider.Changed -= RefreshConfigUi;
            GameFlow.Changed -= Refresh;
            CampaignSaveService.Changed -= RefreshSaveUi;
        }

        private IEnumerator Start()
        {
            yield return null;

            if (_menuContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_menuContent.transform);
            }
        }

        private void ResolvePrefabPageRefs()
        {
            if (_mapPageView != null)
                _mapMenuButton = _mapPageView.transform.Find("HeaderPanel/ReturnToMenuButton")?.GetComponent<Button>();

            if (_eventPageView != null)
            {
                _eventMenuButton = _eventPageView.transform.Find("OptionsPanel/ReturnToMenuButton")?.GetComponent<Button>();
                _eventPreviousButton = _eventPageView.transform.Find("OptionsPanel/TestControls/PreviousButton")?.GetComponent<Button>();
                _eventNextButton = _eventPageView.transform.Find("OptionsPanel/TestControls/NextButton")?.GetComponent<Button>();
            }

            if (_campOptionContainer != null)
            {
                _campMenuButton = _campOptionContainer.Find("HeaderPanel/ReturnToMenuButton")?.GetComponent<Button>();
                _campTitleText = _campOptionContainer.Find("HeaderPanel/Title")?.GetComponent<TMP_Text>();
                _campResourceText = _campOptionContainer.Find("HeaderPanel/Resources")?.GetComponent<TMP_Text>();
                _campFeedbackText = _campOptionContainer.Find("HeaderPanel/Feedback")?.GetComponent<TMP_Text>();
            }

            if (_failurePageView != null)
                _failureMenuButton = _failurePageView.transform.Find("FailureCard/ReturnToMenuButton")?.GetComponent<Button>();

            if (_victoryPage != null)
            {
                Transform card = _victoryPage.transform.Find("VictoryCard");
                _victoryTitleText = card?.Find("Title")?.GetComponent<TMP_Text>();
                _victoryReasonText = card?.Find("Reason")?.GetComponent<TMP_Text>();
                _victoryDetailText = card?.Find("Detail")?.GetComponent<TMP_Text>();
                _victoryMenuButton = card?.Find("ReturnToMenuButton")?.GetComponent<Button>();
                _victoryRestartButton = card?.Find("RestartButton")?.GetComponent<Button>();
                _victoryRestartText = card?.Find("RestartButton/Text")?.GetComponent<TMP_Text>();
            }
        }

        private void BindButtons()
        {
            if (_startNewGameButton == null || _quitButton == null)
            {
                Debug.LogError("[GameUi] 主菜单关键按钮引用缺失，请检查 MainMenu Prefab 与 GameUi 绑定", this);
                return;
            }

            _startNewGameButton.onClick.AddListener(OnStartNewGame);
            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinueGame);
            _quitButton.onClick.AddListener(OnQuit);
            if (_mapMenuButton != null) _mapMenuButton.onClick.AddListener(ReturnToMenu);
            if (_eventMenuButton != null) _eventMenuButton.onClick.AddListener(ReturnToMenu);
            if (_eventPreviousButton != null) _eventPreviousButton.onClick.AddListener(OnPrevEncounter);
            if (_eventNextButton != null) _eventNextButton.onClick.AddListener(OnNextEncounter);
            if (_campMenuButton != null) _campMenuButton.onClick.AddListener(ReturnToMenu);
            if (_failureMenuButton != null) _failureMenuButton.onClick.AddListener(ReturnToMenu);
            if (_victoryMenuButton != null) _victoryMenuButton.onClick.AddListener(ReturnToMenu);
            if (_victoryRestartButton != null) _victoryRestartButton.onClick.AddListener(RestartWithSameSeed);

            if (_startWithSeedButton != null)
            {
                _startWithSeedButton.onClick.AddListener(OnStartWithSeed);
            }

            int testCount = Math.Min(_testEntryButtons.Length, _testEntryStates.Length);
            for (int i = 0; i < testCount; i++)
            {
                GameState state = _testEntryStates[i];
                if (state == GameState.None || state == GameState.MainMenu)
                {
                    Debug.LogWarning("[GameUi] 测试入口按钮 " + _testEntryButtons[i].name + " 对应的状态无效，已跳过绑定", this);
                    continue;
                }

                GameState captured = state;
                _testEntryButtons[i].onClick.AddListener(() => OnEnterTestPage(captured));
            }

            int modeCount = Math.Min(_modeSwitchButtons.Length, _modeSwitchModes.Length);
            for (int i = 0; i < modeCount; i++)
            {
                GameMode captured = _modeSwitchModes[i];
                _modeSwitchButtons[i].onClick.AddListener(() => GameConfigProvider.ApplyMode(captured));
            }
        }

        private void ShowMenu()
        {
            HidePrefabPages();
            if (_menuPanel != null) _menuPanel.SetActive(true);

            bool showTestTools = GameConfigProvider.TestToolsEnabled;
            if (_seedInput != null) _seedInput.gameObject.SetActive(showTestTools);
            if (_startWithSeedButton != null) _startWithSeedButton.gameObject.SetActive(showTestTools);
            RefreshSaveUi();
            RefreshConfigUi();
        }

        private void HidePrefabPages()
        {
            if (_battleView != null) _battleView.Hide();
            if (_mapPageView != null) _mapPageView.gameObject.SetActive(false);
            if (_eventPageView != null) _eventPageView.gameObject.SetActive(false);
            if (_campOptionContainer != null) _campOptionContainer.gameObject.SetActive(false);
            if (_victoryPage != null) _victoryPage.SetActive(false);
            if (_failurePageView != null) _failurePageView.gameObject.SetActive(false);
        }

        private void RefreshSaveUi()
        {
            if (_continueButton != null) _continueButton.interactable = CampaignSaveService.HasValidSave;
            if (_saveStatusText != null) _saveStatusText.text = CampaignSaveService.StatusMessage;
        }

        private void ShowPage(string title, string description)
        {
            _ = title;
            _ = description;

            if (_menuPanel != null) _menuPanel.SetActive(false);
            HidePrefabPages();

            bool shown = false;
            if (RunSession.CurrentState == GameState.Combat && CombatManager.IsActive && _battleView != null)
            {
                _battleView.Show();
                _battleView.Refresh();
                shown = true;
            }
            else if (RunSession.CurrentState == GameState.Map && RegionMap.IsGenerated && _mapPageView != null)
            {
                _mapPageView.gameObject.SetActive(true);
                RefreshMapPage();
                shown = true;
            }
            else if (RunSession.CurrentState == GameState.Event && RunSession.CurrentEvent != null && _eventPageView != null)
            {
                _eventPageView.gameObject.SetActive(true);
                RefreshEventOptions();
                shown = true;
            }
            else if (RunSession.CurrentState == GameState.Camp && _campOptionContainer != null)
            {
                _campOptionContainer.gameObject.SetActive(true);
                if (_campLayoutRoot != null) _campLayoutRoot.SetActive(true);
                shown = true;
            }
            else if (RunSession.CurrentState == GameState.Settlement)
            {
                bool failure = RunSession.LastSettlement != null
                    && RunSession.LastSettlement.Result == "失败";
                if (failure && _failurePageView != null)
                {
                    _failurePageView.gameObject.SetActive(true);
                    shown = true;
                }
                else if (!failure && _victoryPage != null)
                {
                    _victoryPage.SetActive(true);
                    shown = true;
                }
            }
            else if (RegionMap.IsGenerated && _mapPageView != null)
            {
                _mapPageView.gameObject.SetActive(true);
                RefreshMapPage();
                shown = true;
            }

            if (!shown)
                Debug.LogWarning("[GameUi] 当前状态没有可显示的页面 Prefab：" + RunSession.CurrentState, this);

            RefreshConfigUi();
        }

        private void OnStartNewGame()
        {
            RunSession.StartNewGame();
            ShowPage("地图（新游戏入口）", BuildMapDescription());
        }

        private void StartFreshGameFromFailure()
        {
            RunSession.Reset();
            OnStartNewGame();
        }

        private void OnContinueGame()
        {
            if (!RunSession.TryContinue(out string message))
            {
                RefreshSaveUi();
                return;
            }

            switch (RunSession.CurrentState)
            {
                case GameState.Combat:
                    ShowPage("战斗（继续游戏）", BuildCombatDescription());
                    break;
                case GameState.Event:
                    ShowEventPage();
                    break;
                case GameState.Camp:
                    _campMode = CampPageMode.None;
                    ShowCampPage(message);
                    break;
                default:
                    ShowPage("地图（继续游戏）", BuildMapDescription() + "\n" + message);
                    break;
            }
        }

        private void OnStartWithSeed()
        {
            // A2-24：结算页的「同种子重开」——直接用本局种子重开
            if (RunSession.CurrentState == GameState.Settlement)
            {
                RestartWithSameSeed();
                return;
            }

            int? seed = null;
            if (_seedInput != null && int.TryParse(_seedInput.text, out int parsed))
            {
                seed = parsed;
            }

            RunSession.StartNewGame(seed);
            ShowPage("地图（指定种子）",
                "新游戏会话已创建（" + (seed.HasValue ? "种子 " + seed.Value : "随机种子") + "）。\n" + BuildMapDescription());
        }

        private void OnEnterTestPage(GameState page)
        {
            RunSession.EnterTestPage(page);
            string desc;
            if (page == GameState.Combat)
            {
                desc = "遭遇：" + RunSession.CurrentEncounterLabel() + "\n";
                desc += CombatManager.IsActive ? BuildCombatDescription() : "点击「上一组 / 下一组」切换敌人，返回主菜单再次进入测试。";
            }
            else if (page == GameState.Map)
            {
                desc = BuildMapDescription();
            }
            else if (page == GameState.Event)
            {
                desc = "事件测试入口：点击「上一组 / 下一组」在 E01-E20 间切换。\n";
                var evt = RunSession.CurrentEvent;
                if (evt != null)
                {
                    desc += evt.DisplayName + "（" + evt.Id + "）\n" + evt.Description;
                    desc += "\n" + BuildResourceLine();
                }
            }
            else if (page == GameState.Camp)
            {
                _campMode = CampPageMode.None;
                ShowCampPage("");
                return;
            }
            else
            {
                desc = "当前为占位页面，用于验证测试入口与退出清理。后续步骤将在此实现" + RunSession.DisplayName(page) + "流程。";
            }

            ShowPage("测试入口：" + RunSession.DisplayName(page), desc);
        }

        /// <summary>显示由 Prefab 承载的胜利或失败结算页。</summary>
        public void ShowSettlement()
        {
            var s = RunSession.LastSettlement;
            if (s == null)
            {
                Debug.LogWarning("[GameUi] 无结算数据，返回主菜单", this);
                ReturnToMenu();
                return;
            }

            if (s.Result == "失败" && _failurePageView != null)
            {
                _failurePageView.SetFailure(s, StartFreshGameFromFailure);
                ShowPage("失败", string.Empty);
                return;
            }

            SetVictoryPage(s);
            ShowPage("胜利", string.Empty);
        }

        private void SetVictoryPage(RunSession.SettlementSummary summary)
        {
            if (_victoryTitleText != null) _victoryTitleText.text = "旅途凯旋";
            if (_victoryReasonText != null)
                _victoryReasonText.text = string.IsNullOrEmpty(summary.Reason) ? "你完成了本次远征" : summary.Reason;
            if (_victoryDetailText != null)
            {
                _victoryDetailText.text = "抵达 " + summary.RegionProgress
                    + "  ·  用时 " + summary.ElapsedSeconds + " 秒"
                    + "\n牌组 " + summary.Deck + "  ·  伙伴 " + summary.Partners
                    + "\n资源 " + summary.Resources
                    + "\n建筑 " + summary.Buildings + "  ·  遗物 " + summary.Relics
                    + "\n随机种子：" + summary.Seed;
            }
            if (_victoryRestartText != null) _victoryRestartText.text = "同种子重开";
        }

        private void OnPrevEncounter()
        {
            if (RunSession.CurrentState != GameState.Event) return;
            RunSession.PrevEvent();
            ShowEventPage();
        }

        private void OnNextEncounter()
        {
            if (RunSession.CurrentState != GameState.Event) return;
            RunSession.NextEvent();
            ShowEventPage();
        }

        public void ReturnToMenu()
        {
            RunSession.Reset();
            ShowMenu();
        }

        /// <summary>结算页「同种子重开」：用当前局种子重新开始（新局清空战役进度）。</summary>
        public void RestartWithSameSeed()
        {
            int seed = RunSession.Seed;
            RunSession.Reset();
            RunSession.StartNewGame(seed);
            ShowPage("地图（同种子重开）", BuildMapDescription());
        }

        /// <summary>返回地图页（A2-23：奖励结算/区域切换后继续地图）。</summary>
        public void ReturnToMap()
        {
            if (RunSession.CurrentState == GameState.Reward)
            {
                RunSession.CompleteRewardAndReturnToMap(out _);
            }
            else
            {
                CombatManager.End();
                if (GameFlow.TryTransition(GameState.Map, "返回地图"))
                    RunSession.SaveMapCheckpoint(out _);
            }
            ShowPage("地图", BuildMapDescription());
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Refresh()
        {
            // 战斗刷新不依赖测试 HUD 是否存在。
            if (_battleView != null && CombatManager.IsActive)
            {
                _battleView.Refresh();
            }

            if (_hudText == null)
            {
                return;
            }

            var last = RunSession.LastResolution;
            string lastText = last.HasValue
                ? last.Value.Source + "：" + last.Value.Description + " → " + last.Value.Result
                : "暂无";

            var log = GameFlow.Log;
            string lastTransitionText;
            if (log.Count == 0)
            {
                lastTransitionText = "暂无";
            }
            else
            {
                int start = Math.Max(0, log.Count - 3);
                var lines = new string[log.Count - start];
                for (int i = start; i < log.Count; i++)
                {
                    var t = log[i];
                    lines[i - start] = RunSession.DisplayName(t.From) + " → " + RunSession.DisplayName(t.To) + "（" + t.Reason + "）";
                }

                lastTransitionText = string.Join("\n", lines);
            }

            string validationText = ContentRegistry.HasBlockingIssues
                ? "内容校验：" + ContentRegistry.Issues.Count + " 个问题（首个：" + ContentRegistry.Issues[0] + "）"
                : "内容校验：OK";

            int recordCount = RunRecord.Count;
            string recordText;
            if (recordCount == 0)
            {
                recordText = "本局记录：暂无";
            }
            else
            {
                var lastEntry = RunRecord.Entries[recordCount - 1];
                recordText = "本局记录：" + recordCount + " 条（最新：" + RunRecordEntry.CategoryName(lastEntry.Category) + " #" + lastEntry.Index + "）";
            }

            _hudText.text = string.Format(
                "随机种子：{0}\n当前状态：{1}\n当前配置：{2}\n最近一次规则结算：{3}\n最近状态切换：{4}\n{5}\n{6}",
                RunSession.Seed,
                RunSession.DisplayName(RunSession.CurrentState),
                GameConfigProvider.Mode,
                lastText,
                lastTransitionText,
                validationText,
                recordText);
        }

        private void RefreshConfigUi()
        {
            var config = GameConfigProvider.Active;
            bool showTestTools = GameConfigProvider.TestToolsEnabled;

            SetElementsActive(_testEntryElements, showTestTools);
            SetElementsActive(_modeSwitchElements, showTestTools);
            if (_hudText != null) _hudText.gameObject.SetActive(config != null && config.ShowTestHud);

            bool isMenu = _menuPanel != null && _menuPanel.activeSelf;
            if (_seedInput != null) _seedInput.gameObject.SetActive(showTestTools && isMenu);
            if (_startWithSeedButton != null) _startWithSeedButton.gameObject.SetActive(showTestTools && isMenu);

            bool showEventTools = showTestTools
                && _eventPageView != null
                && _eventPageView.gameObject.activeSelf
                && RunSession.CurrentState == GameState.Event;
            if (_eventPreviousButton != null) _eventPreviousButton.gameObject.SetActive(showEventTools);
            if (_eventNextButton != null) _eventNextButton.gameObject.SetActive(showEventTools);

            if (_menuContent != null && _menuContent.activeInHierarchy)
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_menuContent.transform);

            Refresh();
        }

        private void RefreshMapPage()
        {
            if (_mapPageView == null) return;

            bool showMap = RunSession.CurrentState == GameState.Map && RegionMap.IsGenerated;
            _mapPageView.gameObject.SetActive(showMap);
            if (!showMap) return;

            _mapPageView.SetMap(
                RegionMap.Region,
                RegionMap.Nodes,
                RegionMap.Path,
                RegionMap.CurrentNodeIndex,
                RegionMap.VisitedIndexes,
                RegionMap.ReachableNext(),
                BuildResourceLine(),
                BuildMapRiskHint(),
                OnMapNodeClicked);
        }

        private void OnMapNodeClicked(int nodeIndex)
        {
            string result = RunSession.TryMoveToNode(nodeIndex);

            if (RunSession.CurrentState == GameState.Map)
            {
                ShowPage("地图", BuildMapDescription());
                return;
            }

            // 移动成功：按节点类型进入内容
            var node = RegionMap.Nodes[nodeIndex];
            if (node.Type == NodeType.Event)
            {
                // 事件节点：伏击优先（§9.1）
                if (RunSession.AmbushPending && RunSession.StartAmbushCombat())
                {
                    ShowPage("地图", BuildMapDescription() + "\n触发危机伏击！");
                    return;
                }

                RunSession.StartEventFromNode(node);
                ShowEventPage();
                return;
            }

            if (node.Type == NodeType.Camp)
            {
                // A2-21：营地节点进入（风险 -2 + B01 首次粮食）；移动状态 → 营地状态
                GameFlow.TryTransition(GameState.Camp, "进入营地节点");
                _campMode = CampPageMode.None;
                ShowCampPage(RunSession.EnterCampNode());
                return;
            }

            // 战斗/精英/首领节点：进入节点战斗（A2-23）
            if (RunSession.StartNodeCombat(node))
            {
                ShowPage("地图", BuildMapDescription() + "\n" + result);
                return;
            }

            // 初始化失败：停留在地图展示结算
            ShowPage("地图", BuildMapDescription() + "\n" + result);
        }

        /// <summary>四资源显示行（含上限），地图/事件页共用。</summary>
        private static string BuildResourceLine()
        {
            return "资源：粮食 " + RunSession.Food + "/" + GameStartParameters.MaxFood
                + " / 财富 " + RunSession.Wealth + "/" + GameStartParameters.MaxWealth
                + " / 声望 " + RunSession.Reputation + "/" + GameStartParameters.MaxReputation
                + " / 建材 " + RunSession.Materials + "/" + GameStartParameters.MaxBuildingMaterials;
        }

        // === 营地页（A2-21）===

        private void ShowCampPage(string result)
        {
            if (_campTitleText != null) _campTitleText.text = "营地整备";
            if (_campResourceText != null) _campResourceText.text = BuildResourceLine();
            if (_campFeedbackText != null)
            {
                _campFeedbackText.text = string.IsNullOrEmpty(result)
                    ? "左侧查看队伍状态；右侧选择营地服务或建筑入口。"
                    : "最近结算：" + result;
            }

            ShowPage("营地整备", string.Empty);
            RefreshCampButtons();
        }

        private static string CampCostText(BuildingDef b)
        {
            var costs = new System.Collections.Generic.List<string>();
            if (b.CostWealth > 0) costs.Add(b.CostWealth + " 财");
            if (b.CostMaterial > 0) costs.Add(b.CostMaterial + " 建材");
            if (b.CostReputation > 0) costs.Add(b.CostReputation + " 声望");
            return costs.Count > 0 ? string.Join("+", costs) : "无成本";
        }

        private void RefreshCampButtons()
        {
            ResolveCampLayoutRefs();
            if (_campTeamContainer == null || _campFacilityContainer == null) return;
            if (_campTeamCardPrefab == null || _campFacilityCardPrefab == null)
            {
                Debug.LogError("[GameUi] 营地卡片 Prefab 引用缺失", this);
                return;
            }

            ClearChildren(_campTeamContainer);
            ClearChildren(_campFacilityContainer);
            if (_campLayoutRoot != null) _campLayoutRoot.SetActive(true);

            RenderCampTeamRoster();

            if (_campFacilityTitleText != null) _campFacilityTitleText.text = CampModeTitle();
            if (_campMode == CampPageMode.None)
            {
                RenderCampMainFacilities();
            }
            else if (_campMode == CampPageMode.FreeUpgrade)
            {
                RenderCampUpgradeCards();
            }
            else if (_campMode == CampPageMode.DeckView)
            {
                RenderCampDeck();
            }
            else
            {
                RenderCampSelectionPrompt();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_campTeamContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_campFacilityContainer);
        }

        private void RenderCampMainFacilities()
        {
            bool hasFatigue = RunSession.PlayerFatigue > 0 || AnyCampPartnerWith(p => p.Fatigue > 0);
            var rest = MakeCampFacilityButton("篝火休整", "选择一名队员移除 1 层疲劳", !hasFatigue,
                new Color(0.42f, 0.30f, 0.20f));
            if (hasFatigue) rest.GetComponent<Button>().onClick.AddListener(() => { _campMode = CampPageMode.Rest; ShowCampPage(""); });

            var deck = MakeCampFacilityButton("牌组管理",
                "查看当前战役牌组 · " + (RunSession.CampaignDeck != null ? RunSession.CampaignDeck.Count : 0) + " 张",
                RunSession.CampaignDeck == null, new Color(0.25f, 0.34f, 0.48f));
            if (RunSession.CampaignDeck != null)
                deck.GetComponent<Button>().onClick.AddListener(() => { _campMode = CampPageMode.DeckView; ShowCampPage(""); });

            foreach (var building in BuildingCatalog.All)
            {
                bool built = RunSession.HasBuilding(building.Id);
                if (!built)
                {
                    string block = RunSession.BuildBlockReason(building.Id);
                    string detail = "未建设 · " + CampCostText(building) + "\n" + building.EffectText;
                    if (block != null) detail += "\n锁定：" + block;
                    var build = MakeCampFacilityButton(building.DisplayName, detail, block != null,
                        new Color(0.30f, 0.38f, 0.48f));
                    if (block == null)
                    {
                        string captured = building.Id;
                        build.GetComponent<Button>().onClick.AddListener(() =>
                        {
                            string result = RunSession.TryBuildBuilding(captured);
                            ShowCampPage(result);
                        });
                    }
                    continue;
                }

                bool serviceAvailable = false;
                string builtDetail = "已建成\n" + building.EffectText;
                CampPageMode nextMode = CampPageMode.None;
                if (building.Id == "B02")
                {
                    serviceAvailable = true;
                    nextMode = CampPageMode.ClinicCamp;
                    builtDetail = "已建成 · 可使用\n选择一名队员移除 1 层疾病";
                }
                else if (building.Id == "B03")
                {
                    serviceAvailable = RunSession.FreeUpgradePending;
                    nextMode = CampPageMode.FreeUpgrade;
                    builtDetail = serviceAvailable ? "已建成 · 免费升级待使用\n选择一张卡牌升级" : "已建成\n本次免费升级已使用";
                }
                else if (building.Id == "B04")
                {
                    serviceAvailable = true;
                    nextMode = CampPageMode.ClinicTown;
                    builtDetail = "已建成 · 可使用\n选择一名队员移除疾病或疲劳";
                }

                var builtEntry = MakeCampFacilityButton(building.DisplayName, builtDetail, !serviceAvailable,
                    new Color(0.25f, 0.42f, 0.32f));
                if (serviceAvailable)
                {
                    CampPageMode capturedMode = nextMode;
                    builtEntry.GetComponent<Button>().onClick.AddListener(() => { _campMode = capturedMode; ShowCampPage(""); });
                }
            }

            if (RunSession.HasRelic("R04"))
            {
                bool available = RunSession.RelicClinicAvailable;
                var relic = MakeCampFacilityButton("医师药箱（遗物）",
                    available ? "本区域可使用一次\n选择队员移除疾病或疲劳" : "本区域已经使用",
                    !available, new Color(0.48f, 0.38f, 0.20f));
                if (available)
                    relic.GetComponent<Button>().onClick.AddListener(() => { _campMode = CampPageMode.ClinicRelic; ShowCampPage(""); });
            }

            var leave = MakeCampFacilityButton("离开营地", "返回区域地图，继续旅程", false,
                new Color(0.42f, 0.24f, 0.22f));
            leave.GetComponent<Button>().onClick.AddListener(OnCampLeave);
        }

        private void RenderCampSelectionPrompt()
        {
            MakeCampFacilityButton(CampModeTitle(), CampModeInstruction(), true,
                new Color(0.28f, 0.32f, 0.40f));
            var back = MakeCampFacilityButton("返回设施列表", "取消当前选择", false,
                new Color(0.25f, 0.34f, 0.48f));
            back.GetComponent<Button>().onClick.AddListener(() => { _campMode = CampPageMode.None; ShowCampPage(""); });
        }

        private string CampModeTitle()
        {
            switch (_campMode)
            {
                case CampPageMode.Rest: return "篝火休整";
                case CampPageMode.ClinicCamp: return "野战医棚";
                case CampPageMode.ClinicTown: return "医馆服务";
                case CampPageMode.ClinicRelic: return "医师药箱";
                case CampPageMode.FreeUpgrade: return "铁匠铺 · 免费升级";
                case CampPageMode.DeckView: return "战役牌组";
                default: return "设施与建筑";
            }
        }

        private string CampModeInstruction()
        {
            switch (_campMode)
            {
                case CampPageMode.Rest: return "请在左侧队伍中选择有疲劳的成员。";
                case CampPageMode.ClinicCamp: return "请在左侧队伍中选择有疾病的成员。";
                case CampPageMode.ClinicTown:
                case CampPageMode.ClinicRelic: return "请在左侧选择队员，并选择移除疲劳或疾病。";
                default: return "请选择操作。";
            }
        }

        private void RenderCampTeamRoster()
        {
            MakeCampTeamCard("PLAYER", "主角", "上阵 · 指挥核心",
                true, RunSession.PlayerFatigue, RunSession.PlayerDisease);

            foreach (var partner in PartnerRoster.All)
            {
                if (!partner.IsRecruited) continue;
                string position = partner.IsAlive
                    ? (partner.IsInActiveTeam ? "上阵" : "后备")
                    : "阵亡";
                string detail = position + " · " + partner.Def.Role
                    + " · HP " + partner.CurrentHp + "/" + partner.EffectiveMaxHp
                    + " · 忠诚 " + partner.Loyalty;
                MakeCampTeamCard(partner.Def.Id, partner.Def.DisplayName, detail,
                    partner.IsAlive, partner.Fatigue, partner.Disease);
            }
        }

        private void MakeCampTeamCard(string unitId, string displayName, string detail,
            bool alive, int fatigue, int disease)
        {
            var view = Instantiate(_campTeamCardPrefab, _campTeamContainer);
            view.name = "CampDynamic_Team_" + unitId;
            view.SetContent(displayName, detail, alive, fatigue, disease);

            bool canFatigue = alive && fatigue > 0
                && (_campMode == CampPageMode.Rest || _campMode == CampPageMode.ClinicTown || _campMode == CampPageMode.ClinicRelic);
            bool canDisease = alive && disease > 0
                && (_campMode == CampPageMode.ClinicCamp || _campMode == CampPageMode.ClinicTown || _campMode == CampPageMode.ClinicRelic);

            if (canFatigue)
            {
                string captured = unitId;
                view.SetPrimaryAction(
                    _campMode == CampPageMode.Rest ? "休整" : "减疲劳",
                    new Color(0.42f, 0.30f, 0.20f),
                    () => CampServiceChosen(captured, false));
            }

            if (canDisease)
            {
                string captured = unitId;
                view.SetSecondaryAction("治病", new Color(0.25f, 0.42f, 0.32f),
                    () => CampServiceChosen(captured, true));
            }
        }

        private void CampServiceChosen(string unitId, bool removeDisease)
        {
            string result;
            if (_campMode == CampPageMode.Rest) result = RunSession.CampfireRest(unitId);
            else if (_campMode == CampPageMode.ClinicCamp) result = RunSession.CampClinic(unitId);
            else if (_campMode == CampPageMode.ClinicRelic) result = RunSession.RelicClinic(unitId, removeDisease);
            else result = RunSession.TownClinic(unitId, removeDisease);
            _campMode = CampPageMode.None;
            ShowCampPage(result);
        }

        private void RenderCampUpgradeCards()
        {
            bool any = false;
            if (RunSession.CampaignDeck != null)
            {
                var seen = new HashSet<string>();
                foreach (var id in RunSession.CampaignDeck.Cards)
                {
                    if (!seen.Add(id) || RunSession.CampaignDeck.UpgradedCards.Contains(id)) continue;
                    var card = CardCatalog.Find(id);
                    string displayName = card != null ? card.DisplayName : id;
                    var go = MakeCampFacilityButton("升级 " + displayName,
                        card != null ? card.EffectText : id, false, new Color(0.30f, 0.38f, 0.48f));
                    string captured = id;
                    go.GetComponent<Button>().onClick.AddListener(() =>
                    {
                        string result = RunSession.FreeUpgradeCard(captured);
                        _campMode = CampPageMode.None;
                        ShowCampPage(result);
                    });
                    any = true;
                }
            }

            if (!any)
                MakeCampFacilityButton("没有可升级卡牌", "牌组中的卡牌均已升级或牌组不可用", true,
                    new Color(0.22f, 0.24f, 0.28f));
            AddCampBackButton();
        }

        /// <summary>营地牌组管理：右侧设施区列出战役牌组全部卡牌（含数量）与升级标记。</summary>
        private void RenderCampDeck()
        {
            if (RunSession.CampaignDeck != null)
            {
                var counts = new Dictionary<string, int>();
                foreach (var id in RunSession.CampaignDeck.Cards)
                {
                    counts.TryGetValue(id, out int count);
                    counts[id] = count + 1;
                }

                foreach (var pair in counts)
                {
                    var card = CardCatalog.Find(pair.Key);
                    string upgraded = RunSession.CampaignDeck.UpgradedCards.Contains(pair.Key) ? " · 已升级 ★" : "";
                    MakeCampFacilityButton(
                        (card != null ? card.DisplayName : pair.Key) + " ×" + pair.Value,
                        (card != null ? card.EffectText : pair.Key) + upgraded, true,
                        new Color(0.24f, 0.30f, 0.39f));
                }
            }

            AddCampBackButton();
        }

        private void AddCampBackButton()
        {
            var back = MakeCampFacilityButton("返回设施列表", "返回营地主界面", false,
                new Color(0.25f, 0.34f, 0.48f));
            back.GetComponent<Button>().onClick.AddListener(() => { _campMode = CampPageMode.None; ShowCampPage(""); });
        }

        private static bool AnyCampPartnerWith(System.Func<PartnerState, bool> pred)
        {
            foreach (var p in PartnerRoster.All)
            {
                if (p.IsRecruited && p.IsAlive && pred(p)) return true;
            }

            return false;
        }

        private void OnCampLeave()
        {
            if (RegionMap.IsGenerated)
            {
                if (GameFlow.TryTransition(GameState.Map, "离开营地，返回地图"))
                    RunSession.SaveMapCheckpoint(out _);
                ShowPage("地图", BuildMapDescription());
            }
            else
            {
                ReturnToMenu();
            }
        }

        private GameObject MakeCampFacilityButton(string title, string detail, bool disabled, Color color)
        {
            var view = Instantiate(_campFacilityCardPrefab, _campFacilityContainer);
            view.name = "CampDynamic_Facility_" + title;
            view.SetContent(title, detail, disabled, color);
            return view.gameObject;
        }

        private void ResolveCampLayoutRefs()
        {
            if (_campOptionContainer == null) return;
            var layout = _campOptionContainer.Find("CampLayout");
            if (_campLayoutRoot == null && layout != null) _campLayoutRoot = layout.gameObject;
            if (_campTeamContainer == null && layout != null)
                _campTeamContainer = layout.Find("TeamPanel/TeamScroll/Viewport/TeamList");
            if (_campFacilityContainer == null && layout != null)
                _campFacilityContainer = layout.Find("FacilityPanel/FacilityScroll/Viewport/FacilityGrid");
            if (_campFacilityTitleText == null && layout != null)
                _campFacilityTitleText = layout.Find("FacilityPanel/Title")?.GetComponent<TMP_Text>();
        }

        private void ClearChildren(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private void ShowEventPage(string feedback = null)
        {
            var evt = RunSession.CurrentEvent;
            if (evt == null)
            {
                _eventFeedback = null;
                ShowPage("事件", "没有进行中的事件。");
                return;
            }

            _eventFeedback = feedback;
            ShowPage("事件", string.Empty);
        }

        private void RefreshEventOptions()
        {
            if (_eventPageView == null) return;

            var evt = RunSession.CurrentEvent;
            bool showEvents = evt != null && RunSession.CurrentState == GameState.Event;
            _eventPageView.gameObject.SetActive(showEvents);
            if (!showEvents) return;

            _eventPageView.ClearOptions();
            _eventPageView.SetEvent(evt, BuildResourceLine(), EventPromptText());

            if (RunSession.PendingEventChoice != EventOptionChoiceKind.None)
            {
                RenderEventChoiceOptions();
            }
            else
            {
                _eventPageView.SetOptionTitle("可选行动");
                for (int i = 0; i < evt.Options.Length; i++)
                {
                    var option = evt.Options[i];
                    string block = RunSession.EventOptionBlockReason(option);
                    var card = _eventPageView.AddOption(
                        EventOptionBadge(option),
                        option.Label,
                        EventOptionConditionText(option),
                        string.IsNullOrEmpty(option.ResultText) ? "继续事件" : option.ResultText,
                        block,
                        EventOptionColor(option));
                    if (block == null)
                    {
                        int index = i;
                        card.Button.onClick.AddListener(() => OnEventOptionClicked(index));
                    }
                }
            }

            _eventPageView.RebuildLayout();
        }

        private string EventPromptText()
        {
            if (!string.IsNullOrEmpty(_eventFeedback)) return _eventFeedback;
            switch (RunSession.PendingEventChoice)
            {
                case EventOptionChoiceKind.RemoveCard: return "选择一张要移除的卡牌。";
                case EventOptionChoiceKind.UpgradeCard: return "选择一张要升级的卡牌。";
                case EventOptionChoiceKind.StatusFatigue: return "选择一名队员移除疲劳。";
                case EventOptionChoiceKind.StatusDiseaseOrFatigue: return "选择队员和要移除的状态。";
                default: return "选择一个行动。锁定选项会显示具体原因。";
            }
        }

        private void RenderEventChoiceOptions()
        {
            switch (RunSession.PendingEventChoice)
            {
                case EventOptionChoiceKind.RemoveCard:
                {
                    _eventPageView.SetOptionTitle("选择要移除的卡牌");
                    var cards = RunSession.CampaignDeck != null ? RunSession.CampaignDeck.RemoveableCards() : new List<string>();
                    if (cards.Count == 0)
                    {
                        RunSession.RecordResolution("事件", "移除卡", "没有可移除的卡牌");
                        RunSession.CancelEventChoice();
                        ShowEventPage("没有可移除的卡牌。");
                        return;
                    }

                    foreach (string id in cards)
                    {
                        var definition = CardCatalog.Find(id);
                        var card = _eventPageView.AddOption(
                            "牌",
                            "移除 " + (definition != null ? definition.DisplayName : id),
                            "牌组调整",
                            definition != null ? definition.EffectText : id,
                            null,
                            new Color(0.34f, 0.30f, 0.43f));
                        string captured = id;
                        card.Button.onClick.AddListener(() => OnEventCardChosen(captured));
                    }
                    break;
                }

                case EventOptionChoiceKind.UpgradeCard:
                {
                    _eventPageView.SetOptionTitle("选择要升级的卡牌");
                    if (RunSession.CampaignDeck == null) return;
                    var seen = new HashSet<string>();
                    foreach (string id in RunSession.CampaignDeck.Cards)
                    {
                        if (!seen.Add(id)) continue;
                        var definition = CardCatalog.Find(id);
                        bool upgraded = RunSession.CampaignDeck.UpgradedCards.Contains(id);
                        var card = _eventPageView.AddOption(
                            "升",
                            "升级 " + (definition != null ? definition.DisplayName : id),
                            upgraded ? "该卡已经升级" : "牌组调整",
                            definition != null ? definition.EffectText : id,
                            upgraded ? "该卡已经升级" : null,
                            new Color(0.30f, 0.38f, 0.50f));
                        if (!upgraded)
                        {
                            string captured = id;
                            card.Button.onClick.AddListener(() => OnEventCardChosen(captured));
                        }
                    }
                    break;
                }

                case EventOptionChoiceKind.StatusFatigue:
                    _eventPageView.SetOptionTitle("选择要休整的队员");
                    RenderStatusUnitOptions(false);
                    break;

                case EventOptionChoiceKind.StatusDiseaseOrFatigue:
                    _eventPageView.SetOptionTitle("选择队员与治疗项目");
                    RenderStatusUnitOptions(true);
                    break;
            }
        }

        private void RenderStatusUnitOptions(bool includeDisease)
        {
            AddStatusUnitOptions("PLAYER", "主角", RunSession.PlayerFatigue, RunSession.PlayerDisease, includeDisease);
            foreach (var partner in PartnerRoster.All)
            {
                if (!partner.IsRecruited || !partner.IsAlive) continue;
                AddStatusUnitOptions(partner.Def.Id, partner.Def.DisplayName, partner.Fatigue, partner.Disease, includeDisease);
            }
        }

        private void AddStatusUnitOptions(string unitId, string displayName, int fatigue, int disease, bool includeDisease)
        {
            string condition = "疲劳 " + fatigue + " / 疾病 " + disease;
            if (fatigue > 0)
            {
                var fatigueCard = _eventPageView.AddOption(displayName, displayName + " · 移除疲劳",
                    condition, "移除 1 层疲劳", null, new Color(0.35f, 0.31f, 0.23f));
                string captured = unitId;
                fatigueCard.Button.onClick.AddListener(() => OnEventUnitChosen(captured, false));
            }

            if (includeDisease && disease > 0)
            {
                var diseaseCard = _eventPageView.AddOption(displayName, displayName + " · 移除疾病",
                    condition, "移除 1 层疾病", null, new Color(0.24f, 0.39f, 0.31f));
                string captured = unitId;
                diseaseCard.Button.onClick.AddListener(() => OnEventUnitChosen(captured, true));
            }
        }

        private static string EventOptionBadge(EventOptionDef option)
        {
            if (!string.IsNullOrEmpty(option.RequirePartnerId)) return EventPartnerName(option.RequirePartnerId);
            if (!string.IsNullOrEmpty(option.RecruitPartnerId)) return EventPartnerName(option.RecruitPartnerId);
            if (option.CombatEnemyIds != null && option.CombatEnemyIds.Length > 0) return "战";
            if (option.RemoveCard || option.UpgradeCard) return "牌";
            if (option.StatusChoice != EventStatusChoice.None) return "疗";
            if (option.CostFood > 0 || option.CostWealth > 0 || option.CostReputation > 0) return "资";
            return "行";
        }

        private static string EventOptionConditionText(EventOptionDef option)
        {
            var parts = new List<string>();
            switch (option.Condition)
            {
                case EventOptionCondition.PayResource:
                {
                    var costs = new List<string>();
                    if (option.CostFood > 0) costs.Add(option.CostFood + " 粮食");
                    if (option.CostWealth > 0) costs.Add(option.CostWealth + " 财富");
                    if (option.CostReputation > 0) costs.Add(option.CostReputation + " 声望");
                    parts.Add(costs.Count > 0 ? "消耗：" + string.Join(" + ", costs) : "无消耗");
                    break;
                }
                case EventOptionCondition.HasPartnerAndReputation:
                    parts.Add("需要：" + EventPartnerName(option.RequirePartnerId) + " 且声望 " + option.RequireReputation);
                    break;
                case EventOptionCondition.HasPartnerOrReputation:
                    parts.Add("需要：" + EventPartnerName(option.RequirePartnerId) + " 或声望 " + option.RequireReputation);
                    break;
                case EventOptionCondition.HasPartnerOrCard:
                    parts.Add("需要：" + EventPartnerName(option.RequirePartnerId) + " 或卡牌 " + EventCardName(option.RequireCardId));
                    break;
                case EventOptionCondition.HasPartnerOrPartner:
                    parts.Add("需要：" + EventPartnerName(option.RequirePartnerId) + " 或 " + EventPartnerName(option.RequirePartnerId2));
                    break;
                case EventOptionCondition.ReputationAtLeast:
                    parts.Add("需要：声望 " + option.RequireReputation);
                    break;
                case EventOptionCondition.HasRemoveableCard:
                    parts.Add("需要：牌组中存在可移除卡牌");
                    break;
                case EventOptionCondition.HasPartner:
                    parts.Add("需要：" + EventPartnerName(option.RequirePartnerId));
                    break;
            }

            if (!string.IsNullOrEmpty(option.RecruitPartnerId))
                parts.Add("伙伴互动：" + EventPartnerName(option.RecruitPartnerId));
            if (option.CombatEnemyIds != null && option.CombatEnemyIds.Length > 0)
                parts.Add("后续：战斗 · " + option.CombatLabel);
            if (option.RemoveCard) parts.Add("后续：选择要移除的卡牌");
            if (option.UpgradeCard) parts.Add("后续：选择要升级的卡牌");
            if (option.StatusChoice != EventStatusChoice.None) parts.Add("后续：选择队员");

            return parts.Count > 0 ? string.Join("；", parts) : "无条件";
        }

        private static Color EventOptionColor(EventOptionDef option)
        {
            if (option.CombatEnemyIds != null && option.CombatEnemyIds.Length > 0)
                return new Color(0.40f, 0.24f, 0.23f);
            if (option.StatusChoice != EventStatusChoice.None)
                return new Color(0.24f, 0.39f, 0.31f);
            if (!string.IsNullOrEmpty(option.RequirePartnerId) || !string.IsNullOrEmpty(option.RecruitPartnerId))
                return new Color(0.23f, 0.37f, 0.40f);
            if (option.Condition == EventOptionCondition.PayResource)
                return new Color(0.39f, 0.32f, 0.22f);
            return new Color(0.27f, 0.36f, 0.50f);
        }

        private static string EventPartnerName(string partnerId)
        {
            var partner = PartnerRoster.Find(partnerId);
            return partner != null ? partner.Def.DisplayName : partnerId;
        }

        private static string EventCardName(string cardId)
        {
            var card = CardCatalog.Find(cardId);
            return card != null ? card.DisplayName : cardId;
        }

        private void OnEventOptionClicked(int optionIndex)
        {
            string result = RunSession.ChooseEventOption(optionIndex);

            if (RunSession.PendingEventChoice != EventOptionChoiceKind.None)
            {
                // 进入子选择
                ShowEventPage(result);
                return;
            }

            if (RunSession.CurrentState == GameState.Combat)
            {
                // 事件触发战斗：显示战斗页
                ShowPage("测试入口：战斗", BuildCombatDescription());
                return;
            }

            if (RunSession.CurrentEvent != null)
            {
                // 仍在事件中（测试入口无地图），显示结算
                ShowEventPage(result);
                return;
            }

            // 事件结束回地图
            if (RegionMap.IsGenerated)
            {
                ShowPage("地图", BuildMapDescription() + "\n事件结算：" + result);
            }
            else
            {
                ShowPage("事件", "事件已结算：" + result);
            }
        }

        private void OnEventCardChosen(string cardId)
        {
            string result = RunSession.ChooseEventCard(cardId);
            AfterEventChoice(result);
        }

        private void OnEventUnitChosen(string unitId, bool removeDisease)
        {
            string result = RunSession.ChooseEventStatusUnit(unitId, removeDisease);
            AfterEventChoice(result);
        }

        private void AfterEventChoice(string result)
        {
            if (RunSession.CurrentEvent != null)
            {
                ShowEventPage(result);
                return;
            }

            if (RegionMap.IsGenerated)
            {
                ShowPage("地图", BuildMapDescription() + "\n事件结算：" + result);
            }
            else
            {
                ShowPage("事件", "事件已结算：" + result);
            }
        }

        private static string BuildMapRiskHint()
        {
            bool jungle = RegionMap.Region == ContentRegion.Jungle;
            string riskGain = jungle ? "密林移动 +2" : "草原移动 +1";
            string ambush = jungle ? "伏匪+毒丝蛛" : "路匪+野犬";
            string hint = "风险 " + RunSession.Risk + "/" + GameStartParameters.RiskThreshold
                + "  ·  " + riskGain
                + "  ·  精英节点额外 +1"
                + "  ·  达到上限触发危机伏击（" + ambush + "，按精英奖励）";
            if (RunSession.AmbushPending) hint += "  ·  危机伏击将在下一次移动触发";
            return hint;
        }

        private static string BuildMapDescription()
        {
            if (!RegionMap.IsGenerated) return "地图尚未生成。";

            bool jungle = RegionMap.Region == ContentRegion.Jungle;
            string regionName = jungle ? "密林" : "草原";
            string desc = regionName + "地图（共 " + RegionMap.LayerCount + " 层）\n";
            desc += "当前位置：" + (RegionMap.CurrentNodeIndex < 0 ? "起点" : RegionMap.Nodes[RegionMap.CurrentNodeIndex].DisplayName)
                + "（第 " + RegionMap.CurrentLayer + " 层）";
            desc += " | 剩余层数：" + RegionMap.RemainingLayers + "\n";

            if (RegionMap.Path.Count > 0)
            {
                desc += "当前路径：起点";
                for (int i = 0; i < RegionMap.Path.Count; i++)
                {
                    desc += " → " + RegionMap.Nodes[RegionMap.Path[i]].DisplayName;
                }
                desc += "\n";
            }

            desc += BuildResourceLine();
            if (RunSession.Risk > 0) desc += " | 风险 " + RunSession.Risk;
            desc += "\n";
            desc += "风险提示：" + (jungle ? "密林每次移动风险 +2" : "草原每次移动风险 +1")
                + "，精英节点额外 +1；达到 " + GameStartParameters.RiskThreshold
                + " 触发危机伏击（" + (jungle ? "伏匪+毒丝蛛" : "路匪+野犬") + "，按精英奖励结算）。\n";
            if (RunSession.AmbushPending) desc += "⚠ 危机伏击将触发！\n";

            desc += "可移动节点：";
            var reachable = RegionMap.ReachableNext();
            if (reachable.Count == 0)
            {
                desc += "无（已到达终点或地图未生成）";
            }
            else
            {
                for (int i = 0; i < reachable.Count; i++)
                {
                    var node = RegionMap.Nodes[reachable[i]];
                    if (i > 0) desc += "、";
                    desc += node.DisplayName + "（" + RegionMapNode.NodeTypeName(node.Type) + "）";
                }
            }

            return desc;
        }

        private static string BuildCombatDescription()
        {
            if (!CombatManager.IsActive) return "战斗未激活";

            string desc = "回合 " + CombatManager.TurnNumber
                + " | 能量 " + CombatManager.Energy + "/" + CombatManager.MaxEnergy
                + " | 士气 " + CombatManager.Morale + "/" + CombatStatus.MaxMorale
                + " | 战斗：" + CombatManager.Phase
                + " | 阶段：" + CombatManager.CurrentTurnPhase
                + " | 可行动：" + CombatManager.CanPlayerAct;

            desc += "\n玩家队伍：";
            if (CombatManager.PlayerTeam != null)
            {
                foreach (var u in CombatManager.PlayerTeam)
                {
                    string alive = u.IsAlive ? "" : " [阵亡]";
                    desc += "\n  " + u.DisplayName + " HP:" + u.CurrentHp + "/" + u.EffectiveMaxHp + " 护甲:" + u.Armor + "/" + u.EffectiveArmorCap
                        + " 流血:" + u.Bleed + " 疾病:" + u.Disease + " 疲劳:" + u.Fatigue + alive;
                }
            }

            desc += "\n敌人队伍：";
            if (CombatManager.EnemyTeam != null)
            {
                foreach (var e in CombatManager.EnemyTeam)
                {
                    string alive = e.IsAlive ? "" : " [阵亡]";
                    string intentText = "";
                    if (e.IsAlive && e is EnemyUnit eu && eu.CurrentIntent != null)
                    {
                        intentText = " | 意图：" + eu.CurrentIntent.Describe();
                    }

                    desc += "\n  " + e.DisplayName + " HP:" + e.CurrentHp + "/" + e.EffectiveMaxHp + " 护甲:" + e.Armor + "/" + e.EffectiveArmorCap
                        + " 流血:" + e.Bleed + " 疾病:" + e.Disease + " 疲劳:" + e.Fatigue + alive + intentText;
                }

                if (CombatManager.Plunder > 0)
                {
                    desc += "\n掠夺：" + CombatManager.Plunder + " 层（胜利时每层 -2 财富）";
                }
            }

            if (CombatManager.Deck != null)
            {
                desc += "\n手牌：" + CombatManager.Deck.HandSize + " 张";
                if (CombatManager.Deck.HandSize > 0)
                {
                    desc += " [";
                    for (int i = 0; i < CombatManager.Deck.HandSize; i++)
                    {
                        if (i > 0) desc += ", ";
                        string cid = CombatManager.Deck.Hand[i];
                        var cd = CardCatalog.Find(cid);
                        desc += cd != null ? cd.DisplayName : cid;
                    }
                    desc += "]";
                }

                desc += "\n牌堆：抽牌堆 " + CombatManager.Deck.DrawPileCount
                    + " / 弃牌堆 " + CombatManager.Deck.DiscardPileCount
                    + " / 消耗区 " + CombatManager.Deck.ExhaustedCount;
            }

            return desc;
        }

        private static void SetElementsActive(List<GameObject> elements, bool active)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                elements[i].SetActive(active);
            }
        }
    }
}
