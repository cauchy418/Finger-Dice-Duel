using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class BoardController
    {
        private readonly Dictionary<Hand,int> _operationDisplay=new Dictionary<Hand,int>();
        /// <summary>Confirmed arithmetic is visible before target/skill choices; rule snapshots remain intact.</summary>
        public int? PreviewHandValue(Hand hand)
            =>hand!=null&&_operationDisplay.TryGetValue(hand,out var value)?(int?)value:null;
        private void PrepareOperationDisplay()
        {
            if(_operationDisplay.Count>0||SelectedHand==null||_selectedTarget==null)return;
            foreach(var pair in _game.PreviewOperationHands(ActivePlayer,SelectedHand,_selectedTarget))_operationDisplay[pair.Key]=pair.Value;
        }

        // Resolve numerical status damage before asking who the resulting effect targets.
        // The manager retains a one-use token so the eventual turn commit cannot apply it twice.
        private bool CompleteFatalOperationStatusDamage(System.Action continuation=null)
        {
            if (_pendingGambleRoll > 0 || SelectedHand == null || _selectedTarget == null) return false;
            if (_game.OperationEffectsSuppressed(ActivePlayer, SelectedHand, _selectedTarget)) return false;
            var context = DruidChoiceContext(ActivePlayer, TargetPlayer ?? Defender, SelectedHand, _selectedTarget);
            if(_game.PrepareOperationStatusSourceBreak(ActivePlayer,SelectedHand,_selectedTarget,context.Effect))
            {
                FlushSkillLog();
                if(TryStartWizardBoundary(continuation??ContinueSelectedOperation))return true;
                context=DruidChoiceContext(ActivePlayer,TargetPlayer??Defender,SelectedHand,_selectedTarget);
            }
            _game.PrepareOperationStatusDamage(ActivePlayer, SelectedHand, _selectedTarget,
                context.EffectiveResult, context.Effect);
            FlushSkillLog();
            if (ActivePlayer.IsAlive && !_game.IsGameOver) return false;
            _pendingIsAttack = false;
            _giveUpAttack = true;
            ExecuteResolvedTurn();
            return true;
        }
    }
}
