using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.UI
{
    /// <summary>先选职业、再选子职；回调索引始终对应调用者传入的合法名单。</summary>
    public sealed class CharacterRosterView
    {
        private RectTransform _view, _scrollRoot, _content;
        private Text _heading;
        private Button _professionBack;
        private readonly List<Button> _cards = new List<Button>();
        private readonly List<CharacterDefinition> _roster = new List<CharacterDefinition>();
        private readonly List<CharacterProfession> _professions = new List<CharacterProfession>();
        private readonly List<int> _visibleIndices = new List<int>();
        private readonly List<GameObject> _halos = new List<GameObject>();
        private string _professionId;
        private int _selectedIndex = -1;
        private int _columns = 4;
        private int _visibleColumns;
        private float _rowHeight;
        private float _maxRowHeight = float.PositiveInfinity;
        private Vector2 _size = new Vector2(1120, 540);
        private Func<CharacterDefinition, string> _skillText;
        public event Action<int> SelectionChanged;
        public int SelectedIndex => _selectedIndex;
        public RectTransform Root => _view;
        public int Count => _roster.Count;
        public bool IsChoosingProfession => _professionId == null;
        public string ProfessionId => _professionId;
        public IReadOnlyList<CharacterProfession> Professions => _professions;
        public IReadOnlyList<int> VisibleIndices => _visibleIndices;

        public void Build(RectTransform parent, string name, Vector2 position,
            IReadOnlyList<CharacterDefinition> roster, Font font, int initialIndex = 0,
            Func<CharacterDefinition, string> skillText = null)
        {
            _skillText = skillText;
            _view = FrontEndArt.Rect(parent, name, position, _size);
            FrontEndArt.Apply(_view.gameObject.AddComponent<Image>(), "UI/panel_content");
            var scroll = FrontEndArt.Scroll(_view, "RosterScroll", Vector2.zero, Vector2.one, out _content);
            _scrollRoot = (RectTransform)scroll.transform;
            _heading = FrontEndArt.Text(_view, "RosterHeading", "选择职业大类", Vector2.zero, Vector2.zero, 24, title: true);
            _professionBack = FrontEndArt.Button(_view, "ProfessionBack", "‹ 返回职业", Vector2.zero, new Vector2(190, 44), fontSize: 22);
            _professionBack.onClick.AddListener(ShowProfessions);
            LayoutFrame();
            SetRoster(roster, initialIndex);
        }

        public void ConfigureGrid(Vector2 position, Vector2 size, int columns = 4, float maxRowHeight = float.PositiveInfinity)
        {
            _size = size;
            _columns = Mathf.Max(1, columns);
            _maxRowHeight = maxRowHeight;
            _view.anchoredPosition = position;
            LayoutFrame();
            RebuildCards();
        }

        private void LayoutFrame()
        {
            _view.sizeDelta = _size;
            _scrollRoot.anchoredPosition = new Vector2(0, -32);
            _scrollRoot.sizeDelta = new Vector2(_size.x - 16, _size.y - 76);
            var track = (RectTransform)_scrollRoot.Find("Scrollbar");
            track.anchoredPosition = new Vector2(_scrollRoot.sizeDelta.x / 2 - 8, 0);
            track.sizeDelta = new Vector2(12, _scrollRoot.sizeDelta.y - 16);
            _professionBack.GetComponent<RectTransform>().anchoredPosition = new Vector2(-_size.x / 2 + 110, _size.y / 2 - 32);
            _heading.rectTransform.anchoredPosition = new Vector2(IsChoosingProfession ? 0 : 102, _size.y / 2 - 32);
            _heading.rectTransform.sizeDelta = new Vector2(_size.x - (IsChoosingProfession ? 36 : 250), 48);
        }

        public void SetRoster(IReadOnlyList<CharacterDefinition> roster, int selectIndex = 0)
        {
            if (_content == null || roster == null) return;
            bool same = _roster.Count == roster.Count;
            if (same) for (int i = 0; i < roster.Count; i++)
                if (!ReferenceEquals(_roster[i], roster[i])) { same = false; break; }
            // 同名单的详情刷新保留所在职业；双将等阶段换名单则重新先选职业。
            var copy = new List<CharacterDefinition>(roster);
            _roster.Clear(); _roster.AddRange(copy);
            _selectedIndex = selectIndex >= 0 && selectIndex < _roster.Count ? selectIndex : -1;
            _professions.Clear();
            foreach (var profession in CharacterProfession.All)
                if (CountFor(profession.Id) > 0) _professions.Add(profession);
            if (!same || CountFor(_professionId) == 0) _professionId = null;
            RebuildCards();
        }

        private int CountFor(string id)
        {
            if (id == null) return 0;
            int count = 0;
            foreach (var def in _roster) if (def.Profession?.Id == id) count++;
            return count;
        }

        public void ShowProfessions()
        {
            _professionId = null;
            RebuildCards();
        }

        public bool SelectProfession(string id)
        {
            if (CountFor(id) == 0) return false;
            _professionId = id;
            RebuildCards();
            return true;
        }

        private void RebuildCards()
        {
            if (_content == null) return;
            foreach (var card in _cards)
            {
                card.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(card.gameObject);
            }
            _cards.Clear(); _halos.Clear(); _visibleIndices.Clear();
            bool professions = IsChoosingProfession;
            for (int i = 0; i < _roster.Count; i++)
                if (!professions && _roster[i].Profession?.Id == _professionId) _visibleIndices.Add(i);
            int count = professions ? _professions.Count : _visibleIndices.Count;
            int columns = professions ? Mathf.Min(4, Mathf.Max(1, count)) : _columns;
            _visibleColumns = columns;
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
            Vector2 viewport = _scrollRoot.sizeDelta;
            float width = (viewport.x - 44) / columns;
            if (professions) width = Mathf.Min(330, width);
            float height = professions ? Mathf.Min(240, Mathf.Max(110, (viewport.y - 36) / rows))
                : Mathf.Min(_maxRowHeight, Mathf.Max(220, width * 0.94f));
            if (!professions && rows <= 2) height = Mathf.Min(height, Mathf.Max(160, (viewport.y - 36) / rows));
            _rowHeight = height;
            _content.sizeDelta = new Vector2(viewport.x - 24, Mathf.Max(viewport.y, rows * height + 36));
            _content.anchoredPosition = Vector2.zero;
            float left = (_content.sizeDelta.x - columns * width) / 2;
            for (int i = 0; i < count; i++)
            {
                var button = FrontEndArt.Button(_content, professions ? "Profession_" + _professions[i].Id : "Card" + _visibleIndices[i], "",
                    new Vector2(left + (i % columns + 0.5f) * width, -18 - (i / columns + 0.5f) * height),
                    new Vector2(width - 6, height - 4), "roster_tile");
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                if (professions)
                {
                    var profession = _professions[i];
                    FrontEndArt.RoleTile(button, null, profession.NameZh + " · " + CountFor(profession.Id), 26, profession.IconKey);
                    BattleTooltip.Set(button.GetComponent<Image>(), profession.NameZh + "  " + profession.NameEn + "\n点击选择子职 · 当前可选" + CountFor(profession.Id) + "名");
                    string id = profession.Id;
                    button.onClick.AddListener(() => SelectProfession(id));
                }
                else
                {
                    int index = _visibleIndices[i];
                    var def = _roster[index];
                    FrontEndArt.RoleTile(button, def.Id, def.SubclassNameZh, 26);
                    string hint = _skillText != null ? _skillText(def) : Shorten(def.Description);
                    BattleTooltip.Set(button.GetComponent<Image>(), def.DisplayName + "　" + DescribeStats(def) + "\n" + hint);
                    // 显式点击始终确认，包含身份模式尚未确认的默认首项。
                    button.onClick.AddListener(() => { Select(index, false); SelectionChanged?.Invoke(index); });
                }
                _halos.Add(rect.Find("SelectionHalo").gameObject);
                _cards.Add(button);
            }
            _professionBack.gameObject.SetActive(!professions);
            _heading.text = count == 0 ? "当前没有可选子职" : professions ? "选择职业大类" : CharacterProfession.Find(_professionId).NameZh + " · 选择子职";
            LayoutFrame();
            ApplySelection();
            ScrollToSelected();
        }

        public void Select(int index, bool notify = true)
        {
            if (index < 0 || index >= _roster.Count) return;
            bool changed = _selectedIndex != index;
            _selectedIndex = index;
            ApplySelection();
            if (notify && changed) SelectionChanged?.Invoke(index);
        }

        private void ApplySelection()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                bool selected = !IsChoosingProfession && _visibleIndices[i] == _selectedIndex;
                FrontEndArt.StyleButton(_cards[i], "roster_tile", selected);
                _halos[i].SetActive(selected);
            }
        }

        public void ScrollToSelected()
        {
            if (_view == null || IsChoosingProfession || _selectedIndex < 0) return;
            int visible = _visibleIndices.IndexOf(_selectedIndex);
            if (visible < 0) return;
            var scroll = _scrollRoot.GetComponent<ScrollRect>();
            float overflow = _content.sizeDelta.y - _scrollRoot.sizeDelta.y;
            if (overflow <= 0) { scroll.verticalNormalizedPosition = 1; return; }
            float top = visible / _visibleColumns * _rowHeight + 18;
            float offset = _content.anchoredPosition.y;
            const float edgePadding = 18;
            if (top < offset + edgePadding) offset = Mathf.Max(0, top - edgePadding);
            else if (top + _rowHeight > offset + _scrollRoot.sizeDelta.y - edgePadding)
                offset = top + _rowHeight - _scrollRoot.sizeDelta.y + edgePadding;
            scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(offset / overflow);
        }

        public static string DescribeStats(CharacterDefinition def) => CharacterCardText.DescribeStats(def);
        public static string Shorten(string description, int maxChars = 26) => CharacterCardText.Shorten(description, maxChars);
    }
}
