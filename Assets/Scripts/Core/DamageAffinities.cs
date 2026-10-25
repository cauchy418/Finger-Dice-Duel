using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public enum DamageAffinity { Resistance, Vulnerability, Immunity }

    /// <summary>十三种伤害的临时修正；易伤与免疫逐类型独立，不把物理三类合并。</summary>
    public static class DamageAffinityNames
    {
        public static string Name(DamageAffinity affinity) => affinity == DamageAffinity.Immunity ? "免疫"
            : affinity == DamageAffinity.Vulnerability ? "易伤" : "抗性";
        public static string Name(DamageAffinity affinity, AttackDamageType type) => AttackDamageTypes.NameOf(type) + Name(affinity);
        public static string Key(DamageAffinity affinity, AttackDamageType type)
        {
            if (affinity == DamageAffinity.Resistance) return ResistanceNames.Key(type);
            string name = affinity.ToString();
            return name + "/" + name.ToLowerInvariant() + "_" + type.ToString().ToLowerInvariant();
        }
    }

    public partial class PlayerState
    {
        private readonly HashSet<AttackDamageType> _vulnerabilities = new HashSet<AttackDamageType>();
        private readonly HashSet<AttackDamageType> _permanentImmunities=new HashSet<AttackDamageType>();
        public void AddPermanentImmunity(AttackDamageType type)=>_permanentImmunities.Add(type);
        private readonly HashSet<AttackDamageType> _immunities = new HashSet<AttackDamageType>();
        public IReadOnlyCollection<AttackDamageType> Vulnerabilities => _vulnerabilities;
        public IReadOnlyCollection<AttackDamageType> Immunities => _immunities;
        public bool HasVulnerability(AttackDamageType type) => _vulnerabilities.Contains(type);
        public bool HasImmunity(AttackDamageType type) => _immunities.Contains(type)||_permanentImmunities.Contains(type);
        public void AddVulnerability(AttackDamageType type)
        {
            if (!HasStatus(StatusEffect.HolyLight) && System.Array.IndexOf(ResistanceNames.All, type) >= 0)
                _vulnerabilities.Add(type);
        }
        public void AddImmunity(AttackDamageType type)
        {
            if (!HasStatus(StatusEffect.HolyLight) && System.Array.IndexOf(ResistanceNames.All, type) >= 0)
                _immunities.Add(type);
        }
        public void ClearVulnerabilities() => _vulnerabilities.Clear();
        public void ClearImmunities() => _immunities.Clear();
        public void ClearTemporaryDamageAffinities()
        {
            ClearResistances(); ClearVulnerabilities(); ClearImmunities();
        }
    }
}
