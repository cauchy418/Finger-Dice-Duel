using UnityEngine;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 手牌的显示规则：把"数值 / 护盾 / 锁定"翻译成颜色与文字。
    /// 所有与表现有关的判断都集中在这里，后续换成真正的美术资源时只改这一个文件。
    /// </summary>
    public static class HandVisual
    {
        private static readonly Color NormalCard = new Color(0.24f, 0.26f, 0.32f);
        private static readonly Color AttackCard = new Color(0.72f, 0.24f, 0.22f);
        private static readonly Color HealCard = new Color(0.22f, 0.58f, 0.32f);
        private static readonly Color ShieldCard = new Color(0.20f, 0.45f, 0.75f);
        private static readonly Color BrokenCard = new Color(0.30f, 0.30f, 0.36f);
        private static readonly Color LockedCard = new Color(0.16f, 0.15f, 0.15f);

        /// <summary>
        /// **暗置（游侠·幽域追踪者伏击）** 的牌面颜色。
        ///
        /// 与"被锁"不同：被锁是别人强加的（暗灰），
        /// 暗置是游侠·幽域追踪者自己摆的姿态（深青），而且**数值仍然可见**。
        /// </summary>
        private static readonly Color ConcealedCard = new Color(0.13f, 0.24f, 0.26f);

        public static Color CardColor(Core.Hand hand, Core.PlayerState owner = null)
        {
            if (hand == null) return NormalCard;
            if (hand.IsLocked) return LockedCard;

            switch (hand.State)
            {
                case Core.ShieldState.Formed: return ShieldCard;   // 合拢的手掌 = 盾
                case Core.ShieldState.Broken: return BrokenCard;   // 展开的碎盾
                // 太极形态（武僧·太极宗）：借"回复"的绿色系 —— 它既不是攻击也不是盾，
                // 而是"以柔克刚"的防守姿态。用与护盾不同的颜色以示区分。
                case Core.ShieldState.Taichi: return HealCard;
                // 暗置（游侠·幽域追踪者伏击）：数值仍可见，但不能当运算源、也不能被对手碰
                case Core.ShieldState.Concealed: return ConcealedCard;
            }

            switch (EffectOf(hand, owner))
            {
                case Core.HandEffectType.Attack4:
                case Core.HandEffectType.Attack6:
                case Core.HandEffectType.Attack8:
                    return AttackCard;
                case Core.HandEffectType.Heal:
                    return HealCard;
                default:
                    return NormalCard;
            }
        }

        public static Color OutlineColor(bool selected, bool clickable)
        {
            if (selected) return new Color(1f, 0.85f, 0.25f);       // 选中：金色
            if (clickable) return new Color(0.75f, 0.82f, 0.95f);   // 可点：亮边
            return new Color(0f, 0f, 0f, 0.25f);                    // 不可点：几乎无边框
        }

        /// <summary>
        /// 手牌边框的三种状态。
        ///
        /// 必须把"这一手现在可点"和"这一手是当前抉择的候选"区分开：
        /// 前者会持续整整一个回合，若拿它当高亮，手牌就会一直亮着
        /// （游荡者·风流剑客的 1 闪避成 2、2 强化成 3 之后都一直高亮过 —— 真实 bug）。
        /// </summary>
        public enum OutlineState
        {
            /// <summary>不可点：几乎无边框。</summary>
            Hidden = 0,

            /// <summary>可点，但不是当前抉择的候选：低调的亮边。</summary>
            Available = 1,

            /// <summary>当前抉择的候选，或已被选中：高亮。</summary>
            Highlighted = 2,
        }

        public static Color OutlineColor(OutlineState state)
        {
            switch (state)
            {
                // 玩家要求："选中的手是黄色高光，可以选的手是白色高光"。
                // 原来"可点"用的是淡蓝灰（0.62/0.70/0.82），
                // 玩家实测反馈："现在可点的高光变成黄色外框了" ——
                // 那是因为我上一轮把"可点的手"也填进了候选，于是它们都走了金色分支。
                // 现在把两层颜色明确分开：候选/选中 = 金、可点 = 白。
                case OutlineState.Highlighted: return new Color(1f, 0.85f, 0.25f);   // 金色：选中 / 当前候选
                case OutlineState.Available: return new Color(0.95f, 0.96f, 1f);      // 白色：可以点
                default: return new Color(0f, 0f, 0f, 0.25f);
            }
        }

        /// <summary>手牌第二行的小字：护盾 / 锁定 / 效果提示。</summary>
        public static string StatusText(Core.Hand hand, Core.PlayerState owner = null)
        {
            if (hand == null) return "";

            if (hand.IsLocked) return "已锁定";
        if (hand.IsConcealed) return "伏击 · 已暗置";

            switch (hand.State)
            {
                case Core.ShieldState.Formed: return "护盾 · 合拢";
                case Core.ShieldState.Broken: return "碎盾 · 展开";
                // 太极必须说清"这不是盾"——否则玩家会以为它能完全挡下一次攻击。
                case Core.ShieldState.Taichi: return "太极 · 减半反弹";
            }

            switch (EffectOf(hand, owner))
            {
                case Core.HandEffectType.Attack4:
                case Core.HandEffectType.Attack6:
                case Core.HandEffectType.Attack8: return "攻击 " + hand.Value;
                case Core.HandEffectType.Heal: return "回复 9";
                default: return "—";
            }
        }

        /// <summary>
        /// 这张手在界面上应显示成哪种效果。
        ///
        /// ⚠️ **不能直接用 <c>Hand.GetEffectType</c>** ——
        ///    有些角色会改写效果，界面上必须按**角色自己的规则**显示，
        ///    否则会出现"看起来能打人、其实无效果"（玩家实测：
        ///    野蛮人·巨人道途的 4/6/8 显示成红色攻击、而 5 反而显示成护盾，
        ///    两者正好与实际相反）。
        ///
        /// 角色通过 <see cref="Core.IHandEffectDisplay"/> 自报，
        /// 没实现就退回通用规则。
        /// </summary>
        private static Core.HandEffectType EffectOf(Core.Hand hand, Core.PlayerState owner)
        {
            if (hand == null) return Core.HandEffectType.None;

            // owner 可能是 null（早期构建阶段 / 直连测试），此时退回通用规则
            var display = owner?.Character as Core.IHandEffectDisplay;
            if (display != null) return display.DisplayedEffectOf(hand.Value);

            return Core.Hand.GetEffectType(hand.Value);
        }

        /// <summary>护盾状态的简短描述，用于玩家信息行。</summary>
        public static string ShieldSummary(Core.PlayerState player)
        {
            if (player == null) return "";

            if (player.HasDoubleShield) return "状态：双盾";
            if (player.HasAnyShield) return "状态：有盾";

            bool broken = player.LeftHand.State == Core.ShieldState.Broken
                          || player.RightHand.State == Core.ShieldState.Broken;
            return broken ? "状态：碎盾" : "状态：无盾";
        }

        /// <summary>整只手牌的完整描述（日志 / 提示用）。</summary>
        public static string Describe(Core.Hand hand, Core.PlayerState owner)
        {
            if (hand == null || owner == null) return "?";
            return (ReferenceEquals(hand, owner.LeftHand) ? "左手" : "右手") + $"({hand.Value})";
        }

        /// <summary>阶段名，用于 UI 调试显示。</summary>
        public static string PhaseName(Assistant.BoardPhase phase)
        {
            switch (phase)
            {
                case Assistant.BoardPhase.ChoosingAttackerHand: return "选择自己的手";
                case Assistant.BoardPhase.ChoosingTargetHand: return "选择对方的手";
                case Assistant.BoardPhase.ChoosingDefenseShield: return "选择抵挡用的盾";
                case Assistant.BoardPhase.GameOver: return "对局结束";
                default: return phase.ToString();
            }
        }
    }
}
