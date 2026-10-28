using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public interface IAncientRoleChoicePolicy
    {
        Hand ChooseAncientTarget(string key, PlayerState owner, IReadOnlyList<Hand> candidates, GameManager game);
    }
    public static class AncientRoleChoices
    {
        public const string LandSeal="land_seal", LandRestrain="land_restrain", AncientRestrain="ancient_restrain", AncientHeal="ancient_heal", FiendPact="fiend_pact";
        public static List<SkillChoiceOption> PactOptions() => new List<SkillChoiceOption> {
            new SkillChoiceOption((int)StatusEffect.Disabled,"耗尽 · 失能","Status/disabled","移除全部虚假生命，令本次攻击目标失能2回合。"),
            new SkillChoiceOption((int)StatusEffect.Panic,"耗尽 · 恐慌","Status/panic","移除全部虚假生命，令本次攻击目标恐慌2回合。"),
            new SkillChoiceOption(0,"保留虚假生命","Effects/false_life","本次不发动黑暗强运。") };

        public static void Offer(string key, string prompt, PlayerState owner, GameManager game,
            List<Hand> candidates, bool optional, bool players, Action<Hand> apply)
        {
            if(owner==null || !owner.IsAlive || game==null || candidates.Count==0) return;
            var request=new WizardChoice(owner,prompt,candidates,optional,hand=>{
                if(!owner.IsAlive || hand==null || game.OwnerOfHand(hand)?.IsAlive!=true)return;
                apply(hand);
            },players,optional?"不发动":"");
            if(game.DeferWizardChoice?.Invoke(request)==true)return;
            var policy=game.GetAncientPolicy(owner);
            Hand pick=policy?.ChooseAncientTarget(key,owner,candidates,game);
            if(policy==null && !optional)pick=candidates[0];
            if(!request.Choose(pick) && !optional)request.Choose(candidates[0]);
        }
        public static List<Hand> OtherPlayers(PlayerState owner, GameManager game, bool inRange)
        {
            var hands=new List<Hand>();
            foreach(var p in game.Players)
                if(p.IsAlive&&!ReferenceEquals(p,owner)&&(!inRange||game.CanReach(owner,p)))hands.Add(p.LeftHand);
            return hands;
        }
    }

    public partial class GameManager
    {
        public Func<PlayerState,IAncientRoleChoicePolicy> AncientPolicyFor { get; set; }
        public IAncientRoleChoicePolicy GetAncientPolicy(PlayerState owner)
            => AncientPolicyFor?.Invoke(owner)??GetChoiceProvider(owner) as IAncientRoleChoicePolicy;
    }
}
