using System;
using System.Collections.Generic;
namespace FingerDiceDuel.Core
{
    public interface IDruidCaster { }
    public interface ISporeDruid {void OnNearbyDeath(PlayerState owner,PlayerState dead,GameManager game);}
    public sealed class DruidFormView:IDruidForm
    {public string CircleKey{get;}public int FormValue{get;}public bool NeedsSecondAddend=>false;public DruidFormView(string key,int value){CircleKey=key;FormValue=value;}}
    public partial class PlayerState
    {
        private ICharacterBehavior _wildOriginal;
        private int _wildHealth,_wildMaximum,_wildCharges=-1;
        public bool IsWildShaped=>_wildOriginal!=null;
        public int WildForm {get;private set;}
        public int WildStar {get;private set;}
        public bool WildSkipPending {get;internal set;}
        public int SporeLife {get;private set;}
        public int InitialWildCharges{get{int total=0;foreach(var p in DruidWildShape.Parts(DruidOriginal))if(p is IDruidCaster)total+=p is Characters.SpecialLogics.CircleOfTheMoonLogic?3:2;return total;}}
        public bool SpendWildCharge(){if(WildCharges<=0)return false;_wildCharges--;return true;}
        public void RestoreWildCharge(){_wildCharges=Math.Min(InitialWildCharges,WildCharges+1);}
        public void GainSporeLife(){SporeLife=10;}
        public int WildOriginalHealth=>_wildHealth;
        public ICharacterBehavior DruidOriginal=>_wildOriginal??PersistentCharacter;
        public int WildCharges
        {
            get{if(_wildCharges<0){_wildCharges=0;foreach(var part in DruidWildShape.Parts(DruidOriginal))if(part is IDruidCaster)_wildCharges+=part is Characters.SpecialLogics.CircleOfTheMoonLogic?3:2;}return _wildCharges;}
        }
        public bool EnterWild(int form,int star,GameManager game)
        {
            bool moon=DruidWildShape.Has<Characters.SpecialLogics.CircleOfTheMoonLogic>(DruidOriginal);
            bool stars=DruidWildShape.Has<Characters.SpecialLogics.CircleOfTheStarsLogic>(DruidOriginal);
            int cost=form>=5?2:1;
            if(!IsAlive||IsPolymorphed||IsWildShaped||!StatusRules.CanAct(this)||form<1||form>(moon?6:4)||WildCharges<cost||LeftHand.Value+RightHand.Value!=10||stars&&(star<1||star>3)||!stars&&star!=0)return false;
            var frame=game?.CaptureVisualFrame();
            _wildCharges-=cost;_wildOriginal=Character;_wildHealth=Health;_wildMaximum=MaximumHealth;WildForm=form;WildStar=star;
            Character=new WildShapeBehavior(this);MaximumHealth=DruidWildShape.Health[form]+(star==2?4:0);Health=MaximumHealth;WildSkipPending=true;
            game?.NoteSkill(this,"荒野形态",DruidForms.Name("moon",form)+(star==0?"":" · "+DruidForms.Name("stars",star))+"，消耗"+cost+"充能",
                this,new SkillVisualData(value:form,previous:star,variant:"enter",amount:cost),frame);return true;
        }
        public void EndWild()
        {
            if(!IsWildShaped)return;
            if(IsPolymorphed)EndPolymorph();
            Character=_wildOriginal;Health=_wildHealth;MaximumHealth=_wildMaximum;_wildOriginal=null;WildForm=WildStar=0;
        }
        public void ResetWild(){EndWild();_wildCharges=-1;WildSkipPending=false;SporeLife=0;}
    }
    public static class DruidWildShape
    {
        public static readonly int[] Health={0,12,10,8,8,16,10};
        private static readonly int[] Attack = {0,-1,0,1,0,0,2};
        private static readonly int[] Defense = {0,1,-1,-1,0,2,0};
        private static readonly int[] Range = {1,1,1,3,1,2,1};
        public static int AttackOf(int form,int star) => Attack[form] + (star==1 ? 2 : star==3 ? 1 : 0);
        public static int DefenseOf(int form,int star) => Defense[form] + (star==1 ? -1 : star==3 ? 1 : 0);
        public static int RangeOf(int form,int star) => Math.Max(Range[form],star==1||star==3 ? 3 : 1);
        public static AttackDamageType TypeOf(int form,int star)
            => star==1 ? AttackDamageType.Radiant : form==3 ? AttackDamageType.Piercing
                : form==4 ? AttackDamageType.Poison : AttackDamageType.Slashing;
        public static HandEffectType EffectOf(int form,int value)
            => form==1&&value==4 ? HandEffectType.Shield : form==3&&value==6 ? HandEffectType.None : Hand.GetEffectType(value);
        public static IEnumerable<ICharacterBehavior> Parts(ICharacterBehavior root)
        {if(root is ICharacterBehaviorContainer container){foreach(var part in container.Parts)yield return part;}else if(root!=null)yield return root;}
        public static bool Has<T>(ICharacterBehavior root){foreach(var part in Parts(root))if(part is T)return true;return false;}
        public static void Offer(PlayerState owner,GameManager game)
        {
            if(owner==null||owner.IsWildShaped||owner.IsPolymorphed||!owner.IsAlive||!StatusRules.CanAct(owner)||owner.LeftHand.Value+owner.RightHand.Value!=10||!Has<IDruidCaster>(owner.Character)||owner.WildCharges<=0)return;
            var options=DruidForms.Options("moon");
            bool moon=Has<Characters.SpecialLogics.CircleOfTheMoonLogic>(owner.Character);
            options.RemoveAll(o=>o.Value>=5&&(!moon||owner.WildCharges<2));
            var request=new WizardChoice(owner,"荒野形态 · 消耗充能并跳过回合",options,value=>{
                if(value==0)return;
                if(Has<Characters.SpecialLogics.CircleOfTheStarsLogic>(owner.Character))DruidForms.OfferEntry(owner,game,"stars",star=>{if(star!=0)owner.EnterWild(value,star,game);},value);
                else owner.EnterWild(value,0,game);
            });
            if(game.DeferWizardChoice?.Invoke(request)!=true)
            {int pick=game.GetDruidPolicy(owner)?.ChooseForm(owner,"moon",options,game)??0;if(!request.ChooseOption(pick))request.ChooseOption(0);}
        }
    }
    public sealed class WildShapeBehavior:CharacterTemplate,IDruidForm,ISelectiveHandEffectRewriter,IHandEffectRewriter,IHandEffectDisplay,IStatusApplyingAttack,ICompletedDamageDealt,ITurnStartHook,ITurnEndHook
    {
        private readonly PlayerState _owner;private bool _dragonUsed,_fresh=true;
        public WildShapeBehavior(PlayerState owner){_owner=owner;}
        private int F=>_owner.WildForm;private int S=>_owner.WildStar;
        public string CircleKey=>"moon";public int FormValue=>F;public bool NeedsSecondAddend=>false;
        public override string DisplayName=>DruidForms.Name("moon",F);
        public override int AttackBonus=>DruidWildShape.AttackOf(F,S);
        public override int DefenseBonus=>DruidWildShape.DefenseOf(F,S);
        public override int BaseAttackRange=>DruidWildShape.RangeOf(F,S);
        public override AttackDamageType AttackTypeOf(AttackContext ctx)=>DruidWildShape.TypeOf(F,S);
        public bool HandlesResult(int value)=>F==3&&value==6||F==1&&value==4;
        public HandEffectType DisplayedEffectOf(int value)=>DruidWildShape.EffectOf(F,value);
        public HandEffectType RewriteEffect(PlayerState actor,Hand hand,int add,GameManager game)=>DisplayedEffectOf(hand.Value);
        public override void OnResolveAttack(AttackContext ctx)
        {if(ctx.IsStatusDamage||ctx.IsReflected)return;if(F==5)ctx.FinalDamage+=ctx.Manager.DistanceBetween(ctx.Attacker,ctx.Defender);if(F==6)ctx.InflictsBleed=true;}
        public StatusEffect StatusToApplyOnHit(AttackContext ctx)=>ArcaneChoices.ElementStatus(ctx.AttackType);
        public void OnCompletedDamage(AttackContext ctx,GameManager game)
        {if(F==2&&!ctx.IsStatusDamage&&!ctx.IsReflected&&ctx.ActualHealthDamage>0&&ctx.Attacker.IsAlive){int before=_owner.Health;_owner.Heal(ctx.ActualHealthDamage/2);game.NoteSkill(_owner,"狼形态","回复"+(_owner.Health-before),_owner,new SkillVisualData(value:_owner.LastActualHealing));}}
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {
            if(!ReferenceEquals(game.CurrentPlayer,owner)||S!=3||!StatusRules.CanAct(owner))return;
            _dragonUsed=false;
            ClericChoices.FreeOperation(owner,game,null,true,"巨龙座",()=>{});
            // A pure-operation callback records success through OnOperationResolved's
            // dedicated effectless path in GameManager, rather than a normal action.
        }
        public void MarkDragonOperation(){_dragonUsed=true;}
        public void OnTurnEnded(PlayerState owner,GameManager game)
        {
            if(!owner.IsAlive||!owner.IsWildShaped)return;
            Action finish=()=>DruidForms.OfferExit(owner,game,this,()=>{
                var frame=game.CaptureVisualFrame();int form=F,star=S;
                owner.EndWild();game.NoteSkill(owner,"解除荒野","恢复原形",owner,
                    new SkillVisualData(value:form,previous:star,variant:"exit"),frame);
            });
            if(S==3&&!_dragonUsed&&!_fresh)ClericChoices.FreeOperation(owner,game,null,false,"巨龙座",finish);else finish();
            _fresh=false;
        }
        public override void OnOperationResolved(PlayerState owner,Hand hand,int before,int add,HandEffectType effect,GameManager game,IPreTurnChoiceSink sink)
        {
            if(S!=2||hand.Value!=9||effect!=HandEffectType.Heal)return;
            var hands=new List<Hand>();var targets=new List<PlayerState>();foreach(var p in game.Players)if(p.IsAlive){hands.Add(p.LeftHand);targets.Add(p);}
            var request=new WizardChoice(owner,"圣杯座 · 额外回复9",hands,true,h=>{
                var target=game.OwnerOfHand(h);if(target==null)return;
                var frame=game.CaptureVisualFrame();int hp=target.Health;
                target.Heal(9);int healed=target.Health-hp;
                game.NoteSkill(owner,"圣杯座","回复"+target.Name+" "+healed,target,
                    new SkillVisualData(source:hand,value:healed,variant:"heal"),frame);
            },targetsPlayer:true);
            if(game.DeferWizardChoice?.Invoke(request)!=true)request.Choose(game.GetDruidPolicy(owner)?.ChaliceTarget(owner,targets,game)?.LeftHand);
        }
    }
}
