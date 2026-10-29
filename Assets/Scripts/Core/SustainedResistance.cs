using System;
using System.Collections.Generic;
namespace FingerDiceDuel.Core
{
    public interface IPendingElementResistance {bool ProvidesResistance(AttackDamageType type);void SuppressResistance();}
    public partial class PlayerState
    {
        private sealed class Grant {public AttackDamageType Type;public Func<bool> Valid;}
        private readonly Dictionary<object,Grant> _resistanceGrants=new Dictionary<object,Grant>();
        public void GrantSustainedResistance(object source,AttackDamageType type,Func<bool> valid)
        {if(source!=null&&!HasStatus(StatusEffect.HolyLight))_resistanceGrants[source]=new Grant{Type=type,Valid=valid};}
        public void RemoveSustainedResistance(object source){if(source!=null)_resistanceGrants.Remove(source);}
        public bool HasSustainedResistance(AttackDamageType type)
        {
            if(!IsAlive||HasStatus(StatusEffect.HolyLight))return false;
            foreach(var entry in _resistanceGrants.Values)
                if(entry.Valid()&&(entry.Type==type||!AttackDamageTypes.IsMagic(entry.Type)&&!AttackDamageTypes.IsMagic(type)&&type!=AttackDamageType.None))return true;
            foreach(var provider in CharacterBehaviors.Implementers<IPendingElementResistance>(this))if(provider.ProvidesResistance(type))return true;
            return false;
        }
        public void ClearSustainedResistances(){_resistanceGrants.Clear();foreach(var provider in CharacterBehaviors.Implementers<IPendingElementResistance>(this))provider.SuppressResistance();}
    }
}
