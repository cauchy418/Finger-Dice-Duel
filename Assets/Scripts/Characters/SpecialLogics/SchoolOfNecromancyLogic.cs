using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 法师·死灵学派：
    /// 2→1 完全挡下一段攻击；回合开始有 1 时免费打一次 1 点暗蚀攻击；死亡后换牌。
    /// </summary>
    public sealed class SchoolOfNecromancyLogic : CharacterTemplate,
        IAttackTypeProvider, IAttackBlocker, IStatusApplyingAttack, ITurnStartHook
    {
        private PlayerState _owner;
        private int _lastFreeAttackTurn = int.MinValue;

        public override string DisplayName => "法师·死灵学派";
        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _owner = owner;
            _lastFreeAttackTurn = int.MinValue;
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            int distance = context?.Manager?.DistanceBetween(context.Attacker, context.Defender) ?? 1;
            return distance <= 1 ? AttackDamageType.Bludgeoning : AttackDamageType.Piercing;
        }

        public bool TryBlockAttack(AttackContext context, GameManager manager, int hitIndex)
        {
            if (context == null || context.Defender == null) return false;
            if (context.AttackType == AttackDamageType.Force || context.IsForceElement) return false;

            Hand hand = FindTwo(context.Defender);
            if (hand == null) return false;

            hand.SetValue(1);
            manager?.NoteSkill(context.Defender, "死灵抵挡", "2→1，完全挡下这一段");
            return true;
        }

        private static Hand FindTwo(PlayerState owner)
        {
            if (owner == null) return null;
            if (owner.LeftHand.Value == 2 && !owner.LeftHand.IsLocked) return owner.LeftHand;
            if (owner.RightHand.Value == 2 && !owner.RightHand.IsLocked) return owner.RightHand;
            return null;
        }

        public StatusEffect StatusToApplyOnHit(AttackContext context)
            => context != null && context.IsNecromancyFreeAttack ? StatusEffect.Decay : StatusEffect.None;

        public void OnTurnStarted(PlayerState owner, GameManager manager)
        {
            if (owner == null || manager == null || !owner.IsAlive) return;
            if (!ReferenceEquals(manager.CurrentPlayer, owner)) return;
            if (!StatusRules.CanAct(owner)) return;
            if (_lastFreeAttackTurn == manager.TurnCount) return;
            if (FindOne(owner) == null) return;

            _lastFreeAttackTurn = manager.TurnCount;
            PlayerState target = ChooseTarget(owner, manager);
            if (target == null) return;

            Hand source = FindOne(owner);
            var context = new AttackContext(owner, target, source, 1)
            {
                AttackType = AttackDamageType.Necrotic,
                IgnoreStatBonuses = true,
                IsNecromancyFreeAttack = true,
            };
            context.Resolve(manager.GetChoiceProvider(target), manager);
            manager.NoteSkill(owner, "死灵暗蚀", $"对 {target.Name} 发起 1 点暗蚀攻击");
            manager.RefreshGameOver();
        }

        private static Hand FindOne(PlayerState owner)
        {
            if (owner.LeftHand.Value == 1 && !owner.LeftHand.IsLocked) return owner.LeftHand;
            if (owner.RightHand.Value == 1 && !owner.RightHand.IsLocked) return owner.RightHand;
            return null;
        }

        private static PlayerState ChooseTarget(PlayerState owner, GameManager manager)
        {
            PlayerState best = null;
            for (int i = 0; i < manager.Players.Count; i++)
            {
                PlayerState p = manager.Players[i];
                if (p == null || !p.IsAlive) continue;
                if (ReferenceEquals(p, owner)) continue;
                if (best == null || p.Health < best.Health) best = p;
            }
            return best ?? (owner != null && owner.IsAlive ? owner : null);
        }
    }
}
