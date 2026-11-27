namespace FingerDiceDuel.Core
{
    public interface IGraveStrength { bool GraveSpent {get;} void SurviveDeath(PlayerState owner); }
    public interface IShadowEyes { }
    public static class ShadowRules
    {
        public const string Darkness="shadow_darkness";
        public static int Outgoing(PlayerState attacker,PlayerState target,int damage)
            => StatusRules.ApplyOutgoingAttackDamage(attacker,target,damage,AttackDamageType.Bludgeoning);
    }
}
