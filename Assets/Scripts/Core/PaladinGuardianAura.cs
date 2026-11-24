using System.Collections.Generic;
namespace FingerDiceDuel.Core
{
    /// <summary>圣武士共同职业能力；按实际受击座位顺时针询问，一次攻击只接受一位代挡者。</summary>
    public static class PaladinGuardianAura
    {
        public static bool IsPaladin(PlayerState owner)
        {
            if(owner==null||owner.IsPolymorphed||owner.IsWildShaped)return false;
            foreach(var part in CharacterBehaviors.Implementers<CharacterTemplate>(owner))
                if(part.Definition?.Profession?.Id=="paladin")return true;
            return owner.Definition?.Profession?.Id=="paladin";
        }
        public static bool Eligible(PlayerState owner,PlayerState attacker,PlayerState target,GameManager game)
        {
            if(owner?.IsAlive!=true||target?.IsAlive!=true||attacker?.IsAlive!=true||game==null
                ||ReferenceEquals(owner,attacker)||ReferenceEquals(owner,target)||!IsPaladin(owner)||!game.CanReach(owner,target))return false;
            bool devotion=CharacterBehaviors.FirstImplementer<OathOfDevotionLogic>(owner)!=null;
            return Holds(owner.LeftHand,devotion)||Holds(owner.RightHand,devotion);
        }
        private static bool Holds(Hand hand,bool devotion)=>hand!=null&&(hand.Value==5||devotion&&(hand.Value==4||hand.Value==6));
        public static IEnumerable<PlayerState> Clockwise(GameManager game,PlayerState target)
        {
            int start=-1;for(int i=0;i<game.Players.Count;i++)if(ReferenceEquals(game.Players[i],target)){start=i;break;}
            for(int step=1;step<game.Players.Count;step++)yield return game.Players[(start+step)%game.Players.Count];
        }
        public static ChoiceRequest Request(PlayerState owner,PlayerState attacker,PlayerState target,GameManager game,int attackValue=0,int variant=0)
            =>new ChoiceRequest(HandEffect.OathOfDevotionRedirect,$"圣武士【守护灵光】：{target.Name} 正被 {attacker.Name} 攻击，是否由自己承受整次攻击？",
                new List<Hand>{owner.LeftHand,owner.RightHand},true,null,owner,variant:variant,resultValue:attackValue,relatedTarget:target,relatedActor:attacker);
        public static void Collect(PlayerState attacker,PlayerState target,GameManager game,IList<ChoiceRequest> requests,int attackValue=0,int variant=0)
        {
            foreach(var owner in Clockwise(game,target))if(Eligible(owner,attacker,target,game))requests.Add(Request(owner,attacker,target,game,attackValue,variant));
        }
        public static bool Accepts(PlayerState owner,PlayerState attacker,PlayerState target,GameManager game)
        {
            if(!Eligible(owner,attacker,target,game))return false;
            var request=Request(owner,attacker,target,game,game.CurrentRedirectAttack?.RawDamage??0,game.CurrentRedirectAttack?.GuardianChoiceVariant??0);
            // A board has already asked AI and humans alike, in the same order. Read that decision once.
            bool unansweredAi = game.AutomaticChoicesFor?.Invoke(owner) == true
                && game.HasAnsweredChoiceFor?.Invoke(owner,request.Effect,request.Variant) != true;
            Hand chosen = unansweredAi
                ? (game.GetSkillPolicy(owner) as IChoiceProvider ?? game.GetChoiceProvider(owner))?.ChooseHandFor(request,owner)
                : game.AnsweredLookupFor != null ? game.AnsweredLookupFor(owner,request.Effect,request.Variant)
                : PreTurnChoiceReader.Read(game.GetChoiceProvider(owner) as IPreTurnChoiceSink,
                    game.GetSkillPolicy(owner) as IChoiceProvider??game.GetChoiceProvider(owner),request,owner);
            return chosen!=null&&owner.Owns(chosen);
        }
    }
}
