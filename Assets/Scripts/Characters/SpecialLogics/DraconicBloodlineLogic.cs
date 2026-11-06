using System;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>术士·龙族血脉：选择一种元素，获得永久抗性，攻击有八成概率转为该元素。</summary>
    public sealed class DraconicBloodlineLogic : CharacterTemplate,
        IAttackTypeProvider, ITurnStartHook, IStatusApplyingAttack
    {
        private PlayerState _owner;
        private bool _chosen, _queued;
        public AttackDamageType Element { get; private set; } = AttackDamageType.Fire;

        public const int ElementChance = 80;
        public Random Rng { get; set; } = new Random();
        public int ForcedRoll { get; set; } = -1;

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
            var request=new WizardChoice(owner,"龙族先祖 · 选择本局元素",options,value=> {
                ChooseElement((AttackDamageType)value);manager.NoteSkill(owner,"龙族先祖",AttackDamageTypes.NameOf(Element)+"血脉 / 永久抗性",owner,new SkillVisualData(element:Element));
            });
            if(manager.DeferWizardChoice?.Invoke(request)==true) return;
            request.ChooseOption(SkillChoicePolicy.Choose(MartialSkillChoices.Draconic,options,owner,null,manager));
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context.AttackType != AttackDamageType.None || context.IsStatusDamage || context.IsReflected) return;
            int roll = ForcedRoll >= 0 ? ForcedRoll % 100 : Rng.Next(100);
            context.AttackType = roll < ElementChance ? Element : AttackTypeOf(context);
        }

        public override AttackDamageType AttackTypeOf(AttackContext context) => ArcaneChoices.NearFar(context);

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
