using System.Collections.Generic;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private readonly Dictionary<PlayerState, IdentityRole> _identityGuesses = new Dictionary<PlayerState, IdentityRole>();
        private RectTransform _identityGuessOverlay;
        private Text _identityGuessTitle;
        private GameObject _guessPanel, _lordBonusPanel;
        private Text _lordBonusTitle, _lordBonusText;
        private PlayerState _viewingLord;
        private PlayerState _guessingIdentityOf;

        public void ClearIdentityGuesses()
        {
            _identityGuesses.Clear();
            CloseIdentityGuess();
        }

        public void SetRingIdentity(PlayerState owner, IdentityRole revealedRole, string lordTitle)
        {
            var panel = FindPanelOf(owner);
            if (panel == null) return;

            bool canGuess = revealedRole == IdentityRole.None && !owner.IsSummon;
            if (!canGuess)
            {
                _identityGuesses.Remove(owner);
                if (ReferenceEquals(_guessingIdentityOf, owner)) CloseIdentityGuess();
            }

            bool guessed = _identityGuesses.TryGetValue(owner, out var guess);
            var role = guessed ? guess : revealedRole;
            string label = role == IdentityRole.Lord ? lordTitle : IdentityRoleInfo.DisplayName(role);
            var button = panel.IdentityBadge;
            var icon = (Image)button.targetGraphic;
            icon.sprite = MatchMarks.Identity(role, lordTitle);
            bool showsLordBonus = role == IdentityRole.Lord && !owner.IsSummon;
            button.interactable = canGuess || showsLordBonus;
            button.gameObject.SetActive(!owner.IsSummon);
            panel.IdentityTag.text = guessed ? "猜" : canGuess ? "？" : "";
            var tag = panel.IdentityTag.rectTransform;
            tag.anchoredPosition = guessed ? new Vector2(14, -14) : Vector2.zero;
            panel.IdentityTag.fontSize = guessed ? 18 : 34;
            BattleTooltip.Set(icon, guessed ? "猜测 · " + label + "\n点击修改或清除"
                : canGuess ? "身份未公开\n点击设置身份猜测"
                : showsLordBonus ? label + " · 点击查看加成" : "身份 · " + label);
            if (ReferenceEquals(_viewingLord, owner)) RefreshLordBonus(owner, lordTitle);
        }

        private bool CanGuessIdentity(PlayerState owner)
            => owner != null && !owner.IsSummon && !ReferenceEquals(owner, _ringMe)
                && EffectContext?.IsIdentityMode == true && !EffectContext.IsIdentityPublic(owner)
                && FindPanelOf(owner)?.IdentityBadge.interactable == true;

        private void OpenIdentityBadge(PlayerState owner)
        {
            bool isLord = owner != null && !owner.IsSummon && EffectContext?.IsIdentityMode == true
                && EffectContext.IdentityOf(owner) == IdentityRole.Lord;
            if (!isLord && !CanGuessIdentity(owner)) return;
            if (_identityGuessOverlay == null) BuildIdentityGuessOverlay();
            _guessingIdentityOf = isLord ? null : owner;
            _viewingLord = isLord ? owner : null;
            _guessPanel.SetActive(!isLord);
            _lordBonusPanel.SetActive(isLord);
            if (isLord) RefreshLordBonus(owner, EffectContext.IdentityLordTitle);
            else _identityGuessTitle.text = $"猜测 {owner.Name} 的身份";
            _identityGuessOverlay.SetAsLastSibling();
            _identityGuessOverlay.gameObject.SetActive(true);
        }

        private void CloseIdentityGuess()
        {
            _guessingIdentityOf = null;
            _viewingLord = null;
            if (_identityGuessOverlay != null) _identityGuessOverlay.gameObject.SetActive(false);
        }

        private void RefreshLordBonus(PlayerState lord, string title)
        {
            _lordBonusTitle.text = title + " · 主公加成";
            string axis, bonus;
            int perSeat = 1;
            switch (title)
            {
                case "明主": axis = "守序"; bonus = "防御"; break;
                case "暴君": axis = "混乱"; bonus = "本体体力上限"; perSeat = 5; break;
                default: axis = "中立"; bonus = "攻击"; break;
            }
            int count = EffectContext.IdentityLordFactionCount();
            string current = lord.IsOriginalAlive ? $"当前有 {count} 名存活{axis}角色，额外{bonus} +{count * perSeat}。" : "主公已阵亡。";
            _lordBonusText.text = "主公基础加成：本体体力上限 +5，攻防各 +1。\n\n"
                + $"每名存活{axis}角色（含主公）使主公的{bonus} +{perSeat}。\n{current}";
        }

        private void SaveIdentityGuess(IdentityRole role)
        {
            var owner = _guessingIdentityOf;
            CloseIdentityGuess();
            if (!CanGuessIdentity(owner)) return;
            if (role == IdentityRole.None) _identityGuesses.Remove(owner);
            else _identityGuesses[owner] = role;
            SetRingIdentity(owner, IdentityRole.None, EffectContext.IdentityLordTitle);
        }

        private void BuildIdentityGuessOverlay()
        {
            _identityGuessOverlay = FrontEndArt.Rect(_canvasRoot, "IdentityGuessOverlay", Vector2.zero, Vector2.zero);
            FrontEndArt.Stretch(_identityGuessOverlay);
            var veil = _identityGuessOverlay.gameObject.AddComponent<Image>();
            veil.color = new Color(0, 0, 0, .55f);
            var dismiss = _identityGuessOverlay.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = veil;
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(CloseIdentityGuess);

            var panel = FrontEndArt.Image(_identityGuessOverlay, "GuessPanel", "UI/panel_content", Vector2.zero, new Vector2(620, 348));
            panel.raycastTarget = true;
            _guessPanel = panel.gameObject;
            _identityGuessTitle = FrontEndArt.Text(panel.transform, "Title", "", new Vector2(0, 128), new Vector2(540, 50), 30, title: true);
            var roles = new[] { IdentityRole.Loyalist, IdentityRole.Rebel, IdentityRole.Traitor };
            for (int i = 0; i < roles.Length; i++)
            {
                var role = roles[i];
                var button = FrontEndArt.Button(panel.transform, "Guess" + role, IdentityRoleInfo.DisplayName(role),
                    new Vector2((i - 1) * 172, 3), new Vector2(150, 152), "roster_tile");
                var label = button.GetComponentInChildren<Text>().rectTransform;
                label.anchoredPosition = new Vector2(0, -44);
                label.sizeDelta = new Vector2(114, 35);
                var icon = FrontEndArt.Rect(button.transform, "Mark", new Vector2(0, 22), Vector2.one * 78).gameObject.AddComponent<Image>();
                icon.sprite = MatchMarks.Identity(role);
                icon.raycastTarget = false;
                button.onClick.AddListener(() => SaveIdentityGuess(role));
            }
            var clear = FrontEndArt.Button(panel.transform, "ClearGuess", "清除猜测", new Vector2(-125, -117), new Vector2(212, 51));
            clear.onClick.AddListener(() => SaveIdentityGuess(IdentityRole.None));
            var close = FrontEndArt.Button(panel.transform, "CloseGuess", "关闭", new Vector2(125, -117), new Vector2(212, 51));
            close.onClick.AddListener(CloseIdentityGuess);

            var lord = FrontEndArt.Image(_identityGuessOverlay, "LordBonusPanel", "UI/panel_content", Vector2.zero, new Vector2(700, 348));
            lord.raycastTarget = true;
            _lordBonusPanel = lord.gameObject;
            _lordBonusTitle = FrontEndArt.Text(lord.transform, "Title", "", new Vector2(0, 126), new Vector2(620, 52), 32, title: true);
            _lordBonusText = FrontEndArt.Text(lord.transform, "Bonuses", "", new Vector2(0, 12), new Vector2(594, 155), 24);
            var closeBonus = FrontEndArt.Button(lord.transform, "CloseBonus", "关闭", new Vector2(0, -113), new Vector2(212, 51));
            closeBonus.onClick.AddListener(CloseIdentityGuess);
        }
    }
}
