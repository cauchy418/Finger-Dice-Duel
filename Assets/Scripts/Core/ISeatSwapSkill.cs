namespace FingerDiceDuel.Core
{
    /// <summary>可主动跳过回合换位的角色能力；表现层与控制器不依赖具体角色类型。</summary>
    public interface ISeatSwapSkill
    {
        bool CanSwapSeats(PlayerState owner, PlayerState target, GameManager game);
        bool TrySwapSeats(PlayerState owner, PlayerState target, GameManager game);
    }

    public static class SeatSwapSkills
    {
        public static ISeatSwapSkill Of(PlayerState owner)
        {
            if (owner?.Character is ISeatSwapSkill skill) return skill;
            if (owner?.Character is ICharacterBehaviorContainer container)
                foreach (var part in container.Parts)
                    if (part is ISeatSwapSkill nested) return nested;
            return null;
        }
    }
}
