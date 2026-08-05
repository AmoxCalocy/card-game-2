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
        [SerializeField] private Button[] _testEntryButtons;
        [SerializeField] private GameState[] _testEntryStates;
        [SerializeField] private Button[] _modeSwitchButtons;
        [SerializeField] private GameMode[] _modeSwitchModes;
        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _recordResolutionButton;
        [SerializeField] private Button _returnToMenuButton;

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
            Refresh();
        }

        private void OnStartNewGame()
        {
            RunSession.StartNewGame();
            ShowPage("地图（新游戏入口）", "新游戏会话已创建。后续步骤将在此实现地图流程；当前为第 1 步占位页面。");
        }

        private void OnEnterTestPage(GameState page)
        {
            RunSession.EnterTestPage(page);
            ShowPage(
                "测试入口：" + RunSession.DisplayName(page),
                "当前为第 1 步占位页面，用于验证测试入口与退出清理。后续步骤将在此实现" + RunSession.DisplayName(page) + "流程。");
        }

        private void OnRecordSampleResolution()
        {
            RunSession.RecordResolution("测试结算（示例）", "普通伤害结算", "目标生命 28 → 22，护甲 0");
        }

        private void OnReturnToMenu()
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

            var last = RunSession.LastResolution;
            string lastText = last.HasValue
                ? last.Value.Source + "：" + last.Value.Description + " → " + last.Value.Result
                : "暂无";

            var transition = GameFlow.LastTransition;
            string lastTransitionText = transition.HasValue
                ? RunSession.DisplayName(transition.Value.From) + " → " + RunSession.DisplayName(transition.Value.To) + "（" + transition.Value.Reason + "）"
                : "暂无";

            _hudText.text = string.Format(
                "随机种子：{0}\n当前状态：{1}\n当前配置：{2}\n最近一次规则结算：{3}\n最近状态切换：{4}",
                RunSession.Seed,
                RunSession.DisplayName(RunSession.CurrentState),
                GameConfigProvider.Mode,
                lastText,
                lastTransitionText);
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
            Refresh();
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
