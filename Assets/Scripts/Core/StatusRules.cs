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
            if (p != null && p.HasStatus(StatusEffect.Panic)) bonus -= PanicAttackPenalty;
            if (p != null && p.HasStatus(StatusEffect.Bleed)) bonus -= BleedPenalty;

            // 吟游诗人的加成与"角色自身加成"**同层相加**（可正可负）
            if (p != null) bonus += p.BardAttackOffset;
            if (p != null) bonus += p.IdentityAttackBonus;
            if (p != null) bonus += p.DynamicAttackBonus;
            return bonus;
        }

        /// <inheritdoc cref="EffectiveAttackBonus"/>
        public static int EffectiveDefenseBonus(PlayerState p)
        {
            int bonus = p?.Character?.DefenseBonus ?? 0;
            if (p != null && p.HasStatus(StatusEffect.Acid)) bonus -= AcidDefensePenalty;
            if (p != null && p.HasStatus(StatusEffect.Bleed)) bonus -= BleedPenalty;

            // 吟游诗人的加成（与强酸同层相加）
            if (p != null) bonus += p.BardDefenseOffset;
            if (p != null) bonus += p.IdentityDefenseBonus;
            if (p != null) bonus += p.DynamicDefenseBonus;
            return bonus;
        }

        /// <summary>强酸的防御惩罚。</summary>
        public const int AcidDefensePenalty = 2;

        /// <summary>恐慌的攻击惩罚。</summary>
        public const int PanicAttackPenalty = 2;

        /// <summary>流血的攻防惩罚。</summary>
        public const int BleedPenalty = 1;

        /// <summary>兼容旧名：恐慌的攻击惩罚。</summary>
        public const int StunAttackPenalty = PanicAttackPenalty;

        /// <summary>流血每回合开始失去的体力。</summary>
        public const int BleedDamagePerTurn = 1;

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
        ///
        /// ⚠️⚠️ **下限是 1，不能是 0。**
        ///    玩家实测报的 bug："战士被冰冻影响出现了 0，不行，最小是 1"。
        ///    根因：算出 1 时 `1 / 2 == 0`，而 **0 在手牌里是"空手"的意思** ——
        ///    于是那一格变成不能用的死格（界面上显示 `0` 与 `—`）。
        ///    冰冻的正确语义是"削弱"，不是"把手废掉"，所以钳到 1。
        /// </summary>
        public static int ApplyResultModifiers(PlayerState actor, int result)
        {
            if (actor == null) return result;

            if (actor.HasStatus(StatusEffect.Frost))
            {
                result /= 2;
                if (result < 1) result = 1;
            }
            if (actor.HasStatus(StatusEffect.Wet))
            {
                result -= 1;
                if (result < 1) result = 1;
            }
            if (actor.HasStatus(StatusEffect.Dizzy)) return 0;
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
            if (actor.HasStatus(StatusEffect.Stunned)) return false;

            if (actor.HasStatus(StatusEffect.Panic))
            {
                PlayerState source = actor.StatusSourceOf(StatusEffect.Panic);
                if (source != null && ReferenceEquals(source, target)) return false;
            }

            int distance = game.DistanceBetween(actor, target);
            if (actor.HasStatus(StatusEffect.Resonance) && distance != 1) return false;

            int range = StatusRules.EffectiveAttackRange(actor, game);
            if (range >= 0 && distance > range) return false;
            return true;
        }

        /// <summary>**恐慌**：被恐慌者不能攻击施加者。</summary>
        public static bool IsBlockedByPanic(PlayerState actor, PlayerState target)
        {
            if (actor == null || target == null) return false;
            if (!actor.HasStatus(StatusEffect.Panic)) return false;

            PlayerState source = actor.StatusSourceOf(StatusEffect.Panic);
            return source != null && ReferenceEquals(source, target);
        }

        /// <summary>兼容旧名：恐慌来源限制。</summary>
        public static bool IsBlockedByStun(PlayerState actor, PlayerState target)
            => IsBlockedByPanic(actor, target);

        /// <summary>**冻结**：不能行动。</summary>
        public static bool CanAct(PlayerState p) => p == null || !p.HasStatus(StatusEffect.Frozen);

        /// <summary>**跛脚**：攻击距离减半，至少为 1。</summary>
        public static int EffectiveAttackRange(PlayerState actor, GameManager game)
        {
            if (actor == null || game == null) return -1;
            if (!actor.HasStatus(StatusEffect.Limp)) return -1;
            int baseRange = GameManager.BaseRangeOf(actor);
            int range = baseRange / 2;
            return range < 1 ? 1 : range;
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
            int total = 0;
            if (p.HasStatus(StatusEffect.Poison))
            {
                unstoppable = true;
                total += PoisonDamagePerTurn;
            }
            if (p.HasStatus(StatusEffect.Bleed))
            {
                unstoppable = true;
                total += BleedDamagePerTurn;
            }
            return total;
        }

        /// <summary>
        /// 回合结束时（**目标自己的回合结束**）结算的燃烧伤害（可被盾 / 闪避 / 太极抵挡）。
        /// </summary>
        public static int TurnEndDamage(PlayerState p)
            => p != null && p.HasStatus(StatusEffect.Burn) ? BurnDamagePerTurn : 0;

        private static readonly System.Random _interactionRng = new System.Random();

        /// <summary>
        /// 伤害类型与目标状态的联动。
        /// 返回修改后的伤害；同时给出命中后要附加/清除的状态。
        /// </summary>
        public static int ApplyDamageTypeInteraction(
            PlayerState target, DamageType type, int damage,
            out StatusEffect statusToApply, out StatusEffect statusToClear)
        {
            statusToApply = StatusEffect.None;
            statusToClear = StatusEffect.None;
            if (target == null || damage <= 0) return damage;

            switch (type)
            {
                case DamageType.Thunder:
                    if (target.HasStatus(StatusEffect.Resonance))
                    {
                        statusToClear = StatusEffect.Resonance;
                        statusToApply = StatusEffect.Dizzy;
                    }
                    else statusToApply = StatusEffect.Resonance;
                    break;

                case DamageType.Fire:
                    if (target.HasStatus(StatusEffect.Frost) || target.HasStatus(StatusEffect.Wet))
                    {
                        statusToClear = StatusEffect.Frost; // 两个都清由调用方处理
                        damage = (damage * 3 + 1) / 2;
                        statusToApply = StatusEffect.None;
                    }
                    else statusToApply = StatusEffect.Burn;
                    break;

                case DamageType.Cold:
                    if (target.HasStatus(StatusEffect.Frozen))
                    {
                        damage = (damage * 3 + 1) / 2;
                    }
                    else if (target.HasStatus(StatusEffect.Burn))
                    {
                        statusToClear = StatusEffect.Burn;
                        damage = (damage * 3 + 1) / 2;
                    }
                    else if (target.HasStatus(StatusEffect.Frost) || target.HasStatus(StatusEffect.Wet))
                    {
                        statusToClear = StatusEffect.Frost;
                        statusToApply = StatusEffect.Frozen;
                    }
                    else statusToApply = StatusEffect.Frost;
                    break;

                case DamageType.Lightning:
                    if (target.HasStatus(StatusEffect.Wet))
                        damage = (damage * 3 + 1) / 2;
                    statusToApply = StatusEffect.Shock;
                    break;

                case DamageType.Psychic:
                    statusToApply = _interactionRng.Next(2) == 0
                        ? StatusEffect.Panic : StatusEffect.Stunned;
                    break;

                default:
                    statusToApply = StatusNames.StatusOf(type);
                    break;
            }
            return damage;
        }

        /// <summary>清除指定状态（兼容火焰同时清除覆霜 + 潮湿）。</summary>
        public static void ClearInteractionStatuses(PlayerState target, DamageType type)
        {
            if (target == null) return;
            if (type == DamageType.Fire)
            {
                target.RemoveStatus(StatusEffect.Frost);
                target.RemoveStatus(StatusEffect.Wet);
            }
            else if (type == DamageType.Cold)
            {
                if (target.HasStatus(StatusEffect.Frost)) target.RemoveStatus(StatusEffect.Frost);
                if (target.HasStatus(StatusEffect.Wet)) target.RemoveStatus(StatusEffect.Wet);
                if (target.HasStatus(StatusEffect.Burn)) target.RemoveStatus(StatusEffect.Burn);
            }
            else if (type == DamageType.Thunder)
            {
                target.RemoveStatus(StatusEffect.Resonance);
            }
        }

        /// <summary>**倒伏**：受到伤害 ×1.5，向上取整。</summary>
        public static int ApplyIncomingDamage(PlayerState defender, int damage)
        {
            if (defender == null || damage <= 0) return damage;
            if (!defender.HasStatus(StatusEffect.KnockedDown)) return damage;
            return (damage * 3 + 1) / 2;
        }

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
