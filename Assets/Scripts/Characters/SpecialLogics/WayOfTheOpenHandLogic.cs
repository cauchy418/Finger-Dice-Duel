using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>武僧·散打宗：气、共鸣、倒伏/震慑与混元体。</summary>
    public sealed class WayOfTheOpenHandLogic : CharacterTemplate, IAttackTypeProvider, IKiResource, IPreTurnChoiceProvider
    {
        public const int MaxKi = 6;
        public int Ki { get; private set; } = 2;
        private int? _technique;

        public override string DisplayName => "武僧·散打宗";
        public override int BaseAttackRange => 1;

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            Ki = 2;_technique=null;
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

        public override void CollectPreTurnChoices(PreTurnChoiceContext context,IList<ChoiceRequest> requests)
        {
            if(!CharacterBehaviors.ActorHas(context.Actor,this)) return;
            _technique=null;if(!context.IsAttack) return;
            int ki=System.Math.Min(MaxKi,Ki+(context.Actor.GetOtherHand(context.ActedHand)?.Value==context.EffectiveResult?1:0));
            if(ki<2) return;
            requests.Add(new ChoiceRequest(HandEffect.OpenHandTechnique,"散打 · 选择本次招式",null,decider:context.Actor,
                options:MartialSkillChoices.OpenHandOptions(ki),applyOption:value=>_technique=value,optionKey:MartialSkillChoices.OpenHand));
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

            int choice=_technique??SkillChoicePolicy.Choose(MartialSkillChoices.OpenHand,MartialSkillChoices.OpenHandOptions(Ki),context.Attacker,context.Defender,context.Manager);
            _technique=null;
            if(choice==-1 && Ki>=4)
            {
                Ki-=4;context.ApplyUntimedOnHit(()=>{context.Defender.SetResonance(true);context.Manager?.NoteSkill(context.Attacker,"共鸣之气",$"{context.Defender.Name} 被共鸣");});
            }
            else if(Ki>=2 && (choice==(int)StatusEffect.KnockedDown || choice==(int)StatusEffect.Stunned))
            {
                Ki-=2;var status=(StatusEffect)choice;context.ApplyAttachedStatus(context.Defender,status,2,context.Attacker,context.Manager,"散打");

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
