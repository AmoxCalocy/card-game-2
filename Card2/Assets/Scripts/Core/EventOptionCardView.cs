using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneJourney.Core
{
    public sealed class EventOptionCardView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _portraitInitial;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _conditionText;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private TMP_Text _lockText;
        [SerializeField] private Button _button;

        public Button Button => _button;

        public void SetContent(string badge, string title, string condition, string result,
            string blockReason, Color color)
        {
            bool blocked = !string.IsNullOrEmpty(blockReason);
            _background.color = blocked
                ? new Color(color.r * 0.62f, color.g * 0.62f, color.b * 0.62f, 1f)
                : color;
            _portrait.color = blocked
                ? new Color(0.27f, 0.27f, 0.29f, 1f)
                : new Color(
                    Mathf.Min(1f, color.r + 0.14f),
                    Mathf.Min(1f, color.g + 0.14f),
                    Mathf.Min(1f, color.b + 0.14f),
                    1f);
            _portraitInitial.text = string.IsNullOrEmpty(badge) ? "?" : badge.Substring(0, 1);
            _titleText.text = title;
            _conditionText.text = condition;
            _resultText.text = result;
            _lockText.gameObject.SetActive(blocked);
            _lockText.text = blocked ? "锁定：" + blockReason : string.Empty;
            _button.interactable = !blocked;
            _button.onClick.RemoveAllListeners();

            Color textColor = blocked ? new Color(0.70f, 0.70f, 0.70f) : Color.white;
            _titleText.color = textColor;
            _conditionText.color = textColor;
            _resultText.color = textColor;
        }
    }
}
