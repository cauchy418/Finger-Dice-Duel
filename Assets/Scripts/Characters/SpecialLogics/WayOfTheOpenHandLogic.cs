using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>武僧·散打宗：气、共鸣、倒伏/震慑与混元体。</summary>
    public sealed class WayOfTheOpenHandLogic : CharacterTemplate, IAttackTypeProvider
    {
        public const int MaxKi = 6;
        public int Ki { get; private set; } = 2;

        public override string DisplayName => "武僧·散打宗";
        public override int BaseAttackRange => 1;

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            Ki = 2;
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
                                                  int actedHandValueBefore, int targetValue,
                                                  HandEffectType effect, GameManager manager,
                                                  IPreTurnChoiceSink choiceSink)
        {
            if (owner?.LeftHand == null || owner.RightHand == null) return;
            if (owner.LeftHand.Value == owner.RightHand.Value)
                Ki = System.Math.Min(MaxKi, Ki + 1);
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context?.Defender == null) return;

            // 引爆旧共鸣：按正常命中、尚未经护盾/抗性处理的伤害取半。
            if (context.Defender.HasResonance)
            {
                int expected = context.FinalDamage
                             + StatusRules.EffectiveAttackBonus(context.Attacker)
                             - StatusRules.EffectiveDefenseBonus(context.Defender);
                if (expected < 0) expected = 0;
                context.ExtraTrueDamage += expected / 2;
                context.Defender.SetResonance(false);
                context.Manager?.NoteSkill(context.Attacker, "共鸣引爆", $"额外 {expected / 2} 点真实伤害");
            }

            if (Ki >= 4)
            {
                Ki -= 4;
                context.Defender.SetResonance(true);
                context.Manager?.NoteSkill(context.Attacker, "共鸣之气", $"{context.Defender.Name} 被共鸣");
            }
            else if (Ki >= 2)
            {
                Ki -= 2;
                context.Defender.ApplyStatus(StatusEffect.KnockedDown, 2, context.Attacker);
                context.Manager?.NoteSkill(context.Attacker, "散打", $"{context.Defender.Name} 倒伏 2 回合");
            }
        }

        /// <summary>回合开始、气点等于当前体力时，可跳过回合发动混元体。</summary>
        public bool TryHarmonyBody(PlayerState owner, GameManager manager)
        {
            if (owner == null || manager == null || !owner.IsAlive) return false;
            if (Ki <= 0 || Ki != owner.Health) return false;
            int heal = Ki * 2;
            Ki = 0;
            owner.Heal(heal);
            manager.NoteSkill(owner, "混元体", $"移除气点，回复 {heal}");
            manager.SkipTurnAlreadyValidated();
            return true;
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Bludgeoning;
    }
}
