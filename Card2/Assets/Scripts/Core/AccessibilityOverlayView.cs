using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OneJourney.Core
{
    /// <summary>A3-27：全局焦点说明、快捷键提示和安全确认对话框。</summary>
    public sealed class AccessibilityOverlayView : MonoBehaviour
    {
        [SerializeField] private RectTransform _hintPanel;
        [SerializeField] private TMP_Text _focusText;
        [SerializeField] private TMP_Text _shortcutText;
        [SerializeField] private GameObject _confirmationRoot;
        [SerializeField] private TMP_Text _confirmationTitle;
        [SerializeField] private TMP_Text _confirmationDetail;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TMP_Text _confirmButtonText;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TMP_Text _cancelButtonText;

        public GameObject ConfirmationRoot => _confirmationRoot;
        public Button ConfirmButton => _confirmButton;
        public Button CancelButton => _cancelButton;
        public bool ConfirmationVisible => _confirmationRoot != null && _confirmationRoot.activeSelf;

        public void SetHintPlacement(bool topCenter)
        {
            _ = topCenter;
            if (_hintPanel != null) _hintPanel.gameObject.SetActive(false);
        }

        public void SetHints(string focus, string shortcuts)
        {
            _ = focus;
            _ = shortcuts;
            if (_hintPanel != null) _hintPanel.gameObject.SetActive(false);
            if (_focusText != null)
            {
                _focusText.text = string.Empty;
                _focusText.gameObject.SetActive(false);
            }
            if (_shortcutText != null)
            {
                _shortcutText.text = string.Empty;
                _shortcutText.gameObject.SetActive(false);
            }
        }

        public void ShowConfirmation(string title, string detail, string confirmLabel, string cancelLabel,
            UnityAction onConfirm, UnityAction onCancel)
        {
            if (_confirmationRoot == null) return;
            if (_confirmationTitle != null) _confirmationTitle.text = title ?? "请确认";
            if (_confirmationDetail != null) _confirmationDetail.text = detail ?? string.Empty;
            if (_confirmButtonText != null) _confirmButtonText.text = string.IsNullOrEmpty(confirmLabel) ? "确认" : confirmLabel;
            if (_cancelButtonText != null) _cancelButtonText.text = string.IsNullOrEmpty(cancelLabel) ? "取消" : cancelLabel;

            Bind(_confirmButton, onConfirm);
            Bind(_cancelButton, onCancel);
            _confirmationRoot.SetActive(true);
        }

        public void HideConfirmation()
        {
            if (_confirmationRoot != null) _confirmationRoot.SetActive(false);
        }

        private static void Bind(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(action);
        }
    }
}
