using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>所有战斗模式的绘画素材；1v1 与多人分别排版，共用手牌和反馈。</summary>
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
            var badge = ArtImage(area, "SubclassEmblem", "Subclasses/fighter_champion",
                new Vector2(-230, 181), new Vector2(112, 112));
            badge.preserveAspect = true;
            ArtImage(area, "NamePlate", "UI/label_strip", new Vector2(70, 195), new Vector2(460, 44), true);
            name = CreateText(area, "Name", new Vector2(70, 195), new Vector2(430, 38),
                28, TextAnchor.MiddleCenter, BattleArt.Gold);
            name.font = UiFont.Title;
            name.fontStyle = FontStyle.Normal;
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 14; name.resizeTextMaxSize = 28;
            // 双将的职业·子职全名较长，限制在姓名行内缩放，避免换行压住血条。
            name.verticalOverflow = VerticalWrapMode.Truncate;
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
            // 为回合高亮时姓名的放大动画预留间距。
            statuses.SetBattleLayout(new Vector2(330, 195));
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
            bool left = ReferenceEquals(card.Hand, card.Owner.LeftHand);
            string gesture = hand.IsShield && hand.Value == 5 ? "5_shield" : hand.Value.ToString();
            card.Gesture.sprite = BattleArt.Get("Hands/hand_" + (left ? "left_" : "right_") + gesture);
            card.Gesture.color = !card.Owner.IsAlive ? new Color(0.45f, 0.45f, 0.45f) : hand.IsConcealed ? new Color(0.65f, 0.80f, 0.80f) : Color.white;
            string frame = outline == HandVisual.OutlineState.Highlighted ? "selected"
                : outline == HandVisual.OutlineState.Available ? "available"
                : hand.IsLocked || hand.IsConcealed || !card.Owner.IsAlive ? "disabled" : "normal";
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

        /// <summary>章节背景仅覆盖当前战斗，默认键恢复竞技桌面；不改变手牌、面板与规则。</summary>
        public void SetBattleBackground(string key = null)
        {
            if (_battleBackground == null) return;
            BattleArt.Apply(_battleBackground, key ?? "Backgrounds/battle_table");
        }

        private void SetBattleSkinLayout(bool painted)
        {
            _paintedBattle = painted;
            if (_battleHint == null) return;
            _battleChoiceOwner = null;
            _canvasRoot.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (_battleBackground != null)
            {
                _battleBackground.sprite = BattleArt.Get("Backgrounds/battle_table");
                _battleBackground.color = Color.white;
            }
            Position(_battleHint, painted ? new Vector2(-80, 0) : new Vector2(0, 72),
                painted ? new Vector2(1000, 132) : new Vector2(990, 96));
            _hintText.rectTransform.sizeDelta = painted ? new Vector2(920, 112) : new Vector2(940, 88);
            _hintText.fontSize = painted ? 24 : 21;
            _hintText.color = BattleArt.Gold;
            Position(_battleLog, new Vector2(670, -10), new Vector2(460, 670));
            Position(_battleLogScroll.viewport, new Vector2(0, -26), new Vector2(390, 530));
            _battleSubtitle.gameObject.SetActive(painted);
            Position(_titleText.rectTransform, painted ? new Vector2(-650, 477) : new Vector2(-330, 493),
                painted ? new Vector2(480, 36) : new Vector2(1140, 48));
            _titleText.fontSize = painted ? 30 : 26;
            _titleText.resizeTextForBestFit = true; _titleText.resizeTextMinSize = 16; _titleText.resizeTextMaxSize = painted ? 30 : 26;
            _titleText.verticalOverflow = VerticalWrapMode.Truncate;
            _titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _titleText.alignment = TextAnchor.MiddleLeft;
            _titleText.font = UiFont.Title;
            _titleText.fontStyle = FontStyle.Normal;
            _titleText.color = BattleArt.Gold;
            if (painted) Position(_turnText.rectTransform, new Vector2(-650, 340), new Vector2(480, 48));
            else
            {
                Position(_turnText.rectTransform, new Vector2(-750, 449), new Vector2(300, 26));
            }
            _turnText.fontSize = painted ? 26 : 22;
            _turnText.color = BattleArt.Gold;
            _logText.rectTransform.anchoredPosition = Vector2.zero;
            _battleLogScroll.verticalNormalizedPosition = 0;
            if (_skipButton != null) _skipButton.gameObject.SetActive(false);
            if (_gambleButton != null) _gambleButton.gameObject.SetActive(false);
            if (_stealButton != null) _stealButton.gameObject.SetActive(false);
            HideSkillChoices();
            if (painted) RestoreSkillParents();
            if (_exitButton != null) { var rect = (RectTransform)_exitButton.transform; rect.anchoredPosition = new Vector2(-30, -30); rect.sizeDelta = new Vector2(180, 56); }
            SetHistoryOpen(false);
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
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.fontSize = 26;
            }
            var icon = button.transform.Find("SkillIcon");
            if (icon != null) icon.gameObject.SetActive(false);
        }

        private void RefreshBattleHeader(PlayerState player, bool top)
        {
            var name = top ? _nameTextB : _nameTextA;
            string display = player.Definition?.Id == CharacterDefinition.BlankId ? player.Name
                : !string.IsNullOrEmpty(player.DisplayNameOverride) ? player.DisplayNameOverride
                : player.Definition != null ? player.Definition.DisplayName : player.Name;
            name.text = (player.Name == display ? display : display + "（" + player.Name + "）")+(player.IsPolymorphed?" · 变羊":"");
            var stats = top ? _statusTextB : _statusTextA;
            int range = GameManager.BaseRangeOf(player);
            int statusRange = StatusRules.EffectiveAttackRange(player, EffectContext);
            if (statusRange >= 0) range = statusRange;
            stats.text = "攻 " + Signed(StatusRules.EffectiveAttackBonus(player)) + "    防 " +
                Signed(StatusRules.EffectiveDefenseBonus(player)) + "    距 " + (range == int.MaxValue ? "∞" : range.ToString())
                + "    " + HandVisual.ShieldSummary(player);
            (top ? _healthTextB : _healthTextA).text = player.Health + " / " + player.MaximumHealth;
            string id = player.Definition != null ? player.Definition.Id : "fighter_champion";
            (top ? _badgeB : _badgeA).sprite = BattleArt.Get(id == CharacterDefinition.BlankId ? "Adventure/guide_soldier" : player.BattleEmblemKey);
            BattleTooltip.Set(top ? _badgeB : _badgeA, display + "\n" + (player.Definition != null ? player.Definition.Description : ""));
        }

        private void LateUpdate()
        {
            LayoutBattleActions();
        }

        private void LayoutBattleActions()
        {
            if (_paintedBattle) LayoutSkillActions(); else LayoutRingActions();
            BattleArt.Button(_exitButton);
        }

    }
}
