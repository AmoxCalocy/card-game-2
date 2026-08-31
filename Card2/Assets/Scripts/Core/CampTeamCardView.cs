using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OneJourney.Core
{
    public sealed class CampTeamCardView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _portraitInitial;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _detailText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private GameObject _actionsRoot;
        [SerializeField] private Button _primaryActionButton;
        [SerializeField] private TMP_Text _primaryActionText;
        [SerializeField] private Button _secondaryActionButton;
        [SerializeField] private TMP_Text _secondaryActionText;

        public void SetContent(string displayName, string detail, bool alive, int fatigue, int disease)
        {
            _background.color = alive ? new Color(0.24f, 0.31f, 0.40f) : new Color(0.22f, 0.22f, 0.24f);
            _portrait.color = alive ? new Color(0.38f, 0.46f, 0.56f) : new Color(0.30f, 0.30f, 0.32f);
            _portraitInitial.text = string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1);
            _nameText.text = displayName;
            _detailText.text = detail;
            _statusText.text = "疲劳 " + fatigue + " / 疾病 " + disease;

            Color textColor = alive ? Color.white : new Color(0.62f, 0.62f, 0.62f);
            _nameText.color = textColor;
            _detailText.color = textColor;
            _statusText.color = textColor;

            HideAction(_primaryActionButton);
            HideAction(_secondaryActionButton);
            _actionsRoot.SetActive(false);
        }

        public void SetPrimaryAction(string label, Color color, UnityAction action)
        {
            ShowAction(_primaryActionButton, _primaryActionText, label, color, action);
        }

        public void SetSecondaryAction(string label, Color color, UnityAction action)
        {
            ShowAction(_secondaryActionButton, _secondaryActionText, label, color, action);
        }

        private void ShowAction(Button button, TMP_Text labelText, string label, Color color, UnityAction action)
        {
            _actionsRoot.SetActive(true);
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            labelText.text = label;
            var image = button.targetGraphic as Image;
            if (image != null) image.color = color;
        }

        private static void HideAction(Button button)
        {
            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(false);
        }
    }
}
