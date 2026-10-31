using System;
using System.Collections.Generic;
namespace FingerDiceDuel.Core
{
    [Flags] public enum AttackGuard
    { None=0,Shield=1,Necromancy=2,Mitigation=4,WholeAttackReduction=8,Transfer=16,Barrier=32,FalseLife=64,Polymorph=128,TypeImmunity=256,Counter=512 }
    public sealed class ElementHit
    {
        public AttackDamageType AttackType {get;}
        public int Index {get;} public AttackGuard Guard {get;} public bool Resisted {get;} public bool Immune {get;} public int Damage {get;} public int HealthDamage {get;}
        public ElementHit(int index,AttackGuard guard,bool resisted,bool immune,int damage=0,int healthDamage=0,AttackDamageType attackType=AttackDamageType.None){AttackType=attackType;Index=index;Guard=guard;Resisted=resisted;Immune=immune;Damage=damage;HealthDamage=healthDamage;}
        public int StatusDuration(StatusEffect status,int original,bool elemental=true)
            => Immune&&elemental?0:StatusNames.IsNegativeStatus(status)?Duration(original,false):original;
        public int Duration(int original,bool elemental=true)=>Immune&&elemental?0:Math.Max(0,original-(Guard!=AttackGuard.None?1:0)-(Resisted?1:0));
    }
    public partial class AttackContext
    {
        private readonly List<ElementHit> _elementHits=new List<ElementHit>();
        public IReadOnlyList<ElementHit> ElementHits=>_elementHits;
        public bool TempestCountered {get;set;}
        private AttackGuard WholeGuard=> (_wholeAttackHalvings>0?AttackGuard.WholeAttackReduction:AttackGuard.None)
            |(WasRedirected?AttackGuard.Transfer:AttackGuard.None)
            |(TempestCountered?AttackGuard.Counter:AttackGuard.None);
        private void RecordElementHit(int index,AttackGuard guard,int damage=0,int healthDamage=0)
            =>_elementHits.Add(new ElementHit(index,guard|WholeGuard|(WasTypeImmune?AttackGuard.TypeImmunity:AttackGuard.None),Defender.HasResistance(AttackType),WasTypeImmune&&AttackDamageTypes.IsMagic(AttackType),damage,healthDamage,AttackType));
        private bool _attachedStatusReady;
        private readonly List<Action> _queuedAttachedStatuses=new List<Action>();
        public void ApplyAttachedStatus(PlayerState target,StatusEffect status,int turns,PlayerState source,GameManager game,string skill,bool enchantment=false,bool requireDamage=false,bool requireHealthDamage=false)
        {
            bool follows=ReferenceEquals(target,Defender);
            Action apply=()=>{
                var actual=follows?Defender:target;if(actual?.IsAlive!=true||WasCancelled)return;
                if(IsStatusDamage){if(enchantment)actual.ApplyEnchantment(status,source,turns);else actual.ApplyStatus(status,turns,source);return;}
                foreach(var hit in _elementHits){
                    if(requireDamage&&hit.Damage<=0||requireHealthDamage&&hit.HealthDamage<=0)continue;
                    int duration=hit.StatusDuration(status,turns,false);if(duration<=0){game?.NoteSkill(source,skill,$"第{hit.Index+1}段：附加0回合",actual,new SkillVisualData(value:0,variant:"blocked",status:status));continue;}
                    if(enchantment)actual.ApplyEnchantment(status,source,duration);else actual.ApplyStatus(status,duration,source);
                    if(actual.HasStatus(status))game?.NoteSkill(source,skill,$"第{hit.Index+1}段 {actual.Name}：{StatusNames.Of(status)} {duration}回合"+(enchantment?"，后续受伤解除":""),actual,new SkillVisualData(value:duration,variant:"applied",status:status));
                }
            };
            if(_attachedStatusReady||IsStatusDamage)apply();else _queuedAttachedStatuses.Add(apply);
        }
        public void ApplyUntimedOnHit(Action apply)
        {Action gated=()=>{if(!WasCancelled&&_elementHits.Count>0&&Defender.IsAlive)apply();};if(_attachedStatusReady)gated();else _queuedAttachedStatuses.Add(gated);}
        private void FinishAttachedStatusPhase()
        {_attachedStatusReady=true;foreach(var apply in _queuedAttachedStatuses)apply();_queuedAttachedStatuses.Clear();}
    }
}
