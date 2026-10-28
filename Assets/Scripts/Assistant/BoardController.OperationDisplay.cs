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
    }
}
