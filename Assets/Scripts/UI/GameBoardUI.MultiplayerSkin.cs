using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private PlayerState _ringMe;
        private int _ringCount;
        private ScrollRect _ringActionScroll;
        private RectTransform _ringActionRoot, _ringActionContent;

        private void BuildPaintedRingPanel(RingPosition position)
        {
            var root = FrontEndArt.Rect(_canvasRoot, "Ring" + position, Vector2.zero, new Vector2(370, 270));
            var background = root.gameObject.AddComponent<Image>();
            BattleArt.Apply(background, "UI/panel_tooltip", true);
            background.raycastTarget = false;
            var emblem = ArtImage(root, "SubclassEmblem", "Subclasses/fighter_champion", Vector2.zero, new Vector2(60, 60));
            var name = CreateText(root, "Name", Vector2.zero, new Vector2(190, 30), 22, TextAnchor.MiddleCenter, BattleArt.Gold);
            name.font = UiFont.Bold;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 14;
            var seat = CreateText(root, "Seat", Vector2.zero, new Vector2(200, 22), 16, TextAnchor.MiddleCenter, BattleArt.Ink);
            var track = ArtImage(root, "HpBar", "UI/health_track", Vector2.zero, new Vector2(200, 28), true);
            var fill = ArtImage(track.rectTransform, "Fill", "UI/health_fill", Vector2.zero, new Vector2(184, 15));
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
            var health = CreateText(track.rectTransform, "Health", Vector2.zero, new Vector2(160, 26), 18, TextAnchor.MiddleCenter, Color.white);
            health.font = UiFont.Bold;
            var stats = CreateText(root, "Stats", Vector2.zero, new Vector2(260, 22), 16, TextAnchor.MiddleCenter, BattleArt.Ink);
            var identity = CreateText(root, "Identity", Vector2.zero, new Vector2(70, 22), 16, TextAnchor.MiddleCenter, BattleArt.Gold);
            identity.font = UiFont.Bold; identity.gameObject.SetActive(false);
            var panel = new RingPanel
            {
                Position = position, Root = root, NameText = name, SeatText = seat, HpFill = fill,
                HpText = health, Emblem = emblem, StatusText = stats, IdentityText = identity,
                StatusIcons = new StatusIconBar(root, _font, true, "NegativeStatuses"),
                Cards = new[] { CreatePaintedHandCard(root, Vector2.zero), CreatePaintedHandCard(root, Vector2.zero) }
            };
            _ringPanels.Add(panel);
            root.gameObject.SetActive(false);
        }

        private void LayoutPaintedRingPanel(RingPanel panel, bool eight)
        {
            var position = panel.Position;
            bool self = position == RingPosition.SelfBottom;
            Vector2 size = eight ? (self ? new Vector2(460, 290) : new Vector2(370, 270)) : new Vector2(self ? 530 : 500, 370);
            Vector2 center;
            switch (position)
            {
                case RingPosition.SelfBottom: center = new Vector2(0, eight ? -310 : -295); break;
                case RingPosition.UpperLeft: center = new Vector2(eight ? -560 : -410, eight ? 300 : 245); break;
                case RingPosition.UpperRight: center = new Vector2(eight ? 560 : 410, eight ? 300 : 245); break;
                case RingPosition.LowerLeft: center = new Vector2(eight ? -560 : -650, eight ? -300 : -295); break;
                case RingPosition.LowerRight: center = new Vector2(eight ? 560 : 650, eight ? -300 : -295); break;
                case RingPosition.MiddleLeft: center = new Vector2(-730, 0); break;
                case RingPosition.MiddleRight: center = new Vector2(730, 0); break;
                default: center = new Vector2(0, 300); break;
            }
            Position(panel.Root, center, size);
            float top = size.y / 2;
            Position(panel.Emblem.rectTransform, new Vector2(-size.x / 2 + 40, top - 40), Vector2.one * (eight ? 60 : 76));
            Position(panel.NameText.rectTransform, new Vector2(-12, top - 28), new Vector2(size.x - (eight ? 180 : 204), 30));
            panel.NameText.fontSize = eight ? 22 : 25;
            panel.NameText.resizeTextMaxSize = eight ? 22 : 25;
            Position(panel.SeatText.rectTransform, new Vector2(-12, top - 52), new Vector2(size.x - 170, 22));
            var track = (RectTransform)panel.HpFill.transform.parent;
            Position(track, new Vector2(-12, top - 78), new Vector2(size.x - 170, 28));
            Position(panel.HpFill.rectTransform, Vector2.zero, new Vector2(size.x - 186, 15));
            Position(panel.HpText.rectTransform, Vector2.zero, new Vector2(size.x - 190, 26));
            Position(panel.StatusText.rectTransform, new Vector2(38, top - 104), new Vector2(size.x - 110, 22));
            Position(panel.IdentityText.rectTransform, new Vector2(-size.x / 2 + 40, top - 104), new Vector2(70, 22));
            panel.StatusIcons.SetGridLayout(new Vector2(size.x / 2 - 74, top - 28), 22, 1, 3);
            float scale = eight ? (self ? 0.51f : 0.47f) : 0.65f;
            float x = eight ? 60 : 85;
            for (int i = 0; i < 2; i++)
            {
                var card = panel.Cards[i];
                Position((RectTransform)card.Button.transform, new Vector2(i == 0 ? -x : x, eight ? -54 : -65), new Vector2(210, 304));
                card.Button.transform.localScale = Vector3.one * scale;
                card.ValueText.fontSize = eight ? 38 : 32;
                card.NameText.fontSize = eight ? 24 : 20;
                card.StatusText.fontSize = eight ? 21 : 18;
            }
        }

        private void RefreshPaintedRingHeader(RingPanel panel)
        {
            var player = panel.Owner;
            string display = !string.IsNullOrEmpty(player.DisplayNameOverride) ? player.DisplayNameOverride : player.Definition?.DisplayName ?? player.Name;
            panel.NameText.text = display+(player.IsPolymorphed?" · 变羊":"");
            panel.HpText.text = player.Health + " / " + player.MaximumHealth;
            UpdateHealthBar(panel.HpFill, player);
            string id = player.Definition?.Id ?? "fighter_champion";
            panel.Emblem.sprite = BattleArt.Get(player.BattleEmblemKey);
            panel.Emblem.color = player.IsAlive ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            panel.Root.GetComponent<Image>().color = player.IsAlive ? Color.white : new Color(0.58f, 0.58f, 0.58f);
            panel.SeatText.color = panel.Relation == RingRelation.Self ? BattleArt.Gold
                : panel.Relation == RingRelation.Teammate ? new Color(0.5f, 0.85f, 0.85f) : BattleArt.Ink;
            int range = GameManager.BaseRangeOf(player);
            int statusRange = StatusRules.EffectiveAttackRange(player, EffectContext);
            if (statusRange >= 0) range = statusRange;
            panel.StatusText.text = "攻 " + Signed(StatusRules.EffectiveAttackBonus(player)) + "   防 " + Signed(StatusRules.EffectiveDefenseBonus(player))
                + "   距 " + (range == int.MaxValue ? "∞" : range.ToString());
            panel.StatusIcons.Refresh(player,EffectContext);
            BattleTooltip.Set(panel.Emblem, display + "（" + player.Name + "）\n" + player.Definition?.Description);
        }

        private void LayoutRingActionTray()
        {
            if (_paintedBattle) return;
            if (_ringActionRoot == null)
            {
                _ringActionScroll = FrontEndArt.Scroll(_canvasRoot, "SkillActions", Vector2.zero, new Vector2(990, 144), out _ringActionContent);
                _ringActionRoot = (RectTransform)_ringActionScroll.transform;
                BattleArt.Apply(_ringActionRoot.GetComponent<Image>(), "UI/panel_tooltip", true);
            }
            bool eight = _ringCount == 8;
            Position(_battleHint, _adventureGuideCompact ? new Vector2(0,-82) : new Vector2(0, eight ? 72 : 0), _adventureGuideCompact ? new Vector2(1160,44) : new Vector2(990,96));
            bool mateOnLeft = FindPanel(RingPosition.LowerLeft)?.Owner != null;
            Position(_ringActionRoot, eight ? new Vector2(0, -60) : new Vector2(mateOnLeft ? 650 : -650, -295),
                eight ? new Vector2(990, 152) : new Vector2(500, 360));
            var track = (RectTransform)_ringActionRoot.Find("Scrollbar");
            Position(track, new Vector2(_ringActionRoot.sizeDelta.x / 2 - 8, 0), new Vector2(12, _ringActionRoot.sizeDelta.y - 24));
            _ringActionContent.sizeDelta = new Vector2(_ringActionRoot.sizeDelta.x - 24, _ringActionContent.sizeDelta.y);
        }

        private void LayoutRingActions()
        {
            LayoutRingActionTray();
            if (Visible(_acceptButton))
            {
                if (_skillEffect == HandEffect.SwashbucklerDodge) _acceptButton.GetComponentInChildren<Text>().text = "闪避 · 1→2";
                if (_skillEffect == HandEffect.SwashbucklerDuelistThrust) _acceptButton.GetComponentInChildren<Text>().text = "乘风突刺 · 2→3";
            }
            if (Visible(_skipButton))
                _skipButton.GetComponentInChildren<Text>().text = _skillActor?.Character is IAttackTargetRule rule && rule.IsImmuneToAttack(_skillActor)
                    ? "潜行 · 跳过回合" : _skillActor?.Definition?.Id == CharacterDefinition.TheArchfeyId ? "妖精契约 · 跳过回合" : "跳过回合";
            var actions = new List<Button>();
            foreach (var button in _skillChoiceButtons) if (Visible(button)) actions.Add(button);
            foreach (var button in new[] { _acceptButton, _declineButton, _stealButton, _gambleButton, _skipButton, _sacrificeButton, _seatSwapButton })
                if (Visible(button)) actions.Add(button);
            _ringActionRoot.gameObject.SetActive(actions.Count > 0);
            if (actions.Count == 0) return;
            int columns = _ringCount == 8 ? 4 : 2;
            float cell = (_ringActionRoot.sizeDelta.x - 36) / columns;
            float contentHeight = Mathf.Max(_ringActionRoot.sizeDelta.y, Mathf.Ceil(actions.Count / (float)columns) * 72 + 8);
            _ringActionContent.sizeDelta = new Vector2(_ringActionRoot.sizeDelta.x - 24, contentHeight);
            for (int i = 0; i < actions.Count; i++)
            {
                var button = actions[i];
                if (button.transform.parent != _ringActionContent) button.transform.SetParent(_ringActionContent, false);
                var owner = button == _stealButton || button == _gambleButton || button == _skipButton || button == _sacrificeButton || button == _seatSwapButton ? _skillActor : _battleChoiceOwner;
                DecorateSkillAction(button, owner, Vector2.zero, 64, cell - 8);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(12 + cell * (i % columns + 0.5f), -40 - i / columns * 72);
            }
        }

        private void RestoreSkillParents()
        {
            if (_ringActionRoot != null) _ringActionRoot.gameObject.SetActive(false);
            foreach (var button in _skillChoiceButtons) if (button != null) button.transform.SetParent(_canvasRoot, false);
            foreach (var button in new[] { _acceptButton, _declineButton, _stealButton, _gambleButton, _skipButton, _sacrificeButton, _seatSwapButton })
                if (button != null) button.transform.SetParent(_canvasRoot, false);
        }
    }
}
