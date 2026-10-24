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
        { if(manager?.ResolvingPassiveShieldBreak!=true)RestrainInRange(owner, manager); }

        public override void OnOtherShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager)
            => RestrainInRange(owner, manager);

        private static void RestrainInRange(PlayerState owner, GameManager manager)
        {
            if (owner == null || manager == null) return;
            AncientRoleChoices.Offer(AncientRoleChoices.AncientRestrain,"古贤束缚 · 选择射程内角色或不发动",owner,manager,
                AncientRoleChoices.OtherPlayers(owner,manager,true),true,true,hand=>{
                    var target=manager.OwnerOfHand(hand);if(!manager.CanReach(owner,target))return;
                    target.ApplyStatus(StatusEffect.Restrained,2,owner);manager.NoteSkill(owner,"古贤束缚",$"{target.Name} 束缚2回合");});
        }

        public void OnDamageTaken(PlayerState owner, int damage, AttackContext context, GameManager manager)
        {
            if (owner == null || !owner.IsAlive || damage <= 0 || manager == null) return;
            if (owner.Health >= 10)
            {
                AncientRoleChoices.Offer(AncientRoleChoices.AncientHeal,"古贤庇护 · 选择射程内另一名角色回复2",owner,manager,
                    AncientRoleChoices.OtherPlayers(owner,manager,true),false,true,hand=>{
                        var target=manager.OwnerOfHand(hand);if(!manager.CanReach(owner,target))return;
                        int before=target.Health;target.Heal(2);manager.NoteSkill(owner,"古贤庇护",$"{target.Name} 回复{target.Health-before}");});
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
