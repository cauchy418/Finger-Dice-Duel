namespace FingerDiceDuel.Core
{
    public interface IGraveStrength { bool GraveSpent {get;} void SurviveDeath(PlayerState owner); }
    public interface IShadowEyes { }
    public static class ShadowRules
    {
        public const string Darkness="shadow_darkness";
        public static int Outgoing(PlayerState attacker,PlayerState target,int damage)
            =>attacker!=null&&target!=null&&attacker.HasStatus(StatusEffect.DarknessBlind)&&!target.HasStatus(StatusEffect.DarknessBlind)?(damage+1)/2:damage;
    }
}
