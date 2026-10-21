using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private Button _historyButton;
        private GameObject _historyDismiss;
        public bool IsHistoryOpen => _battleLog != null && _battleLog.gameObject.activeSelf;

        private void BuildHistoryToggle()
        {
            _historyButton = CreateButton(_canvasRoot, "HistoryButton", new Vector2(-30, -98), new Vector2(180, 56), "战斗记录", true);
            _historyButton.onClick.AddListener(() => SetHistoryOpen(!IsHistoryOpen));
            _historyDismiss = new GameObject("HistoryDismiss", typeof(Image), typeof(Button));
            _historyDismiss.transform.SetParent(_canvasRoot, false);
            Stretch((RectTransform)_historyDismiss.transform);
            _historyDismiss.GetComponent<Image>().color = new Color(0, 0, 0, 0.58f);
            var dismiss = _historyDismiss.GetComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(() => SetHistoryOpen(false));
            var close = CreateButton(_battleLog, "CloseHistory", new Vector2(168, 286), new Vector2(56, 40), "×");
            close.onClick.AddListener(() => SetHistoryOpen(false));
            SetHistoryOpen(false);
        }

        public void SetHistoryOpen(bool open)
        {
            if (_battleLog == null) return;
            _battleLog.gameObject.SetActive(open);
            if (_historyDismiss != null) _historyDismiss.SetActive(open);
            if (_historyButton != null) _historyButton.GetComponentInChildren<Text>().text = open ? "收起记录" : "战斗记录";
            if (open)
            {
                _historyDismiss?.transform.SetAsLastSibling();
                _battleLog.SetAsLastSibling();
                _exitButton.transform.SetAsLastSibling();
                _historyButton.transform.SetAsLastSibling();
                Canvas.ForceUpdateCanvases();
                _battleLogScroll.verticalNormalizedPosition = 0;
            }
        }
    }
}
