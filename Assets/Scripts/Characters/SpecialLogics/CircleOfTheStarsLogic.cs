using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class CircleOfTheStarsLogic:CharacterTemplate,IDruidCaster,IDruidForm
    {
        public enum StarryForm { None, Archer, Chalice, Dragon }
        private PlayerState _owner;
        public StarryForm Form=>(StarryForm)(_owner?.WildStar??0);
        public string CircleKey=>"stars";
        public int FormValue=>(int)Form;
        public bool NeedsSecondAddend=>false;
        public override string DisplayName=>"德鲁伊·星辰结社";
        public override void OnMatchReset(PlayerState owner,GameManager game){_owner=owner;}
        public bool TryEnter(StarryForm form,PlayerState owner,GameManager game)=>owner.EnterWild(1,(int)form,game);
        public bool TryExit(PlayerState owner,GameManager game){if(!owner.IsWildShaped)return false;owner.EndWild();return true;}
    }
}
