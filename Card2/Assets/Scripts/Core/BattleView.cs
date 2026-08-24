using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneJourney.Core
{
    /// <summary>
    /// 战斗界面（A1-14）。BattlePage 从 Prefab 动态实例化，场景中不留战斗 UI。
    /// </summary>
    public sealed class BattleView : MonoBehaviour
    {
        [Header("整体战斗界面 Prefab")]
        [SerializeField] private GameObject _battlePagePrefab;

        [Header("卡片 Prefab")]
        [SerializeField] private GameObject _handCardPrefab;
        [SerializeField] private GameObject _unitCardPrefab;   // 队伍用
        [SerializeField] private GameObject _enemyCardPrefab;  // 敌人用
        [SerializeField] private GameObject _skipRewardButtonPrefab; // 跳过奖励按钮（旧结算区用）
        [SerializeField] private GameObject _rewardPagePrefab;       // 独立奖励页（胜利后弹出）
        [SerializeField] private GameObject _relicRewardPrefab;      // 遗物奖励条目（A2-22）

        // 从 Prefab 实例中解析的引用（非序列化）
        private GameObject _rootPanel;
        private TMP_Text _turnInfoText;
        private TMP_Text _energyText;
        private TMP_Text _moraleText;
        private TMP_Text _plunderText;
        private Button _endTurnButton;
        private Button _returnButton;
        private Button _prevEncounterButton;
        private Button _nextEncounterButton;
        private Button _simulateVictoryButton;
        private Button _simulateDefeatButton;
        private Transform _teamContainer;
        private Transform _enemyContainer;
        private Transform _handContainer;
        private TMP_Text _drawPileText;
        private TMP_Text _discardPileText;

        // 奖励页（A2-20.5）运行时引用
        private GameObject _rewardPanel;
        private TMP_Text _rewardTitleText;
        private TMP_Text _rewardDetailText;
        private Transform _rewardCardContainer;
        private Button _rewardSkipBtn;
        private Button _rewardContinueBtn;
        private readonly List<GameObject> _rewardCardGos = new List<GameObject>();
        private string _rewardStatusText; // 领卡/跳过后显示在明细下

        private int _selectedHandIndex = -1;
        private CardDef _selectedCard;
        private readonly List<GameObject> _handCardGos = new List<GameObject>();
        private readonly List<GameObject> _teamUnitGos = new List<GameObject>();
        private readonly List<GameObject> _enemyUnitGos = new List<GameObject>();
        private bool _targetMode;
        private bool _combatWon; // 本场胜利标记（模拟胜利 End 后 Phase 变 Ended，不依赖 Phase 判断）

        private void Awake()
        {
            // 按钮绑定延迟到首次 Show 时（因为那时才实例化 Prefab）
        }

        public void Show()
        {
            if (_rootPanel == null)
            {
                if (_battlePagePrefab == null) return;
                var canvas = GetComponentInChildren<Canvas>();
                var canvasTr = canvas != null ? canvas.transform : transform;
                _rootPanel = Instantiate(_battlePagePrefab, canvasTr);
                ResolveRefs();
                if (_endTurnButton != null) _endTurnButton.onClick.AddListener(OnEndTurn);
                if (_returnButton != null) _returnButton.onClick.AddListener(OnReturn);
                if (_prevEncounterButton != null) _prevEncounterButton.onClick.AddListener(OnPrevEncounter);
                if (_nextEncounterButton != null) _nextEncounterButton.onClick.AddListener(OnNextEncounter);
                if (_simulateVictoryButton != null) _simulateVictoryButton.onClick.AddListener(OnSimulateVictory);
                if (_simulateDefeatButton != null) _simulateDefeatButton.onClick.AddListener(OnSimulateDefeat);
                EnsureRewardPanel(canvasTr);

                // 确保 HUD 渲染在最上层
                var hud = canvasTr.Find("TestHud");
                if (hud != null) hud.SetAsLastSibling();
            }

            _postCombatBuilt = false;
            _rewardStatusText = null;
            _combatWon = false;
            if (_rewardPanel != null) _rewardPanel.SetActive(false);
            _rootPanel.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (_rootPanel != null) _rootPanel.SetActive(false);
            if (_rewardPanel != null) _rewardPanel.SetActive(false);
            _selectedHandIndex = -1; _selectedCard = null; _targetMode = false;
            _postCombatBuilt = false;
            _rewardStatusText = null;
            _combatWon = false;
        }

        public void Refresh()
        {
            if (_rootPanel == null || !_rootPanel.activeSelf) return;
            if (CombatManager.Phase != CombatPhase.Running)
            {
                // 战斗已结束（胜利/失败），显示结算信息
                if (CombatManager.Phase == CombatPhase.Victory || CombatManager.Phase == CombatPhase.Defeat)
                    RefreshPostCombat();
                else
                    Hide();
                return;
            }
            RefreshTurnInfo();
            RefreshTeam();
            RefreshEnemies();
            RefreshHand();
            RefreshDeckCounts();
            if (_endTurnButton != null)
            {
                _endTurnButton.gameObject.SetActive(CombatManager.CanPlayerAct);
                _endTurnButton.interactable = CombatManager.CanPlayerAct;
            }
        }

        private void ResolveRefs()
        {
            var r = _rootPanel.transform;
            _turnInfoText = r.Find("TopBar/TurnInfo")?.GetComponent<TMP_Text>();
            _energyText = r.Find("TopBar/EnergyLabel")?.GetComponent<TMP_Text>();
            _moraleText = r.Find("TopBar/MoraleLabel")?.GetComponent<TMP_Text>();
            _plunderText = r.Find("TopBar/PlunderLabel")?.GetComponent<TMP_Text>();
            // 结束回合按钮：TopBar 或 MainArea 下均可
            _endTurnButton = r.Find("TopBar/EndTurnBtn")?.GetComponent<Button>()
                ?? r.Find("MainArea/EndTurnBtn")?.GetComponent<Button>();
            _returnButton = r.Find("MainArea/RightPanel/ReturnBtn")?.GetComponent<Button>();
            // 测试入口遭遇翻页（BattlePage 顶部；TestPage 在战斗中隐藏，翻页按钮需在战斗页内）
            _prevEncounterButton = r.Find("TopBar/Button_PrevEncounter")?.GetComponent<Button>();
            _nextEncounterButton = r.Find("TopBar/Button_NextEncounter")?.GetComponent<Button>();
            _simulateVictoryButton = r.Find("TopBar/Button_SimulateVictory")?.GetComponent<Button>();
            _simulateDefeatButton = r.Find("TopBar/Button_SimulateDefeat")?.GetComponent<Button>();
            _teamContainer = r.Find("MainArea/TeamPanel");
            _enemyContainer = r.Find("MainArea/EnemyPanel");
            _handContainer = r.Find("BottomBar/HandCards");
            _drawPileText = r.Find("BottomBar/DrawPile/DrawCount")?.GetComponent<TMP_Text>();
            _discardPileText = r.Find("BottomBar/DiscardPile/DiscardCount")?.GetComponent<TMP_Text>();
        }

        private bool _postCombatBuilt;

        private void RefreshPostCombat()
        {
            // 防重入：同一状态只重建一次
            if (_postCombatBuilt) return;
            _postCombatBuilt = true;

            // 清空手牌区（延迟销毁避免点击事件被吞）
            for (int i = _handContainer.childCount - 1; i >= 0; i--)
                Destroy(_handContainer.GetChild(i).gameObject);
            _handCardGos.Clear();

            // 结算信息：胜利的标题/资源/卡牌由独立奖励页展示，战斗页不再显示；失败提示保留在战斗页
            if (_turnInfoText != null)
            {
                _turnInfoText.text = CombatManager.Phase == CombatPhase.Defeat ? "战斗结束 — 失败" : "";
            }
            if (_energyText != null) _energyText.text = "";
            if (_moraleText != null) _moraleText.text = "";
            if (_plunderText != null) _plunderText.text = "";

            // 刷新队伍/敌人显示最终状态
            RefreshTeam();
            RefreshEnemies();

            // 胜利：弹出独立奖励页（资源明细 + 卡牌选项）
            if (CombatManager.Phase == CombatPhase.Victory || _combatWon)
            {
                ShowRewardPage();
            }
            else if (CombatManager.Phase == CombatPhase.Defeat && RunSession.CurrentState == GameState.Defeat)
            {
                // A2-24：真实主角死亡（已转移 Defeat）→ 进入结算页
                var ui = FindObjectOfType<GameUi>();
                if (ui != null)
                {
                    RunSession.EnterSettlement(false, "主角阵亡");
                    ui.ShowSettlement();
                }
            }

            if (_endTurnButton != null) _endTurnButton.gameObject.SetActive(false);
        }

        private System.Collections.IEnumerator RebuildPostCombatNextFrame()
        {
            yield return null; // 等点击事件完全结束再重建
            _postCombatBuilt = false;
            RefreshPostCombat();
        }

        // === 奖励页（A2-20.5）===

        private void EnsureRewardPanel(Transform canvasTr)
        {
            if (_rewardPagePrefab == null || _rewardPanel != null) return;
            _rewardPanel = Instantiate(_rewardPagePrefab, canvasTr);
            _rewardPanel.SetActive(false);
            var r = _rewardPanel.transform;
            _rewardTitleText = r.Find("TitleBar/Title/Text")?.GetComponent<TMP_Text>();
            _rewardDetailText = r.Find("TitleBar/RewardDetail/Text")?.GetComponent<TMP_Text>();
            _rewardCardContainer = r.Find("CardOptions");
            _rewardSkipBtn = r.Find("BottomBar/SkipBtn")?.GetComponent<Button>();
            _rewardContinueBtn = r.Find("BottomBar/ContinueBtn")?.GetComponent<Button>();
            if (_rewardSkipBtn != null) _rewardSkipBtn.onClick.AddListener(OnRewardSkip);
            if (_rewardContinueBtn != null) _rewardContinueBtn.onClick.AddListener(OnRewardContinue);
        }

        /// <summary>弹出奖励页（战斗胜利后自动调用；模拟胜利等外部路径也可直接调用）。</summary>
        public bool ShowRewardPage()
        {
            if (_rewardPanel == null) return false;
            _combatWon = true; // End 后 Phase 变 Ended，领卡重建等路径依赖此标记
            _rewardPanel.SetActive(true);

            // 确保 HUD 渲染在最上层
            var hud = _rewardPanel.transform.parent?.Find("TestHud");
            if (hud != null) hud.SetAsLastSibling();

            if (_rewardTitleText != null) _rewardTitleText.text = "战斗胜利";
            if (_rewardDetailText != null)
            {
                string detail = string.IsNullOrEmpty(RunSession.LastCombatRewardText)
                    ? "无资源奖励" : RunSession.LastCombatRewardText;
                if (!string.IsNullOrEmpty(_rewardStatusText)) detail += "\n" + _rewardStatusText;
                _rewardDetailText.text = detail;
            }

            // 清空旧选项（清理 RewardPage 根下本页生成的卡片/遗物条目）
            for (int i = _rewardPanel.transform.childCount - 1; i >= 0; i--)
            {
                var old = _rewardPanel.transform.GetChild(i);
                if (old.name.StartsWith("Reward")) Destroy(old.gameObject);
            }
            _rewardCardGos.Clear();

            if (RewardResolver.HasPendingRewards)
            {
                int total = RewardResolver.PendingOptions.Count;
                int cardCount = 0, relicCount = 0;
                foreach (var o in RewardResolver.PendingOptions)
                {
                    if (!string.IsNullOrEmpty(o.CardId)) cardCount++;
                    else if (!string.IsNullOrEmpty(o.RelicId)) relicCount++;
                }

                int cardIdx = 0, relicIdx = 0;
                for (int i = 0; i < total; i++)
                {
                    int idx = i;
                    var opt = RewardResolver.PendingOptions[i];

                    if (!string.IsNullOrEmpty(opt.RelicId))
                    {
                        // 遗物奖励：金色条目（prefab），排在卡牌右侧
                        var relic = RelicCatalog.Find(opt.RelicId);
                        if (relic == null || _relicRewardPrefab == null) continue;
                        float x = (cardCount + relicIdx - (total - 1) * 0.5f) * 224f;
                        var go = Instantiate(_relicRewardPrefab, _rewardPanel.transform);
                        go.name = "RewardRelic_" + relic.Id;
                        var rrt = go.GetComponent<RectTransform>();
                        rrt.anchorMin = new Vector2(0.5f, 0.5f);
                        rrt.anchorMax = new Vector2(0.5f, 0.5f);
                        rrt.pivot = new Vector2(0.5f, 0.5f);
                        rrt.sizeDelta = new Vector2(200, 300);
                        rrt.anchoredPosition = new Vector2(x, 0);
                        SetTmp(go, "Name", relic.DisplayName);
                        SetTmp(go, "Effect", relic.EffectText);
                        go.GetComponent<Button>().onClick.RemoveAllListeners();
                        go.GetComponent<Button>().onClick.AddListener(() =>
                        {
                            string claimed = RewardResolver.ClaimRelic(idx);
                            if (claimed != null)
                            {
                                RunSession.RecordResolution("战斗奖励", "选择遗物 " + claimed,
                                    "已获得：" + RelicCatalog.Find(claimed)?.DisplayName);
                                _rewardStatusText = "已领取遗物：" + relic.DisplayName;
                            }
                            _postCombatBuilt = false;
                            StartCoroutine(RebuildPostCombatNextFrame());
                        });
                        _rewardCardGos.Add(go);
                        relicIdx++;
                        continue;
                    }

                    var cardDef = CardCatalog.Find(opt.CardId);
                    if (cardDef == null) continue;

                    // 卡片直接挂到奖励页根并手动布局：CardOptions 容器下渲染异常（已实测），
                    // 挂到有 Image 的 RewardPage 根可正常渲染
                    var go2 = Instantiate(_handCardPrefab, _rewardPanel.transform);
                    go2.name = "Reward_" + cardDef.Id;
                    var cardRt = go2.GetComponent<RectTransform>();
                    cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                    cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                    cardRt.pivot = new Vector2(0.5f, 0.5f);
                    cardRt.sizeDelta = new Vector2(200, 300);
                    cardRt.anchoredPosition = new Vector2((cardIdx - (total - 1) * 0.5f) * 224f, 0);
                    Color baseColor = CardColor(cardDef);
                    var img = go2.GetComponent<Image>();
                    if (img != null) img.color = new Color(baseColor.r * 0.85f, baseColor.g * 0.85f, baseColor.b * 0.85f);
                    var bar = go2.transform.Find("TopBar");
                    if (bar != null)
                    {
                        var barImg = bar.GetComponent<Image>();
                        if (barImg != null) barImg.color = baseColor;
                    }
                    SetTmp(go2, "Word/CostRow/Cost", cardDef.Cost.ToString());
                    SetTmp(go2, "Word/CostRow/Name", cardDef.DisplayName);
                    SetTmp(go2, "Word/Effect", cardDef.EffectText);

                    var btn = go2.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => {
                            string claimed = RewardResolver.ClaimCard(idx);
                            if (claimed != null && RunSession.CampaignDeck != null)
                            {
                                RunSession.CampaignDeck.AddCard(claimed);
                                RunSession.RecordResolution("战斗奖励", "选择卡牌 " + claimed,
                                    "已加入牌组，当前 " + RunSession.CampaignDeck.Count + " 张");
                                _rewardStatusText = "已领取：" + cardDef.DisplayName;
                            }
                            _postCombatBuilt = false;
                            StartCoroutine(RebuildPostCombatNextFrame());
                        });
                    }
                    _rewardCardGos.Add(go2);
                    cardIdx++;
                }
                if (_rewardSkipBtn != null) _rewardSkipBtn.gameObject.SetActive(true);
                if (_rewardContinueBtn != null) _rewardContinueBtn.gameObject.SetActive(false);
            }
            else
            {
                if (_rewardSkipBtn != null) _rewardSkipBtn.gameObject.SetActive(false);
                if (_rewardContinueBtn != null) _rewardContinueBtn.gameObject.SetActive(true);
            }
            return true;
        }

        private void OnRewardSkip()
        {
            RewardResolver.SkipReward();
            RunSession.RecordResolution("战斗奖励", "跳过卡牌奖励", "已放弃选择");
            _rewardStatusText = "已跳过奖励";
            _postCombatBuilt = false;
            StartCoroutine(RebuildPostCombatNextFrame());
        }

        private void OnRewardContinue()
        {
            Hide();
            var ui = FindObjectOfType<GameUi>();
            if (ui != null)
            {
                // A2-24：密林首领胜利（Victory 状态）→ 结算页
                if (RunSession.CurrentState == GameState.Victory)
                {
                    RunSession.EnterSettlement(true, "击败密林首领（垂直切片）");
                    ui.ShowSettlement();
                    return;
                }

                // A2-23：地图战斗（含区域首领胜利后切密林）→ 返回地图继续；测试入口无地图 → 回主菜单
                if (RegionMap.IsGenerated && RunSession.CurrentState != GameState.MainMenu)
                    ui.ReturnToMap();
                else
                    ui.ReturnToMenu();
            }
        }

        private void RefreshTurnInfo()
        {
            if (_turnInfoText != null)
                _turnInfoText.text = "回合 " + CombatManager.TurnNumber + "  |  "
                    + (CombatManager.CanPlayerAct ? "你的回合" : "敌方行动中");
            if (_energyText != null)
                _energyText.text = "能量 " + CombatManager.Energy + "/" + CombatManager.MaxEnergy;
            if (_moraleText != null) _moraleText.text = "士气 " + CombatManager.Morale;
            if (_plunderText != null) _plunderText.text = "掠夺 " + CombatManager.Plunder;
        }

        // === 队伍 / 敌人 ===

        private void RefreshTeam()
        {
            ClearContainer(_teamContainer, _teamUnitGos);
            if (CombatManager.PlayerTeam == null || _unitCardPrefab == null) return;
            foreach (var u in CombatManager.PlayerTeam)
                _teamUnitGos.Add(CreateUnitCard(u, _teamContainer, true, _unitCardPrefab));
        }

        private void RefreshEnemies()
        {
            ClearContainer(_enemyContainer, _enemyUnitGos);
            if (CombatManager.EnemyTeam == null || _enemyCardPrefab == null) return;
            foreach (var e in CombatManager.EnemyTeam)
                _enemyUnitGos.Add(CreateUnitCard(e, _enemyContainer, false, _enemyCardPrefab));
        }

        private GameObject CreateUnitCard(CombatUnit unit, Transform parent, bool isAlly, GameObject prefab)
        {
            var go = Instantiate(prefab, parent);
            go.name = "Unit_" + unit.DisplayName;

            var img = go.GetComponent<Image>();
            if (img != null) img.color = isAlly
                ? new Color(0.08f, 0.1f, 0.2f) : new Color(0.15f, 0.06f, 0.06f);

            var bar = go.transform.Find("TopBar");
            if (bar != null)
            {
                var barImg = bar.GetComponent<Image>();
                if (barImg != null) barImg.color = isAlly
                    ? (unit.IsPlayerCharacter ? new Color(0.2f, 0.5f, 0.7f) : new Color(0.22f, 0.35f, 0.55f))
                    : new Color(0.65f, 0.2f, 0.15f);
            }

            SetTmp(go, "Name", unit.DisplayName + (unit.IsAlive ? "" : " [阵亡]"));
            var nameT = go.transform.Find("Name")?.GetComponent<TMP_Text>();
            if (nameT != null) nameT.color = unit.IsAlive ? Color.white : Color.gray;

            // 伙伴定位/特质
            string subText = "";
            if (!unit.IsPlayerCharacter)
            {
                var partner = PartnerRoster.Find(unit.Id);
                if (partner != null) subText = partner.Def.Role + " · " + partner.Def.Trait;
            }
            string hpStr = (subText.Length > 0 ? subText + "\n" : "")
                + "生命 " + unit.CurrentHp + "/" + unit.EffectiveMaxHp;
            if (unit.Armor > 0) hpStr += "  护甲 " + unit.Armor;
            SetTmp(go, "HP", hpStr);

            var parts = new List<string>();
            if (unit.Bleed > 0) parts.Add("流血" + unit.Bleed);
            if (unit.Disease > 0) parts.Add("疾病" + unit.Disease);
            if (unit.Fatigue > 0) parts.Add("疲劳" + unit.Fatigue);
            if (unit.FocusFireExtra > 0) parts.Add("集火+" + unit.FocusFireExtra);
            var st = go.transform.Find("Status")?.GetComponent<TMP_Text>();
            if (st != null) { st.text = parts.Count > 0 ? string.Join("  ", parts) : ""; st.gameObject.SetActive(parts.Count > 0); }

            var it = go.transform.Find("Intent")?.GetComponent<TMP_Text>();
            if (it != null)
            {
                bool show = !isAlly && unit is EnemyUnit eu && eu.CurrentIntent != null;
                it.text = show ? "意图：" + ((EnemyUnit)unit).CurrentIntent.Describe() : "";
                it.gameObject.SetActive(show);
            }

            if (_targetMode && unit.IsAlive && IsValidTarget(unit))
            {
                var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
                var captured = unit;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnTargetSelected(captured));
                if (img != null) img.color = new Color(0.25f, 0.25f, 0.08f);
            }

            return go;
        }

        // === 手牌 ===

        private void RefreshHand()
        {
            ClearContainer(_handContainer, _handCardGos);
            if (CombatManager.Deck == null || _handCardPrefab == null) return;
            for (int i = 0; i < CombatManager.Deck.HandSize; i++)
            {
                var cd = CardCatalog.Find(CombatManager.Deck.Hand[i]);
                if (cd != null) _handCardGos.Add(CreateHandCard(cd, i));
            }
        }

        private GameObject CreateHandCard(CardDef card, int handIndex)
        {
            var go = Instantiate(_handCardPrefab, _handContainer);
            go.name = "HC_" + card.Id;

            Color baseColor = CardColor(card);
            bool selected = _selectedHandIndex == handIndex;

            var img = go.GetComponent<Image>();
            if (img != null) img.color = selected
                ? new Color(0.5f, 0.4f, 0.15f)
                : new Color(baseColor.r * 0.6f, baseColor.g * 0.6f, baseColor.b * 0.6f);

            var bar = go.transform.Find("TopBar");
            if (bar != null)
            {
                var barImg = bar.GetComponent<Image>();
                if (barImg != null) barImg.color = baseColor;
            }

            SetTmp(go, "Word/CostRow/Cost", card.Cost.ToString());
            SetTmp(go, "Word/CostRow/Name", card.DisplayName);
            SetTmp(go, "Word/Effect", card.EffectText);

            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                int captured = handIndex;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnHandCardClicked(captured));
            }

            return go;
        }

        private void RefreshDeckCounts()
        {
            if (CombatManager.Deck == null) return;
            if (_drawPileText != null) _drawPileText.text = "抽牌堆\n" + CombatManager.Deck.DrawPileCount;
            if (_discardPileText != null) _discardPileText.text = "弃牌堆\n" + CombatManager.Deck.DiscardPileCount;
        }

        // === 交互 ===

        private void OnHandCardClicked(int handIndex)
        {
            if (!CombatManager.CanPlayerAct) return;
            var card = CardCatalog.Find(CombatManager.Deck.Hand[handIndex]);
            if (card == null) return;
            if (card.TargetType == TargetType.None || card.TargetType == TargetType.Self
                || card.TargetType == TargetType.AllEnemies || card.TargetType == TargetType.AllAllies)
            {
                string result = CombatResolver.PlayCard(handIndex);
                // 若这一击结束战斗，CheckEndCondition 已记录「战斗奖励」，出牌记录不再覆盖
                if (CombatManager.Phase != CombatPhase.Victory && CombatManager.Phase != CombatPhase.Defeat)
                    RunSession.RecordResolution("手牌出牌", "打出 " + card.DisplayName, result);
                _selectedHandIndex = -1; _selectedCard = null; _targetMode = false;
                Refresh();
            }
            else
            {
                _targetMode = _selectedHandIndex != handIndex;
                _selectedHandIndex = _targetMode ? handIndex : -1;
                _selectedCard = _targetMode ? card : null;
                Refresh();
            }
        }

        private void OnTargetSelected(CombatUnit target)
        {
            if (!_targetMode || _selectedCard == null || _selectedHandIndex < 0) return;
            if (!CombatManager.CanPlayerAct) return;
            string result = CombatResolver.PlayCard(_selectedHandIndex, target);
            if (CombatManager.Phase != CombatPhase.Victory && CombatManager.Phase != CombatPhase.Defeat)
                RunSession.RecordResolution("手牌出牌",
                    "打出 " + _selectedCard.DisplayName + " → " + target.DisplayName, result);
            _selectedHandIndex = -1; _selectedCard = null; _targetMode = false;
            Refresh();
        }

        private void OnEndTurn()
        {
            if (!CombatManager.CanPlayerAct) return;
            CombatManager.EndPlayerTurn();
            _selectedHandIndex = -1; _selectedCard = null; _targetMode = false;
            Refresh();
            if (CombatManager.IsActive) Refresh();
        }

        private void OnReturn()
        {
            Hide();
            var ui = FindObjectOfType<GameUi>();
            if (ui != null) ui.ReturnToMenu();
        }

        // === 测试遭遇翻页（A1-12 测试辅助，战斗中重开战斗）===

        private void OnPrevEncounter()
        {
            RunSession.PrevEncounter();
            RunSession.RelaunchTestCombat();
            Refresh();
        }

        private void OnNextEncounter()
        {
            RunSession.NextEncounter();
            RunSession.RelaunchTestCombat();
            Refresh();
        }

        // === 测试模拟胜负（A2-21：首领遭遇胜利解锁城镇建筑）===

        private void OnSimulateVictory()
        {
            if (!CombatManager.IsActive) return;
            foreach (var e in CombatManager.EnemyTeam)
            {
                if (e.IsAlive) e.TakeDamage(e.CurrentHp + e.Armor);
            }

            CombatManager.CheckEndCondition();
            bool won = CombatManager.Phase == CombatPhase.Victory;
            CombatManager.End();
            Refresh(); // Ended → 隐藏战斗页
            if (won) ShowRewardPage(); // 胜利弹出独立奖励页（CheckEndCondition 已记录战斗奖励）
        }

        private void OnSimulateDefeat()
        {
            if (!CombatManager.IsActive) return;
            CombatManager.ForceDefeat();
            CombatManager.End();
            // A2-24：失败 → Defeat 状态 + 结算页
            RunSession.EnterDefeatState();
            RunSession.EnterSettlement(false, "主角阵亡");
            var ui = FindObjectOfType<GameUi>();
            if (ui != null) ui.ShowSettlement();
            else Refresh();
        }

        // === 辅助 ===

        private static void SetTmp(GameObject root, string childPath, string value)
        {
            var t = root.transform.Find(childPath);
            if (t != null)
            {
                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = value;
            }
        }

        private void ClearContainer(Transform container, List<GameObject> tracked)
        {
            foreach (var go in tracked) { if (go != null) Destroy(go); }
            tracked.Clear();
        }

        private bool IsValidTarget(CombatUnit unit)
        {
            if (_selectedCard == null || !unit.IsAlive) return false;
            var tt = _selectedCard.TargetType;
            bool isEnemy = CombatManager.EnemyTeam != null && CombatManager.EnemyTeam.Contains(unit);
            bool isAlly = CombatManager.PlayerTeam != null && CombatManager.PlayerTeam.Contains(unit);
            switch (tt)
            {
                case TargetType.SingleEnemy: return isEnemy;
                case TargetType.SingleAlly: return isAlly;
                default: return true;
            }
        }

        private static Color CardColor(CardDef card)
        {
            if (card.TargetType == TargetType.SingleEnemy || card.TargetType == TargetType.AllEnemies)
            {
                bool hasDef = card.Effects.Exists(e =>
                    e.Type == CardEffectType.GainArmor || e.Type == CardEffectType.SelfArmor);
                return hasDef ? new Color(0.55f, 0.25f, 0.2f) : new Color(0.6f, 0.18f, 0.15f);
            }
            if (card.TargetType == TargetType.Self || card.TargetType == TargetType.SingleAlly
                || card.TargetType == TargetType.AllAllies)
            {
                bool hasDmg = card.Effects.Exists(e => e.Type == CardEffectType.Damage);
                return hasDmg ? new Color(0.4f, 0.25f, 0.5f) : new Color(0.18f, 0.3f, 0.55f);
            }
            bool isLog = card.Effects.Exists(e =>
                e.Type == CardEffectType.Heal || e.Type == CardEffectType.SupplyFood
                || e.Type == CardEffectType.RemoveBleed || e.Type == CardEffectType.RemoveDisease
                || e.Type == CardEffectType.RemoveFatigue || e.Type == CardEffectType.RemoveInjury);
            return isLog ? new Color(0.15f, 0.4f, 0.2f) : new Color(0.35f, 0.22f, 0.5f);
        }
    }
}
