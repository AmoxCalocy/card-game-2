using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OneJourney.Core
{
    public sealed class FailurePageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _reasonText;
        [SerializeField] private TMP_Text _detailText;
        [SerializeField] private Button _startNewGameButton;

        public Button StartNewGameButton => _startNewGameButton;

        public void SetFailure(RunSession.SettlementSummary summary, UnityAction onStartNewGame)
        {
            _titleText.text = "旅途终结";
            _reasonText.text = string.IsNullOrEmpty(summary.Reason) ? "本次旅途失败" : summary.Reason;
            _detailText.text = "抵达 " + summary.RegionProgress
                + "  ·  用时 " + summary.ElapsedSeconds + " 秒"
                + "\n随机种子：" + summary.Seed;

            _startNewGameButton.onClick.RemoveAllListeners();
            _startNewGameButton.onClick.AddListener(onStartNewGame);
        }
    }
}
