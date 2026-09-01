using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneJourney.Core
{
    public enum MapNodeVisualState
    {
        Future,
        Reachable,
        Current,
        Visited
    }

    public sealed class MapNodeView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _iconBackground;
        [SerializeField] private TMP_Text _iconText;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _layerText;
        [SerializeField] private TMP_Text _stateText;
        [SerializeField] private Outline _outline;
        [SerializeField] private Button _button;

        private MapNodeVisualState _state;
        private bool _selected;

        public Button Button => _button;
        public RectTransform RectTransform => (RectTransform)transform;

        public void SetContent(string icon, string title, string layer, MapNodeVisualState state)
        {
            _iconText.text = icon;
            _titleText.text = title;
            _layerText.text = layer;
            _state = state;
            _selected = false;
            _button.interactable = state == MapNodeVisualState.Reachable;
            _button.onClick.RemoveAllListeners();
            ApplyVisual();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected && _state == MapNodeVisualState.Reachable;
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            Color background;
            Color icon;
            Color text;
            Color outline;
            string stateText;
            bool showOutline;

            switch (_state)
            {
                case MapNodeVisualState.Current:
                    background = new Color(0.23f, 0.38f, 0.28f, 1f);
                    icon = new Color(0.68f, 0.54f, 0.22f, 1f);
                    text = Color.white;
                    outline = new Color(0.92f, 0.70f, 0.30f, 1f);
                    stateText = "当前位置";
                    showOutline = true;
                    break;
                case MapNodeVisualState.Reachable:
                    background = new Color(0.20f, 0.31f, 0.43f, 1f);
                    icon = new Color(0.74f, 0.49f, 0.18f, 1f);
                    text = Color.white;
                    outline = new Color(0.92f, 0.66f, 0.25f, 1f);
                    stateText = "可前往";
                    showOutline = true;
                    break;
                case MapNodeVisualState.Visited:
                    background = new Color(0.24f, 0.24f, 0.22f, 1f);
                    icon = new Color(0.43f, 0.38f, 0.28f, 1f);
                    text = new Color(0.78f, 0.76f, 0.70f, 1f);
                    outline = new Color(0.48f, 0.42f, 0.30f, 1f);
                    stateText = "已经过";
                    showOutline = false;
                    break;
                default:
                    background = new Color(0.12f, 0.15f, 0.20f, 1f);
                    icon = new Color(0.24f, 0.27f, 0.32f, 1f);
                    text = new Color(0.54f, 0.57f, 0.62f, 1f);
                    outline = new Color(0.30f, 0.33f, 0.38f, 1f);
                    stateText = "未开放";
                    showOutline = false;
                    break;
            }

            if (_selected)
            {
                background = new Color(0.33f, 0.38f, 0.47f, 1f);
                icon = new Color(0.95f, 0.66f, 0.22f, 1f);
                outline = new Color(1f, 0.76f, 0.30f, 1f);
                stateText = "再次点击前往";
                showOutline = true;
            }

            _background.color = background;
            _iconBackground.color = icon;
            _titleText.color = text;
            _layerText.color = text;
            _stateText.color = text;
            _stateText.text = stateText;
            _outline.effectColor = outline;
            _outline.enabled = showOutline;
        }
    }
}
