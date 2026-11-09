using System;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class PathOfTheBeastLogic:BarbarianTemplate,ITurnEndHook
    {
        public const int TriggerSum=7,HealthBonus=5,GrowthPerTrigger=1;
        public int TriggerCount {get;private set;}
        public int BeastSouls {get;private set;}
        public override string DisplayName=>"野蛮人·野兽道途";
        public override void OnOperationResolved(PlayerState owner,Hand hand,int before,int add,HandEffectType effect,GameManager game,IPreTurnChoiceSink sink)
        {
            if(owner.LeftHand.Value+owner.RightHand.Value!=7)return;
            BeastSouls++;game?.NoteSkill(owner,"野兽形态","兽性之魂+1，当前"+BeastSouls,owner,new SkillVisualData(value:BeastSouls));
        }
        public override int ScoreMoveBonus(MoveValueContext context)
        {
            if(BarbarianOwner==null||context.ActedValueAfter+context.OtherHandValue!=7)return 0;
            int threshold=Math.Min(StatusRules.EffectiveAttackBonus(BarbarianOwner),StatusRules.EffectiveDefenseBonus(BarbarianOwner));
            return BeastSouls+1>=threshold?75+(BarbarianOwner.MaximumHealth-BarbarianOwner.Health)*3:15;
        }
        public void OnTurnEnded(PlayerState owner,GameManager game)
        {
            if(!owner.IsAlive||BeastSouls<=0||BeastSouls<Math.Min(StatusRules.EffectiveAttackBonus(owner),StatusRules.EffectiveDefenseBonus(owner)))return;
            BeastSouls=0;TriggerCount++;int heal=(owner.MaximumHealth-owner.Health)/2;
            owner.Heal(heal,game);GrowStats(1,1);
            game?.NoteSkill(owner,"野兽形态","清除兽性之魂，回复"+owner.LastActualHealing+"，永久攻防+1",owner,new SkillVisualData(value:1));
        }
        public override void OnMatchReset(PlayerState owner,GameManager game){BindBarbarianOwner(owner);BeastSouls=0;TriggerCount=0;ResetStats();}
    }
}
