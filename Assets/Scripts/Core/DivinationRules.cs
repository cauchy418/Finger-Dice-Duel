using System;
using System.Collections.Generic;
namespace FingerDiceDuel.Core
{
    public interface IPortentBook
    {
        IReadOnlyCollection<int> Portents {get;}
        bool ConsumePortent(int value);
    }
    public static class DivinationRules
    {
        public const string Key="divination_replace";
        public static double AiScore(int value,PlayerState owner,PlayerState actor,GameManager game)
        {
            if(value<0)return 1;
            bool ally=ReferenceEquals(owner,actor)||game.IsKnownAlly(owner,actor);
            int original=game.CurrentPortentResult;
            Func<int,double> worth=n=>n==9?Math.Min(9,actor.MaximumHealth-actor.Health):n==5?5:n==8?8:n==6?6:n==4?4:0;
            double difference=worth(value)-worth(original);
            return (ally?difference:-difference)-1;
        }
    }
    public partial class GameManager
    {
        public int CurrentPortentResult {get;private set;}
        private PlayerState _portentActor;
        private Hand _portentSource,_portentAddend;
        private int _portentTurn=-1,_portentEpoch;
        private bool _portentReady;
        private int? _portentResult;
        private bool SamePortent(PlayerState actor,Hand source,Hand add)=>_portentTurn==TurnCount&&ReferenceEquals(actor,_portentActor)&&ReferenceEquals(source,_portentSource)&&ReferenceEquals(add,_portentAddend);
        public void ClearPreparedPortent(){_portentEpoch++;_portentActor=null;_portentSource=_portentAddend=null;_portentReady=false;_portentResult=null;_portentTurn=-1;}
        public int PortentPreview(PlayerState actor,Hand source,Hand add,int fallback)=>SamePortent(actor,source,add)&&_portentReady&&_portentResult.HasValue?_portentResult.Value:fallback;
        public bool HasPreparedPortent(PlayerState actor,Hand source,Hand add)=>SamePortent(actor,source,add)&&_portentReady&&_portentResult.HasValue;
        public bool RequestPortentOperation(PlayerState actor,Hand source,Hand add,Action completed)
        {
            if(SamePortent(actor,source,add)&&_portentReady){completed();return false;}
            if(actor==null||source==null||add==null||WillSuppressOperationEffects(actor)){completed();return false;}
            ClearPreparedPortent();_portentActor=actor;_portentSource=source;_portentAddend=add;_portentTurn=TurnCount;
            int epoch=_portentEpoch;
            int original=PreviewOperationResult(actor,source,add);
            original=TrickeryPreview(actor,source,add,Math.Max(1,StatusRules.ApplyResultModifiers(actor,original)));
            bool deferred=false;
            Action<int> next=null;next=index=>{
                if(epoch!=_portentEpoch||_portentTurn!=TurnCount)return;
                if(index>=Players.Count){_portentReady=true;completed();return;}
                var owner=Players[index];var book=CharacterBehaviors.FirstImplementer<IPortentBook>(owner);
                int now=_portentResult??original;
                bool seven=ReferenceEquals(owner,actor)?now==7||owner.GetOtherHand(source).Value==7:owner.LeftHand.Value==7||owner.RightHand.Value==7;
                if(!owner.IsAlive||owner.IsPolymorphed||!seven||book==null||book.Portents.Count==0){next(index+1);return;}
                var values=new List<int>(book.Portents);values.Sort();
                var options=new List<SkillChoiceOption>();
                foreach(int value in values)options.Add(new SkillChoiceOption(value,"替换为"+value,"Effects/portent"));
                options.Add(new SkillChoiceOption(-1,"不发动","Effects/portent"));
                CurrentPortentResult=now;
                var request=new WizardChoice(owner,"【预知未来】"+actor.Name+"运算结果为"+now+"，选择一个预兆替换结果，或不发动。",options,value=>{
                    if(epoch!=_portentEpoch||_portentTurn!=TurnCount)return;
                    if(value>=0&&book.ConsumePortent(value)){_portentResult=value;NoteSkill(owner,"预知未来",actor.Name+"结果"+now+"→"+value+"，消耗预兆"+value,actor);}
                    next(index+1);
                });
                if(DeferWizardChoice?.Invoke(request)==true){deferred=true;return;}
                request.ChooseOption(SkillChoicePolicy.Choose(DivinationRules.Key,options,owner,actor,this));
            };
            next(0);return deferred;
        }
    }
}
