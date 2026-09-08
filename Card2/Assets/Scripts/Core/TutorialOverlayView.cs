using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OneJourney.Core
{
    public sealed class TutorialOverlayView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _summaryText;
        [SerializeField] private Button _detailButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _skipButton;

        public bool IsVisible => gameObject.activeSelf;

        public void Show(TutorialEntry entry, int completedCount, UnityAction onDetail,
            UnityAction onContinue, UnityAction onSkip)
        {
            if (entry == null) return;

            _progressText.text = "基础引导  " + (completedCount + 1) + "/" + TutorialContent.TutorialCount;
            _titleText.text = entry.Title;
            _summaryText.text = entry.Summary;

            Bind(_detailButton, onDetail);
            Bind(_continueButton, onContinue);
            Bind(_skipButton, onSkip);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private static void Bind(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(action);
        }
    }
}
