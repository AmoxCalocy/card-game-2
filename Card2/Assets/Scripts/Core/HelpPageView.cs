using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OneJourney.Core
{
    public sealed class HelpPageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _contextText;
        [SerializeField] private TMP_Text _bodyText;
        [SerializeField] private RectTransform _contentRoot;
        [SerializeField] private Button _overviewButton;
        [SerializeField] private Button _cardTypeButton;
        [SerializeField] private Button _statusButton;
        [SerializeField] private Button _resourceButton;
        [SerializeField] private Button _iconButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _resetTutorialButton;

        private TutorialEntry _contextEntry;
        private string _statusMessage;
        private HelpSection _currentSection;
        private bool _initialized;

        public bool IsVisible => gameObject.activeSelf;

        public void Initialize(UnityAction onClose, UnityAction onResetTutorial)
        {
            if (_initialized) return;
            _initialized = true;

            BindSection(_overviewButton, HelpSection.Overview);
            BindSection(_cardTypeButton, HelpSection.CardTypes);
            BindSection(_statusButton, HelpSection.Statuses);
            BindSection(_resourceButton, HelpSection.Resources);
            BindSection(_iconButton, HelpSection.Icons);
            Bind(_closeButton, onClose);
            Bind(_resetTutorialButton, onResetTutorial);
        }

        public void Show(HelpSection section, TutorialEntry contextEntry, bool showResetButton, string statusMessage = null)
        {
            _contextEntry = contextEntry;
            _statusMessage = statusMessage;
            _currentSection = section;
            gameObject.SetActive(true);
            RefreshTestControls(showResetButton);
            Render();
        }

        public void RefreshTestControls(bool showResetButton)
        {
            if (_resetTutorialButton != null)
                _resetTutorialButton.gameObject.SetActive(showResetButton);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void SelectSection(HelpSection section)
        {
            _currentSection = section;
            _contextEntry = null;
            _statusMessage = null;
            Render();
        }

        private void Render()
        {
            HelpSectionEntry section = TutorialContent.Find(_currentSection);
            if (section == null) return;

            _titleText.text = "规则说明 · " + section.Title;
            _bodyText.text = section.Body;

            string context = null;
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                context = _statusMessage;
            }
            else if (_contextEntry != null)
            {
                context = "当前引导：" + _contextEntry.Title + "\n" + _contextEntry.Detail;
            }

            bool showContext = !string.IsNullOrEmpty(context);
            _contextText.gameObject.SetActive(showContext);
            if (showContext) _contextText.text = context;

            SetSelected(_overviewButton, _currentSection == HelpSection.Overview);
            SetSelected(_cardTypeButton, _currentSection == HelpSection.CardTypes);
            SetSelected(_statusButton, _currentSection == HelpSection.Statuses);
            SetSelected(_resourceButton, _currentSection == HelpSection.Resources);
            SetSelected(_iconButton, _currentSection == HelpSection.Icons);

            if (_contentRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);
        }

        private void BindSection(Button button, HelpSection section)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectSection(section));
        }

        private static void Bind(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(action);
        }

        private static void SetSelected(Button button, bool selected)
        {
            if (button != null) button.interactable = !selected;
        }
    }
}
