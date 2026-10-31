using System;
using System.Collections.Generic;
namespace FingerDiceDuel.Core
{
    public interface ITrickeryDomain {} public interface ITempestDomain {}
    public sealed class TrickeryTouch
    {
        public PlayerState Owner,Actor;public Hand Source,Addend;public int SourceValue,AddendValue,Result;public bool Effectless;
    }
    public interface IClericDomainPolicy
    {bool SwapTrickery(TrickeryTouch touch,GameManager game);bool CharmTrickery(TrickeryTouch touch,GameManager game);
     Hand TempestHand(PlayerState owner,PlayerState attacker,IReadOnlyList<Hand> hands,GameManager game);
     AttackDamageType TempestType(PlayerState owner,PlayerState target,GameManager game);
     Hand TempestAddend(PlayerState owner,Hand source,IReadOnlyList<Hand> targets,GameManager game);}
    public static class ClericChoices
    {
        public static void FreeOperation(PlayerState owner,GameManager game,Hand fixedSource,bool optional,string skill,Action completed=null)
        {
            if(owner?.IsAlive!=true||owner.IsPolymorphed||!StatusRules.CanAct(owner)){completed?.Invoke();return;}
            var sources=new List<Hand>();foreach(var m in game.GetLegalMoves(owner))
                if((fixedSource==null||ReferenceEquals(m.ActedHand,fixedSource))&&!sources.Contains(m.ActedHand))sources.Add(m.ActedHand);
            if(sources.Count==0){completed?.Invoke();return;}
            Action<Hand> choose=source=>{
                if(source==null){completed?.Invoke();return;}
                var targets=new List<Hand>();foreach(var m in game.GetLegalMoves(owner))if(ReferenceEquals(m.ActedHand,source))targets.Add(m.TargetHand);
                if(targets.Count==0){completed?.Invoke();return;}
                Action<Hand> apply=target=>{
                    if(target==null){completed?.Invoke();return;}
                    game.RequestOperationAddend(owner,source,target,actual=>game.RequestTrickeryOperation(owner,source,actual,()=>{
                        game.ApplyEffectlessOperation(owner,source,actual,skill);completed?.Invoke();},true),effectless:true);
                };
                if(fixedSource==null)NewSubclassChoices.OfferHand(NewSubclassChoices.StormAddend,skill+" · 点击加数（只改手值）",owner,game,targets,optional,apply);
                else{
                    var request=new WizardChoice(owner,skill+" · 点击加数（只改手值）",targets,false,apply);
                    if(game.DeferWizardChoice?.Invoke(request)!=true){
                        var pick=game.ClericPolicy(owner)?.TempestAddend(owner,source,targets,game);
                        if(!request.Choose(pick))request.Choose(targets[0]);
                    }
                }
            };
            if(fixedSource!=null)choose(fixedSource);
            else NewSubclassChoices.OfferHand(NewSubclassChoices.StormSource,skill+" · 点击运算手，或不发动",owner,game,sources,optional,choose);
        }
    }
    public partial class GameManager
    {
        public Func<PlayerState,IClericDomainPolicy> ClericPolicyFor {get;set;}
        public IClericDomainPolicy ClericPolicy(PlayerState p)=>ClericPolicyFor?.Invoke(p)??GetChoiceProvider(p) as IClericDomainPolicy;
        private sealed class TrickeryStep {public TrickeryTouch Touch;public bool Swap,Charm;}
        private readonly List<TrickeryStep> _trickSteps=new List<TrickeryStep>();
        private PlayerState _trickActor;private Hand _trickSource,_trickAddend;private int _trickTurn=-1,_trickEpoch,_trickResult;private bool _trickReady;
        public void ClearPreparedTrickery(){_trickEpoch++;_trickSteps.Clear();_trickActor=null;_trickSource=_trickAddend=null;_trickTurn=-1;_trickReady=false;}
        private bool SameTrick(PlayerState actor,Hand source,Hand add)=>_trickTurn==TurnCount&&ReferenceEquals(_trickActor,actor)&&ReferenceEquals(_trickSource,source)&&ReferenceEquals(_trickAddend,add);
        public bool RequestTrickeryOperation(PlayerState actor,Hand source,Hand add,Action completed,bool effectless=false)
        {
            if(SameTrick(actor,source,add)&&_trickReady){completed();return false;}
            if(actor==null||source==null||add==null||WillSuppressOperationEffects(actor)){completed();return false;}
            ClearPreparedTrickery();_trickActor=actor;_trickSource=source;_trickAddend=add;_trickTurn=TurnCount;
            int epoch=_trickEpoch,before=source.Value;var addends=new List<Hand>{add};
            int result=Hand.ComputeSum(before,add.Value);
            if(!effectless&&DruidForms.IsDragon(actor)&&DruidSecondAddend!=null){result=Hand.ComputeSum(result,DruidSecondAddend.Value);if(!ReferenceEquals(add,DruidSecondAddend))addends.Add(DruidSecondAddend);}
            _trickResult=Math.Max(1,StatusRules.ApplyResultModifiers(actor,result));bool deferred=false;
            Action<int> next=null;next=index=>{
                if(epoch!=_trickEpoch||_trickTurn!=TurnCount)return;
                if(index>=addends.Count){_trickReady=true;completed();return;}
                var hand=addends[index];var owner=OwnerOfHand(hand);
                if(owner==null||!owner.IsAlive||owner.IsPolymorphed||ReferenceEquals(owner,actor)||CharacterBehaviors.FirstImplementer<ITrickeryDomain>(owner)==null
                    ||!IsLegalMove(actor,source,hand,out _)||before!=2&&hand.Value!=2&&_trickResult!=2){next(index+1);return;}
                var t=new TrickeryTouch{Owner=owner,Actor=actor,Source=source,Addend=hand,SourceValue=before,AddendValue=hand.Value,Result=_trickResult,Effectless=effectless};
                var step=new TrickeryStep{Touch=t};_trickSteps.Add(step);
                var request=new WizardChoice(owner,$"【诡诈换位】{actor.Name}用{CombatText.HandName(actor,source)}{before}借你的{CombatText.HandName(owner,hand)}{t.AddendValue}，结果{t.Result}。交换加数和结果？",
                    NewSubclassChoices.YesNo("交换数字","Subclasses/cleric_trickery_domain"),v=>{
                        if(epoch!=_trickEpoch||_trickTurn!=TurnCount)return;step.Swap=v==1;
                        if(!step.Swap){next(index+1);return;}_trickResult=t.AddendValue;
                        var charm=new WizardChoice(owner,"诡术魅惑 · 是否令"+actor.Name+"魅惑2回合？",NewSubclassChoices.YesNo("魅惑2回合","Status/charm"),c=>{
                            if(epoch!=_trickEpoch||_trickTurn!=TurnCount)return;step.Charm=c==1;next(index+1);});
                        if(DeferWizardChoice?.Invoke(charm)==true){deferred=true;return;}
                        charm.ChooseOption(ClericPolicy(owner)?.CharmTrickery(t,this)==true?1:0);
                    });
                if(DeferWizardChoice?.Invoke(request)==true){deferred=true;return;}
                request.ChooseOption(ClericPolicy(owner)?.SwapTrickery(t,this)==true?1:0);
            };
            next(0);return deferred;
        }
        public int TrickeryPreview(PlayerState actor,Hand source,Hand add,int fallback)
            =>SameTrick(actor,source,add)&&_trickReady&&_trickSteps.Exists(s=>s.Swap)?_trickResult:fallback;
        public bool ApplyTrickery(PlayerState actor,Hand source,Hand add,int calculated,out int final)
        {
            final=calculated;if(!SameTrick(actor,source,add)||!_trickReady)return false;bool changed=false;
            foreach(var step in _trickSteps){var t=step.Touch;if(!step.Swap||!t.Owner.IsAlive||t.Owner.IsPolymorphed)continue;
                bool shield=t.Addend.IsShield;t.Addend.SetValue(final);final=t.AddendValue;source.SetValue(final);changed=true;
                if(shield)foreach(var w in CharacterBehaviors.Implementers<PathOfTheWardenLogic>(t.Owner))w.OnOwnShieldBroken(t.Owner,t.Addend,this);
                if(step.Charm)actor.ApplyStatus(StatusEffect.Charm,2,t.Owner);
                NoteSkill(t.Owner,"诡诈换位",$"{actor.Name}打出{final}；{CombatText.HandName(t.Owner,t.Addend)}→{t.Addend.Value}"+(step.Charm?"，魅惑2回合":""),actor);
            }return changed;
        }
        public Func<AttackContext,Action,bool> DeferClericAttack {get;set;}
        public bool PendingClericResolution {get;private set;}
        public bool HasTempestDomain()
        {foreach(var p in Players)if(p.IsAlive&&!p.IsPolymorphed&&CharacterBehaviors.FirstImplementer<ITempestDomain>(p)!=null)return true;return false;}
        public bool DeferOrdinaryClericAttack(AttackContext attack,Action resolve)
        {
            if(DeferClericAttack==null)return false;
            bool possible=false;foreach(var p in Players)if(p.IsAlive&&!p.IsPolymorphed&&CharacterBehaviors.FirstImplementer<ITempestDomain>(p)!=null)possible=true;
            if(!possible)return false;
            PendingClericResolution=true;
            if(DeferClericAttack(attack,()=>{PendingClericResolution=false;resolve();}))return true;
            PendingClericResolution=false;return false;
        }
        public void OfferTempestReaction(AttackContext incoming,Action completed)
        {
            if(incoming.TempestReactionPrepared){completed();return;}incoming.TempestReactionPrepared=true;
            var owner=incoming.Defender;var enemy=incoming.Attacker;
            if(incoming.WasCancelled||incoming.IsStatusDamage||incoming.IsReflected||incoming.IsTempestCounter||!owner.IsAlive||owner.IsPolymorphed||!enemy.IsAlive||ReferenceEquals(owner,enemy)
                ||CharacterBehaviors.FirstImplementer<ITempestDomain>(owner)==null||!StatusRules.CanAttackTarget(owner,enemy,this)){completed();return;}
            var hands=new List<Hand>();foreach(var h in owner.Hands)if(h.Value==4)hands.Add(h);
            if(hands.Count==0){completed();return;}
            Action<Hand> selected=source=>{
                if(source==null){completed();return;}
                var options=new List<SkillChoiceOption>();foreach(var type in new[]{AttackDamageType.Lightning,AttackDamageType.Thunder})
                    options.Add(new SkillChoiceOption((int)type,AttackDamageTypes.NameOf(type),"Damage/"+AttackDamageTypes.IconKeyOf(type)));
                var types=new WizardChoice(owner,"风暴之怒 · 选择反击类型",options,v=>{
                    if(!owner.IsAlive||!enemy.IsAlive){completed();return;}
                    var counter=new AttackContext(owner,enemy,source,4){IsTempestCounter=true,AttackType=(AttackDamageType)v,AttackValueOverride=4,AttackerProvider=GetChoiceProvider(owner)};
                    Action finish=()=>{
                        counter.Resolve(GetChoiceProvider(enemy),this);if(!counter.WasCancelled)incoming.TempestCountered=true;NoteSkill(owner,"风暴之怒",$"{enemy.Name} {AttackDamageTypes.NameOf(counter.AttackType)} {counter.AppliedDamage}伤害",enemy);
                        ClericChoices.FreeOperation(owner,this,source,false,"风暴之怒",completed);
                    };
                    if(DeferWizardAttack?.Invoke(counter,finish)!=true)finish();
                });
                if(DeferWizardChoice?.Invoke(types)!=true)types.ChooseOption((int)(ClericPolicy(owner)?.TempestType(owner,enemy,this)??AttackDamageType.Lightning));
            };
            var request=new WizardChoice(owner,"风暴之怒 · 点击一只4反击"+enemy.Name+"，或不发动",hands,true,selected,false,"不发动");
            if(DeferWizardChoice?.Invoke(request)!=true)request.Choose(ClericPolicy(owner)?.TempestHand(owner,enemy,hands,this));
        }
    }
}
