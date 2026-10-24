using System;
using FingerDiceDuel.Assistant;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// **冒险教学的引导面板**（自包含、纯代码构建，与勇气学院面板同一做法）。
    ///
    /// 三种形态共用一套构件：
    ///   · **开场剧情**：全屏暗化 + 旁白 + 「跟随指引」按钮（模态，挡住桌面点击）；
    ///   · **过程引导**：左侧小面板（老兵头像 + 旁白），不挡点击、不压手牌；
    ///   · **胜利结算**：全屏暗化 + 要点卡 + 「重打本关」/「返回章节」。
    /// </summary>
    public sealed class AdventureGuidePanel
    {
        private readonly Font _font;

        // 模态层（开场 / 胜利共用：暗化背景 + 内容面板）
        private RectTransform _modalRoot;
        private Text _modalTitle;
        private Text _modalBody;
        private Text _modalGuideName;
        private Image _modalAvatar;
        private Button _confirmButton;
        private Button _secondButton;
        private Text _confirmLabel;
        private Text _secondLabel;
        private Button _skipTutorialButton;
        private Action _skipTutorialAction;
        private Action _confirmAction;
        private Action _secondAction;

        // 过程引导层（左侧小面板）
        private RectTransform _guideRoot;
        private Text _guideName;
        private Text _guideText;
        private Image _guideAvatar;
        private readonly AdventureGuideHistory _history = new AdventureGuideHistory();
        private Button _previousGuide, _nextGuide, _currentGuide;
        private Text _guidePage;

        /// <summary>模态层是否可见（开场 / 胜利 —— 挡住桌面输入）。</summary>
        public bool IsModalVisible => _modalRoot != null && _modalRoot.gameObject.activeSelf;

        /// <summary>过程引导是否可见。</summary>
        public bool IsGuideVisible => _guideRoot != null && _guideRoot.gameObject.activeSelf;

        public AdventureGuidePanel(RectTransform parent, Font font)
        {
            _font = font;
            BuildGuide(parent);
            BuildModal(parent);
        }

        // ============================================================
        // 开场 / 胜利（模态）
        // ============================================================

        /// <summary>显示开场剧情：「跟随指引」进入教学；「稍后再来」走 onCancel（默认只关面板）。</summary>
        public void ShowIntro(string title, string introText, string avatarKey,
                              string guideName, Action onBegin, Action onCancel = null, Action onSkipTutorial = null, string beginLabel = "跟随指引")
        {
            if (_modalRoot == null) return;

            _modalTitle.text = title;
            _modalBody.text = FrontEndArt.WrapReadableText(_modalBody, introText, 630f);
            _modalGuideName.text = guideName;   // 头像下方的署名
            SetAvatar(_modalAvatar, avatarKey);

            _history.Clear();
            _confirmLabel.text = beginLabel;
            _secondLabel.text = "稍后再来";
            _skipTutorialButton.GetComponentInChildren<Text>().text = "已通关 · 自由练习";
            _skipTutorialAction = onSkipTutorial;
            _skipTutorialButton.gameObject.SetActive(onSkipTutorial != null);
            _confirmAction = onBegin;
            _secondAction = onCancel ?? (Action)HideModal;

            _confirmButton.gameObject.SetActive(true);
            _secondButton.gameObject.SetActive(true);
            _modalRoot.gameObject.SetActive(true);
            _modalRoot.SetAsLastSibling();
        }

        /// <summary>显示胜利要点卡。</summary>
        public void ShowVictory(string title, string victoryText, string avatarKey,
                                Action onReplay, Action onReturn, Action onNext = null, string nextLabel = "继续次战", string replayLabel = "重打本关")
        {
            if (_modalRoot == null) return;

            _modalTitle.text = title;
            _modalBody.text = FrontEndArt.WrapReadableText(_modalBody, victoryText, 630f);
            _modalGuideName.text = AdventureLevel1.GuideName;
            _skipTutorialButton.GetComponentInChildren<Text>().text = nextLabel;
            _skipTutorialButton.gameObject.SetActive(onNext != null);
            _skipTutorialAction = onNext;
            SetAvatar(_modalAvatar, avatarKey);

            _confirmLabel.text = replayLabel;
            _secondLabel.text = "返回章节";
            _confirmAction = onReplay;
            _secondAction = onReturn;

            _confirmButton.gameObject.SetActive(true);
            _secondButton.gameObject.SetActive(true);
            _modalRoot.gameObject.SetActive(true);
            _modalRoot.SetAsLastSibling();
        }

        /// <summary>关闭模态层（不影响过程引导）。</summary>
        public void HideModal()
        {
            if (_modalRoot != null) _modalRoot.gameObject.SetActive(false);
        }

        // ============================================================
        // 过程引导（左侧小面板）
        // ============================================================

        /// <summary>更新过程引导旁白；文案为空时隐藏。</summary>
        public void ShowNarration(string guideName, string narration, string avatarKey)
        {
            if (_guideRoot == null) return;
            if (string.IsNullOrEmpty(narration)) { _guideRoot.gameObject.SetActive(false); return; }

            _history.Update(guideName, guideName, narration);
            RefreshGuidePage();
            SetAvatar(_guideAvatar, avatarKey);
            _guideRoot.gameObject.SetActive(true);
        }

        private void RefreshGuidePage()
        {
            var page = _history.Current;
            if (page == null) return;
            _guideName.text = page.Title;
            _guideText.text = FrontEndArt.WrapReadableText(_guideText, page.Text, 278f);
            _previousGuide.interactable = _history.CanPrevious;
            _nextGuide.interactable = _history.CanNext;
            _currentGuide.interactable = _history.CanNext;
            _guidePage.text = "指引 " + (_history.Index + 1) + "/" + _history.Count;
        }

        public void HideNarration()
        {
            if (_guideRoot != null) _guideRoot.gameObject.SetActive(false);
        }

        public void HideAll()
        {
            HideModal();
            HideNarration();
        }

        // ============================================================
        // 构件
        // ============================================================

        private void SetAvatar(Image image, string avatarKey)
        {
            if (image == null) return;
            Sprite sprite = BattleArt.Get(avatarKey);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            image.gameObject.SetActive(true);
        }

        private void BuildModal(RectTransform parent)
        {
            // 暗化背景（拦截点击）
            var backdrop = new GameObject("AdventureModal", typeof(Image));
            backdrop.transform.SetParent(parent, false);
            _modalRoot = backdrop.GetComponent<RectTransform>();
            _modalRoot.anchorMin = Vector2.zero;
            _modalRoot.anchorMax = Vector2.one;
            _modalRoot.sizeDelta = Vector2.zero;
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);

            // 内容面板
            var panelGo = new GameObject("AdventureModalPanel", typeof(Image));
            panelGo.transform.SetParent(_modalRoot, false);
            var panel = panelGo.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(860f, 560f);
            BattleArt.Apply(panelGo.GetComponent<Image>(), "UI/panel_tooltip", true);

            // 标题
            _modalTitle = MakeText(panel, "Title", new Vector2(0f, 230f),
                                   new Vector2(700f, 44f), 26,
                                   TextAnchor.MiddleCenter, BattleArt.Gold);
            _modalTitle.font = UiFont.Title;

            // 头像（左上）+ 名字
            var avatarGo = new GameObject("Avatar", typeof(Image));
            avatarGo.transform.SetParent(panel, false);
            _modalAvatar = avatarGo.GetComponent<Image>();
            var avatarRect = avatarGo.GetComponent<RectTransform>();
            avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0.5f, 0.5f);
            avatarRect.pivot = new Vector2(0.5f, 0.5f);
            avatarRect.sizeDelta = new Vector2(96f, 96f);
            avatarRect.anchoredPosition = new Vector2(-330f, 120f);
            _modalAvatar.preserveAspect = true;

            _modalGuideName = MakeText(panel, "GuideName", new Vector2(-330f, 50f),
                                       new Vector2(150f, 30f), 18,
                                       TextAnchor.MiddleCenter, BattleArt.Gold);

            // 正文
            _modalBody = MakeText(panel, "Body", new Vector2(70f, 0f),
                                  new Vector2(630f, 270f), 20,
                                  TextAnchor.UpperLeft, Color.white);

            // 按钮（右下两枚）
            _confirmButton = MakeButton(panel, "Confirm", "跟随指引",
                                         new Vector2(-165f, -220f), OnConfirm);
            _secondButton = MakeButton(panel, "Second", "稍后再来",
                                        new Vector2(165f, -220f), OnSecond);
            _skipTutorialButton = MakeButton(panel, "SkipTutorial", "已通关 · 自由练习",
                new Vector2(0f, -164f), () => { var action = _skipTutorialAction; HideModal(); action?.Invoke(); });
            _skipTutorialButton.gameObject.SetActive(false);
            _confirmLabel = _confirmButton.GetComponentInChildren<Text>();
            _secondLabel = _secondButton.GetComponentInChildren<Text>();

            backdrop.SetActive(false);
        }

        private void OnConfirm()
        {
            Action action = _confirmAction;
            _confirmAction = null;
            HideModal();
            action?.Invoke();
        }

        private void OnSecond()
        {
            Action action = _secondAction;
            _secondAction = null;
            HideModal();
            action?.Invoke();
        }

        private void BuildGuide(RectTransform parent)
        {
            var go = new GameObject("AdventureGuide", typeof(Image));
            go.transform.SetParent(parent, false);
            _guideRoot = go.GetComponent<RectTransform>();
            _guideRoot.anchorMin = _guideRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _guideRoot.pivot = new Vector2(0.5f, 0.5f);
            _guideRoot.sizeDelta = new Vector2(320f, 470f);
            // 左侧：与"发动 / 不发动"按钮（右侧）对称，不压双方手牌
            _guideRoot.anchoredPosition = new Vector2(-750f, 50f);
            BattleArt.Apply(go.GetComponent<Image>(), "UI/panel_tooltip", true);
            go.GetComponent<Image>().raycastTarget = false;

            var avatarGo = new GameObject("Avatar", typeof(Image));
            avatarGo.transform.SetParent(_guideRoot, false);
            _guideAvatar = avatarGo.GetComponent<Image>();
            var avatarRect = avatarGo.GetComponent<RectTransform>();
            avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0.5f, 0.5f);
            avatarRect.pivot = new Vector2(0.5f, 0.5f);
            avatarRect.sizeDelta = new Vector2(72f, 72f);
            avatarRect.anchoredPosition = new Vector2(-108f, 156f);
            _guideAvatar.preserveAspect = true;

            _guideName = MakeText(_guideRoot, "GuideName", new Vector2(40f, 174f),
                                  new Vector2(160f, 50f), 18,
                                  TextAnchor.MiddleLeft, BattleArt.Gold);

            _guideText = MakeText(_guideRoot, "Narration", new Vector2(0f, -40f),
                                  new Vector2(278f, 292f), 19,
                                  TextAnchor.UpperLeft, Color.white);

            _previousGuide = MakeGuideButton("GuidePrevious", "上一页", new Vector2(-99, -197), () => { _history.Previous(); RefreshGuidePage(); });
            _nextGuide = MakeGuideButton("GuideNext", "下一页", new Vector2(0, -197), () => { _history.Next(); RefreshGuidePage(); });
            _currentGuide = MakeGuideButton("GuideCurrent", "回到当前", new Vector2(99, -197), () => { _history.Latest(); RefreshGuidePage(); });
            _guidePage = MakeText(_guideRoot, "GuidePage", new Vector2(0, -150), new Vector2(278, 24), 16, TextAnchor.MiddleCenter, BattleArt.Ink);
            _guideText.rectTransform.sizeDelta = new Vector2(278, 250);
            _guideText.rectTransform.anchoredPosition = new Vector2(0, -7);
            go.SetActive(false);
        }

        private Button MakeGuideButton(string name, string label, Vector2 position, Action action)
        {
            var button = MakeButton(_guideRoot, name, label, position, action);
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(92, 34);
            var text = button.GetComponentInChildren<Text>();
            text.rectTransform.sizeDelta = new Vector2(88, 30); text.fontSize = 16;
            return button;
        }

        private Button MakeButton(RectTransform parent, string name, string label,
                                  Vector2 position, Action onClick)
        {
            var go = new GameObject("Adventure" + name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, 52f);
            rect.anchoredPosition = position;

            go.GetComponent<Image>().color = new Color(0.22f, 0.28f, 0.38f);

            Text text = MakeText(rect, "Label", Vector2.zero,
                                 new Vector2(220f, 44f), 19,
                                 TextAnchor.MiddleCenter, Color.white);
            text.text = label;

            var btn = go.GetComponent<Button>();
            BattleArt.Button(btn);
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        private Text MakeText(RectTransform parent, string name, Vector2 position,
                              Vector2 size, int fontSize, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }
    }
}
