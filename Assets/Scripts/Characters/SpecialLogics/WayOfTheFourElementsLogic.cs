using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class WayOfTheFourElementsLogic : MonkLogic, IIncomingElementReaction, IStatusApplyingAttack
    {
        private int? _elementChoice;private bool? _preparedDefense;private AttackContext _defenseAttack;private bool _defend;
        public AttackDamageType ChosenElement {get;set;}=AttackDamageType.Fire;
        public override string DisplayName=>"武僧·四象宗";
        public override void OnMatchReset(PlayerState owner,GameManager game){base.OnMatchReset(owner,game);_elementChoice=null;_preparedDefense=null;_defenseAttack=null;ChosenElement=AttackDamageType.Fire;}
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,IList<ChoiceRequest> requests)
        {
            if(CharacterBehaviors.ActorHas(context.Defender,this)&&context.IsAttack)
            {
                _preparedDefense=null;if(Ki>0)requests.Add(new ChoiceRequest(HandEffect.FourElementsDefense,"四象法门 · 本次受元素攻击时，是否消耗1气获得对应抗性？",null,decider:context.Defender,
                    options:MonkRules.YesNo("四象法门 · 1气"),applyOption:v=>_preparedDefense=v==1,optionKey:MonkRules.Defense,relatedTarget:context.Actor));
            }
            if(!CharacterBehaviors.ActorHas(context.Actor,this))return;_elementChoice=null;if(!context.IsAttack||Ki<1)return;
            requests.Add(new ChoiceRequest(HandEffect.FourElementsTechnique,"四象门徒 · 选择本次元素或不发动",null,decider:context.Actor,
                options:MartialSkillChoices.Elements(false),applyOption:v=>_elementChoice=v,optionKey:MartialSkillChoices.FourElements));
        }
        public override void OnResolveAttack(AttackContext context)
        {
            if(context.IsStatusDamage||context.IsReflected||Ki<1)return;
            int choice=_elementChoice??(context.Manager?.GetSkillPolicy(context.Attacker)!=null?SkillChoicePolicy.Choose(MartialSkillChoices.FourElements,MartialSkillChoices.Elements(false),context.Attacker,context.Defender,context.Manager):(int)ChosenElement);
            _elementChoice=null;if(choice==0||!IsElement((AttackDamageType)choice)||!Spend(1))return;
            context.AttackType=(AttackDamageType)choice;context.VisualWeapon="weapon_fist";context.Manager?.NoteSkill(context.Attacker,"四象门徒","消耗1气，改为"+AttackDamageTypes.NameOf(context.AttackType),context.Defender,new SkillVisualData(source:context.SourceHand,element:context.AttackType));
        }
        public void OnIncomingElementAttack(AttackContext context,GameManager game)
        {
            if(context.IsStatusDamage||context.Defender==null||!IsElement(context.AttackType)||context.Defender.HasResistance(context.AttackType)||Ki<1)return;
            if(!ReferenceEquals(_defenseAttack,context)){_defenseAttack=context;_defend=_preparedDefense??(SkillChoicePolicy.Choose(MonkRules.Defense,MonkRules.YesNo("四象法门 · 1气"),context.Defender,context.Attacker,game)==1);_preparedDefense=null;}
            if(!_defend||!Spend(1))return;context.Defender.AddResistance(context.AttackType);
            game?.NoteSkill(context.Defender,"四象法门","消耗1气，获得"+AttackDamageTypes.NameOf(context.AttackType)+"抗性",context.Defender,new SkillVisualData(element:context.AttackType));
        }
        public StatusEffect StatusToApplyOnHit(AttackContext context)=>StatusNames.StatusOf(AttackDamageTypes.ToDamageType(context.AttackType));
        public static bool IsElement(AttackDamageType type)=>type==AttackDamageType.Fire||type==AttackDamageType.Cold||type==AttackDamageType.Lightning||type==AttackDamageType.Thunder||type==AttackDamageType.Acid;
    }
}
