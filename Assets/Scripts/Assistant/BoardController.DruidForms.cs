using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class BoardController
    {
        private PreTurnChoiceContext DruidChoiceContext(PlayerState actor,PlayerState defender,Hand acted,Hand target)
            {var context=new PreTurnChoiceContext(actor,defender,acted,target){Manager=_game};context.ResultValue=context.EffectiveResult;return context;}
        private bool TryBeginDragonAddend(Hand first)
        {
            if(!DruidForms.IsDragon(ActivePlayer)||_game.DruidSecondAddend!=null)return false;
            var candidates=new List<Hand>();foreach(var move in _game.GetLegalMoves(ActivePlayer))if(ReferenceEquals(move.ActedHand,SelectedHand))candidates.Add(move.TargetHand);
            if(candidates.Count==0)return false;
            var actor=ActivePlayer;var acted=SelectedHand;
            if(GetAi(actor)!=null)
            {_game.DruidSecondAddend=_game.GetDruidPolicy(actor)?.SecondAddend(actor,acted,first,candidates,_game)??first;return false;}
            var request=new WizardChoice(actor,"巨龙座 · 选择第二个加数（可重复）",candidates,false,hand=>_game.DruidSecondAddend=hand);
            QueueWizardChoice(request);
            TryStartWizardBoundary(()=>{Phase=BoardPhase.ChoosingTargetHand;SelectedHand=acted;ClickHand(first);});return true;
        }
    }
}
