using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>圣武士·古贤之誓：碎盾束缚、受伤后按血量治疗。</summary>
    public sealed class OathOfTheAncientsLogic : CharacterTemplate,
        IAttackTypeProvider, IPostDamageTaken
    {
        public override string DisplayName => "圣武士·古贤之誓";
        public override int DefaultAttackBonus => -1;
        public override int DefaultDefenseBonus => 1;
        public override int BaseAttackRange => 1;

        public override void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager)
            => RestrainInRange(owner, manager);

        public override void OnOtherShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager)
            => RestrainInRange(owner, manager);

        private static void RestrainInRange(PlayerState owner, GameManager manager)
        {
            if (owner == null || manager == null) return;
            PlayerState best = null;
            int bestHealth = int.MaxValue;
            for (int i = 0; i < manager.Players.Count; i++)
            {
                PlayerState p = manager.Players[i];
                if (p == null || !p.IsAlive || ReferenceEquals(p, owner)) continue;
                if (!manager.CanReach(owner, p)) continue;
                if (p.Health < bestHealth) { best = p; bestHealth = p.Health; }
            }
            if (best == null) return;
            best.ApplyStatus(StatusEffect.Restrained, 2, owner);
            manager.NoteSkill(owner, "古贤束缚", $"{best.Name} 束缚 2 回合");
        }

        public void OnDamageTaken(PlayerState owner, int damage, AttackContext context, GameManager manager)
        {
            if (owner == null || damage <= 0 || manager == null) return;
            if (owner.Health >= 10)
            {
                // 目标必须在攻击范围内；优先血量最低的其他角色。
                PlayerState best = null;
                int bestHealth = int.MaxValue;
                for (int i = 0; i < manager.Players.Count; i++)
                {
                    PlayerState p = manager.Players[i];
                    if (p == null || !p.IsAlive || ReferenceEquals(p, owner)) continue;
                    if (!manager.CanReach(owner, p)) continue;
                    if (p.Health < bestHealth) { best = p; bestHealth = p.Health; }
                }
                if (best != null)
                {
                    best.Heal(2);
                    manager.NoteSkill(owner, "古贤庇护", $"回复 {best.Name} 2 点");
                }
            }
            else
            {
                owner.Heal(2);
                manager.NoteSkill(owner, "古贤自愈", "回复 2 点");
            }
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Bludgeoning;
    }
}
