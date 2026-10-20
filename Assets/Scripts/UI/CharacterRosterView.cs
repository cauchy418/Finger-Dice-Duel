using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.UI
{
    /// <summary>绘画徽记选将网格；名单替换与选择事件仍由原来的选将状态机管理。</summary>
    public sealed class CharacterRosterView
    {
        private RectTransform _view, _content;
        private readonly List<Button> _cards = new List<Button>();
        private readonly List<CharacterDefinition> _roster = new List<CharacterDefinition>();
        private readonly List<GameObject> _halos = new List<GameObject>();
        private int _selectedIndex = -1;
        private int _columns = 6;
        private Vector2 _size = new Vector2(1120, 540);
        private Func<CharacterDefinition, string> _skillText;
        public event Action<int> SelectionChanged;
        public int SelectedIndex => _selectedIndex;
        public RectTransform Root => _view;
        public int Count => _cards.Count;

        public void Build(RectTransform parent, string name, Vector2 position,
            IReadOnlyList<CharacterDefinition> roster, Font font, int initialIndex = 0,
            Func<CharacterDefinition, string> skillText = null)
        {
            _skillText = skillText;
            var scroll = FrontEndArt.Scroll(parent, name, position, _size, out _content);
            _view = scroll.transform as RectTransform;
            FrontEndArt.Apply(_view.GetComponent<Image>(), "UI/panel_content");
            SetRoster(roster, initialIndex);
        }

        public void ConfigureGrid(Vector2 position, Vector2 size, int columns = 6)
        {
            _size = size;
            _columns = columns;
            _view.anchoredPosition = position;
            _view.sizeDelta = size;
            Transform track = _view.Find("Scrollbar");
            ((RectTransform)track).anchoredPosition = new Vector2(size.x / 2 - 8, 0);
            ((RectTransform)track).sizeDelta = new Vector2(12, size.y - 32);
            var copy = _roster.ToArray();
            SetRoster(copy, _selectedIndex);
        }

        public void SetRoster(IReadOnlyList<CharacterDefinition> roster, int selectIndex = 0)
        {
            if (_content == null || roster == null) return;
            foreach (var card in _cards)
            {
                card.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(card.gameObject);
            }
            _cards.Clear();
            _halos.Clear();
            _roster.Clear();
            for (int i = 0; i < roster.Count; i++) _roster.Add(roster[i]);
            float width = (_size.x - 44) / _columns;
            float height = (_size.y - 36) / 4;
            _content.sizeDelta = new Vector2(_size.x - 24, Mathf.Max(_size.y, Mathf.CeilToInt(roster.Count / (float)_columns) * height + 36));
            _content.anchoredPosition = Vector2.zero;
            for (int i = 0; i < roster.Count; i++)
            {
                CharacterDefinition def = roster[i];
                var button = FrontEndArt.Button(_content, "Card" + i, "",
                    new Vector2(20 + (i % _columns + 0.5f) * width, -18 - (i / _columns + 0.5f) * height),
                    new Vector2(width - 6, height - 4), "roster_tile");
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                float iconSize = Mathf.Min(width - 12, height - 32);
                var halo = FrontEndArt.Image(rect, "SelectionHalo", "UI/selection_halo",
                    new Vector2(0, 12), new Vector2(iconSize + 12, iconSize + 12), false);
                _halos.Add(halo.gameObject);
                FrontEndArt.Emblem(rect, "Emblem", def.Id, new Vector2(0, 12), new Vector2(iconSize, iconSize));
                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.anchoredPosition = new Vector2(0, -height / 2 + 18);
                label.rectTransform.sizeDelta = new Vector2(width - 12, 28);
                label.fontSize = 18;
                label.text = def.DisplayName;
                var namePlate = FrontEndArt.Rect(rect, "NamePlate", label.rectTransform.anchoredPosition, new Vector2(width - 18, 28)).gameObject.AddComponent<Image>();
                namePlate.color = new Color(0.025f, 0.04f, 0.05f, 0.96f);
                namePlate.raycastTarget = false;
                label.transform.SetAsLastSibling();
                string hint = _skillText != null ? _skillText(def) : Shorten(def.Description);
                BattleTooltip.Set(button.GetComponent<Image>(), def.DisplayName + "　" + DescribeStats(def) + "\n" + hint);
                int captured = i;
                button.onClick.AddListener(() => Select(captured));
                _cards.Add(button);
            }
            _selectedIndex = -1;
            if (selectIndex >= 0 && selectIndex < _cards.Count) Select(selectIndex, false);
            else for (int i = 0; i < _halos.Count; i++) _halos[i].SetActive(false);
        }

        public void Select(int index, bool notify = true)
        {
            if (index < 0 || index >= _cards.Count) return;
            bool changed = _selectedIndex != index;
            _selectedIndex = index;
            for (int i = 0; i < _cards.Count; i++)
            {
                FrontEndArt.StyleButton(_cards[i], "roster_tile", i == index);
                _halos[i].SetActive(i == index);
            }
            if (notify && changed) SelectionChanged?.Invoke(index);
        }

        public void ScrollToSelected()
        {
            if (_view == null || _selectedIndex < 0) return;
            var scroll = _view.GetComponent<ScrollRect>();
            float overflow = _content.sizeDelta.y - _size.y;
            if (overflow <= 0) { scroll.verticalNormalizedPosition = 1; return; }
            float height = (_size.y - 36) / 4;
            scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01((_selectedIndex / _columns * height - _size.y / 2 + height / 2) / overflow);
        }

        public static string DescribeStats(CharacterDefinition def) => CharacterCardText.DescribeStats(def);
        public static string Shorten(string description, int maxChars = 26) => CharacterCardText.Shorten(description, maxChars);
    }
}
