using System;
using System.Collections;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    internal static class MatchMarks
    {
        private static readonly Sprite[] Identities = new Sprite[8];
        private static readonly Sprite[] Alignments = new Sprite[6];

        public static Sprite Identity(IdentityRole role, string lordTitle = null)
        {
            int index;
            switch (role)
            {
                case IdentityRole.Lord: index = lordTitle == "明主" ? 1 : lordTitle == "霸王" ? 2 : lordTitle == "暴君" ? 3 : 0; break;
                case IdentityRole.Loyalist: index = 4; break;
                case IdentityRole.Rebel: index = 5; break;
                case IdentityRole.Traitor: index = 6; break;
                default: index = 7; break;
            }
            return Tile(Identities, "identity_marks", 4, index);
        }

        // The lower row is reserved for 善良 / 中立 / 混乱.
        public static Sprite Alignment(int index) => Tile(Alignments, "alignment_marks", 3, index);

        private static Sprite Tile(Sprite[] cache, string atlas, int columns, int index)
        {
            if (cache[index] != null) return cache[index];
            var texture = Resources.Load<Texture2D>(BattleArt.Root + "UI/" + atlas);
            if (texture == null) return null;
            float width = (float)texture.width / columns;
            float height = texture.height / 2f;
            float size = Mathf.Floor(Mathf.Min(width, height));
            float x = index % columns * width + (width - size) / 2;
            float y = (1 - index / columns) * height + (height - size) / 2;
            var sprite = Sprite.Create(texture, new Rect(x, y, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = atlas + "_" + index;
            return cache[index] = sprite;
        }
    }

    internal sealed class MatchDrawCards
    {
        public const int Count = 8;
        private readonly Button[] _buttons = new Button[Count];
        private readonly Image[] _marks = new Image[Count];
        private readonly Text[] _labels = new Text[Count];
        public RectTransform Root { get; }

        public MatchDrawCards(Transform parent, string name, Vector2 position, Action<int> onClick)
        {
            Root = FrontEndArt.Rect(parent, name, position, new Vector2(1440, 212));
            for (int i = 0; i < Count; i++)
            {
                int index = i;
                var card = FrontEndArt.Button(Root, "Card" + (i + 1), "", new Vector2((i - 3.5f) * 179, 0),
                    new Vector2(166, 212), "roster_tile", 24);
                _buttons[i] = card;
                var label = _labels[i] = card.GetComponentInChildren<Text>();
                label.font = UiFont.Bold;
                label.rectTransform.anchoredPosition = new Vector2(0, -58);
                label.rectTransform.sizeDelta = new Vector2(126, 54);
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 16;
                label.resizeTextMaxSize = 24;
                var mark = _marks[i] = FrontEndArt.Rect(card.transform, "Mark", new Vector2(0, 27), Vector2.one * 104)
                    .gameObject.AddComponent<Image>();
                mark.preserveAspect = true;
                mark.raycastTarget = false;
                card.onClick.AddListener(() => onClick(index));
            }
        }

        public void Back(int index, bool offered, bool clickable)
            => Set(index, MatchMarks.Identity(IdentityRole.None), offered ? "？" : "未发放", clickable, false);

        public void Face(int index, Sprite mark, string label, bool clickable, bool selected = false)
            => Set(index, mark, label, clickable, selected);

        private void Set(int index, Sprite mark, string label, bool clickable, bool selected)
        {
            var button = _buttons[index];
            FrontEndArt.StyleButton(button, "roster_tile", selected);
            button.interactable = clickable;
            if (selected && !clickable)
            {
                button.transition = Selectable.Transition.None;
                ((Image)button.targetGraphic).overrideSprite = null;
            }
            _marks[index].sprite = mark;
            _marks[index].enabled = mark != null;
            _marks[index].color = clickable || selected ? Color.white : new Color(.58f, .58f, .58f);
            _labels[index].text = label;
            _labels[index].color = clickable || selected ? FrontEndArt.Gold : BattleArt.Ink;
        }

        public IEnumerator Flip(int first, Action showFace, int count = 1)
        {
            const float duration = .16f;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                SetScale(Mathf.Lerp(1, .04f, t / duration));
                yield return null;
            }
            showFace();
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                SetScale(Mathf.Lerp(.04f, 1, t / duration));
                yield return null;
            }
            SetScale(1);

            void SetScale(float x)
            {
                for (int i = first; i < first + count; i++) _buttons[i].transform.localScale = new Vector3(x, 1, 1);
            }
        }

        public void ResetScale()
        {
            foreach (var card in _buttons) card.transform.localScale = Vector3.one;
        }
    }
}
