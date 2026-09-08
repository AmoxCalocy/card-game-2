using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OneJourney.Core
{
    /// <summary>A3-26 引导队列、帮助页和页面触发的协调器。</summary>
    public sealed class TutorialCoordinator : MonoBehaviour
    {
        public static TutorialCoordinator Instance { get; private set; }

        private readonly Queue<TutorialTopic> _pending = new Queue<TutorialTopic>();
        private readonly HashSet<TutorialTopic> _queued = new HashSet<TutorialTopic>();
        private readonly HashSet<int> _registeredHelpButtons = new HashSet<int>();

        private Canvas _canvas;
        private TutorialOverlayView _overlay;
        private HelpPageView _helpPage;
        private TutorialTopic? _current;
        private bool _viewsInitialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Tutorial] 场景中存在多个 TutorialCoordinator", this);
                return;
            }

            Instance = this;
            TutorialProgressService.Initialize();
            GameConfigProvider.Changed += RefreshConfig;
            EnsureViews();
        }

        private void Start()
        {
            EnsureViews();
            RegisterStaticHelpButtons();
        }

        private void OnDestroy()
        {
            GameConfigProvider.Changed -= RefreshConfig;
            if (Instance == this) Instance = null;
        }

        public void EnqueueForState(GameState state)
        {
            switch (state)
            {
                case GameState.Map:
                    Enqueue(TutorialTopic.MapMovement, TutorialTopic.Food);
                    break;
                case GameState.Combat:
                    Enqueue(TutorialTopic.PlayCards, TutorialTopic.SharedEnergy, TutorialTopic.EnemyIntent);
                    break;
                case GameState.Event:
                    Enqueue(TutorialTopic.EventChoice);
                    break;
                case GameState.Camp:
                    Enqueue(TutorialTopic.CampBuildings);
                    break;
            }
        }

        public void Enqueue(params TutorialTopic[] topics)
        {
            EnsureViews();
            if (_overlay == null || topics == null) return;

            for (int i = 0; i < topics.Length; i++)
            {
                TutorialTopic topic = topics[i];
                if (TutorialProgressService.IsCompleted(topic)) continue;
                if (_current.HasValue && _current.Value == topic) continue;
                if (!_queued.Add(topic)) continue;
                _pending.Enqueue(topic);
            }

            ShowNext();
        }

        public void RegisterHelpButton(Button button)
        {
            if (button == null) return;
            int id = button.GetInstanceID();
            if (!_registeredHelpButtons.Add(id)) return;
            button.onClick.AddListener(() => ShowHelp(HelpSection.Overview));
        }

        public void ShowHelp(HelpSection section)
        {
            EnsureViews();
            if (_helpPage == null) return;
            _helpPage.Show(section, null, GameConfigProvider.TestToolsEnabled);
            _helpPage.transform.SetAsLastSibling();
        }

        public void CloseAllOverlays()
        {
            _pending.Clear();
            _queued.Clear();
            _current = null;
            if (_overlay != null) _overlay.Hide();
            if (_helpPage != null) _helpPage.Hide();
        }

        private void EnsureViews()
        {
            if (_canvas == null) _canvas = GetComponentInChildren<Canvas>(true);
            if (_canvas == null) return;

            if (_overlay == null)
                _overlay = _canvas.transform.Find("TutorialOverlay")?.GetComponent<TutorialOverlayView>();
            if (_helpPage == null)
                _helpPage = _canvas.transform.Find("HelpPage")?.GetComponent<HelpPageView>();

            if (!_viewsInitialized && _overlay != null && _helpPage != null)
            {
                _viewsInitialized = true;
                _overlay.Hide();
                _helpPage.Hide();
                _helpPage.Initialize(CloseHelp, ResetTutorialProgress);
            }
        }

        private void RegisterStaticHelpButtons()
        {
            if (_canvas == null) return;
            Button[] buttons = _canvas.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == "HelpButton" || buttons[i].name == "Button_规则说明")
                    RegisterHelpButton(buttons[i]);
            }
        }

        private void ShowNext()
        {
            if (_current.HasValue || (_helpPage != null && _helpPage.IsVisible)) return;

            while (_pending.Count > 0)
            {
                TutorialTopic topic = _pending.Dequeue();
                _queued.Remove(topic);
                if (TutorialProgressService.IsCompleted(topic)) continue;

                TutorialEntry entry = TutorialContent.Find(topic);
                if (entry == null) continue;

                _current = topic;
                _overlay.Show(entry, TutorialProgressService.CompletedCount,
                    OpenCurrentDetails, CompleteCurrent, SkipAllTutorials);
                _overlay.transform.SetAsLastSibling();
                return;
            }
        }

        private void CompleteCurrent()
        {
            if (!_current.HasValue) return;
            TutorialProgressService.Complete(_current.Value);
            _current = null;
            if (_overlay != null) _overlay.Hide();
            ShowNext();
        }

        private void SkipAllTutorials()
        {
            TutorialProgressService.SkipAll();
            _pending.Clear();
            _queued.Clear();
            _current = null;
            if (_overlay != null) _overlay.Hide();
        }

        private void OpenCurrentDetails()
        {
            if (!_current.HasValue) return;
            EnsureViews();
            TutorialEntry entry = TutorialContent.Find(_current.Value);
            if (entry == null || _helpPage == null) return;
            _helpPage.Show(entry.HelpSection, entry, GameConfigProvider.TestToolsEnabled);
            _helpPage.transform.SetAsLastSibling();
        }

        private void CloseHelp()
        {
            if (_helpPage != null) _helpPage.Hide();
            if (_current.HasValue && _overlay != null)
            {
                _overlay.transform.SetAsLastSibling();
                return;
            }
            ShowNext();
        }

        private void ResetTutorialProgress()
        {
            TutorialProgressService.ResetProgress();
            _pending.Clear();
            _queued.Clear();
            _current = null;
            if (_overlay != null) _overlay.Hide();
            if (_helpPage != null)
            {
                _helpPage.Show(HelpSection.Overview, null, GameConfigProvider.TestToolsEnabled,
                    "基础引导进度已重置。关闭帮助页后，下一次进入对应机制时会重新显示。");
                _helpPage.transform.SetAsLastSibling();
            }
        }

        private void RefreshConfig()
        {
            if (_helpPage != null && _helpPage.IsVisible)
                _helpPage.RefreshTestControls(GameConfigProvider.TestToolsEnabled);
        }
    }
}
