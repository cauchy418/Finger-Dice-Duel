using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class CollegeOfLoreLogic : CharacterTemplate,ILoreRecord,ITurnEndHook,IWholeAttackDefense,IAttackTypeProvider
    {
        private readonly HashSet<int> _records=new HashSet<int>();private bool? _defend;private int _preparedValue;
        public override string DisplayName=>"吟游诗人·逸闻学院";
        public override int DefaultDefenseBonus=>-1;
        public int LoreLevel {get;private set;}
        public int RecordCount=>_records.Count;
        public bool HasRecord(int value)=>_records.Contains(value);
        public bool CanReduceAttack(PlayerState owner,int attackValue)=>HasRecord(attackValue);
        public override int ScoreMoveBonus(MoveValueContext context)
            =>context.ActedValueAfter>=1&&context.ActedValueAfter<=9&&!HasRecord(context.ActedValueAfter)?(RecordCount==8?90:24):0;
        public override int AttackBonus=>base.AttackBonus+LoreLevel;
        public override int DefenseBonus=>base.DefenseBonus+LoreLevel;
        public override int BaseAttackRange=>1+LoreLevel;
        public override void OnMatchReset(PlayerState owner,GameManager manager){_records.Clear();LoreLevel=0;_defend=null;_preparedValue=0;}
        public override void OnOperationResolved(PlayerState owner,Hand hand,int before,int add,HandEffectType effect,GameManager game,IPreTurnChoiceSink sink)
        {if(hand?.Value>=1&&hand.Value<=9&&_records.Add(hand.Value))game?.NoteSkill(owner,"逸闻记录",$"记录{hand.Value}（{RecordCount}/9）");}
        public void OnTurnEnded(PlayerState owner,GameManager game)
        {
            if(!owner.IsAlive||RecordCount!=9)return;
            var options=NewSubclassChoices.YesNo("清空记录 · 成长","Subclasses/bard_college_of_lore","保留九数记录");
            var request=new WizardChoice(owner,"无与伦比技艺 · 清空九数记录，攻／防／距离各+1",options,value=>{
                if(value!=1||RecordCount!=9)return;_records.Clear();LoreLevel++;game.NoteSkill(owner,"无与伦比技艺",$"攻／防／距离各+1（累计{LoreLevel}）");});
            if(game.DeferWizardChoice?.Invoke(request)==true)return;
            request.ChooseOption(SkillChoicePolicy.Choose(NewSubclassChoices.LoreGrowth,options,owner,null,game));
        }
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,IList<ChoiceRequest> requests)
        {
            if(!context.IsAttack||!CharacterBehaviors.ActorHas(context.Defender,this))return;
            _defend=null;_preparedValue=context.EffectiveResult;if(!HasRecord(_preparedValue)||!NewSubclassChoices.NormalDamageCanBeReduced(context))return;
            var options=NewSubclassChoices.YesNo("清记录"+_preparedValue+" · 减半","Subclasses/bard_college_of_lore","保留对应记录");
            requests.Add(new ChoiceRequest(HandEffect.CollegeOfLoreDefense,"切割语",null,decider:context.Defender,options:options,
                applyOption:v=>_defend=v==1,optionKey:NewSubclassChoices.LoreDefense));
        }
        public void ReduceWholeAttack(AttackContext context,GameManager game)
        {
            int value=context.AttackValue;if(!HasRecord(value)||context.IgnoreIncomingModifiers||context.IsStatusDamage)return;
            bool use=_defend==true&&_preparedValue==value;
            if(game?.AnsweredLookupFor==null)use=SkillChoicePolicy.Choose(NewSubclassChoices.LoreDefense,NewSubclassChoices.YesNo("减伤","Subclasses/bard_college_of_lore"),context.Defender,context.Attacker,game)==1;
            _defend=null;if(!use)return;_records.Remove(value);context.HalveWholeAttackDamage();
            game?.NoteSkill(context.Defender,"切割语",$"清除记录{value}，本次伤害减半");
        }
        public override AttackDamageType AttackTypeOf(AttackContext context)=>AttackDamageType.Bludgeoning;
    }
}
