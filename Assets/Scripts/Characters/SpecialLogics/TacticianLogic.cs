using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    public interface ITacticianOperationPolicy
    {
        bool ChooseSubtraction(GameManager game, PlayerState actor, Hand source, Hand operand);
    }

    /// <summary>战士·战术家：普通运算可选加法或减法。</summary>
    public sealed class TacticianLogic : CharacterTemplate
    {
        public override string DisplayName => "战士·战术家";
        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => 0;
        public override int BaseAttackRange => 1;

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Slashing;
    }
}
