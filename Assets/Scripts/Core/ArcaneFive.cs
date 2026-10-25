using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public interface IArcaneChoicePolicy
    { PlayerState ChooseArcaneTarget(string key,PlayerState owner,IReadOnlyList<PlayerState> targets,GameManager game); }
    public interface ICompletedDamageDealt
    {void OnCompletedDamage(AttackContext attack,GameManager game);}
    public interface ICompletedDamageTaken
    { void OnAttackDamageTaken(PlayerState owner,AttackContext attack,GameManager game); }
    public interface ITargetDamageBonus
    { int BonusAgainst(PlayerState owner,PlayerState target); }
    public interface IElementEnchantment
    { AttackDamageType Enchantment {get;} }
    public interface IBladesong
    { bool Singing {get;} AttackDamageType SongElement {get;} }
    public interface IHexbladeCurse
    {
        PlayerState CurseTarget {get;}
        int CurseTurns {get;}
        void TickCurse(PlayerState ended);
    }

    public static class ArcaneChoices
    {
        public const string Transmute="transmute",Enchantment="enchantment",KnightElement="knight_element",KnightCopy="knight_copy",Bladesong="bladesong",Hex="hex_curse";
        public static void Option(string key,string prompt,PlayerState owner,PlayerState target,GameManager game,List<SkillChoiceOption> options,Action<int> apply)
        {
            if(owner==null||!owner.IsAlive||game==null)return;
            var request=new WizardChoice(owner,prompt,options,v=>{if(owner.IsAlive&&!owner.IsPolymorphed)apply(v);});
            if(game.DeferWizardChoice?.Invoke(request)==true)return;
            request.ChooseOption(SkillChoicePolicy.Choose(key,options,owner,target,game));
        }
        public static List<SkillChoiceOption> Elements(string decline="不发动")
        {
            var result=new List<SkillChoiceOption>();
            foreach(var type in new[]{AttackDamageType.Fire,AttackDamageType.Cold,AttackDamageType.Lightning,AttackDamageType.Thunder,AttackDamageType.Acid})
                result.Add(new SkillChoiceOption((int)type,AttackDamageTypes.NameOf(type),"Damage/"+AttackDamageTypes.IconKeyOf(type)));
            result.Add(new SkillChoiceOption(0,decline,"UI/status_counter"));return result;
        }
        public static void Target(string key,string prompt,PlayerState owner,GameManager game,List<PlayerState> targets,Action<PlayerState> apply)
        {
            if(targets.Count==0||owner==null||!owner.IsAlive)return;
            var hands=new List<Hand>();foreach(var p in targets)hands.Add(p.LeftHand);
            var request=new WizardChoice(owner,prompt,hands,true,h=>{
                if(h==null||!owner.IsAlive||owner.IsPolymorphed)return;
                var target=game.OwnerOfHand(h);if(target?.IsAlive==true)apply(target);
            },true,"不发动");
            if(game.DeferWizardChoice?.Invoke(request)==true)return;
            var policy=game.ArcanePolicyFor?.Invoke(owner)??game.GetChoiceProvider(owner) as IArcaneChoicePolicy;
            var pick=policy?.ChooseArcaneTarget(key,owner,targets,game);
            request.Choose(pick?.LeftHand);
        }
        public static bool MatchingHands(PlayerState a,PlayerState b)
            =>a!=null&&b!=null&&(a.LeftHand.Value==b.LeftHand.Value&&a.RightHand.Value==b.RightHand.Value
                ||a.LeftHand.Value==b.RightHand.Value&&a.RightHand.Value==b.LeftHand.Value);
        public static StatusEffect ElementStatus(AttackDamageType type)=>Enum.TryParse(type.ToString(),out DamageType element)?StatusNames.StatusOf(element):StatusEffect.None;
        public static AttackDamageType NearFar(AttackContext ctx)
            =>(ctx?.Manager?.DistanceBetween(ctx.Attacker,ctx.Defender)??1)<=1?AttackDamageType.Bludgeoning:AttackDamageType.Piercing;
        public static int BonusAgainst(PlayerState actor,PlayerState target)
        {int total=0;foreach(var part in CharacterBehaviors.Implementers<ITargetDamageBonus>(actor))total+=part.BonusAgainst(actor,target);return total;}
    }

    public partial class PlayerState
    {
        private ICharacterBehavior _untransformedCharacter;
        private int _untransformedHealth,_untransformedMaximum;
        public bool IsPolymorphed=>_untransformedCharacter!=null;
        public int PolymorphTurns {get;private set;}
        public bool BeganAttackThisTurn {get;internal set;}
        public bool FormedShieldThisTurn {get;internal set;}
        public string BattleEmblemKey=>IsPolymorphed?"Units/polymorphed_sheep":IsSummon?"Units/hexbound_specter":"Subclasses/"+(Definition?.Id=="blank"?"fighter_champion":Definition?.Id??"fighter_champion");
        public ICharacterBehavior PersistentCharacter=>_untransformedCharacter??Character;
        public bool TryPolymorph(int turns=2)
        {
            if(!IsAlive||turns<=0||HasStatus(StatusEffect.HolyLight))return false;
            if(!IsPolymorphed){_untransformedCharacter=Character??new SheepBehavior();_untransformedHealth=Health;_untransformedMaximum=MaximumHealth;Character=new SheepBehavior();MaximumHealth=10;Health=10;}
            PolymorphTurns=turns;return true;
        }
        public void EndPolymorph()
        {
            if(!IsPolymorphed)return;
            Character=_untransformedCharacter;Health=_untransformedHealth;MaximumHealth=_untransformedMaximum;
            _untransformedCharacter=null;PolymorphTurns=0;
        }
        private void TickPolymorph(){if(IsPolymorphed&&--PolymorphTurns<=0)EndPolymorph();}
        private readonly Dictionary<StatusEffect,int> _enchantments=new Dictionary<StatusEffect,int>();
        private readonly Dictionary<StatusEffect,PlayerState> _enchantmentSources=new Dictionary<StatusEffect,PlayerState>();
        public void ApplyEnchantment(StatusEffect status,PlayerState source,int turns=2)
        {
            if(!IsAlive||HasStatus(StatusEffect.HolyLight)||turns<=0||(status!=StatusEffect.Disabled&&status!=StatusEffect.Charm))return;
            _enchantments[status]=turns;_enchantmentSources[status]=source;
        }
        private void TickEnchantments()
        {
            foreach(var key in new List<StatusEffect>(_enchantments.Keys))
            {int left=_enchantments[key]-1;if(left<=0){_enchantments.Remove(key);_enchantmentSources.Remove(key);}else _enchantments[key]=left;}
        }
        private void ClearEnchantments(){_enchantments.Clear();_enchantmentSources.Clear();}
        public int LastDamageToOriginalForm {get;private set;}
        public int TakeDamageResolved(int amount)
        {
            LastDamageToOriginalForm=0;
            if(amount<=0)return 0;
            int absorbed=Math.Min(FalseLife,amount);FalseLife-=absorbed;amount-=absorbed;
            bool wasSheep=IsPolymorphed;
            int actual=Math.Min(Health,amount);Health-=actual;int overflow=amount-actual;
            if(!wasSheep)LastDamageToOriginalForm=actual;
            if(actual>0)ClearEnchantments();
            if(IsPolymorphed&&Health<=0)
            {EndPolymorph();int loss=Math.Min(Health,overflow);Health-=loss;actual+=loss;LastDamageToOriginalForm=loss;}
            TookDamageThisTurn=true;return actual;
        }

        private ICharacterBehavior _deadCharacter;
        private CharacterDefinition _deadDefinition;
        private string _deadName,_deadDisplayName;
        private int _deadMaximum;
        public bool IsSummon {get;private set;}
        public bool IsOriginalAlive=>IsAlive&&!IsSummon;
        public PlayerState SummonOwner {get;private set;}
        public int SummonActiveRound {get;private set;}
        internal void BecomeSpecter(PlayerState owner,int nextRound)
        {
            EndPolymorph();_deadCharacter=Character;_deadDefinition=Definition;_deadName=Name;_deadDisplayName=DisplayNameOverride;_deadMaximum=MaximumHealth;
            int left=LeftHand.Value,right=RightHand.Value;
            ClearStatuses();ClearAllResistances();ClearVulnerabilities();ClearImmunities();_permanentImmunities.Clear();ClearFalseLife();SetResonance(false);
            LeftHand.Reset(left);RightHand.Reset(right);SetDynamicBonuses(0,0);
            Character=new SpecterBehavior();Definition=SpecterDefinition;Name="咒缚幽灵";DisplayNameOverride=null;
            MaximumHealth=Health=10;IsSummon=true;SummonOwner=owner;SummonActiveRound=nextRound;
            AddPermanentImmunity(AttackDamageType.Necrotic);AddPermanentImmunity(AttackDamageType.Poison);
            foreach(var type in new[]{AttackDamageType.Acid,AttackDamageType.Cold,AttackDamageType.Fire,AttackDamageType.Lightning,AttackDamageType.Thunder})AddPermanentResistance(type);
        }
        internal void RestoreDeadSeat()
        {
            if(!IsSummon)return;EndPolymorph();ClearStatuses();ClearAllResistances();ClearVulnerabilities();ClearImmunities();_permanentImmunities.Clear();ClearFalseLife();
            Character=_deadCharacter;Definition=_deadDefinition;Name=_deadName;DisplayNameOverride=_deadDisplayName;MaximumHealth=_deadMaximum;Health=0;
            IsSummon=false;SummonOwner=null;_deadCharacter=null;
        }
        private static readonly CharacterDefinition SpecterDefinition=new CharacterDefinition("hexbound_specter","咒缚幽灵",SpecialLogicType.None,defenseBonus:-1,maxHealth:10);
        private sealed class SheepBehavior:CharacterTemplate
        {public override string DisplayName=>"羊";public override int DefaultAttackBonus=>-2;public override int DefaultDefenseBonus=>-2;public override AttackDamageType AttackTypeOf(AttackContext context)=>AttackDamageType.Bludgeoning;}
        private sealed class SpecterBehavior:CharacterTemplate,IStatusApplyingAttack
        {public override string DisplayName=>"咒缚幽灵";public override int DefaultDefenseBonus=>-1;public override AttackDamageType AttackTypeOf(AttackContext context)=>AttackDamageType.Necrotic;public StatusEffect StatusToApplyOnHit(AttackContext context)=>StatusEffect.Decay;}
    }

    public partial class GameManager
    {
        public Func<PlayerState,IArcaneChoicePolicy> ArcanePolicyFor {get;set;}
        public Func<PlayerState,IChoiceProvider> SummonProviderFor {get;set;}
        public Func<PlayerState,bool> AutomaticChoicesFor {get;set;}
        public AttackContext CurrentRedirectAttack {get;internal set;}
        public IdentityRole ActingIdentityOf(PlayerState p)=>IdentityOf(p?.IsSummon==true?p.SummonOwner:p);
        private readonly Dictionary<PlayerState,PlayerState> _pendingSpecters=new Dictionary<PlayerState,PlayerState>();
        public void QueueHexSpecter(PlayerState owner,PlayerState victim)
        {if(owner?.IsOriginalAlive==true&&victim!=null&&!victim.IsAlive&&!victim.IsSummon)_pendingSpecters[victim]=owner;}
        private void RemoveInvalidSpecters()
        {foreach(var p in _players)if(p.IsSummon&&(!p.IsAlive||p.SummonOwner?.IsOriginalAlive!=true)){p.RestoreDeadSeat();SeatRevision++;}}
        private void CreatePendingSpecters()
        {
            foreach(var pair in _pendingSpecters)
            {
                var dead=pair.Key;var owner=pair.Value;
                if(!owner.IsOriginalAlive||dead.IsAlive||dead.IsSummon||IsGameOver)continue;
                foreach(var existing in _players)if(existing.IsSummon&&ReferenceEquals(existing.SummonOwner,owner)){existing.RestoreDeadSeat();SeatRevision++;}
                dead.BecomeSpecter(owner,RoundCount+1);SeatRevision++;NoteSkill(owner,"咒缚幽灵",$"在死者座位召唤，下一轮行动");
            }
            _pendingSpecters.Clear();
        }
        private void ArcaneTurnEnded(PlayerState actor)
        {
            foreach(var player in _players)
            {
                var original=player.PersistentCharacter;
                if(original is ICharacterBehaviorContainer container){foreach(var part in container.Parts)if(part is IHexbladeCurse curse)curse.TickCurse(actor);}
                else if(original is IHexbladeCurse curse)curse.TickCurse(actor);
            }
        }
        public int OriginalTeamOf(PlayerState p)=>p!=null&&_team.TryGetValue(p,out int team)?team:-1;
        public bool IsKnownAlly(PlayerState observer,PlayerState target)
        {
            if(observer==null||target==null)return false;
            if(observer.IsSummon)observer=observer.SummonOwner;
            if(target.IsSummon)target=target.SummonOwner;
            if(ReferenceEquals(observer,target))return true;
            if(!IsIdentityMode)return IsTeammate(observer,target);
            if(!IsIdentityPublic(target))return false;
            var a=IdentityOf(observer);var b=IdentityOf(target);
            return IdentityRoleInfo.IsLordSide(a)&&IdentityRoleInfo.IsLordSide(b)||a==IdentityRole.Rebel&&b==IdentityRole.Rebel;
        }
        public bool CanTakeTurn(PlayerState p)=>p!=null&&p.IsAlive&&(!p.IsSummon||RoundCount>=p.SummonActiveRound);
    }

    public partial class AttackContext
    {
        internal void NotifyCompletedHealthDamage(GameManager game)
        {
            if(ActualHealthDamage>0&&!WasCancelled&&ActualDamageSource!=null)
                foreach(var hook in CharacterBehaviors.Implementers<ICompletedDamageDealt>(ActualDamageSource))hook.OnCompletedDamage(this,game);
            if(OriginalFormHealthDamage>0&&!WasCancelled&&Defender?.IsAlive==true)
                foreach(var hook in CharacterBehaviors.Implementers<ICompletedDamageTaken>(Defender))hook.OnAttackDamageTaken(Defender,this,game);
        }
        internal static void NotifyStatusHealthDamage(PlayerState source,PlayerState target,int actual,int original,AttackDamageType type,GameManager game)
        {
            if(actual<=0)return;
            var ctx=new AttackContext(source??target,target,(source??target).LeftHand,actual,game)
                {IsStatusDamage=true,DamageOrigin=source,AttackType=type,ActualHealthDamage=actual,OriginalFormHealthDamage=original,AppliedDamage=actual};
            ctx.NotifyCompletedHealthDamage(game);
        }
    }
}
