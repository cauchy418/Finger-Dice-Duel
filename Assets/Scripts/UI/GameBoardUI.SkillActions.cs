using System.Collections.Generic;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private HandEffect _skillEffect;
        private PlayerState _skillActor;
        private readonly Button[] _skillChoiceButtons = new Button[16];
        private readonly Hand[] _skillHands = new Hand[16];

        /// <summary>绑定全部技能抉择；目标按钮等价于点击对应的高亮手牌。</summary>
        public void SetSkillActionContext(HandEffect effect, PlayerState chooser, PlayerState actor,
            IReadOnlyList<Hand> candidates, bool hasAcceptButton)
        {
            bool changed = _skillEffect != effect || !ReferenceEquals(_battleChoiceOwner, chooser);
            _skillEffect = effect;
            _skillActor = actor;
            _battleChoiceOwner = chooser;
            bool chooseHand = chooser != null && effect != HandEffect.None
                && effect != HandEffect.SchoolOfEvocationDamageType && candidates != null
                && (!hasAcceptButton || (effect == HandEffect.BreakerChainBreak && candidates.Count > 1));
            if (chooseHand && hasAcceptButton) _acceptButton.gameObject.SetActive(false);
            int count = chooseHand ? Mathf.Min(candidates.Count, _skillChoiceButtons.Length) : 0;
            // 以角色为目标的技能，两只手代表同一人，只列一次。
            var shownOwners = new HashSet<PlayerState>();
            int next = 0;
            for (int i = 0; i < count; i++)
            {
                Hand hand = candidates[i];
                if (TargetsPlayer(effect) && !shownOwners.Add(OwnerOf(hand))) continue;
                _skillHands[next++] = hand;
            }
            for (int i = 0; i < _skillChoiceButtons.Length; i++)
            {
                bool visible = i < next;
                if (visible && _skillChoiceButtons[i] == null)
                {
                    int index = i;
                    _skillChoiceButtons[i] = CreateButton(_canvasRoot, "SkillChoice" + i, Vector2.zero, new Vector2(250, 76), "发动");
                    _skillChoiceButtons[i].onClick.AddListener(() =>
                    {
                        if (_battleChoiceOwner != null && _skillHands[index] != null)
                            HandClicked?.Invoke(OwnerOf(_skillHands[index]), _skillHands[index]);
                    });
                }
                if (!visible) _skillHands[i] = null;
                if (_skillChoiceButtons[i] == null) continue;
                _skillChoiceButtons[i].gameObject.SetActive(visible);
                if (visible) _skillChoiceButtons[i].GetComponentInChildren<Text>().text = ChoiceLabel(effect, _skillHands[i]);
            }
            LayoutBattleActions();
            if (changed && _ringActionScroll != null) _ringActionScroll.verticalNormalizedPosition = 1;
        }

        private PlayerState OwnerOf(Hand hand)
        {
            if (!_paintedBattle)
                foreach (var panel in _ringPanels) if (panel.Owner != null && panel.Owner.Owns(hand)) return panel.Owner;
            var a = _cardsA?[0].Owner;
            return a != null && a.Owns(hand) ? a : _cardsB?[0].Owner;
        }

        private static bool TargetsPlayer(HandEffect effect) => effect == HandEffect.LifeDomainHealTarget
            || effect == HandEffect.LifeDomainExtraHealTarget || effect == HandEffect.OathOfDevotionShockTarget
            || effect == HandEffect.OathbreakerTransfer || effect == HandEffect.OathOfDevotionRedirect
            || effect == HandEffect.SchoolOfConjurationCastling;

        private string ChoiceLabel(HandEffect effect, Hand hand)
        {
            var owner = OwnerOf(hand);
            string side = ReferenceEquals(owner?.LeftHand, hand) ? "左手" : "右手";
            string player = !_paintedBattle ? SeatName(FindPanelOf(owner)?.Seat ?? 0) + "号"
                : ReferenceEquals(owner, _cardsA?[0].Owner) ? "我方" : "对手";
            switch (effect)
            {
                case HandEffect.SwashbucklerDodge: return "闪避" + side + " · 1→2";
                case HandEffect.SwashbucklerDuelistThrust: return "强化" + side + " · 2→3";
                case HandEffect.PathOfTheWardenLockTarget: return "锁定 · " + player + side;
                case HandEffect.BreakerChainBreak: return "破盾 · " + player + side;
                case HandEffect.TheArchfeyCancel: return "取消攻击 · " + side + "+1";
                case HandEffect.SchoolOfConjurationCastling: return "换位 · " + player;
                case HandEffect.LifeDomainHealTarget: return "回复 · " + player;
                case HandEffect.LifeDomainExtraHealTarget: return "额外回复 · " + player;
                case HandEffect.OathOfDevotionShockTarget: return "冲击 · " + player;
                case HandEffect.OathbreakerTransfer: return "转移至 · " + player + (owner?.Definition?.DisplayName ?? "角色");
                case HandEffect.OathOfDevotionRedirect: return "代挡 · 由我承受";
                case HandEffect.ShieldBlock: return "抵挡 · " + side;
                default: return "发动 · " + side;
            }
        }

        private void LayoutSkillActions()
        {
            bool top = _cardsB != null && ReferenceEquals(_battleChoiceOwner, _cardsB[0].Owner);
            int count = (Visible(_acceptButton) ? 1 : 0) + (Visible(_declineButton) ? 1 : 0);
            foreach (var button in _skillChoiceButtons) if (Visible(button)) count++;
            foreach (var button in new[] { _stealButton, _gambleButton, _skipButton, _seatSwapButton }) if (Visible(button)) count++;
            bool compact = count >= 4;
            float start = top ? (compact ? 320 : 300) : (compact ? -222 : -264);
            float gap = compact ? 72 : 88;
            float height = compact ? 62 : 76;
            int row = 0;
            foreach (var button in _skillChoiceButtons)
                if (Visible(button)) DecorateSkillAction(button, _battleChoiceOwner, new Vector2(290, start - row++ * gap), height);
            if (Visible(_acceptButton))
            {
                if (_skillEffect == HandEffect.SwashbucklerDodge) _acceptButton.GetComponentInChildren<Text>().text = "闪避 · 1→2";
                if (_skillEffect == HandEffect.SwashbucklerDuelistThrust) _acceptButton.GetComponentInChildren<Text>().text = "乘风突刺 · 2→3";
                DecorateSkillAction(_acceptButton, _battleChoiceOwner, new Vector2(290, start - row++ * gap), height);
            }
            if (Visible(_declineButton))
                DecorateSkillAction(_declineButton, _battleChoiceOwner, new Vector2(290, start - row++ * gap), height);
            if (Visible(_skipButton))
            {
                bool stealth = _skillActor?.Character is IAttackTargetRule rule && rule.IsImmuneToAttack(_skillActor);
                _skipButton.GetComponentInChildren<Text>().text = stealth ? "潜行 · 跳过回合"
                    : _skillActor?.Definition?.Id == CharacterDefinition.TheArchfeyId ? "妖精契约 · 跳过回合" : "跳过回合";
            }
            bool actorTop = _cardsB != null && ReferenceEquals(_skillActor, _cardsB[0].Owner);
            foreach (var button in new[] { _stealButton, _gambleButton, _skipButton, _seatSwapButton })
                if (Visible(button)) DecorateSkillAction(button, _skillActor, new Vector2(290, (actorTop ? (compact ? 320 : 300) : (compact ? -222 : -264)) - row++ * gap), height);
            if (Visible(_skipButton))
            {
                bool stealth = _skillActor?.Character is IAttackTargetRule rule && rule.IsImmuneToAttack(_skillActor);
                BattleTooltip.Set(_skipButton.GetComponent<Image>(), stealth
                    ? "潜行已生效。本次潜行可跳过一回合；出手与解除时机仍按原规则结算。"
                    : "按当前角色与手牌满足的条件跳过本回合。");
            }
        }

        private static bool Visible(Button button) => button != null && button.gameObject.activeSelf;

        private void DecorateSkillAction(Button button, PlayerState owner, Vector2 position, float height, float width = 250)
        {
            Position((RectTransform)button.transform, position, new Vector2(width, height));
            BattleArt.Button(button);
            var icon = button.transform.Find("SkillIcon")?.GetComponent<Image>();
            if (icon == null) icon = ArtImage((RectTransform)button.transform, "SkillIcon", "Subclasses/fighter_champion", new Vector2(-88, 0), new Vector2(48, 48));
            icon.gameObject.SetActive(true);
            icon.rectTransform.anchoredPosition = new Vector2(-width / 2 + 37, 0);
            string id = owner?.Definition?.Id ?? "fighter_champion";
            icon.sprite = BattleArt.Get("Subclasses/" + (id == "blank" ? "fighter_champion" : id));
            icon.preserveAspect = true;
            var label = button.GetComponentInChildren<Text>();
            label.font = UiFont.Bold;
            label.fontSize = _paintedBattle ? 20 : 18;
            if (!_paintedBattle) label.text = label.text.Replace(" · ", "\n");
            label.rectTransform.sizeDelta = new Vector2(width - 96, height - 8);
            label.rectTransform.anchoredPosition = new Vector2(15, 0);
            label.verticalOverflow = VerticalWrapMode.Truncate;
            UiInkAlignment.Center(label, new Vector2(15, 0));
            BattleTooltip.Set(button.GetComponent<Image>(), label.text + "\n" + (owner?.Definition?.Description ?? "角色操作"));
        }

        private void HideSkillChoices()
        {
            foreach (var button in _skillChoiceButtons) if (button != null) button.gameObject.SetActive(false);
        }
    }
}
