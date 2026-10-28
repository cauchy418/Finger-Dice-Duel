using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>邪术师·妖魔：黑暗赐福将治疗溢出转虚假生命，攻击可消耗虚假生命施加失能/恐慌。</summary>
    public sealed class TheFiendLogic : CharacterTemplate,
        IAttackTypeProvider, IHealingOverflow, IOnDamageDealt, IPreTurnChoiceProvider
    {
        private int? _pactChoice;
        public override string DisplayName => "邪术师·妖魔";
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;
        public override void OnMatchReset(PlayerState owner,GameManager manager) { _pactChoice=null; }
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,System.Collections.Generic.IList<ChoiceRequest> requests)
        {
            if(!CharacterBehaviors.ActorHas(context.Actor,this))return;
            _pactChoice=null;
            if(!context.IsAttack || context.Actor.FalseLife<=0)return;
            requests.Add(new ChoiceRequest(HandEffect.TheFiendPact,"黑暗强运 · 选择控制或保留虚假生命",null,decider:context.Actor,
                options:AncientRoleChoices.PactOptions(),applyOption:value=>_pactChoice=value,optionKey:AncientRoleChoices.FiendPact));
        }

        public void OnHealingOverflow(PlayerState owner, int overflow)
        {
            owner?.GainFalseLife(overflow);
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context?.Attacker == null || context.Defender == null || context.IsStatusDamage) return;
            if (context.Attacker.FalseLife <= 0) return;
            int choice=_pactChoice??SkillChoicePolicy.Choose(AncientRoleChoices.FiendPact,AncientRoleChoices.PactOptions(),context.Attacker,context.Defender,context.Manager);
            _pactChoice=null;
            if(choice!=(int)StatusEffect.Disabled && choice!=(int)StatusEffect.Panic)return;
            var status=(StatusEffect)choice;
            context.Attacker.ClearFalseLife();
            context.ApplyAttachedStatus(context.Defender,status,2,context.Attacker,context.Manager,"黑暗强运");
            context.Manager?.NoteSkill(context.Attacker, "黑暗强运", $"耗尽虚假生命，准备附加{StatusNames.Of(status)}");
        }

        public void OnHitLanded(AttackContext context, GameManager manager)
        {
            if (context == null || context.IsStatusDamage || context.AppliedDamage <= 0 || context.Attacker == null || !context.Attacker.IsAlive) return;
            if (context.Defender != null
                && context.Defender.Health * 2 <= context.Defender.MaximumHealth)
            {
                context.Attacker.Heal(3);
                manager?.NoteSkill(context.Attacker, "直坠噩梦", "回复 3 点");
            }
        }
        public void OnDamageDealt(AttackContext context,GameManager manager) => OnHitLanded(context,manager);

        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            int distance = context?.Manager?.DistanceBetween(context.Attacker, context.Defender) ?? 1;
            return distance <= 1 ? AttackDamageType.Bludgeoning : AttackDamageType.Piercing;
        }
    }
}
