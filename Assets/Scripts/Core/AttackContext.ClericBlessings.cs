using System;
using System.Collections.Generic;
using FingerDiceDuel.Characters.SpecialLogics;
namespace FingerDiceDuel.Core
{
 public partial class AttackContext
 {
  private StatusEffect _natureStoredStatus;
  private bool _lightBlessing,_lightPierces,_natureBlessedTarget;
  private readonly HashSet<int> _dawnAnswered=new HashSet<int>();
  private readonly Dictionary<int,int> _dawnBonuses=new Dictionary<int,int>();
  private void BeginClericAttack()
  {
   if(IsStatusDamage||IsReflected||IsKnowledgeForceDamage)return;
   if(!Attacker.IsWildShaped&&!Attacker.IsPolymorphed)_natureStoredStatus=CharacterBehaviors.FirstImplementer<NatureDomainLogic>(Attacker)?.ConsumeStoredStatus()??StatusEffect.None;
   _lightBlessing=Attacker.HasStatus(StatusEffect.LightBlessing)&&!Attacker.HasStatus(StatusEffect.HolyLight);
  }
  private void ConvertLightAttack()
  {
   if(!_lightBlessing)return;
   _lightPierces=AttackType==AttackDamageType.Radiant;
   AttackType=AttackDamageType.Radiant;ElementOverride=DamageType.None;
  }
  private bool SuppressesNatureElement()
  {
   var nature=CharacterBehaviors.FirstImplementer<NatureDomainLogic>(Defender);
   if(IsStatusDamage||Defender.IsPolymorphed||Defender.IsWildShaped||nature==null||!NatureDomainLogic.IsElement(AttackType))return false;
   nature.StoreElement(AttackType);return true;
  }
  private bool RequestDawn(int index,GameManager game,Action resume)
  {
   if(_dawnAnswered.Contains(index)||IsStatusDamage||IsReflected||Attacker.IsPolymorphed||Attacker.IsWildShaped||CharacterBehaviors.FirstImplementer<RadiantLightDomainLogic>(Attacker)==null)return false;
   var negatives=new List<StatusEffect>();foreach(var status in Defender.Statuses)if(StatusNames.IsNegativeStatus(status))negatives.Add(status);
   if(negatives.Count==0){_dawnAnswered.Add(index);return false;}
   bool deferred=false;
   var choice=new WizardChoice(Attacker,"黎明曙光 · 清除目标全部负面状态，每个令本段伤害+2？",NewSubclassChoices.YesNo("净化并增伤","Subclasses/cleric_light_domain"),value=>{
    _dawnAnswered.Add(index);
    if(value==1){var frame=game?.CaptureVisualFrame();int count=0;foreach(var status in negatives)if(Defender.HasStatus(status)){Defender.RemoveStatus(status);count++;}
     _dawnBonuses[index]=count*2;game?.NoteSkill(Attacker,"黎明曙光",Defender.Name+" 清除"+count+"种负面状态，本段伤害+"+(count*2),Defender,new SkillVisualData(value:count),frame);}
    if(deferred)resume();
   });
   if(game?.DeferWizardChoice?.Invoke(choice)==true){deferred=true;return true;}
   choice.ChooseOption(game?.ClericPolicy(Attacker)?.CleanseLightAttack(Attacker,Defender,game)==true?1:0);return false;
  }
  private void FinishClericBlessings(GameManager game)
  {
   if(WasCancelled||IsStatusDamage||IsKnowledgeForceDamage)return;
   if(_natureStoredStatus!=StatusEffect.None&&Defender.IsAlive)
    foreach(var hit in _elementHits){int turns=hit.StatusDuration(_natureStoredStatus,2);if(turns>0)Defender.ApplyStatus(_natureStoredStatus,turns,Attacker);}
   if(_natureBlessedTarget&&Attacker.IsAlive){var frame=game?.CaptureVisualFrame();Attacker.ApplyStatus(StatusEffect.Restrained,2,Defender);
    if(Attacker.HasStatus(StatusEffect.Restrained))game?.NoteSkill(Defender,"自然祝福",Attacker.Name+" 束缚2回合",Attacker,new SkillVisualData(status:StatusEffect.Restrained),frame);}
  }
  private void ApplyLightBlind(ElementHit hit)
  {
   if((!_lightBlessing&&CharacterBehaviors.FirstImplementer<RadiantLightDomainLogic>(Attacker)==null)||WasCancelled||SuppressAttackStatuses||!Defender.IsAlive)return;
   int turns=hit.StatusDuration(StatusEffect.Blind,2);if(turns>0)Defender.ApplyStatus(StatusEffect.Blind,turns,Attacker);
  }
 }
}
