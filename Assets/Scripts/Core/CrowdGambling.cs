using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public partial class GameManager
    {
        // Earliest global action serial: self-targeting starts after this main action.
        // Expiry belongs to the target's next own main turn, including a skipped turn.
        private readonly Dictionary<PlayerState,int> _crowdGambling = new Dictionary<PlayerState,int>();
        public bool HasCrowdGambling(PlayerState player) => player!=null && _crowdGambling.ContainsKey(player);
        public bool RequiresCrowdGambling(PlayerState player,bool extraAction=false)
            => !extraAction && player!=null && _crowdGambling.TryGetValue(player,out int after) && TurnCount>=after;
        public bool MustUseGambling(PlayerState player,bool extraAction=false)
            => player!=null && (CharacterBehaviors.FirstImplementer<GamblerLogic>(player) != null && player.MustGamble || RequiresCrowdGambling(player,extraAction));

        private void ExpireCrowdGambling(PlayerState actor)
        {
            if(actor!=null && _crowdGambling.TryGetValue(actor,out int after) && TurnCount>after)
                _crowdGambling.Remove(actor);
        }

        public void OfferCrowdGambling(PlayerState owner,Hand source)
        {
            if(owner?.IsAlive!=true || owner.IsPolymorphed || source?.Value!=1 || IsGameOver)return;
            int offeredAt=TurnCount;
            var hands=new List<Hand>();
            foreach(var player in Players)if(player.IsAlive)hands.Add(player.LeftHand);
            var choice=new WizardChoice(owner,"聚众赌博 · 选择一名存活角色，令其下个自己的回合必须赌博，或不发动",hands,true,hand=>{
                var target=OwnerOfHand(hand);
                if(owner.IsAlive && !owner.IsPolymorphed && target?.IsAlive==true)
                {
                    var before=CaptureVisualFrame();
                    _crowdGambling[target]=offeredAt+(ReferenceEquals(owner,target)?1:0);
                    NoteSkill(owner,"聚众赌博",target.Name+"下个自己的回合必须赌博（不叠加）",target,
                        new SkillVisualData(source:source,value:1,variant:"forced"),before);
                }
            },true,"不发动聚众赌博");
            if(DeferWizardChoice?.Invoke(choice)!=true)
                choice.Choose(CrowdGamblingPolicy.Choose(owner,hands,this));
        }
    }

    /// <summary>Also used by headless and UI AI: deny a strong enemy operation or rescue a weak friendly one.</summary>
    public static class CrowdGamblingPolicy
    {
        public static Hand Choose(PlayerState owner,IReadOnlyList<Hand> candidates,GameManager game)
        {
            Hand best=null;double bestScore=0;
            foreach(var hand in candidates)
            {
                var target=game.OwnerOfHand(hand);
                if(target?.IsAlive!=true || game.HasCrowdGambling(target) || game.MustUseGambling(target))continue;
                double normal=0;
                foreach(var move in game.GetLegalMoves(target))
                {
                    int value=game.PreviewOperationResult(target,move.ActedHand,move.TargetHand);
                    double quality=value==4||value==6||value==8?value:value==9&&target.Health<target.MaximumHealth?4:value==5?2:0;
                    if(quality>normal)normal=quality;
                }
                double score=game.IsKnownAlly(owner,target)?2.5-normal:normal-2.5;
                if(score>bestScore){best=hand;bestScore=score;}
            }
            return best;
        }
    }
}
