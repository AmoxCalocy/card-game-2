using System;
using System.Collections;
using System.Collections.Generic;
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

        [Header("地图节点（A2-17）")]
        [SerializeField] private Transform _mapNodeContainer;

        [Header("事件选项（A2-19）")]
        [SerializeField] private Transform _eventOptionContainer;

        [Header("战斗界面（A1-14）")]
        [SerializeField] private BattleView _battleView;

        private void Awake()
        {
            BindButtons();

            RunSession.Changed += Refresh;
            GameConfigProvider.Changed += RefreshConfigUi;
            GameFlow.Changed += Refresh;

            ShowMenu();
            RefreshConfigUi();
        }

        private void OnDestroy()
        {
            RunSession.Changed -= Refresh;
            GameConfigProvider.Changed -= RefreshConfigUi;
            GameFlow.Changed -= Refresh;
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
            Refresh();
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
                && GameConfigProvider.Active.EnableTestEntries
                && !GameConfigProvider.IsReleaseLocked;
            bool canSwitchEncounter = showTestEntries
                && (RunSession.CurrentState == GameState.Combat || RunSession.CurrentState == GameState.Event);
            if (_prevEncounterButton != null) _prevEncounterButton.gameObject.SetActive(canSwitchEncounter);
            if (_nextEncounterButton != null) _nextEncounterButton.gameObject.SetActive(canSwitchEncounter);

            RefreshHandCards();
            RefreshMapNodes();
            RefreshEventOptions();

            Refresh();
        }

        private void OnStartNewGame()
        {
            RunSession.StartNewGame();
            ShowPage("地图（新游戏入口）", BuildMapDescription());
        }

        private void OnStartWithSeed()
        {
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
                }
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
            CombatManager.End();
            RunSession.RecordResolution("战斗结算", "模拟胜利", "战斗结束，临时状态已清理");
            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnSimulateDefeat()
        {
            if (!CombatManager.IsActive) return;

            CombatManager.ForceDefeat();
            CombatManager.End();
            RunSession.RecordResolution("战斗结算", "模拟失败", "主角阵亡，临时状态已清理");
            ShowPage("测试入口：战斗", BuildCombatDescription());
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
            if (CombatManager.IsActive) RelaunchCombat();
            else RefreshCombatPage();
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
            if (CombatManager.IsActive) RelaunchCombat();
            else RefreshCombatPage();
        }

        private void RefreshCombatPage()
        {
            string desc = "遭遇：" + RunSession.CurrentEncounterLabel() + "\n";
            desc += "点击「◀ 上一组 / 下一组 ▶」切换敌人，返回主菜单再次进入测试。";
            ShowPage("测试入口：战斗", desc);
        }

        private void RelaunchCombat()
        {
            CombatManager.End();
            RunSession.EnterTestPage(GameState.Combat);
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
            ShowMenu();
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

        private void RefreshMapNodes()
        {
            // 如果场景中未指定容器，则尝试在 TestPage 下找到或创建一个
            if (_mapNodeContainer == null)
            {
                var pagePanel = _pagePanel != null ? _pagePanel.transform : transform;
                var existing = pagePanel.Find("MapNodes");
                if (existing != null) _mapNodeContainer = existing;
            }

            if (_mapNodeContainer == null) return;

            bool showMap = RunSession.CurrentState == GameState.Map && RegionMap.IsGenerated;
            _mapNodeContainer.gameObject.SetActive(showMap);
            if (!showMap) return;

            // 清理旧按钮
            for (int i = _mapNodeContainer.childCount - 1; i >= 0; i--)
            {
                var child = _mapNodeContainer.GetChild(i);
                if (child.name.StartsWith("MN_"))
                    Destroy(child.gameObject);
            }

            var reachable = RegionMap.ReachableNext();
            var nodes = RegionMap.Nodes;
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                bool canMove = reachable.Contains(i);
                bool isCurrent = RegionMap.CurrentNodeIndex == i;
                bool isVisited = RegionMap.IsVisited(i);

                string marker = isCurrent ? "◆ " : (isVisited ? "· " : "");
                string label = marker + "第" + node.Layer + "层·" + node.DisplayName;

                var go = new GameObject("MN_" + i, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(_mapNodeContainer, false);

                var img = go.GetComponent<Image>();
                if (isCurrent) img.color = new Color(0.4f, 0.55f, 0.3f);
                else if (canMove) img.color = new Color(0.3f, 0.45f, 0.6f);
                else img.color = new Color(0.22f, 0.22f, 0.28f);

                var le = go.GetComponent<LayoutElement>();
                le.minWidth = 200;
                le.minHeight = 30;

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
                var btn = go.GetComponent<Button>();
                btn.interactable = canMove;
                btn.onClick.AddListener(() => OnMapNodeClicked(index));
            }
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
                RunSession.StartEventFromNode(node);
                ShowEventPage();
                return;
            }

            // 战斗/精英/首领/营地节点内容后续步骤接入；当前停留在地图展示结算
            ShowPage("地图", BuildMapDescription() + "\n" + result);
        }

        private void ShowEventPage()
        {
            var evt = RunSession.CurrentEvent;
            if (evt == null)
            {
                ShowPage("事件", "没有进行中的事件。");
                return;
            }

            string desc = evt.DisplayName + "（" + evt.Id + "）\n" + evt.Description + "\n\n";
            desc += "资源：粮食 " + RunSession.Food + " / 财富 " + RunSession.Wealth
                + " / 声望 " + RunSession.Reputation + " / 建材 " + RunSession.Materials + "\n";
            ShowPage("事件", desc);
            RefreshEventOptions();
        }

        private void RefreshEventOptions()
        {
            // 如果场景中未指定容器，则尝试在 TestPage 下找到或创建一个
            if (_eventOptionContainer == null)
            {
                var pagePanel = _pagePanel != null ? _pagePanel.transform : transform;
                var existing = pagePanel.Find("EventOptions");
                if (existing != null) _eventOptionContainer = existing;
            }

            if (_eventOptionContainer == null) return;

            for (int i = _eventOptionContainer.childCount - 1; i >= 0; i--)
            {
                var child = _eventOptionContainer.GetChild(i);
                if (child.name.StartsWith("EO_"))
                    Destroy(child.gameObject);
            }

            var evt = RunSession.CurrentEvent;
            bool showEvents = evt != null && RunSession.CurrentState == GameState.Event;
            _eventOptionContainer.gameObject.SetActive(showEvents);
            if (!showEvents) return;

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 待定子选择：渲染可选项（卡牌或单位）
            if (RunSession.PendingEventChoice != EventOptionChoiceKind.None)
            {
                RenderEventChoiceOptions(defaultFont);
                return;
            }

            for (int i = 0; i < evt.Options.Length; i++)
            {
                var opt = evt.Options[i];
                string block = RunSession.EventOptionBlockReason(opt);
                string label = opt.Label + "  [ " + opt.ResultText + " ]";
                if (opt.Condition == EventOptionCondition.PayResource)
                {
                    var costs = new System.Collections.Generic.List<string>();
                    if (opt.CostFood > 0) costs.Add(opt.CostFood + " 粮");
                    if (opt.CostWealth > 0) costs.Add(opt.CostWealth + " 财");
                    if (opt.CostReputation > 0) costs.Add(opt.CostReputation + " 声望");
                    label = opt.Label + "（" + string.Join(" + ", costs) + "）  [ " + opt.ResultText + " ]";
                }

                var go = MakeEventButton(defaultFont, label, block != null);
                if (block == null)
                {
                    int index = i;
                    go.GetComponent<Button>().onClick.AddListener(() => OnEventOptionClicked(index));
                }
            }
        }

        private void RenderEventChoiceOptions(Font defaultFont)
        {
            switch (RunSession.PendingEventChoice)
            {
                case EventOptionChoiceKind.RemoveCard:
                {
                    var cards = RunSession.CampaignDeck != null ? RunSession.CampaignDeck.RemoveableCards() : new List<string>();
                    if (cards.Count == 0)
                    {
                        RunSession.RecordResolution("事件", "移除卡", "没有可移除的卡牌");
                        RunSession.CancelEventChoice();
                        ShowEventPage();
                        return;
                    }

                    foreach (var id in cards)
                    {
                        var c = CardCatalog.Find(id);
                        var go = MakeEventButton(defaultFont, "移除 " + (c != null ? c.DisplayName : id), false);
                        string captured = id;
                        go.GetComponent<Button>().onClick.AddListener(() => OnEventCardChosen(captured));
                    }

                    break;
                }

                case EventOptionChoiceKind.UpgradeCard:
                {
                    if (RunSession.CampaignDeck == null) return;
                    var seen = new HashSet<string>();
                    foreach (var id in RunSession.CampaignDeck.Cards)
                    {
                        if (!seen.Add(id)) continue;
                        var c = CardCatalog.Find(id);
                        var go = MakeEventButton(defaultFont, "升级 " + (c != null ? c.DisplayName : id), false);
                        string captured = id;
                        go.GetComponent<Button>().onClick.AddListener(() => OnEventCardChosen(captured));
                    }

                    break;
                }

                case EventOptionChoiceKind.StatusFatigue:
                case EventOptionChoiceKind.StatusDiseaseOrFatigue:
                {
                    bool disease = RunSession.PendingEventChoice == EventOptionChoiceKind.StatusDiseaseOrFatigue;
                    RenderStatusUnitButtons(defaultFont, disease);
                    break;
                }
            }
        }

        private void RenderStatusUnitButtons(Font defaultFont, bool includeDisease)
        {
            // 主角
            if (RunSession.PlayerFatigue > 0 || (includeDisease && RunSession.PlayerDisease > 0))
            {
                var go = MakeEventButton(defaultFont, "主角（疲劳 " + RunSession.PlayerFatigue
                    + (includeDisease ? " / 疾病 " + RunSession.PlayerDisease : "") + "）", false);
                go.GetComponent<Button>().onClick.AddListener(() => OnEventUnitChosen("PLAYER", false));
                if (includeDisease && RunSession.PlayerDisease > 0)
                {
                    var go2 = MakeEventButton(defaultFont, "主角 — 移除疾病", false);
                    go2.GetComponent<Button>().onClick.AddListener(() => OnEventUnitChosen("PLAYER", true));
                }
            }

            // 存活伙伴
            foreach (var p in PartnerRoster.All)
            {
                if (!p.IsRecruited || !p.IsAlive) continue;
                if (p.Fatigue > 0 || (includeDisease && p.Disease > 0))
                {
                    var go = MakeEventButton(defaultFont, p.Def.DisplayName + "（疲劳 " + p.Fatigue
                        + (includeDisease ? " / 疾病 " + p.Disease : "") + "）", false);
                    string pid = p.Def.Id;
                    go.GetComponent<Button>().onClick.AddListener(() => OnEventUnitChosen(pid, false));
                    if (includeDisease && p.Disease > 0)
                    {
                        var go2 = MakeEventButton(defaultFont, p.Def.DisplayName + " — 移除疾病", false);
                        string pid2 = p.Def.Id;
                        go2.GetComponent<Button>().onClick.AddListener(() => OnEventUnitChosen(pid2, true));
                    }
                }
            }
        }

        private GameObject MakeEventButton(Font font, string label, bool disabled)
        {
            var go = new GameObject("EO_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_eventOptionContainer, false);

            var img = go.GetComponent<Image>();
            img.color = disabled ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.3f, 0.45f, 0.6f);

            // 宽度按文本估算（字号 16，中文约 16px/字，留边距）；布局组按 preferredWidth 控制宽度
            var le = go.GetComponent<LayoutElement>();
            le.minWidth = 160;
            le.preferredWidth = label.Length * 18 + 32;
            le.minHeight = 38;
            le.preferredHeight = 38;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);

            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.font = font;
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8, 2);
            textRt.offsetMax = new Vector2(-8, -2);

            go.GetComponent<Button>().interactable = !disabled;
            return go;
        }

        private void OnEventOptionClicked(int optionIndex)
        {
            string result = RunSession.ChooseEventOption(optionIndex);

            if (RunSession.PendingEventChoice != EventOptionChoiceKind.None)
            {
                // 进入子选择
                ShowEventPage();
                _pageDescriptionText.text += "\n" + result;
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
                ShowEventPage();
                _pageDescriptionText.text += "\n" + result;
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
                ShowEventPage();
                _pageDescriptionText.text += "\n" + result;
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

        private static string BuildMapDescription()
        {
            if (!RegionMap.IsGenerated) return "地图尚未生成。";

            string desc = "草原地图（共 " + RegionMap.LayerCount + " 层）\n";
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

            desc += "资源：粮食 " + RunSession.Food
                + " / 财富 " + RunSession.Wealth
                + " / 声望 " + RunSession.Reputation
                + " / 建材 " + RunSession.Materials;
            if (RunSession.Risk > 0) desc += " | 风险 " + RunSession.Risk;
            desc += "\n";
            desc += "风险提示：草原每次移动风险 +1，精英节点额外 +1；达到 " + GameStartParameters.RiskThreshold
                + " 触发危机伏击（移动消耗与风险结算在后续步骤接入）。\n";

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
