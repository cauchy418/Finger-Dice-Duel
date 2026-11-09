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
            bool requiredDragon = skill == "巨龙座" && !optional;
            Action<Hand> choose=source=>{
                if(source==null){completed?.Invoke();return;}
                var targets=new List<Hand>();foreach(var m in game.GetLegalMoves(owner))if(ReferenceEquals(m.ActedHand,source))targets.Add(m.TargetHand);
                if(targets.Count==0){completed?.Invoke();return;}
                Action<Hand> apply=target=>{
                    if(target==null){completed?.Invoke();return;}
                    game.RequestOperationAddend(owner,source,target,actual=>game.RequestTrickeryOperation(owner,source,actual,()=>{
                        game.ApplyEffectlessOperation(owner,source,actual,skill);completed?.Invoke();},true),effectless:true);
                };
                if(fixedSource==null)NewSubclassChoices.OfferHand(requiredDragon ? NewSubclassChoices.DragonRequiredAddend : NewSubclassChoices.StormAddend,skill+" · 点击加数（只改手值）",owner,game,targets,optional,apply);
                else{
                    var request=new WizardChoice(owner,skill+" · 点击加数（只改手值）",targets,false,apply);
                    if(game.DeferWizardChoice?.Invoke(request)!=true){
                        var pick=game.ClericPolicy(owner)?.TempestAddend(owner,source,targets,game);
                        if(!request.Choose(pick))request.Choose(targets[0]);
                    }
                }
            };
            if(fixedSource!=null)choose(fixedSource);
            else NewSubclassChoices.OfferHand(requiredDragon ? NewSubclassChoices.DragonRequiredSource : NewSubclassChoices.StormSource,
                skill+(optional ? " · 点击运算手，或不发动" : " · 点击运算手"),owner,game,sources,optional,choose);
        }
    }
    public partial class GameManager
    {
        private readonly List<(PlayerState Healer,PlayerState Target)> _extraClericHealing=new List<(PlayerState,PlayerState)>();
        public void RegisterExtraClericHealing(PlayerState healer,PlayerState target)
        {if(target!=null)_extraClericHealing.Add((healer,target));}
        public void ResolveAllClericHealing(PlayerState healer,PlayerState target,Action completed)
        {
            var heals=new List<(PlayerState Healer,PlayerState Target)>(_extraClericHealing);_extraClericHealing.Clear();
            if(target!=null)heals.Add((healer,target));
            Action<int> next=null;next=i=>{if(i>=heals.Count){completed();return;}ResolveClericHealing(heals[i].Healer,heals[i].Target,()=>next(i+1));};next(0);
        }
        public void ResolveClericHealing(PlayerState healer,PlayerState target,Action completed)
        {
            var finish=completed;PendingClericResolution=true;completed=()=>{PendingClericResolution=false;finish();};
            if(!target.IsAlive){completed();return;}
            if(CharacterBehaviors.FirstImplementer<Characters.SpecialLogics.LightDomainLogic>(healer)!=null)
            {
                target.ApplyStatus(StatusEffect.HolyLight,2,healer);
                NoteSkill(healer,"圣焰赐福",target.Name+" 圣火2回合",target,new SkillVisualData(status:StatusEffect.HolyLight));
            }
            if(CharacterBehaviors.FirstImplementer<Characters.SpecialLogics.WarDomainLogic>(healer)!=null)
            {
                target.ApplyStatus(StatusEffect.WarBlessing,2,healer);
                NoteSkill(healer,"战争赐福",target.Name+" 战争祝福2回合",target,new SkillVisualData(status:StatusEffect.WarBlessing));
            }
            if(CharacterBehaviors.FirstImplementer<Characters.SpecialLogics.KnowledgeDomainLogic>(healer)!=null)
            {target.GainKnowledge(healer);NoteSkill(healer,"知识赐福",target.Name+" 知识祝福+1",target,new SkillVisualData(value:target.KnowledgeStacks,status:StatusEffect.KnowledgeBlessing));}
            Action storm=()=>{
                if(CharacterBehaviors.FirstImplementer<ITempestDomain>(healer)!=null&&target.IsAlive)
                    ClericChoices.FreeOperation(target,this,null,true,"风暴赐福",completed);
                else completed();
            };
            if(CharacterBehaviors.FirstImplementer<ITrickeryDomain>(healer)==null){storm();return;}
            var request=new WizardChoice(healer,"诡术赐福 · 点击受疗者一只手改为2，或不发动",new List<Hand>{target.LeftHand,target.RightHand},true,hand=>{
                if(hand!=null&&target.IsAlive){hand.SetValue(2);NoteSkill(healer,"诡术赐福",target.Name+"手值改为2",target,new SkillVisualData(source:hand,value:2));}storm();},false,"不发动");
            if(DeferWizardChoice?.Invoke(request)!=true)
            {
                Hand pick=null;
                if(IsKnownAlly(healer,target))pick=target.LeftHand.Value!=5?target.LeftHand:target.RightHand.Value!=5?target.RightHand:null;
                request.Choose(pick);
            }
        }
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
            int result = OperationSum(actor, add, before, add.Value, !effectless, !effectless, !effectless && ShouldSubtract(actor));
            if (!effectless && DruidForms.IsDragon(actor) && DruidSecondAddend != null && !ReferenceEquals(add, DruidSecondAddend))
                addends.Add(DruidSecondAddend);
            _trickResult = ModifiedOperationValue(actor, result);
            bool deferred=false;
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
        private static bool CanApplyTrickerySwap(TrickeryStep step)
            => step.Swap && step.Touch.Owner.IsAlive && !step.Touch.Owner.IsPolymorphed;

        private bool HasPreparedTrickerySwap(PlayerState actor, Hand source, Hand add)
            => SameTrick(actor, source, add) && _trickReady && _trickSteps.Exists(CanApplyTrickerySwap);

        private int TrickeryPreview(PlayerState actor, Hand source, Hand add, int value,
                                    List<KeyValuePair<Hand, int>> shown)
        {
            if (!SameTrick(actor, source, add) || !_trickReady) return value;
            foreach (var step in _trickSteps)
            {
                if (!CanApplyTrickerySwap(step)) continue;
                shown?.Add(new KeyValuePair<Hand, int>(step.Touch.Addend, value));
                value = step.Touch.AddendValue;
            }
            return value;
        }
        public bool ApplyTrickery(PlayerState actor,Hand source,Hand add,int calculated,out int final)
        {
            final=calculated;if(!SameTrick(actor,source,add)||!_trickReady)return false;bool changed=false;
            foreach(var step in _trickSteps){var t=step.Touch;if(!CanApplyTrickerySwap(step))continue;
                bool shield=t.Addend.IsShield;t.Addend.SetValue(final);final=t.AddendValue;source.SetValue(final);changed=true;
                if(shield)foreach(var w in CharacterBehaviors.Implementers<PathOfTheWardenLogic>(t.Owner))w.OnOwnShieldBroken(t.Owner,t.Addend,this);
                if(step.Charm)actor.ApplyStatus(StatusEffect.Charm,2,t.Owner);
                NoteSkill(t.Owner,"诡诈换位",$"{actor.Name}打出{final}；{CombatText.HandName(t.Owner,t.Addend)}→{t.Addend.Value}"+(step.Charm?"，魅惑2回合":""),actor,new SkillVisualData(t.Addend,source,t.AddendValue,t.Addend.Value));
            }return changed;
        }
        public Func<AttackContext,Action,bool> DeferClericAttack {get;set;}
        public bool PendingClericResolution {get;private set;}
        public bool HasTempestDomain()
        {foreach(var p in Players)if(p.IsAlive&&!p.IsPolymorphed&&(CharacterBehaviors.FirstImplementer<ITempestDomain>(p)!=null||CharacterBehaviors.FirstImplementer<PathOfTheWardenLogic>(p)!=null))return true;return false;}
        public bool DeferOrdinaryClericAttack(AttackContext attack,Action resolve)
        {
            if(DeferClericAttack==null)return false;
            bool possible=false;foreach(var p in Players)if(p.IsAlive&&!p.IsPolymorphed&&(CharacterBehaviors.FirstImplementer<ITempestDomain>(p)!=null||CharacterBehaviors.FirstImplementer<PathOfTheWardenLogic>(p)!=null))possible=true;
            if(!possible)return false;
            PendingClericResolution=true;
            if(DeferClericAttack(attack,()=>{PendingClericResolution=false;resolve();}))return true;
            PendingClericResolution=false;return false;
        }
        public void OfferTempestReaction(AttackContext incoming,Action completed)
        {
            if(!incoming.WardenReactionPrepared)
            {
                incoming.WardenReactionPrepared=true;
                var warden=incoming.Defender;
                if(!incoming.WasCancelled&&!incoming.IsStatusDamage&&!incoming.IsKnowledgeForceDamage&&warden?.IsAlive==true&&warden.Health<=15
                    && CharacterBehaviors.FirstImplementer<PathOfTheWardenLogic>(warden)!=null)
                {
                    var oath=new WizardChoice(warden,"守望誓约 · 来袭攻击改为力场，清除附加状态与元素反应？",NewSubclassChoices.YesNo("守望誓约","Subclasses/barbarian_path_of_the_warden"),v=>{
                        if(v==1&&warden.IsAlive&&warden.Health<=15){incoming.ConvertToWardenForce();NoteSkill(warden,"守望誓约","来袭攻击改为力场",warden,new SkillVisualData(element:AttackDamageType.Force));}
                        OfferTempestReaction(incoming,completed);
                    });
                    if(DeferWizardChoice?.Invoke(oath)!=true)oath.ChooseOption(SkillChoicePolicy.Choose("warden_oath",oath.Options,warden,incoming.Attacker,this));
                    return;
                }
            }
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
                        counter.Resolve(GetChoiceProvider(enemy),this,()=>{if(!counter.WasCancelled)incoming.TempestCountered=true;NoteSkill(owner,"风暴之怒",$"{enemy.Name} {AttackDamageTypes.NameOf(counter.AttackType)} {counter.AppliedDamage}伤害",enemy,new SkillVisualData(variant:counter.AttackType.ToString().ToLowerInvariant()));
                        ClericChoices.FreeOperation(owner,this,source,false,"风暴之怒",completed);
                        });
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
