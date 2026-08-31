using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneJourney.Core
{
    public sealed class CampFacilityCardView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _iconInitial;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _detailText;
        [SerializeField] private Button _button;

        public Button Button => _button;

        public void SetContent(string title, string detail, bool disabled, Color color)
        {
            _background.color = disabled
                ? new Color(color.r * 0.68f, color.g * 0.68f, color.b * 0.68f, 1f)
                : color;
            _icon.color = disabled
                ? new Color(0.30f, 0.30f, 0.32f, 1f)
                : new Color(
                    Mathf.Min(1f, color.r + 0.12f),
                    Mathf.Min(1f, color.g + 0.12f),
                    Mathf.Min(1f, color.b + 0.12f),
                    1f);
            _iconInitial.text = string.IsNullOrEmpty(title) ? "?" : title.Substring(0, 1);
            _titleText.text = title;
            _detailText.text = detail;
            _button.interactable = !disabled;
            _button.onClick.RemoveAllListeners();

            Color textColor = disabled ? new Color(0.72f, 0.72f, 0.72f) : Color.white;
            _titleText.color = textColor;
            _detailText.color = textColor;
        }
    }
}
