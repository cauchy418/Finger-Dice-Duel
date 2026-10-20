using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>1v1 的绘制素材与布局；环形模式继续使用原有手牌与坐标。</summary>
    public sealed partial class GameBoardUI
    {
        private bool _paintedBattle = true;
        private Image _battleBackground;
        private RectTransform _battleHint, _battleLog;
        private Text _logHeading, _battleSubtitle, _healthTextA, _healthTextB;
        private Image _badgeA, _badgeB;
        private ScrollRect _battleLogScroll;
        private PlayerState _battleChoiceOwner;

        private static void Position(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private Image ArtImage(RectTransform parent, string name, string key, Vector2 position,
                               Vector2 size, bool sliced = false)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            Position(image.rectTransform, position, size);
            BattleArt.Apply(image, key, sliced);
            image.raycastTarget = false;
            return image;
        }

        private void BuildPaintedPlayerArea(bool top, out RectTransform area, out HandCard[] cards,
                                            out Text name, out Text status)
        {
            area = CreatePanel(_canvasRoot, top ? "AreaB" : "AreaA",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(1000, 430), Color.clear, new Vector2(-80, top ? 285 : -315));
            area.GetComponent<Image>().raycastTarget = false;
            var badge = ArtImage(area, "ClassEmblem", "Classes/soldier",
                new Vector2(-230, 181), new Vector2(112, 112));
            badge.preserveAspect = true;
            ArtImage(area, "NamePlate", "UI/label_strip", new Vector2(70, 195), new Vector2(460, 44), true);
            name = CreateText(area, "Name", new Vector2(70, 195), new Vector2(430, 38),
                28, TextAnchor.MiddleCenter, BattleArt.Gold);
            name.font = UiFont.Title;
            name.fontStyle = FontStyle.Normal;
            status = CreateText(area, "Stats", new Vector2(70, 119), new Vector2(600, 30),
                20, TextAnchor.MiddleCenter, BattleArt.Ink);
            var track = ArtImage(area, "HpBar", "UI/health_track", new Vector2(70, 155), new Vector2(360, 34), true);
            var fill = ArtImage(track.rectTransform, "Fill", "UI/health_fill", Vector2.zero, new Vector2(336, 18));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            var hp = CreateText(track.rectTransform, "Health", Vector2.zero, new Vector2(300, 30),
                18, TextAnchor.MiddleCenter, Color.white);
            UiFont.ApplyBold(hp);
            cards = new[]
            {
                CreatePaintedHandCard(area, new Vector2(-120, -45)),
                CreatePaintedHandCard(area, new Vector2(120, -45))
            };
            var statuses = new StatusIconBar(area, _font, top, top ? "StatusIconsB" : "StatusIconsA");
            statuses.SetBattleLayout(new Vector2(-300, 121));
            if (top)
            {
                _badgeB = badge; _healthTextB = hp; _hpBarFillB = fill;
                _nameRowB = name.rectTransform; _statusIconsB = statuses;
            }
            else
            {
                _badgeA = badge; _healthTextA = hp; _hpBarFillA = fill;
                _nameRowA = name.rectTransform; _statusIconsA = statuses;
            }
        }

        private HandCard CreatePaintedHandCard(RectTransform parent, Vector2 position)
        {
            var go = new GameObject("PaintedHandCard", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Position(rect, position, new Vector2(210, 304));
            var card = new HandCard { Button = go.GetComponent<Button>(), Background = go.GetComponent<Image>() };
            BattleArt.Apply(card.Background, "UI/hand_plate");
            card.Button.targetGraphic = card.Background;
            card.Button.transition = Selectable.Transition.None;
            var navigation = card.Button.navigation;
            navigation.mode = Navigation.Mode.None;
            card.Button.navigation = navigation;
            card.Gesture = ArtImage(rect, "Gesture", "Hands/hand_left_1", new Vector2(0, 35), new Vector2(185, 231.25f));
            card.Gesture.preserveAspect = true;
            card.Outline = ArtImage(rect, "Frame", "UI/hand_frame_normal", Vector2.zero, rect.sizeDelta, true);
            ArtImage(rect, "NumberPlate", "UI/label_strip", new Vector2(0, -118), new Vector2(168, 34), true);
            card.ValueText = CreateText(rect, "Value", new Vector2(-29, -118), new Vector2(52, 34),
                28, TextAnchor.MiddleCenter, BattleArt.Gold);
            UiFont.ApplyBold(card.ValueText);
            card.NameText = CreateText(rect, "Side", new Vector2(31, -118), new Vector2(70, 30),
                18, TextAnchor.MiddleCenter, BattleArt.Ink);
            card.StatusText = CreateText(rect, "Status", new Vector2(0, -86), new Vector2(196, 18),
                16, TextAnchor.MiddleCenter, BattleArt.Ink);
            card.StateIcon = ArtImage(rect, "HandState", "Overlay/shield", new Vector2(-72, -64), new Vector2(38, 38));
            card.LockIcon = ArtImage(rect, "Locked", "Overlay/locked", new Vector2(72, -64), new Vector2(38, 38));
            card.StateIcon.enabled = card.LockIcon.enabled = false;
            card.Flash = ArtImage(rect, "Flash", "UI/hand_plate", Vector2.zero, rect.sizeDelta);
            card.Flash.color = new Color(1, 1, 1, 0);
            card.Button.onClick.AddListener(() =>
            {
                if (card.Hand != null) HandClicked?.Invoke(card.Owner, card.Hand);
            });
            return card;
        }

        private static void RefreshPaintedHand(HandCard card, Hand hand, HandVisual.OutlineState outline)
        {
            bool left = ReferenceEquals(hand, card.Owner.LeftHand);
            card.Gesture.sprite = BattleArt.Get("Hands/hand_" + (left ? "left_" : "right_") + hand.Value);
            card.Gesture.color = hand.IsConcealed ? new Color(0.65f, 0.80f, 0.80f) : Color.white;
            string frame = outline == HandVisual.OutlineState.Highlighted ? "selected"
                : outline == HandVisual.OutlineState.Available ? "available"
                : hand.IsLocked || hand.IsConcealed ? "disabled" : "normal";
            BattleArt.Apply(card.Outline, "UI/hand_frame_" + frame, true);
            card.Outline.enabled = true;
            card.Background.color = Color.white;
            string state = hand.IsShield ? "shield" : hand.State == ShieldState.Broken ? "shield_broken"
                : hand.IsTaichi ? "taichi" : hand.IsConcealed ? "concealed" : null;
            card.StateIcon.enabled = state != null;
            if (state != null) card.StateIcon.sprite = BattleArt.Get("Overlay/" + state);
            card.LockIcon.enabled = hand.IsLocked;
        }

        private void BuildPaintedLog()
        {
            _battleLog = CreatePanel(_canvasRoot, "LogPanel", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(460, 670), Color.white, new Vector2(670, -10));
            BattleArt.Apply(_battleLog.GetComponent<Image>(), "UI/panel_log", true);
            _logHeading = CreateText(_battleLog, "LogHeading", new Vector2(0, 286), new Vector2(390, 40),
                28, TextAnchor.MiddleCenter, BattleArt.Gold);
            _logHeading.font = UiFont.Title;
            _logHeading.text = "战斗记录";
            ArtImage(_battleLog, "Divider", "UI/divider", new Vector2(0, 252), new Vector2(380, 16));
            var viewportGo = new GameObject("Viewport", typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(_battleLog, false);
            var viewport = viewportGo.GetComponent<RectTransform>();
            Position(viewport, new Vector2(0, -26), new Vector2(390, 530));
            viewportGo.GetComponent<Image>().color = Color.clear;
            _logText = CreateText(viewport, "LogText", Vector2.zero, new Vector2(390, 530),
                19, TextAnchor.UpperLeft, new Color(0.82f, 0.80f, 0.73f));
            var content = _logText.rectTransform;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            _logText.lineSpacing = 1.25f;
            var fitter = _logText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _battleLogScroll = _battleLog.gameObject.AddComponent<ScrollRect>();
            _battleLogScroll.viewport = viewport;
            _battleLogScroll.content = content;
            _battleLogScroll.horizontal = false;
            _battleLogScroll.movementType = ScrollRect.MovementType.Clamped;
            _battleLogScroll.scrollSensitivity = 28;
        }

        private void CompleteBattleSkin()
        {
            _battleHint = _hintText.rectTransform.parent as RectTransform;
            BattleArt.Apply(_battleHint.GetComponent<Image>(), "UI/panel_tooltip", true);
            _battleSubtitle = CreateText(_canvasRoot, "BattleSubtitle", new Vector2(-650, 440), new Vector2(480, 22),
                16, TextAnchor.MiddleLeft, BattleArt.Ink);
            _battleSubtitle.text = "F I N G E R   D I C E   D U E L";
            BattleArt.Apply(_gameOverPanel.GetComponent<Image>(), "UI/panel_tooltip", true);
            _gameOverText.font = UiFont.Title;
            _gameOverText.fontStyle = FontStyle.Normal;
            _gameOverText.color = BattleArt.Gold;
            SetBattleSkinLayout(true);
        }

        private void SetBattleSkinLayout(bool painted)
        {
            _paintedBattle = painted;
            if (_battleHint == null) return;
            _battleChoiceOwner = null;
            _canvasRoot.GetComponent<CanvasScaler>().screenMatchMode = painted
                ? CanvasScaler.ScreenMatchMode.Expand : CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            if (_battleBackground != null)
            {
                _battleBackground.sprite = painted ? BattleArt.Get("Backgrounds/battle_table") : null;
                _battleBackground.color = painted ? Color.white : BgColor;
            }
            Position(_battleHint, painted ? new Vector2(-80, 0) : new Vector2(0, 56),
                painted ? new Vector2(1000, 132) : new Vector2(1600, 108));
            _hintText.rectTransform.sizeDelta = painted ? new Vector2(920, 112) : new Vector2(1560, 96);
            _hintText.fontSize = painted ? 24 : 30;
            _hintText.color = BattleArt.Gold;
            Position(_battleLog, painted ? new Vector2(670, -10) : new Vector2(0, -462),
                painted ? new Vector2(460, 670) : new Vector2(1520, 130));
            _logHeading.gameObject.SetActive(painted);
            _battleLog.Find("Divider").gameObject.SetActive(painted);
            Position(_battleLogScroll.viewport, painted ? new Vector2(0, -26) : Vector2.zero,
                painted ? new Vector2(390, 530) : new Vector2(1480, 104));
            _battleSubtitle.gameObject.SetActive(painted);
            Position(_titleText.rectTransform, painted ? new Vector2(-650, 477) : new Vector2(0, -30),
                painted ? new Vector2(480, 56) : new Vector2(1200, 50));
            _titleText.fontSize = painted ? 30 : 36;
            _titleText.alignment = painted ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            _titleText.font = painted ? UiFont.Title : UiFont.Bold;
            _titleText.fontStyle = FontStyle.Normal;
            _titleText.color = painted ? BattleArt.Gold : TextColor;
            if (painted) Position(_turnText.rectTransform, new Vector2(-650, 340), new Vector2(480, 48));
            else
            {
                _turnText.rectTransform.anchorMin = _turnText.rectTransform.anchorMax = new Vector2(0, 1);
                _turnText.rectTransform.pivot = new Vector2(0, 1);
                _turnText.rectTransform.anchoredPosition = new Vector2(110, -40);
                _turnText.rectTransform.sizeDelta = new Vector2(240, 48);
            }
            _turnText.color = painted ? BattleArt.Gold : TextColor;
            _logText.rectTransform.anchoredPosition = Vector2.zero;
            _battleLogScroll.verticalNormalizedPosition = 0;
            if (_skipButton != null) _skipButton.gameObject.SetActive(false);
            if (_gambleButton != null) _gambleButton.gameObject.SetActive(false);
            if (_stealButton != null) _stealButton.gameObject.SetActive(false);
            if (!painted)
            {
                RestoreAction(_acceptButton, new Vector2(ChoiceButtonX, ChoiceAcceptY), new Vector2(ChoiceButtonWidth, ChoiceButtonHeight));
                RestoreAction(_declineButton, new Vector2(ChoiceButtonX, ChoiceDeclineY), new Vector2(ChoiceButtonWidth, ChoiceButtonHeight));
                RestoreAction(_skipButton, Vector2.zero, new Vector2(180, 56));
                RestoreAction(_gambleButton, Vector2.zero, new Vector2(160, 56));
                RestoreAction(_stealButton, Vector2.zero, new Vector2(160, 56));
            }
            LayoutBattleActions();
        }

        private static void RestoreAction(Button button, Vector2 position, Vector2 size)
        {
            if (button == null) return;
            Position(button.GetComponent<RectTransform>(), position, size);
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.rectTransform.sizeDelta = size;
                label.fontSize = 26;
            }
        }

        private void RefreshBattleHeader(PlayerState player, bool top)
        {
            var name = top ? _nameTextB : _nameTextA;
            string display = !string.IsNullOrEmpty(player.DisplayNameOverride) ? player.DisplayNameOverride
                : player.Definition != null ? player.Definition.DisplayName : player.Name;
            name.text = display + "（" + player.Name + "）";
            var stats = top ? _statusTextB : _statusTextA;
            int range = GameManager.BaseRangeOf(player);
            stats.text = "攻 " + Signed(StatusRules.EffectiveAttackBonus(player)) + "    防 " +
                Signed(StatusRules.EffectiveDefenseBonus(player)) + "    距 " + (range == int.MaxValue ? "∞" : range.ToString())
                + "    " + HandVisual.ShieldSummary(player);
            (top ? _healthTextB : _healthTextA).text = player.Health + " / " + player.MaximumHealth;
            string id = player.Definition != null ? player.Definition.Id : "soldier";
            if (id == "blank") id = "soldier";
            (top ? _badgeB : _badgeA).sprite = BattleArt.Get("Classes/" + id);
            BattleTooltip.Set(top ? _badgeB : _badgeA, display + "\n" + (player.Definition != null ? player.Definition.Description : ""));
        }

        private void LateUpdate()
        {
            if (_paintedBattle) LayoutBattleActions();
        }

        private void LayoutBattleActions()
        {
            if (!_paintedBattle) return;
            bool top = _cardsB != null && ReferenceEquals(_battleChoiceOwner, _cardsB[0].Owner);
            SkinAction(_acceptButton, new Vector2(267, top ? 290 : -280));
            SkinAction(_declineButton, new Vector2(267, top ? 215 : -355));
            SkinAction(_skipButton, new Vector2(267, -430));
            SkinAction(_gambleButton, new Vector2(267, -355));
            SkinAction(_stealButton, new Vector2(267, -280));
            BattleArt.Button(_exitButton);
        }

        private static void SkinAction(Button button, Vector2 position)
        {
            if (button == null) return;
            Position(button.GetComponent<RectTransform>(), position, new Vector2(210, 62));
            BattleArt.Button(button);
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.rectTransform.sizeDelta = new Vector2(186, 50);
                label.fontSize = 22;
            }
        }
    }
}
