using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>德鲁伊·大地结社：回合结束封锁一只手作为被加数；3 可束缚。</summary>
    public sealed class CircleOfTheLandLogic : CharacterTemplate,
        IAttackTypeProvider, ITurnStartHook, ITurnEndHook
    {
        private PlayerState _owner;
        private Hand _restricted;

        public override string DisplayName => "德鲁伊·大地结社";
        public override int DefaultDefenseBonus => 1;
        public override int AttackBonus => 0;
        public override int BaseAttackRange => 1;

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _owner = owner;
            ClearRestriction();
        }

        private void ClearRestriction()
        {
            if (_restricted != null) _restricted.SetTargetRestricted(this,false);
            _restricted = null;
        }

        public bool RestrictHand(Hand hand, GameManager manager)
        {
            if (hand == null || manager?.OwnerOfHand(hand)?.IsAlive!=true) return false;
            ClearRestriction();
            _restricted = hand;
            hand.SetTargetRestricted(this,true);
            var target=manager.OwnerOfHand(hand);
            manager.NoteSkill(_owner, "大地行者", $"{target.Name}{(ReferenceEquals(target.LeftHand,hand)?"左手":"右手")}禁作加数，至下次回合开始");
            return true;
        }

        public void OnTurnStarted(PlayerState owner, GameManager manager)
        {
            if (!owner.IsAlive || ReferenceEquals(manager?.CurrentPlayer, owner)) ClearRestriction();
        }

        public void OnTurnEnded(PlayerState owner, GameManager manager)
        {
            if (!owner.IsAlive || manager==null) return;
            var hands=new System.Collections.Generic.List<Hand>();
            foreach(var p in manager.Players)if(p.IsAlive)foreach(var h in p.Hands)hands.Add(h);
            AncientRoleChoices.Offer(AncientRoleChoices.LandSeal,"大地行者 · 选择禁作加数的一只手",owner,manager,hands,false,false,hand=>RestrictHand(hand,manager));
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
                                                  int actedHandValueBefore, int targetValue,
                                                  HandEffectType effect, GameManager manager,
                                                  IPreTurnChoiceSink choiceSink)
        {
            if (owner == null || actedHand == null || actedHand.Value != 3) return;
            AncientRoleChoices.Offer(AncientRoleChoices.LandRestrain,"自然恢复 · 选择角色或不发动",owner,manager,
                AncientRoleChoices.OtherPlayers(owner,manager,false),true,true,hand=>{
                    var target=manager.OwnerOfHand(hand);target.ApplyStatus(StatusEffect.Restrained,2,owner);
                    manager.NoteSkill(owner,"自然恢复",$"{target.Name} 束缚2回合");});
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Bludgeoning;
    }
}
