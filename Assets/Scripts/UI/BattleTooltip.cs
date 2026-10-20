using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>徽记、伤害与状态图标的说明；不拦截下方控件的射线。</summary>
    public sealed class BattleTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private string _body;
        private GameObject _popup;

        public static void Set(Image icon, string body)
        {
            var tooltip = icon.GetComponent<BattleTooltip>();
            if (tooltip == null) tooltip = icon.gameObject.AddComponent<BattleTooltip>();
            tooltip._body = body;
            icon.raycastTarget = true;
        }

        public void OnPointerEnter(PointerEventData data)
        {
            Hide();
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || string.IsNullOrEmpty(_body)) return;
            var root = canvas.transform as RectTransform;
            _popup = new GameObject("BattleTooltip", typeof(Image));
            _popup.transform.SetParent(root, false);
            var rect = _popup.GetComponent<RectTransform>();
            var image = _popup.GetComponent<Image>();
            BattleArt.Apply(image, "UI/panel_tooltip", true);
            image.raycastTarget = false;
            var textGo = new GameObject("Text", typeof(Text));
            textGo.transform.SetParent(rect, false);
            var text = textGo.GetComponent<Text>();
            text.font = UiFont.Regular;
            text.fontSize = 20;
            text.color = BattleArt.Gold;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = _body;
            text.rectTransform.sizeDelta = new Vector2(430, 240);
            float height = Mathf.Max(84, text.preferredHeight + 40);
            rect.sizeDelta = new Vector2(478, height);
            text.rectTransform.sizeDelta = new Vector2(430, height - 40);
            Vector2 point;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, data.position, data.enterEventCamera, out point);
            float x = Mathf.Clamp(point.x + 255, root.rect.xMin + 250, root.rect.xMax - 250);
            float y = Mathf.Clamp(point.y - height / 2 - 24, root.rect.yMin + height / 2 + 12,
                root.rect.yMax - height / 2 - 12);
            rect.localPosition = new Vector3(x, y, 0);
            _popup.transform.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData data) { Hide(); }
        private void OnDisable() { Hide(); }

        private void Hide()
        {
            if (_popup != null) Destroy(_popup);
            _popup = null;
        }
    }
}
