using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class CircleOfTheMoonLogic:CharacterTemplate,IDruidCaster,IDruidForm
    {
        public enum WildShape { None, Bear, Wolf, Eagle, Snake, Owlbear, Panther }
        private PlayerState _owner;
        public WildShape Form=>(WildShape)(_owner?.WildForm??0);
        public string CircleKey=>"moon";
        public int FormValue=>(int)Form;
        public bool NeedsSecondAddend=>false;
        public HandEffectType DisplayedEffectOf(int value)=>_owner?.Character is WildShapeBehavior wild?wild.DisplayedEffectOf(value):Hand.GetEffectType(value);
        public override string DisplayName=>"德鲁伊·月光结社";
        public override void OnMatchReset(PlayerState owner,GameManager game){_owner=owner;}
        public bool TryEnter(WildShape form,PlayerState owner,GameManager game)=>owner.EnterWild((int)form,0,game);
        public bool TryExit(PlayerState owner,GameManager game){if(!owner.IsWildShaped)return false;owner.EndWild();return true;}
    }
}
