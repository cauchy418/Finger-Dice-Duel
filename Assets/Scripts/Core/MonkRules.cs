using System;
using System.Collections.Generic;
namespace FingerDiceDuel.Core
{
    public interface IMonkResource : IKiResource { void BindMonkOwner(PlayerState owner); }
    public interface IDrunkenStance { bool Drunken { get; } void EndDrunken(); int RollOutgoing(); int RollIncoming(); }
    public static class MonkRules
    {
        public const string Ninja="monk_ninja", Taichi="monk_taichi_boost", Defense="monk_element_defense", Harmony="monk_harmony", Drunken="monk_drunken";
        public static List<SkillChoiceOption> YesNo(string label,string icon="Effects/ki") => new List<SkillChoiceOption>{new SkillChoiceOption(1,label,icon),new SkillChoiceOption(0,"不发动",icon)};
        public static void Offer(PlayerState owner,GameManager game,string key,string prompt,IReadOnlyList<SkillChoiceOption> options,Action<int> apply)
        {
            var request=new WizardChoice(owner,prompt,options,apply);
            if(game.DeferWizardChoice?.Invoke(request)!=true)request.ChooseOption(SkillChoicePolicy.Choose(key,options,owner,null,game));
        }
        public static void Bind(PlayerState owner){foreach(var monk in CharacterBehaviors.Implementers<IMonkResource>(owner))monk.BindMonkOwner(owner);}
        public static void AfterAction(PlayerState owner)
        {if(owner?.IsAlive==true&&!owner.IsPolymorphed&&CharacterBehaviors.FirstImplementer<IMonkResource>(owner)!=null&&owner.LeftHand.Value==owner.RightHand.Value)owner.GainMonkKi();}
        public static IDrunkenStance DrunkenOf(PlayerState owner)
        {var stance=CharacterBehaviors.FirstImplementer<IDrunkenStance>(owner);return stance?.Drunken==true?stance:null;}
    }
    public partial class PlayerState
    {
        public int MonkKi {get;private set;}=2;
        public void ResetMonkKi()=>MonkKi=2;
        public void GainMonkKi(int amount=1){if(amount>0)MonkKi=Math.Min(6,MonkKi+amount);}
        public bool TrySpendMonkKi(int amount){if(amount<0||MonkKi<amount)return false;MonkKi-=amount;return true;}
        public PlayerState HiddenForceSource {get;private set;}
        public void SetHiddenForce(PlayerState source){if(HasResonance||source==null)return;HasResonance=true;HiddenForceSource=source;}
    }
}
