using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public enum CombatVisualKind { Attack, ShieldForm, ShieldBreak, Taichi, Sync, VowTransfer }
    public enum CombatVisualOutcome { Hit, Blocked, Dodged, Cancelled }

    // Copies only display data. No live status collections or mutable Hand values escape.
    public sealed class CombatVisualPlayer
    {
        public PlayerState Player { get; }
        public int Health { get; }
        public int MaximumHealth { get; }
        public string Stats { get; }
        public string Emblem { get; }
        public IReadOnlyList<CombatStatusIcon> Icons { get; }
        private readonly Hand _left,_right;
        public Hand Left => Copy(_left);
        public Hand Right => Copy(_right);
        public CombatVisualPlayer(PlayerState p, GameManager game)
        {
            Player=p;Health=p.Health;MaximumHealth=p.MaximumHealth;Emblem=p.BattleEmblemKey;
            int range=StatusRules.EffectiveAttackRange(p,game);if(range<0)range=GameManager.BaseRangeOf(p);
            Stats="攻 "+Signed(StatusRules.EffectiveAttackBonus(p))+"    防 "+Signed(StatusRules.EffectiveDefenseBonus(p))+"    距 "+(range==int.MaxValue?"∞":range.ToString());
            Icons=CombatStatusIcons.For(p,game).AsReadOnly();_left=Copy(p.LeftHand);_right=Copy(p.RightHand);
        }
        private static string Signed(int n)=>n>0?"+"+n:n.ToString();
        private static Hand Copy(Hand hand)
        {var h=new Hand(hand.Value);h.SetShieldState(hand.State);if(hand.IsLocked)h.Lock();h.IsRestrained=hand.IsRestrained;return h;}
    }
    public sealed class CombatVisualFrame
    {
        public IReadOnlyList<CombatVisualPlayer> Players { get; }
        internal CombatVisualFrame(GameManager game)
        {var list=new List<CombatVisualPlayer>();foreach(var p in game.Players)list.Add(new CombatVisualPlayer(p,game));Players=list.AsReadOnly();}
    }
    public sealed class CombatVisualEvent
    {
        public CombatVisualKind Kind { get; }
        public CombatVisualOutcome Outcome { get; }
        public PlayerState Actor { get; }
        public PlayerState Target { get; }
        public PlayerState IntendedTarget { get; }
        public Hand SourceHand { get; }
        public AttackDamageType Type { get; }
        public string Weapon { get; }
        public int Distance { get; }
        public int Segment { get; }
        public int Damage { get; }
        public bool Reflected { get; }
        public bool Mitigated { get; }
        public string RedirectKind {get;}
        public PlayerState EvasionActor {get;}
        public CombatVisualFrame Before { get; }
        public CombatVisualFrame After { get; }
        internal CombatVisualEvent(CombatVisualKind kind,CombatVisualOutcome outcome,PlayerState actor,PlayerState target,PlayerState intended,
            Hand hand,AttackDamageType type,string weapon,int distance,int segment,int damage,bool reflected,bool mitigated,CombatVisualFrame before,CombatVisualFrame after,string redirect=null,PlayerState evasionActor=null)
        {Kind=kind;Outcome=outcome;Actor=actor;Target=target;IntendedTarget=intended;SourceHand=hand;Type=type;Weapon=weapon;Distance=distance;Segment=segment;Damage=damage;Reflected=reflected;Mitigated=mitigated;Before=before;After=after;RedirectKind=redirect;EvasionActor=evasionActor;}
    }
    public sealed class CombatVisualToken
    {
        internal int Index;internal AttackContext Attack;internal int Segment,Distance;internal string Weapon;
        internal CombatVisualFrame Before;internal bool Finished;
    }
    public partial class GameManager
    {
        // Opt in only for an observed match; simulations retain their existing cost and behavior.
        public bool RecordCombatVisuals { get; set; }
        private readonly List<CombatVisualEvent> _combatVisuals=new List<CombatVisualEvent>();
        public CombatVisualFrame CaptureVisualFrame()=>RecordCombatVisuals?new CombatVisualFrame(this):null;
        public IReadOnlyList<CombatVisualEvent> DrainCombatVisuals()
        {var copy=_combatVisuals.FindAll(e=>e!=null).AsReadOnly();_combatVisuals.Clear();return copy;}
        public void ClearCombatVisuals()=>_combatVisuals.Clear();
        public CombatVisualToken BeginVisualAttack(AttackContext attack,int segment)
        {
            if(!RecordCombatVisuals||attack.IsStatusDamage)return null;
            var token=new CombatVisualToken{Index=_combatVisuals.Count,Attack=attack,Segment=segment,
                Distance=DistanceBetween(attack.Attacker,attack.Defender),Weapon=CombatVisualRouting.Weapon(attack,this),Before=CaptureVisualFrame()};
            _combatVisuals.Add(null);return token;
        }
        public void FinishVisualAttack(CombatVisualToken token,CombatVisualOutcome outcome,int damage=0)
        {
            if(token==null||token.Finished)return;token.Finished=true;var a=token.Attack;
            _combatVisuals[token.Index]=new CombatVisualEvent(CombatVisualKind.Attack,outcome,a.Attacker,a.Defender,a.VisualIntendedTarget??a.Defender,
                a.SourceHand,a.AttackType,token.Weapon,token.Distance,token.Segment,damage,a.IsReflected,a.WasMitigated,token.Before,CaptureVisualFrame(),a.VisualRedirectKind,a.VisualEvasionActor);
        }
        public void RecordVowTransfer(PlayerState owner,PlayerState previous,PlayerState target,CombatVisualFrame before)
        {if(RecordCombatVisuals)_combatVisuals.Add(new CombatVisualEvent(CombatVisualKind.VowTransfer,CombatVisualOutcome.Hit,owner,target,previous,owner.LeftHand,AttackDamageType.None,"Effects/vow_of_enmity",0,0,0,false,false,before,CaptureVisualFrame()));}
        public void RecordVisualTrueDamage(AttackContext attack,int damage,CombatVisualFrame before)
        {if(RecordCombatVisuals)_combatVisuals.Add(new CombatVisualEvent(CombatVisualKind.Attack,CombatVisualOutcome.Hit,attack.Attacker,attack.Defender,attack.Defender,attack.SourceHand,AttackDamageType.None,"fx_impact",0,0,damage,false,false,before,CaptureVisualFrame()));}
        public void RecordVisualState(CombatVisualKind kind,PlayerState owner,Hand hand,CombatVisualFrame before=null)
        {
            if(!RecordCombatVisuals)return;
            _combatVisuals.Add(new CombatVisualEvent(kind,CombatVisualOutcome.Hit,owner,owner,owner,hand,AttackDamageType.None,null,0,0,0,false,false,before??CaptureVisualFrame(),CaptureVisualFrame()));
        }
    }
    public static class CombatVisualRouting
    {
        public static string Weapon(AttackContext a,GameManager game)
        {
            if(a.IsReflected)return "fx_impact";
            if(a.IsSpecialAttack&&a.IgnoreStatBonuses&&a.RawDamage==OathOfDevotionLogic.ShockDamage
                &&CharacterBehaviors.FirstImplementer<OathOfDevotionLogic>(a.Attacker)!=null)return "fx_radiant";
            if(AttackDamageTypes.IsMagic(a.AttackType))return a.VisualWeapon??"fx_"+AttackDamageTypes.IconKeyOf(a.AttackType);
            if(a.AttackType==AttackDamageType.Slashing)return "weapon_sword";
            if(a.AttackType==AttackDamageType.Piercing)return game.DistanceBetween(a.Attacker,a.Defender)<=1?"weapon_rapier":"projectile_arrow";
            string profession=a.Attacker.Definition?.Profession?.Id;
            if(profession=="monk")return "weapon_fist";
            if(profession=="wizard"||profession=="sorcerer"||profession=="cleric")return "weapon_staff";
            return "weapon_hammer";
        }
    }
}
