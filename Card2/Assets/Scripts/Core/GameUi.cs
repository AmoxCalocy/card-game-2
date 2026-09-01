using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneJourney.Core
{
    /// <summary>主界面驱动：UI 结构在场景中搭建，本组件只持有引用并绑定交互。</summary>
    public sealed class GameUi : MonoBehaviour
    {
        [Header("根引用")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Text _hudText;
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private GameObject _menuContent;
        [SerializeField] private GameObject _pagePanel;
        [SerializeField] private Text _pageTitleText;
        [SerializeField] private Text _pageDescriptionText;

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
        [SerializeField] private Button _recordResolutionButton;
        [SerializeField] private Button _returnToMenuButton;
        [SerializeField] private Button _combatVictoryButton;
        [SerializeField] private Button _combatDefeatButton;
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private Button _spendEnergyButton;
        [SerializeField] private Button _drawCardButton;
        [SerializeField] private Button _discardHandButton;
        [SerializeField] private Button _exhaustLastButton;
        [SerializeField] private Button _addTempCardButton;
        [SerializeField] private Button _playSingleCardButton;
        [SerializeField] private Button _playAoeCardButton;
        [SerializeField] private Button _bleedButton;
        [SerializeField] private Button _diseaseButton;
        [SerializeField] private Button _fatigueButton;
        [SerializeField] private Button _moraleButton;
        [SerializeField] private Button _prevEncounterButton;
        [SerializeField] private Button _nextEncounterButton;

        [Header("手牌出牌（A1-13）")]
        [SerializeField] private Transform _handCardContainer;

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
        [SerializeField] private Transform _settlementOptionContainer;

        private enum CampPageMode { None, Rest, ClinicCamp, ClinicTown, ClinicRelic, FreeUpgrade, DeckView }
        private CampPageMode _campMode;

        [Header("事件页面（A2-19 / 布局优化）")]
        [SerializeField] private EventPageView _eventPageView;
        private string _eventFeedback;

        [Header("战斗界面（A1-14）")]
        [SerializeField] private BattleView _battleView;

        private void Awake()
        {
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

            if (_pagePanel != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_pagePanel.transform);
            }
        }

        private void BindButtons()
        {
            if (_startNewGameButton == null
                || _quitButton == null
                || _recordResolutionButton == null
                || _returnToMenuButton == null)
            {
                Debug.LogError("[GameUi] 关键按钮引用未在场景中配置，请检查 GameUi 组件的按钮绑定", this);
                return;
            }

            _startNewGameButton.onClick.AddListener(OnStartNewGame);
            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinueGame);
            _quitButton.onClick.AddListener(OnQuit);
            _recordResolutionButton.onClick.AddListener(OnRecordSampleResolution);
            _returnToMenuButton.onClick.AddListener(OnReturnToMenu);

            if (_startWithSeedButton != null)
            {
                _startWithSeedButton.onClick.AddListener(OnStartWithSeed);
            }

            if (_combatVictoryButton != null)
            {
                _combatVictoryButton.onClick.AddListener(OnSimulateVictory);
            }

            if (_combatDefeatButton != null)
            {
                _combatDefeatButton.onClick.AddListener(OnSimulateDefeat);
            }

            if (_endTurnButton != null)
            {
                _endTurnButton.onClick.AddListener(OnEndTurn);
            }

            if (_spendEnergyButton != null)
            {
                _spendEnergyButton.onClick.AddListener(OnSpendEnergy);
            }

            if (_drawCardButton != null)
            {
                _drawCardButton.onClick.AddListener(OnDrawCard);
            }

            if (_discardHandButton != null)
            {
                _discardHandButton.onClick.AddListener(OnDiscardHand);
            }

            if (_exhaustLastButton != null)
            {
                _exhaustLastButton.onClick.AddListener(OnExhaustLast);
            }

            if (_addTempCardButton != null)
            {
                _addTempCardButton.onClick.AddListener(OnAddTempCard);
            }

            if (_playSingleCardButton != null)
            {
                _playSingleCardButton.onClick.AddListener(() => OnPlayHandCard(0));
            }

            if (_playAoeCardButton != null)
            {
                _playAoeCardButton.onClick.AddListener(() =>
                {
                    // 找到手牌中第一个 AOE 卡并打出
                    if (CombatManager.Deck == null) return;
                    for (int i = 0; i < CombatManager.Deck.HandSize; i++)
                    {
                        var c = CardCatalog.Find(CombatManager.Deck.Hand[i]);
                        if (c != null && c.TargetType == TargetType.AllEnemies)
                        {
                            OnPlayHandCard(i);
                            return;
                        }
                    }
                    // 没有 AOE 卡则打出第一张
                    if (CombatManager.Deck.HandSize > 0) OnPlayHandCard(0);
                });
            }

            if (_bleedButton != null)
            {
                _bleedButton.onClick.AddListener(() => OnAddStatus("流血", () => CombatStatus.AddBleed(CombatManager.EnemyTeam[0], 2)));
            }

            if (_diseaseButton != null)
            {
                _diseaseButton.onClick.AddListener(() => OnAddStatus("疾病", () => CombatStatus.AddDisease(CombatManager.EnemyTeam[0], 1)));
            }

            if (_fatigueButton != null)
            {
                _fatigueButton.onClick.AddListener(() => OnAddStatus("疲劳", () => CombatStatus.AddFatigue(CombatManager.PlayerTeam[0], 1)));
            }

            if (_moraleButton != null)
            {
                _moraleButton.onClick.AddListener(() => OnAddStatus("士气", () => CombatManager.AddMorale(2)));
            }

            if (_prevEncounterButton != null)
            {
                _prevEncounterButton.onClick.AddListener(OnPrevEncounter);
            }

            if (_nextEncounterButton != null)
            {
                _nextEncounterButton.onClick.AddListener(OnNextEncounter);
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
            _menuPanel.SetActive(true);
            _pagePanel.SetActive(false);
            if (_campOptionContainer != null) _campOptionContainer.gameObject.SetActive(false);
            if (_settlementOptionContainer != null) _settlementOptionContainer.gameObject.SetActive(false);
            if (_eventPageView != null) _eventPageView.gameObject.SetActive(false);
            if (_mapPageView != null) _mapPageView.gameObject.SetActive(false);
            RefreshSaveUi();
            Refresh();
        }

        private void RefreshSaveUi()
        {
            if (_continueButton != null) _continueButton.interactable = CampaignSaveService.HasValidSave;
            if (_saveStatusText != null) _saveStatusText.text = CampaignSaveService.StatusMessage;
        }

        private void ShowPage(string title, string description)
        {
            _pageTitleText.text = title;
            _pageDescriptionText.text = description;
            _menuPanel.SetActive(false);
            _pagePanel.SetActive(true);

            bool isCombat = RunSession.CurrentState == GameState.Combat && CombatManager.IsActive;

            // A1-14：战斗中优先显示 BattleView，隐藏旧版 TestPage
            if (isCombat && _battleView != null)
            {
                _battleView.Show();
                _battleView.Refresh();
                if (_pagePanel != null) _pagePanel.SetActive(false);
            }
            else if (_battleView != null)
            {
                _battleView.Hide();
            }
            if (_combatVictoryButton != null) _combatVictoryButton.gameObject.SetActive(isCombat);
            if (_combatDefeatButton != null) _combatDefeatButton.gameObject.SetActive(isCombat);
            if (_endTurnButton != null) _endTurnButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            if (_spendEnergyButton != null)
            {
                bool canSpend = isCombat && CombatManager.CanPlayerAct && CombatManager.Energy > 0;
                _spendEnergyButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
                _spendEnergyButton.interactable = canSpend;
            }
            if (_drawCardButton != null) _drawCardButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            if (_discardHandButton != null) _discardHandButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            if (_exhaustLastButton != null) _exhaustLastButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            if (_addTempCardButton != null) _addTempCardButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            if (_playSingleCardButton != null) _playSingleCardButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            if (_playAoeCardButton != null) _playAoeCardButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            if (_bleedButton != null) _bleedButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            if (_diseaseButton != null) _diseaseButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            if (_fatigueButton != null) _fatigueButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            if (_moraleButton != null) _moraleButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            // 翻页按钮为测试辅助（切遭遇/切事件），仅测试配置可见
            bool showTestEntries = GameConfigProvider.Active != null
                && GameConfigProvider.Active.EnableTestEntries                && !GameConfigProvider.IsReleaseLocked;
            bool canSwitchEncounter = showTestEntries
                && (RunSession.CurrentState == GameState.Combat || RunSession.CurrentState == GameState.Event);
            if (_prevEncounterButton != null) _prevEncounterButton.gameObject.SetActive(canSwitchEncounter);
            if (_nextEncounterButton != null) _nextEncounterButton.gameObject.SetActive(canSwitchEncounter);

            RefreshHandCards();
            // A2-21：营地页只显示营地内容，隐藏其他动态容器与测试按钮区块
            bool isCamp = RunSession.CurrentState == GameState.Camp;
            bool isEvent = RunSession.CurrentState == GameState.Event && RunSession.CurrentEvent != null;
            bool isMap = RunSession.CurrentState == GameState.Map && RegionMap.IsGenerated;
            // A2-24：结算页只显示结算操作（返回主菜单 / 同种子重开）
            bool isSettlement = RunSession.CurrentState == GameState.Settlement;
            _pageTitleText.gameObject.SetActive(!isEvent && !isMap);
            _pageDescriptionText.gameObject.SetActive(!isEvent && !isMap);
            if (_eventPageView != null) _eventPageView.gameObject.SetActive(isEvent);
            if (_mapPageView != null) _mapPageView.gameObject.SetActive(isMap);
            // 手牌容器仅在营地/事件/地图页强制隐藏；非这些页面由 RefreshHandCards 决定
            if (_handCardContainer != null && (isCamp || isEvent || isMap)) _handCardContainer.gameObject.SetActive(false);
            var pagePanel = _pagePanel != null ? _pagePanel.transform : transform;
            var combatActions = pagePanel.Find("CombatActions");
            if (combatActions != null) combatActions.gameObject.SetActive(!isCamp && !isEvent && !isMap);
            var bottomRow = pagePanel.Find("BottomRow");
            if (bottomRow != null) bottomRow.gameObject.SetActive(!isCamp && !isSettlement);
            ResolveCampLayoutRefs();
            if (_campOptionContainer != null) _campOptionContainer.gameObject.SetActive(isCamp);
            if (_campLayoutRoot != null) _campLayoutRoot.SetActive(isCamp);
            if (_settlementOptionContainer != null) _settlementOptionContainer.gameObject.SetActive(isSettlement);
            // 结算页：隐藏返回/指定种子按钮（由结算页自己的按钮替代）
            if (_returnToMenuButton != null) _returnToMenuButton.gameObject.SetActive(!isCamp && !isSettlement);
            if (_startWithSeedButton != null) _startWithSeedButton.gameObject.SetActive(!isCamp && !isSettlement && !isEvent);
            if (!isCamp)
            {
                RefreshMapPage();
                RefreshEventOptions();
            }

            Refresh();
        }

        private void OnStartNewGame()
        {
            RunSession.StartNewGame();
            ShowPage("地图（新游戏入口）", BuildMapDescription());
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
                desc += CombatManager.IsActive ? BuildCombatDescription() : "点击「◀ 上一组 / 下一组 ▶」切换敌人，返回主菜单再次进入测试。";
            }
            else if (page == GameState.Map)
            {
                desc = BuildMapDescription();
            }
            else if (page == GameState.Event)
            {
                desc = "事件测试入口：点击「◀ 上一组 / 下一组 ▶」在 E01-E20 间切换。\n";
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

        private void OnRecordSampleResolution()
        {
            RunSession.RecordResolution("测试结算（示例）", "普通伤害结算", "目标生命 28 → 22，护甲 0");
        }

        private void OnSimulateVictory()
        {
            if (!CombatManager.IsActive) return;

            // 将所有敌人血量清零模拟胜利
            foreach (var e in CombatManager.EnemyTeam)
            {
                if (e.IsAlive) e.TakeDamage(e.CurrentHp + e.Armor);
            }

            CombatManager.CheckEndCondition();
            bool won = CombatManager.Phase == CombatPhase.Victory;
            CombatManager.End();
            // 胜利：弹出独立奖励页（CheckEndCondition 已记录「战斗奖励」）；失败仍刷新战斗页
            if (won && _battleView != null && _battleView.ShowRewardPage()) return;

            // 密林首领胜利 → Victory 状态（奖励页「继续」后进结算页）
            if (won && RunSession.CurrentState == GameState.Victory)
            {
                RunSession.EnterSettlement(true, "击败密林首领（垂直切片）");
                ShowSettlement();
                return;
            }

            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnSimulateDefeat()
        {
            if (!CombatManager.IsActive) return;

            CombatManager.ForceDefeat();
            CombatManager.End();
            RunSession.EnterDefeatState();
            RunSession.EnterSettlement(false, "主角阵亡");
            ShowSettlement();
        }

        /// <summary>显示结算页（A2-24）：摘要 + 真实按钮（返回主菜单 / 同种子重开）。</summary>
        public void ShowSettlement()
        {
            var s = RunSession.LastSettlement;
            if (s == null)
            {
                ShowPage("结算", "无结算数据。");
                return;
            }

            string desc = "【" + s.Result + "】\n";
            desc += "原因：" + s.Reason + "\n";
            desc += "用时：" + s.ElapsedSeconds + " 秒\n";
            desc += "区域进度：" + s.RegionProgress + "\n";
            desc += "最终牌组：" + s.Deck + "  |  伙伴：" + s.Partners + "\n";
            desc += "资源：" + s.Resources + "\n";
            desc += "建筑：" + s.Buildings + "  |  遗物：" + s.Relics + "\n";
            desc += "随机种子：" + s.Seed + "\n\n";
            ShowPage("本局结算", desc);
            RefreshSettlementButtons();
        }

        /// <summary>结算页操作按钮：返回主菜单 / 同种子重开。</summary>
        private void RefreshSettlementButtons()
        {
            ResolveCampLayoutRefs();
            if (_settlementOptionContainer == null) return;
            ClearChildren(_settlementOptionContainer);

            if (_campOptionContainer != null) _campOptionContainer.gameObject.SetActive(false);
            if (_campLayoutRoot != null) _campLayoutRoot.SetActive(false);
            _settlementOptionContainer.gameObject.SetActive(true);
            TMP_FontAsset defaultFont = _campFacilityTitleText != null && _campFacilityTitleText.font != null
                ? _campFacilityTitleText.font
                : TMP_Settings.defaultFontAsset;

            var toMenu = MakeCampSimpleButton(_settlementOptionContainer, defaultFont, "返回主菜单", false);
            toMenu.GetComponent<Button>().onClick.AddListener(ReturnToMenu);

            var restart = MakeCampSimpleButton(_settlementOptionContainer, defaultFont,
                "同种子重开（种子 " + RunSession.LastSettlement.Seed + "）", false);
            restart.GetComponent<Button>().onClick.AddListener(RestartWithSameSeed);
        }

        private void OnEndTurn()
        {
            if (!CombatManager.CanPlayerAct) return;

            CombatManager.EndPlayerTurn();
            RunSession.RecordResolution("回合结算", "结束第 " + CombatManager.TurnNumber + " 回合", "进入敌方回合");
            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnSpendEnergy()
        {
            if (!CombatManager.CanSpendEnergy(1)) return;

            CombatManager.SpendEnergy(1);
            RunSession.RecordResolution("回合操作", "消耗 1 点能量", "剩余能量 " + CombatManager.Energy);
            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnDrawCard()
        {
            if (!CombatManager.CanPlayerAct) return;

            if (CombatManager.Deck.HandSize >= GameStartParameters.MaxHandSize)
            {
                RunSession.RecordResolution("牌堆操作", "抽牌失败", "手牌已满（上限 " + GameStartParameters.MaxHandSize + "）");
                ShowPage("测试入口：战斗", BuildCombatDescription());
                return;
            }

            int drawn = CombatManager.Deck.DrawToHand(1, GameStartParameters.MaxHandSize);
            string msg = drawn > 0
                ? "抽到 " + CombatManager.Deck.Hand[CombatManager.Deck.HandSize - 1]
                : "牌堆已空";
            RunSession.RecordResolution("牌堆操作", "抽 1 张牌", msg);
            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnDiscardHand()
        {
            if (!CombatManager.CanPlayerAct || CombatManager.Deck == null) return;

            int count = CombatManager.Deck.HandSize;
            CombatManager.Deck.DiscardHand();
            RunSession.RecordResolution("牌堆操作", "弃掉全部手牌", count + " 张进入弃牌堆");
            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnExhaustLast()
        {
            if (!CombatManager.CanPlayerAct || CombatManager.Deck == null) return;
            if (CombatManager.Deck.HandSize == 0) return;

            string card = CombatManager.Deck.Hand[CombatManager.Deck.HandSize - 1];
            CombatManager.Deck.ExhaustFromHand(card);
            RunSession.RecordResolution("牌堆操作", "消耗 " + card, "进入消耗区，不再回到牌堆");
            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnAddTempCard()
        {
            if (!CombatManager.CanPlayerAct || CombatManager.Deck == null) return;

            string tempId = "TEMP_" + CombatManager.TurnNumber + "_" + CombatManager.Deck.HandSize;
            CombatManager.Deck.Hand.Add(tempId);
            RunSession.RecordResolution("牌堆操作", "生成临时卡 " + tempId, "仅本场战斗有效");
            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnPlayTestCard(TargetType type, int cost, int damage)
        {
            if (!CombatManager.CanPlayerAct) return;

            string result = CombatResolver.PlayTestCard(cost, type, damage);
            string typeName = type == TargetType.SingleEnemy ? "单体" : "全体";
            RunSession.RecordResolution("出牌结算", "测试卡（" + typeName + " " + cost + "费 " + damage + "伤）", result);
            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnPlayHandCard(int handIndex)
        {
            if (!CombatManager.CanPlayerAct || CombatManager.Deck == null) return;
            if (handIndex < 0 || handIndex >= CombatManager.Deck.HandSize) return;

            string result = CombatResolver.PlayCard(handIndex);
            // 若这一击结束战斗，CheckEndCondition 已记录「战斗奖励」，出牌记录不再覆盖
            if (CombatManager.Phase != CombatPhase.Victory && CombatManager.Phase != CombatPhase.Defeat)
                RunSession.RecordResolution("手牌出牌", "打出第 " + (handIndex + 1) + " 张手牌", result);
            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnAddStatus(string name, System.Action action)
        {
            if (!CombatManager.CanPlayerAct) return;

            action();
            RunSession.RecordResolution("状态操作", "施加 " + name, BuildCombatDescription().Replace("\n", " / "));
            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnPrevEncounter()
        {
            if (RunSession.CurrentState == GameState.Event)
            {
                RunSession.PrevEvent();
                ShowEventPage();
                return;
            }

            RunSession.PrevEncounter();
            RelaunchCombat();
        }

        private void OnNextEncounter()
        {
            if (RunSession.CurrentState == GameState.Event)
            {
                RunSession.NextEvent();
                ShowEventPage();
                return;
            }

            RunSession.NextEncounter();
            RelaunchCombat();
        }

        private void RefreshCombatPage()
        {
            string desc = "遭遇：" + RunSession.CurrentEncounterLabel() + "\n";
            desc += "点击「◀ 上一组 / 下一组 ▶」切换敌人，返回主菜单再次进入测试。";
            ShowPage("测试入口：战斗", desc);
        }

        private void RelaunchCombat()
        {
            // 状态已在 Combat，直接重开（EnterTestPage 的 Combat→Combat 转移会被状态机拒绝）
            RunSession.RelaunchTestCombat();
            string desc = "遭遇：" + RunSession.CurrentEncounterLabel() + "\n" + BuildCombatDescription();
            ShowPage("测试入口：战斗", desc);
        }

        private void OnReturnToMenu()
        {
            ReturnToMenu();
        }

        public void ReturnToMenu()
        {
            RunSession.Reset();
            if (_campOptionContainer != null) _campOptionContainer.gameObject.SetActive(false);
            if (_settlementOptionContainer != null) _settlementOptionContainer.gameObject.SetActive(false);
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
            if (_hudText == null)
            {
                return;
            }

            // A1-14：战斗中同步刷新 BattleView
            if (_battleView != null && CombatManager.IsActive)
            {
                _battleView.Refresh();
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
            if (_hudText == null)
            {
                return;
            }

            var config = GameConfigProvider.Active;
            bool showTestEntries = config != null && config.EnableTestEntries && !GameConfigProvider.IsReleaseLocked;
            bool showModeSwitch = !GameConfigProvider.IsReleaseLocked;

            SetElementsActive(_testEntryElements, showTestEntries);
            SetElementsActive(_modeSwitchElements, showModeSwitch);
            _hudText.gameObject.SetActive(config != null && config.ShowTestHud);

            // 翻页按钮（切遭遇/切事件）同为测试辅助：配置切换时同步显隐
            bool inSwitchablePage = RunSession.CurrentState == GameState.Combat
                || RunSession.CurrentState == GameState.Event;
            if (_prevEncounterButton != null) _prevEncounterButton.gameObject.SetActive(showTestEntries && inSwitchablePage);
            if (_nextEncounterButton != null) _nextEncounterButton.gameObject.SetActive(showTestEntries && inSwitchablePage);

            Refresh();
        }

        private void RefreshHandCards()
        {
            // 如果场景中未指定容器，则尝试在 TestPage 下找到或创建一个
            if (_handCardContainer == null)
            {
                var pagePanel = _pagePanel != null ? _pagePanel.transform : transform;
                var existing = pagePanel.Find("HandCards");
                if (existing != null) _handCardContainer = existing;
            }

            if (_handCardContainer == null) return;

            // 手牌区仅战斗状态显示（避免事件/地图页残留黑色背景条）
            bool showHand = RunSession.CurrentState == GameState.Combat && CombatManager.IsActive;
            _handCardContainer.gameObject.SetActive(showHand);
            if (!showHand) return;

            // 清理旧按钮
            for (int i = _handCardContainer.childCount - 1; i >= 0; i--)
            {
                var child = _handCardContainer.GetChild(i);
                if (child.name.StartsWith("HC_"))
                    Destroy(child.gameObject);
            }

            if (CombatManager.Deck == null || CombatManager.Deck.HandSize == 0) return;

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < CombatManager.Deck.HandSize; i++)
            {
                string cardId = CombatManager.Deck.Hand[i];
                var card = CardCatalog.Find(cardId);
                string label = card != null
                    ? card.DisplayName + " " + card.Cost + "费"
                    : cardId;

                var go = new GameObject("HC_" + i, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(_handCardContainer, false);

                var img = go.GetComponent<Image>();
                img.color = new Color(0.25f, 0.35f, 0.5f);

                var le = go.GetComponent<LayoutElement>();
                le.minWidth = 120;
                le.minHeight = 32;

                var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(go.transform, false);

                var text = textGo.GetComponent<Text>();
                text.text = label;
                text.font = defaultFont;
                text.fontSize = 14;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;

                var textRt = (RectTransform)textGo.transform;
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.sizeDelta = Vector2.zero;

                int index = i;
                go.GetComponent<Button>().onClick.AddListener(() => OnPlayHandCard(index));
            }
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
            string desc = BuildResourceLine();
            desc += "\n左侧查看队伍状态；右侧选择营地服务或建筑入口。";
            if (!string.IsNullOrEmpty(result)) desc += "\n最近结算：" + result;
            ShowPage("营地整备", desc);
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
            if (_settlementOptionContainer != null) _settlementOptionContainer.gameObject.SetActive(false);

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

        private static GameObject MakeCampSimpleButton(Transform parent, TMP_FontAsset font, string label, bool disabled)
        {
            var go = new GameObject("CampDynamic_Action_" + label,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = disabled ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.30f, 0.45f, 0.60f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.interactable = !disabled;
            var element = go.GetComponent<LayoutElement>();
            element.minWidth = 520;
            element.preferredWidth = 520;
            element.minHeight = 52;
            element.preferredHeight = 52;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.font = font;
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            return go;
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
            if (_settlementOptionContainer == null && _campOptionContainer.parent != null)
                _settlementOptionContainer = _campOptionContainer.parent.Find("SettlementActions");
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
