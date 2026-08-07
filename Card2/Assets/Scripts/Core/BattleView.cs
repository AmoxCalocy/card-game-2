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

        // 从 Prefab 实例中解析的引用（非序列化）
        private GameObject _rootPanel;
        private TMP_Text _turnInfoText;
        private TMP_Text _energyText;
        private TMP_Text _moraleText;
        private TMP_Text _plunderText;
        private Button _endTurnButton;
        private Button _returnButton;
        private Transform _teamContainer;
        private Transform _enemyContainer;
        private Transform _handContainer;
        private TMP_Text _drawPileText;
        private TMP_Text _discardPileText;

        private int _selectedHandIndex = -1;
        private CardDef _selectedCard;
        private readonly List<GameObject> _handCardGos = new List<GameObject>();
        private readonly List<GameObject> _teamUnitGos = new List<GameObject>();
        private readonly List<GameObject> _enemyUnitGos = new List<GameObject>();
        private bool _targetMode;

        private void Awake()
        {
            // 按钮绑定延迟到首次 Show 时（因为那时才实例化 Prefab）
        }

        public void Show()
        {
            if (_rootPanel == null)
            {
                if (_battlePagePrefab == null) return;
                var canvasTr = GetComponentInChildren<Canvas>()?.transform ?? transform;
                _rootPanel = Instantiate(_battlePagePrefab, canvasTr);
                ResolveRefs();
                if (_endTurnButton != null) _endTurnButton.onClick.AddListener(OnEndTurn);
                if (_returnButton != null) _returnButton.onClick.AddListener(OnReturn);
            }

            _rootPanel.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (_rootPanel != null) _rootPanel.SetActive(false);
            _selectedHandIndex = -1; _selectedCard = null; _targetMode = false;
        }

        public void Refresh()
        {
            if (_rootPanel == null || !_rootPanel.activeSelf) return;
            if (!CombatManager.IsActive) { Hide(); return; }
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
            _endTurnButton = r.Find("TopBar/EndTurnBtn")?.GetComponent<Button>();
            _returnButton = r.Find("MainArea/RightPanel/ReturnBtn")?.GetComponent<Button>();
            _teamContainer = r.Find("MainArea/TeamPanel");
            _enemyContainer = r.Find("MainArea/EnemyPanel");
            _handContainer = r.Find("BottomBar/HandCards");
            _drawPileText = r.Find("BottomBar/DrawPile/DrawCount")?.GetComponent<TMP_Text>();
            _discardPileText = r.Find("BottomBar/DiscardPile/DiscardCount")?.GetComponent<TMP_Text>();
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

            string hpStr = "生命 " + unit.CurrentHp + "/" + unit.EffectiveMaxHp;
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
                RunSession.RecordResolution("手牌出牌", "打出 " + card.DisplayName, CombatResolver.PlayCard(handIndex));
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
            RunSession.RecordResolution("手牌出牌",
                "打出 " + _selectedCard.DisplayName + " → " + target.DisplayName,
                CombatResolver.PlayCard(_selectedHandIndex, target));
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
            CombatManager.End();
            Hide();
            var ui = FindObjectOfType<GameUi>();
            if (ui != null) ui.ReturnToMenu();
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
