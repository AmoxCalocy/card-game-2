using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneJourney.Core
{
    public sealed class GameUi : MonoBehaviour
    {
        private static Font _builtinFont;

        private Canvas _canvas;
        private Text _hudText;
        private GameObject _menuPanel;
        private GameObject _menuContent;
        private GameObject _pagePanel;
        private Text _pageTitleText;
        private Text _pageDescriptionText;
        private readonly List<GameObject> _testEntryElements = new List<GameObject>();
        private readonly List<GameObject> _modeSwitchElements = new List<GameObject>();
        private float _menuY;

        private void Awake()
        {
            BuildCanvas();
            BuildEventSystem();
            BuildHud();
            BuildMenuPanel();
            BuildPagePanel();

            RunSession.Changed += Refresh;
            GameConfigProvider.Changed += RefreshConfigUi;

            ShowMenu();
            RefreshConfigUi();
        }

        private void OnDestroy()
        {
            RunSession.Changed -= Refresh;
            GameConfigProvider.Changed -= RefreshConfigUi;
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

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
        }

        private void BuildEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.transform.SetParent(transform, false);
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
        }

        private void BuildHud()
        {
            var hudGo = new GameObject("TestHud");
            hudGo.transform.SetParent(_canvas.transform, false);

            var rect = hudGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(680f, 200f);

            _hudText = hudGo.AddComponent<Text>();
            _hudText.font = GetBuiltinFont();
            _hudText.fontSize = 20;
            _hudText.color = new Color(0.95f, 0.9f, 0.3f, 1f);
            _hudText.alignment = TextAnchor.UpperLeft;
            _hudText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hudText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void BuildMenuPanel()
        {
            _menuPanel = CreatePanel("MainMenu");
            var contentRect = BuildScrollContent(_menuPanel);
            _menuContent = contentRect.gameObject;
            _menuY = 0f;

            CreateMenuText("一人旅途 · 运行基线（第 1 步）", 40, TextAnchor.MiddleCenter, 56f);
            CreateMenuButton("新游戏", OnStartNewGame);

            _testEntryElements.Add(CreateMenuText("测试入口", 28, TextAnchor.MiddleLeft, 40f).gameObject);
            _testEntryElements.Add(CreateMenuButton("测试：战斗", () => OnEnterTestPage(GameState.Combat)).gameObject);
            _testEntryElements.Add(CreateMenuButton("测试：地图", () => OnEnterTestPage(GameState.Map)).gameObject);
            _testEntryElements.Add(CreateMenuButton("测试：事件", () => OnEnterTestPage(GameState.Event)).gameObject);
            _testEntryElements.Add(CreateMenuButton("测试：营地", () => OnEnterTestPage(GameState.Camp)).gameObject);

            _modeSwitchElements.Add(CreateMenuText("运行配置（开发/测试/发布）", 28, TextAnchor.MiddleLeft, 40f).gameObject);
            _modeSwitchElements.Add(CreateMenuButton("使用开发配置", () => GameConfigProvider.ApplyMode(GameMode.Development)).gameObject);
            _modeSwitchElements.Add(CreateMenuButton("使用测试配置", () => GameConfigProvider.ApplyMode(GameMode.Testing)).gameObject);
            _modeSwitchElements.Add(CreateMenuButton("使用发布配置", () => GameConfigProvider.ApplyMode(GameMode.Release)).gameObject);

            CreateMenuButton("退出", OnQuit);

            contentRect.sizeDelta = new Vector2(0f, -_menuY);
        }

        private void BuildPagePanel()
        {
            _pagePanel = CreatePanel("TestPage");
            _pageTitleText = CreateText(_pagePanel.transform, string.Empty, 36, TextAnchor.MiddleCenter);
            _pageDescriptionText = CreateText(_pagePanel.transform, string.Empty, 24, TextAnchor.UpperLeft);

            var descriptionLayout = _pageDescriptionText.GetComponent<LayoutElement>();
            descriptionLayout.minHeight = 160f;
            descriptionLayout.preferredHeight = 240f;

            CreateButton(_pagePanel.transform, "写入测试结算记录", OnRecordSampleResolution);
            CreateButton(_pagePanel.transform, "返回主菜单", OnReturnToMenu);
        }

        private RectTransform BuildScrollContent(GameObject panel)
        {
            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(panel.transform, false);

            var viewportRect = viewportGo.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(20f, 20f);
            viewportRect.offsetMax = new Vector2(-20f, -20f);
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);

            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var scrollRect = panel.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            return contentRect;
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

            _hudText.text = string.Format(
                "随机种子：{0}\n当前状态：{1}\n当前配置：{2}\n最近一次规则结算：{3}",
                RunSession.Seed,
                RunSession.DisplayName(RunSession.CurrentState),
                GameConfigProvider.Mode,
                lastText);
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

        private GameObject CreatePanel(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(80f, 60f);
            rect.offsetMax = new Vector2(-80f, -60f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.06f, 0.07f, 0.1f, 0.96f);

            return go;
        }

        private Text CreateMenuText(string content, int size, TextAnchor alignment, float height)
        {
            var text = CreateText(_menuContent.transform, content, size, alignment);
            PlaceAtTop(text.rectTransform, _menuY, 700f, height);
            _menuY -= height + 10f;
            return text;
        }

        private Button CreateMenuButton(string label, Action onClick)
        {
            var button = CreateButton(_menuContent.transform, label, onClick);
            PlaceAtTop(button.GetComponent<RectTransform>(), _menuY, 700f, 48f);
            _menuY -= 58f;
            return button;
        }

        private static void PlaceAtTop(RectTransform rect, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetElementsActive(List<GameObject> elements, bool active)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                elements[i].SetActive(active);
            }
        }

        private static void AddVerticalLayout(GameObject go)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static Text CreateText(Transform parent, string content, int size, TextAnchor alignment)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = GetBuiltinFont();
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = size + 12f;
            layout.preferredHeight = size + 12f;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Action onClick)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.3f, 1f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(go.transform, label, 24, TextAnchor.MiddleCenter);
            text.color = Color.white;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 48f;
            layout.preferredHeight = 48f;

            button.onClick.AddListener(() => onClick());
            return button;
        }

        private static Font GetBuiltinFont()
        {
            if (_builtinFont == null)
            {
                _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _builtinFont;
        }
    }
}
