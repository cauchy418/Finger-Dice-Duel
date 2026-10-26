using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public interface IVowOfEnmity { PlayerState VowTarget { get; } }
    public static class DivineChoices
    {
        public const string Blessing="war_blessing", Nine="war_blessing_nine", Vow="vow_of_enmity", Retarget="vow_retarget";
        public static bool IsAttack(HandEffectType effect)=>effect==HandEffectType.Attack4||effect==HandEffectType.Attack6||effect==HandEffectType.Attack8;
        public static bool AttackDigit(PlayerState owner,int value)
            =>IsAttack(CharacterBehaviors.DisplayedEffectOf(owner,value))
                ||value==9&&owner.HasStatus(StatusEffect.WarBlessing)&&!owner.IsPolymorphed;
        public static List<SkillChoiceOption> NineOptions()=>new List<SkillChoiceOption>{
            new SkillChoiceOption(1,"放弃回复 · 攻击9","Status/war_blessing","本次9改为9点基础伤害的攻击。"),
            new SkillChoiceOption(0,"照常回复","UI/status_counter","保留本次回复。")};
        public static void ChooseTarget(string key,string prompt,PlayerState owner,GameManager game,List<PlayerState> targets,bool optional,Action<PlayerState> apply)
        {
            if(owner==null||!owner.IsAlive||targets.Count==0)return;
            var hands=new List<Hand>();foreach(var p in targets)hands.Add(p.LeftHand);
            var request=new WizardChoice(owner,prompt,hands,optional,h=>{
                if(h==null){apply(null);return;}
                var target=game.OwnerOfHand(h);if(owner.IsAlive&&!owner.IsPolymorphed&&target?.IsAlive==true)apply(target);
            },true,optional?"不发动":"");
            if(game.DeferWizardChoice?.Invoke(request)==true)return;
            var policy=game.ArcanePolicyFor?.Invoke(owner)??game.GetChoiceProvider(owner) as IArcaneChoicePolicy;
            var picked=policy?.ChooseArcaneTarget(key,owner,targets,game);
            if(!request.Choose(picked?.LeftHand)&&!optional)request.Choose(hands[0]);
        }
        public static void OfferFreeOperation(PlayerState owner,GameManager game)
        {
            if(!owner.IsAlive||owner.IsPolymorphed||!StatusRules.CanAct(owner))return;
            var sources=new List<Hand>();foreach(var move in game.GetLegalMoves(owner))if(!sources.Contains(move.ActedHand))sources.Add(move.ActedHand);
            NewSubclassChoices.OfferHand(NewSubclassChoices.StormSource,"誓言反击 · 选择运算手，或放弃额外运算",owner,game,sources,true,source=>{
                if(source==null||!owner.IsAlive||owner.IsPolymorphed)return;
                var targets=new List<Hand>();foreach(var move in game.GetLegalMoves(owner))if(ReferenceEquals(move.ActedHand,source))targets.Add(move.TargetHand);
                NewSubclassChoices.OfferHand(NewSubclassChoices.StormAddend,"誓言反击 · 点击加数（只改变手值）",owner,game,targets,true,target=>{
                    if(target!=null)game.ApplyEffectlessOperation(owner,source,target,"誓言反击");
                });
            });
        }
    }
    public partial class GameManager
    {
        public bool ApplyEffectlessOperation(PlayerState actor,Hand source,Hand target,string skill)
        {
            if(!actor.IsAlive||actor.IsPolymorphed||!StatusRules.CanAct(actor)||!IsLegalMove(actor,source,target,out _))return false;
            int before=source.Value,add=target.Value;bool shield=source.IsShield;
            int value=Math.Max(1,StatusRules.ApplyResultModifiers(actor,Hand.ComputeSum(before,add)));source.SetValue(value);
            if(shield)foreach(var warden in CharacterBehaviors.Implementers<PathOfTheWardenLogic>(actor))warden.OnOwnShieldBroken(actor,source,this);
            NoteSkill(actor,skill,$"{CombatText.HandName(actor,source)}{before}+{add}→{value}（只改手值）");return true;
        }
        public bool? WarBlessingNineAttack { get; set; }
        public PlayerState ShieldBreaker { get; private set; }
        public HandEffectType OperationEffect(PlayerState actor,int value)
        {
            var effect=CharacterBehaviors.DisplayedEffectOf(actor,value);
            return value==9&&effect==HandEffectType.Heal&&actor!=null&&actor.HasStatus(StatusEffect.WarBlessing)
                &&!actor.IsPolymorphed&&WarBlessingNineAttack==true?HandEffectType.Attack8:effect;
        }
        private HandEffectType ResolveWarBlessingNine(PlayerState actor,Hand hand,HandEffectType effect)
        {
            if(hand.Value!=9||effect!=HandEffectType.Heal||!actor.HasStatus(StatusEffect.WarBlessing)||actor.IsPolymorphed)return effect;
            bool attack=WarBlessingNineAttack??SkillChoicePolicy.Choose(DivineChoices.Nine,DivineChoices.NineOptions(),actor,DamageTargetPlayer??GetOpponent(actor),this)==1;
            WarBlessingNineAttack=attack;
            if(!attack)return effect;
            NoteSkill(actor,"战恩","9放弃回复，改为攻击");return HandEffectType.Attack8;
        }
    }
    public partial class PlayerState
    {
        /// <summary>治疗仅净化不利状态，保留战恩；圣光与重开仍清除全部。</summary>
        public void ClearNegativeStatuses()
        {
            int turns=StatusTurnsLeft(StatusEffect.WarBlessing);var source=StatusSourceOf(StatusEffect.WarBlessing);
            ClearStatuses();if(turns>0)ApplyStatus(StatusEffect.WarBlessing,turns,source);
        }
    }
}
