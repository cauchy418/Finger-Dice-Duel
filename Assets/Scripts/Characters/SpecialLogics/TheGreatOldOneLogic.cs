using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// **邪术师·旧日支配者（TheGreatOldOne）**：攻 0 / 防 −1，**攻击距离无限**，中立邪恶。
    ///
    /// 玩家确认的规则：
    ///   · **你命中的攻击**（闪避 / 取消除外）令**你与目标之间的距离 +2**
    ///   · 持续到**目标自己的 2 个回合**结束（1v1 里就是"对手之后两个回合打不到你"）
    ///   · 距离**只影响你与他之间**，不改变其他玩家之间的距离
    ///   · 再次命中**只刷新时长、不叠加**（+2 封顶）、**不封顶**
    ///   · 攻击类型：距离 **1 以内挥砍**、**1 以外穿刺**
    ///
    /// ⚠️ 1v1 里这会构成"对手两回合无法攻击"的强控（玩家确认按此保留）。
    ///    此时界面会自动给出「放弃攻击」出口 —— 因为
    ///    `BoardController.CanBeAttacked` 里就含 `CanReach` 判定。
    /// </summary>
    public sealed class TheGreatOldOneLogic : CharacterTemplate,
                                       IAttackRangeProvider,
                                       IOnHitLanded
    {
        public override string DisplayName => "邪术师·旧日支配者";

        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => -1;

        /// <summary>攻击距离**无限**（玩家确认）。</summary>
        public override int BaseAttackRange => InfiniteAttackRange;

        /// <summary>命中后加多少距离（玩家确认 +2）。</summary>
        public const int DistancePenalty = 2;

        /// <summary>
        /// 命中即加距离。
        ///
        /// 距离修正由 `GameManager` 保管（成对、限时、刷新不叠加），
        /// 这里只管"通知他加"。
        /// </summary>
        public void OnHitLanded(AttackContext context, GameManager manager)
        {
            if (context == null || manager == null) return;

            manager.ApplyDistancePenalty(context.Attacker, context.Defender, DistancePenalty);
            manager.NoteSkill(context.Attacker, "异界疏离", $"{context.Defender.Name} 到自身的距离＋{DistancePenalty}");
        }

        /// <summary>普通攻击：距离 1 以内**挥砍**、1 以外**穿刺**。</summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => ByDistance(AttackDamageType.Slashing, AttackDamageType.Piercing);
    }
}
