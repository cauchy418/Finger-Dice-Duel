using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>术士·龙族血脉：选择一种元素，攻击与抗性永久转化为该元素。</summary>
    public sealed class DraconicBloodlineLogic : CharacterTemplate,
        IAttackTypeProvider, ITurnStartHook, IStatusApplyingAttack
    {
        private PlayerState _owner;
        private bool _chosen, _queued;
        public AttackDamageType Element { get; private set; } = AttackDamageType.Fire;

        public override string DisplayName => "术士·龙族血脉";
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;

        public void ChooseElement(AttackDamageType element)
        {
            if (!IsDraconicElement(element)) return;
            if(_chosen) return;
            Element = element;_chosen=true;_queued=false;
            _owner?.AddPermanentResistance(element);
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _owner=owner;_chosen=false;_queued=false;Element=AttackDamageType.Fire;
        }

        public void OnTurnStarted(PlayerState owner,GameManager manager)
        {
            if(_chosen || _queued || !ReferenceEquals(manager.CurrentPlayer,owner)) return;
            var options=MartialSkillChoices.Elements(true);_queued=true;
            var request=new WizardChoice(owner,"龙族血脉 · 选择本局元素",options,value=> {
                ChooseElement((AttackDamageType)value);manager.NoteSkill(owner,"龙族血脉",AttackDamageTypes.NameOf(Element)+"血脉 / 永久抗性");
            });
            if(manager.DeferWizardChoice?.Invoke(request)==true) return;
            request.ChooseOption(SkillChoicePolicy.Choose(MartialSkillChoices.Draconic,options,owner,null,manager));
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context != null) context.StatusDuration = 3;
        }

        public override AttackDamageType AttackTypeOf(AttackContext context) => Element;

        public StatusEffect StatusToApplyOnHit(AttackContext context)
            => StatusNames.StatusOf(AttackDamageTypes.ToDamageType(context.AttackType));

        public static bool IsDraconicElement(AttackDamageType type)
            => type == AttackDamageType.Cold
            || type == AttackDamageType.Fire
            || type == AttackDamageType.Lightning
            || type == AttackDamageType.Poison
            || type == AttackDamageType.Acid;
    }
}
