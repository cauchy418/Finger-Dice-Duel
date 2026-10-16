using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 忍者 Ninja（攻防 +1 / −1）
    ///
    /// 技能：攻击改为**两段伤害减半**的攻击 ——
    /// 4 变成两次 2、6 变成两次 3、8 变成两次 4。
    /// 每一段**各自结算攻防加成**（见 <see cref="AttackContext.ApplyBonuses"/>）。
    ///
    /// 两段是两次独立结算：护盾只能挡下其中一段，另一段照常造成伤害
    /// （见 RULES.md §6.3）。
    /// </summary>
    public sealed class NinjaLogic : CharacterTemplate, IMultiHitAttack
    {
        public override string DisplayName => "忍者";

        public override int DefaultAttackBonus => 1;
        public override int DefaultDefenseBonus => -1;

        /// <summary>攻击段数。</summary>
        public const int HitCount = 2;

        /// <summary>供上层提前知道段数（每段各问一次闪避）。</summary>
        public int MaxHits => HitCount;

        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null) return;

            // 只影响自己发起的攻击
            if (!CharacterBehaviors.ActorHas(context.Attacker, this)) return;

            // 把这次攻击拆成两段；伤害在 AttackContext 里平均分配
            context.HitCount = HitCount;
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager) { }
    }
}
