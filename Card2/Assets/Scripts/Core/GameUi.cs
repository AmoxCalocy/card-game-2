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
            if (_combatVictoryButton != null) _combatVictoryButton.gameObject.SetActive(isCombat);
            if (_combatDefeatButton != null) _combatDefeatButton.gameObject.SetActive(isCombat);
            if (_endTurnButton != null) _endTurnButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct);
            if (_spendEnergyButton != null) _spendEnergyButton.gameObject.SetActive(isCombat && CombatManager.CanPlayerAct && CombatManager.Energy > 0);

            Refresh();
        }

        private void OnStartNewGame()
        {
            RunSession.StartNewGame();
            ShowPage("地图（新游戏入口）", "新游戏会话已创建。后续步骤将在此实现地图流程；当前为第 1 步占位页面。");
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
                "新游戏会话已创建（" + (seed.HasValue ? "种子 " + seed.Value : "随机种子") + "）。");
        }

        private void OnEnterTestPage(GameState page)
        {
            RunSession.EnterTestPage(page);
            string desc;
            if (page == GameState.Combat && CombatManager.IsActive)
            {
                desc = BuildCombatDescription();
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

            string result = CombatManager.CheckEndCondition();
            RunSession.RecordResolution("战斗结算", "模拟胜利", result ?? "未知");
            ShowPage("测试入口：战斗", BuildCombatDescription());
        }

        private void OnSimulateDefeat()
        {
            if (!CombatManager.IsActive) return;

            CombatManager.ForceDefeat();
            RunSession.RecordResolution("战斗结算", "模拟失败", "主角阵亡");
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
            Refresh();
        }

        private static string BuildCombatDescription()
        {
            if (!CombatManager.IsActive) return "战斗未激活";

            string desc = "回合 " + CombatManager.TurnNumber + " | 能量 " + CombatManager.Energy + "/" + CombatManager.MaxEnergy + " | 阶段：" + CombatManager.CurrentTurnPhase;
            desc += " | 可行动：" + CombatManager.CanPlayerAct;

            desc += "\n玩家队伍：";
            if (CombatManager.PlayerTeam != null)
            {
                foreach (var u in CombatManager.PlayerTeam)
                {
                    string alive = u.IsAlive ? "" : " [阵亡]";
                    desc += "\n  " + u.DisplayName + " HP:" + u.CurrentHp + "/" + u.MaxHp + " 护甲:" + u.Armor + alive;
                }
            }

            desc += "\n敌人队伍：";
            if (CombatManager.EnemyTeam != null)
            {
                foreach (var e in CombatManager.EnemyTeam)
                {
                    string alive = e.IsAlive ? "" : " [阵亡]";
                    desc += "\n  " + e.DisplayName + " HP:" + e.CurrentHp + "/" + e.MaxHp + " 护甲:" + e.Armor + alive;
                }
            }

            if (CombatManager.Deck != null)
            {
                desc += "\n牌堆：抽牌堆 " + CombatManager.Deck.DrawPileCount
                    + " / 手牌 " + CombatManager.Deck.HandSize
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
