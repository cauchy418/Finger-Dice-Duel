using System;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// **术士·狂野魔法（WildMagic）**：攻 0 / 防 −1，**攻击距离无限**，混乱善良。
    ///
    /// 玩家确认的规则：
    ///   · 每次攻击**掷一次骰**：10 种元素**各 8%**（合计 80%），
    ///     剩下 **20%** 就是普通攻击
    ///   · 改写成元素时，按该元素**挂对应的负面状态**（力场不挂，走穿盾）
    ///   · **多段攻击（武僧·忍宗 / 战士·剑士追加）每段各掷一次**
    ///   · 命中口径**与法师·塑能学派一致**：被盾抵挡、被太极减免**仍算命中**；
    ///     被闪避、被邪术师·至高妖精取消**不算命中**
    ///   · 普攻（没改写时）：距离 **1 以内钝击**、**1 以外穿刺**
    ///
    /// ⚠️ 掷骰必须能确定性复现 —— 单元测试靠 <see cref="ForcedRoll"/> 指定点数，
    ///    与游荡者·赌徒的 `Roll(Random)` 是同一套思路。
    /// </summary>
    public sealed class WildMagicLogic : CharacterTemplate,
                                        IAttackRangeProvider,
                                        IStatusApplyingAttack,
                                        IDodgeBlocker, IPerHitAttackType
    {
        public override string DisplayName => "术士·狂野魔法";

        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => -1;

        /// <summary>攻击距离**无限**（玩家确认）。</summary>
        public override int BaseAttackRange => InfiniteAttackRange;

        /// <summary>每种元素被掷中的概率（百分比）。10 种 × 8% = 80%。</summary>
        public const int ElementChancePercent = 8;

        /// <summary>可被改写的元素数量（雷鸣…力场，共 10 种）。</summary>
        public const int ElementCount = 10;

        /// <summary>掷骰面数：100 面（0~99），其中 0~79 落在 10 种元素上。</summary>
        public const int RollFaces = 100;

        /// <summary>随机源。测试可注入固定种子的 <see cref="Random"/>。</summary>
        public Random Rng { get; set; } = new Random();

        /// <summary>
        /// **测试钩子**：≥ 0 时**不掷骰**，直接把它当作本次掷出的点数（0~99）。
        /// 负数表示正常随机。
        /// </summary>
        public int ForcedRoll { get; set; } = -1;

        /// <summary>掷一次骰，返回 0~99。</summary>
        public int Roll()
        {
            if (ForcedRoll >= 0) return ForcedRoll % RollFaces;
            Random rng = Rng ?? (Rng = new Random());
            return rng.Next(RollFaces);
        }

        /// <summary>
        /// 把掷出的点数翻译成元素（<see cref="DamageType.None"/> = 不改写、走普攻）。
        ///
        /// 映射：0~7 雷鸣、8~15 强酸 …… 72~79 力场，80~99 普攻。
        /// 顺序与 <see cref="DamageTypesForRoll"/> 一致。
        /// </summary>
        public static DamageType ElementOfRoll(int roll)
        {
            if (roll < 0 || roll >= ElementChancePercent * ElementCount)
                return DamageType.None;

            return DamageTypesForRoll[roll / ElementChancePercent];
        }

        /// <summary>掷骰可落到的 10 种元素（顺序即掷骰区间顺序）。</summary>
        public static readonly DamageType[] DamageTypesForRoll =
        {
            DamageType.Thunder,   // 雷鸣
            DamageType.Acid,      // 强酸
            DamageType.Poison,    // 毒素
            DamageType.Radiant,   // 光耀
            DamageType.Necrotic,  // 暗蚀
            DamageType.Fire,      // 火焰
            DamageType.Cold,      // 寒冷
            DamageType.Lightning, // 闪电
            DamageType.Psychic,   // 心灵
            DamageType.Force,     // 力场
        };

        // ============================================================
        // 结算钩子
        // ============================================================

        /// <summary>
        /// 掷骰并改写这次攻击（每段各掷一次）。
        ///
        /// ⚠️ 必须在 `OnResolveAttack` 里做，因为
        ///    `AttackContext` 是在**这个钩子之后**才去问"伤害类型"的，
        ///    在这里写 <see cref="AttackContext.AttackType"/> 才能盖掉普攻的类型。
        /// </summary>
        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null || context.IsReflected || context.IsStatusDamage) return;
            context.PerHitTypeProvider = this;
            PrepareAttackHit(context, 0);
            context.PreparedPerHitType = context.AttackType;
        }

        public void PrepareAttackHit(AttackContext context, int hitIndex)
        {
            var element = ElementOfRoll(Roll());
            context.ElementOverride = element;
            _forceThisAttack = element == DamageType.Force;
            context.AttackType = element == DamageType.None
                ? ((context.Manager?.DistanceBetween(context.Attacker, context.Defender) ?? LastAttackDistance) <= 1 ? AttackDamageType.Bludgeoning : AttackDamageType.Piercing)
                : AttackDamageTypes.FromDamageType(element);
            context.Manager?.NoteSkill(context.Attacker, "狂野魔法浪涌", $"第{hitIndex + 1}段 {AttackDamageTypes.NameOf(context.AttackType)}",context.Defender,new SkillVisualData(source:context.SourceHand,value:hitIndex,element:context.AttackType));
        }

        /// <summary>
        /// 本次攻击要挂上的状态。
        /// 力场不挂状态（只有穿透），普攻也不挂。
        /// </summary>
        public StatusEffect StatusToApplyOnHit(AttackContext context)
        {
            if (context == null) return StatusEffect.None;
            return StatusNames.StatusOf(context.ElementOverride);
        }

        /// <summary>力场攻击不可被闪避（与法师·咒法学派同源）。</summary>
        public bool PreventsDodge => _forceThisAttack;

        private bool _forceThisAttack;

        // ============================================================
        // 伤害类型
        // ============================================================

        /// <summary>
        /// 没被改写时的普通攻击：距离 1 以内**钝击**、1 以外**穿刺**。
        ///
        /// （被改写时 `AttackType` 已在 <see cref="OnResolveAttack"/> 里写好，
        ///   `AttackContext` 不会再问这里 —— 这里只兜底普攻。）
        /// </summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => ByDistance(AttackDamageType.Bludgeoning, AttackDamageType.Piercing);
    }
}
