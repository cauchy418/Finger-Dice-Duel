using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class SchoolOfAbjurationLogic : CharacterTemplate,
        IAttackTypeProvider, IIncomingDamageBarrier, IAbjurationSkill
    {
        public override string DisplayName => "法师·防护学派";
        public override int DefaultAttackBonus => 0;
        public override int ScoreMoveBonus(MoveValueContext context) => context.ActedValueAfter==2?32:0;
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;
        private PlayerState _owner,_resistanceTarget;
        public int BarrierPoints { get; private set; }
        private AttackDamageType _selectedResistance=AttackDamageType.Bludgeoning;
        public AttackDamageType SelectedResistance
        { get => _selectedResistance; set { if(ResistanceNames.CanGrant(value)) _selectedResistance=value; } }
        public void StoreBarrier(int points) { if (points>0) BarrierPoints+=points; }
        public void GrantResistance(PlayerState target, AttackDamageType type)
        {
            if(!ResistanceNames.CanGrant(type))return;
            _resistanceTarget?.RemoveSustainedResistance(this);_resistanceTarget=target;
            target?.GrantSustainedResistance(this,type,()=>_owner==null||_owner.IsAlive);
        }
        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {_resistanceTarget?.RemoveSustainedResistance(this);_resistanceTarget=null;_owner=owner;BarrierPoints=0; SelectedResistance=AttackDamageType.Bludgeoning; }

        public override void CollectPreTargetChoices(PreTurnChoiceContext context, IList<ChoiceRequest> requests)
        {
            if(context==null || !CharacterBehaviors.ActorHas(context.Actor,this)) return;
            if(context.EffectiveSum==2)
            {
                var targets=new List<Hand>();
                if(context.Manager!=null) foreach(var p in context.Manager.Players)
                    if(p.IsAlive) { targets.Add(p.LeftHand); targets.Add(p.RightHand); }
                if(targets.Count==0) targets.Add(context.Actor.LeftHand);
                requests.Add(new ChoiceRequest(HandEffect.SchoolOfAbjurationResistanceTarget,
                    "【法术抗性】选择一名存活角色，再选择物理整组或雷鸣、强酸、火焰、寒冷、闪电抗性。",targets,
                    allowDecline:false,decider:context.Actor));
                requests.Add(new ChoiceRequest(HandEffect.SchoolOfAbjurationResistanceType,
                    "【法术抗性】选择抗性类型；物理整组包含挥砍、钝击和穿刺。",
                    new List<Hand>{context.ActedHand},allowDecline:false,decider:context.Actor));
            }
            else if(context.IsAttack)
                requests.Add(new ChoiceRequest(HandEffect.SchoolOfAbjurationBarrier,
                    "【奥术守御】放弃这次攻击，将攻击数字存为屏障？",
                    new List<Hand>{context.ActedHand},allowDecline:true,decider:context.Actor,acceptLabel:"存储奥术守御"));
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
            int actedHandValueBefore, int targetValue, HandEffectType effect, GameManager manager, IPreTurnChoiceSink sink)
        {
            if(owner==null || actedHand?.Value!=2) return;
            Hand picked=manager?.AnsweredLookupFor?.Invoke(owner,HandEffect.SchoolOfAbjurationResistanceTarget,0)
                ?? sink?.GetExplicitChoice(HandEffect.SchoolOfAbjurationResistanceTarget);
            PlayerState target=owner;
            if(picked!=null && manager!=null)
                foreach(var p in manager.Players) if(p.IsAlive && p.Owns(picked)) {target=p;break;}
            if(manager!=null && manager.AnsweredLookupFor==null && manager.WizardPolicy(owner) is IWizardChoicePolicy policy)
            {
                target=policy.ResistanceTarget(owner,manager)??owner;
                SelectedResistance=policy.ResistanceType(owner,target,manager);
            }
            GrantResistance(target,SelectedResistance);
            string label=AttackDamageTypes.IsMagic(SelectedResistance)?ResistanceNames.Name(SelectedResistance):"物理抗性";
            manager?.NoteSkill(owner,"法术抗性",$"赋予{target.Name}{label}，持续至施法者死亡或再次施法",target,new SkillVisualData(variant:"grant"));
        }
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => (context?.Manager?.DistanceBetween(context.Attacker,context.Defender)??1)<=1
                ?AttackDamageType.Bludgeoning:AttackDamageType.Piercing;
        public int AbsorbDamage(PlayerState target, int damage, AttackContext context, GameManager manager)
        {
            if(damage<=0 || BarrierPoints<=0) return damage;
            int absorbed=System.Math.Min(damage,BarrierPoints); BarrierPoints=0; return damage-absorbed;
        }
    }
}
