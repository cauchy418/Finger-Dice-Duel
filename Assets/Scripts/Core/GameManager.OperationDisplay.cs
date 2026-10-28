using System;
using System.Collections.Generic;
namespace FingerDiceDuel.Core
{
    public partial class GameManager
    {
        /// <summary>Pure display projection; never invokes effect rewriters, RNG or skill hooks.</summary>
        public IReadOnlyList<KeyValuePair<Hand,int>> PreviewOperationHands(PlayerState actor,Hand source,Hand addend)
        {
            var shown=new List<KeyValuePair<Hand,int>>();
            if(actor==null||source==null||addend==null)return shown;
            int value=Hand.ComputeSum(source.Value,addend.Value);
            if(!WillSuppressOperationEffects(actor)&&DruidForms.IsDragon(actor)&&DruidSecondAddend!=null)value=Hand.ComputeSum(value,DruidSecondAddend.Value);
            value=Math.Max(1,StatusRules.ApplyResultModifiers(actor,value));
            if(SameTrick(actor,source,addend)&&_trickReady)
                foreach(var step in _trickSteps)if(step.Swap&&step.Touch.Owner.IsAlive&&!step.Touch.Owner.IsPolymorphed)
                {shown.Add(new KeyValuePair<Hand,int>(step.Touch.Addend,value));value=step.Touch.AddendValue;}
            shown.Add(new KeyValuePair<Hand,int>(source,value));return shown;
        }
    }
}
