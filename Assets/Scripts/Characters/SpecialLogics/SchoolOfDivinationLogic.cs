using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class SchoolOfDivinationLogic : CharacterTemplate, IPortentBook
    {
        private readonly HashSet<int> _portents=new HashSet<int>();
        public IReadOnlyCollection<int> Portents=>_portents;
        public override string DisplayName=>"法师·预言学派";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override int ScoreMoveBonus(MoveValueContext context)=>context.ActedValueAfter==7?35:0;
        public void RecordPortent(int value){if(value>=0&&value<=9)_portents.Add(value);}
        public bool ConsumePortent(int value)=>_portents.Remove(value);
        public override void OnMatchReset(PlayerState owner,GameManager manager)=>_portents.Clear();
        public override void OnOperationResolved(PlayerState owner,Hand hand,int before,int add,HandEffectType effect,GameManager manager,IPreTurnChoiceSink sink)
        {
            if(hand?.Value!=7)return;
            int value=owner.GetOtherHand(hand).Value;
            if(_portents.Add(value))manager?.NoteSkill(owner,"预兆","记录"+value,owner,new SkillVisualData(source:owner.GetOtherHand(hand),value:value));
        }
        public override AttackDamageType AttackTypeOf(AttackContext context)=>ByDistance(AttackDamageType.Bludgeoning,AttackDamageType.Piercing);
    }
}
