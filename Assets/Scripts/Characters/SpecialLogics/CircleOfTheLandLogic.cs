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
            if (_restricted != null) _restricted.SetTargetRestricted(false);
            _restricted = null;
        }

        public bool RestrictHand(Hand hand, GameManager manager)
        {
            if (hand == null) return false;
            ClearRestriction();
            _restricted = hand;
            hand.SetTargetRestricted(true);
            manager?.NoteSkill(_owner, "大地封锁", "一只手直到下回合不能作为被加数");
            return true;
        }

        public void OnTurnStarted(PlayerState owner, GameManager manager)
        {
            if (ReferenceEquals(manager?.CurrentPlayer, owner)) ClearRestriction();
        }

        public void OnTurnEnded(PlayerState owner, GameManager manager)
        {
            if (!ReferenceEquals(manager?.CurrentPlayer, owner) || _restricted != null) return;
            // 无 UI 时默认封锁最近的对手一只手。
            for (int i = 0; i < manager.Players.Count; i++)
            {
                PlayerState p = manager.Players[i];
                if (p == null || !p.IsAlive || ReferenceEquals(p, owner)) continue;
                if (manager.DistanceBetween(owner, p) <= 1)
                {
                    RestrictHand(p.LeftHand, manager);
                    return;
                }
            }
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
                                                  int actedHandValueBefore, int targetValue,
                                                  HandEffectType effect, GameManager manager,
                                                  IPreTurnChoiceSink choiceSink)
        {
            if (owner == null || actedHand == null || actedHand.Value != 3) return;
            PlayerState best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < manager.Players.Count; i++)
            {
                PlayerState p = manager.Players[i];
                if (p == null || !p.IsAlive || ReferenceEquals(p, owner)) continue;
                int d = manager.DistanceBetween(owner, p);
                if (d >= bestDistance) continue;
                best = p;
                bestDistance = d;
            }
            if (best != null)
            {
                best.ApplyStatus(StatusEffect.Restrained, 2, owner);
                manager.NoteSkill(owner, "大地束缚", $"{best.Name} 束缚 2 回合");
            }
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Bludgeoning;
    }
}
