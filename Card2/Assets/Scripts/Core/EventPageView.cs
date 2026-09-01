using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneJourney.Core
{
    public sealed class EventPageView : MonoBehaviour
    {
        [SerializeField] private Image _illustration;
        [SerializeField] private TMP_Text _illustrationInitial;
        [SerializeField] private TMP_Text _metaText;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _resourceText;
        [SerializeField] private TMP_Text _promptText;
        [SerializeField] private TMP_Text _optionTitleText;
        [SerializeField] private Transform _optionList;
        [SerializeField] private EventOptionCardView _optionCardPrefab;

        public void SetEvent(EventDef evt, string resources, string prompt)
        {
            _titleText.text = evt.DisplayName;
            _descriptionText.text = evt.Description;
            _resourceText.text = resources;
            _promptText.text = string.IsNullOrEmpty(prompt) ? "选择一个行动。" : prompt;
            _illustrationInitial.text = string.IsNullOrEmpty(evt.DisplayName) ? "事" : evt.DisplayName.Substring(0, 1);
            _metaText.text = RegionName(evt.Region) + " · " + CategoryName(evt.Category) + " · " + evt.Id;
            _optionTitleText.text = "可选行动";

            switch (evt.Category)
            {
                case EventCategory.Disaster:
                    _illustration.color = new Color(0.34f, 0.19f, 0.18f, 1f);
                    break;
                case EventCategory.Social:
                    _illustration.color = new Color(0.20f, 0.31f, 0.28f, 1f);
                    break;
                default:
                    _illustration.color = new Color(0.22f, 0.27f, 0.36f, 1f);
                    break;
            }
        }

        public void SetOptionTitle(string title)
        {
            _optionTitleText.text = title;
        }

        public void ClearOptions()
        {
            for (int i = _optionList.childCount - 1; i >= 0; i--)
            {
                var child = _optionList.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        public EventOptionCardView AddOption(string badge, string title, string condition,
            string result, string blockReason, Color color)
        {
            var view = Instantiate(_optionCardPrefab, _optionList);
            view.name = "EventDynamic_Option_" + title;
            view.SetContent(badge, title, condition, result, blockReason, color);
            return view;
        }

        public void RebuildLayout()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_optionList);
        }

        private static string RegionName(ContentRegion region)
        {
            return region == ContentRegion.Jungle ? "密林" : "草原";
        }

        private static string CategoryName(EventCategory category)
        {
            switch (category)
            {
                case EventCategory.Disaster: return "灾害";
                case EventCategory.Social: return "社交";
                default: return "遭遇";
            }
        }
    }
}
