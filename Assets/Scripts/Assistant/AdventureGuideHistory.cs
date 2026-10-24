using System.Collections.Generic;

namespace FingerDiceDuel.Assistant
{
    /// <summary>只翻阅已解锁的指引文字，不持有或回退战斗状态。</summary>
    public sealed class AdventureGuideHistory
    {
        public sealed class Page { public string Key, Title, Text; }
        private readonly List<Page> _pages = new List<Page>();
        public int Index { get; private set; } = -1;
        public int Count => _pages.Count;
        public bool CanPrevious => Index > 0;
        public bool CanNext => Index >= 0 && Index < Count - 1;
        public Page Current => Index >= 0 ? _pages[Index] : null;
        public void Clear() { _pages.Clear(); Index = -1; }
        public void Update(string key, string title, string text)
        {
            foreach (var page in _pages)
                if (page.Key == key) { page.Title = title; page.Text = text; return; }
            bool follows = Index == Count - 1;
            _pages.Add(new Page { Key = key, Title = title, Text = text });
            if (follows) Index = Count - 1;
        }
        public void Previous() { if (CanPrevious) Index--; }
        public void Next() { if (CanNext) Index++; }
        public void Latest() { Index = Count - 1; }
    }
}
