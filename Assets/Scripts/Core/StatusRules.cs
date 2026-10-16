using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **法师状态体系的规则中心**。
    ///
    /// 为什么要有这个类：状态挂在 <see cref="PlayerState"/> 上，但用到它的地方有三处
    ///   · <see cref="AttackContext"/>（伤害结算：强酸降防、震慑降攻）
    ///   · <see cref="GameManager"/>（合法性过滤：残响 / 目盲 / 震慑；回血禁令）
    ///   · AI（斩杀线估算必须与真实结算**同一份**实现，否则会漂移）
    ///
    /// 所以规则只能有**一处**实现 —— 全部收在这里，三边都调它。
    /// </summary>
    public static class StatusRules
    {
        /// <summary>
        /// **强酸 / 震慑** 对攻防加成的修正（已累加状态带来的变化）。
        ///
        /// · 强酸：防御加成 **−2**
        /// · 震慑：攻击加成 **−2**
        /// </summary>
        public static int EffectiveAttackBonus(PlayerState p)
        {
            int bonus = p?.Character?.AttackBonus ?? 0;
            if (p != null && p.HasStatus(StatusEffect.Stun)) bonus -= StunAttackPenalty;
            return bonus;
        }

        /// <inheritdoc cref="EffectiveAttackBonus"/>
        public static int EffectiveDefenseBonus(PlayerState p)
        {
            int bonus = p?.Character?.DefenseBonus ?? 0;
            if (p != null && p.HasStatus(StatusEffect.Acid)) bonus -= AcidDefensePenalty;
            return bonus;
        }

        /// <summary>强酸的防御惩罚。</summary>
        public const int AcidDefensePenalty = 2;

        /// <summary>震慑的攻击惩罚。</summary>
        public const int StunAttackPenalty = 2;

        /// <summary>中毒：每回合开始时受到 2 点毒素伤害，**不可被抵挡**。</summary>
        public const int PoisonDamagePerTurn = 2;

        /// <summary>燃烧：每回合结束时受到 3 点火焰伤害。</summary>
        public const int BurnDamagePerTurn = 3;

        /// <summary>感电：每回合开始时自己与相邻玩家各受 1 点闪电伤害。</summary>
        public const int ShockDamagePerTurn = 1;

        // ============================================================
        // 运算结果的修正
        // ============================================================

        /// <summary>
        /// **冰冻**：运算得到的结果**减半（向下取整）**（算出 9 变 4、算出 5 变 2）。
        ///
        /// ⚠️ 必须在"归一化之后、查效果表之前"应用 —— 否则 9 已经先被当成回复处理了。
        /// </summary>
        public static int ApplyResultModifiers(PlayerState actor, int result)
        {
            if (actor == null) return result;
            if (actor.HasStatus(StatusEffect.Freeze)) return result / 2;
            return result;
        }

        // ============================================================
        // 合法性过滤
        // ============================================================

        /// <summary>
        /// **残响**：自己只能攻击与**自己距离 1** 的角色。
        /// 没有该状态时不做限制（返回 true）。
        /// </summary>
        public static bool CanAttackTarget(PlayerState actor, PlayerState target, GameManager game)
        {
            if (actor == null || target == null || game == null) return true;
            if (!actor.HasStatus(StatusEffect.Resonance)) return true;

            return game.DistanceBetween(actor, target) == 1;
        }

        /// <summary>
        /// **震慑**：被震慑者**不能攻击施加者**。
        ///
        /// 玩家确认的表述："是法师（或者说心灵伤害发起者）不能成为被震慑的人的合法目标"。
        /// 所以这里要反过来读：被震慑的人打不到"那个给他挂震慑的法师"。
        ///
        /// ⚠️ 没有记施加者时（例如状态是测试直接挂上的）不做限制 ——
        ///    此时应当由 <see cref="PlayerState.StatusSourceOf"/> 返回 null。
        /// </summary>
        public static bool IsBlockedByStun(PlayerState actor, PlayerState target)
        {
            if (actor == null || target == null) return false;
            if (!actor.HasStatus(StatusEffect.Stun)) return false;

            PlayerState source = actor.StatusSourceOf(StatusEffect.Stun);
            return source != null && ReferenceEquals(source, target);
        }

        /// <summary>
        /// **目盲**：计算时只能选择与**自己距离 1** 的手当被加数。
        ///
        /// 自己两只手的距离是 0（<see cref="GameManager.DistanceBetween"/> 对自己返回 0），
        /// 所以魔术师那种"自加"**不受目盲限制**。
        /// </summary>
        public static bool CanUseHandAsAddend(PlayerState actor, Hand hand, GameManager game)
        {
            if (actor == null || hand == null || game == null) return true;
            if (!actor.HasStatus(StatusEffect.Blind)) return true;

            PlayerState owner = game.OwnerOfHand(hand);
            if (owner == null) return true;
            if (ReferenceEquals(owner, actor)) return true;      // 自己的手 = 距离 0，允许

            return game.DistanceBetween(actor, owner) == 1;
        }

        /// <summary>
        /// **腐朽**：不能回复生命值。
        /// </summary>
        public static bool CanHeal(PlayerState p)
            => p == null || !p.HasStatus(StatusEffect.Decay);

        /// <summary>
        /// 该玩家**两回合掉血类**状态在"回合开始"要结算的伤害。
        ///
        /// 返回 (伤害值, 是否不可被抵挡)。
        ///   · 中毒：2 点，**不可被抵挡**
        ///   · 感电：1 点，**可被抵挡**（由调用方按普通伤害流程处理）
        ///
        /// ⚠️ 这两种伤害**不会再挂回状态**（玩家明确要求），否则会变成无法摆脱的循环。
        /// </summary>
        public static int TurnStartDamage(PlayerState p, out bool unstoppable)
        {
            unstoppable = false;
            if (p == null) return 0;
            if (p.HasStatus(StatusEffect.Poison))
            {
                unstoppable = true;
                return PoisonDamagePerTurn;
            }
            return 0;
        }

        /// <summary>
        /// 回合结束时（**目标自己的回合结束**）结算的燃烧伤害（可被盾 / 闪避 / 太极抵挡）。
        /// </summary>
        public static int TurnEndDamage(PlayerState p)
            => p != null && p.HasStatus(StatusEffect.Burn) ? BurnDamagePerTurn : 0;

        /// <summary>**感电**：自己与环上相邻的玩家都要吃 1 点闪电伤害。</summary>
        public static List<PlayerState> ShockVictims(PlayerState p, GameManager game)
        {
            var list = new List<PlayerState>();
            if (p == null || game == null || !p.HasStatus(StatusEffect.Shock)) return list;

            list.Add(p);
            IReadOnlyList<PlayerState> all = game.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState other = all[i];
                if (other == null || !other.IsAlive) continue;
                if (ReferenceEquals(other, p)) continue;
                if (game.DistanceBetween(p, other) == 1) list.Add(other);
            }
            return list;
        }
    }
}
