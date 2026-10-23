using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>游侠·猎人：7 标记、标记目标无视正防御并追加已损体力/5。</summary>
    public sealed class RangerHunterLogic : CharacterTemplate, IAttackTypeProvider, IHunterMarkSource
    {
        private readonly HashSet<PlayerState> _marked = new HashSet<PlayerState>();
        private PlayerState _owner;

        public override string DisplayName => "游侠·猎人";
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => 3;

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _owner = owner;
            _marked.Clear();
        }

        public bool IsMarked(PlayerState target) => target != null && _marked.Contains(target);

        public void Mark(PlayerState target)
        {
            if (target == null || !target.IsAlive) return;
            _marked.Add(target);
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
                                                  int actedHandValueBefore, int targetValue,
                                                  HandEffectType effect, GameManager manager,
                                                  IPreTurnChoiceSink choiceSink)
        {
            if (owner == null || actedHand == null || actedHand.Value != 7) return;
            PlayerState best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < manager.Players.Count; i++)
            {
                PlayerState p = manager.Players[i];
                if (p == null || !p.IsAlive || ReferenceEquals(p, owner)) continue;
                int d = manager.DistanceBetween(owner, p);
                if (d > 3 || d >= bestDistance) continue;
                best = p;
                bestDistance = d;
            }
            if (best != null)
            {
                Mark(best);
                manager.NoteSkill(owner, "猎人标记", $"{best.Name} 被标记");
            }
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context?.Defender == null || !_marked.Contains(context.Defender)) return;
            context.IgnorePositiveDefense = true;
            int lost = context.Defender.MaximumHealth - context.Defender.Health;
            if (lost > 0) context.FinalDamage += lost / 5;
            _marked.Remove(context.Defender);
            context.Manager?.NoteSkill(context.Attacker, "猎人印记", $"消耗 {context.Defender.Name} 的标记，已损体力/5 加伤");
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Piercing;
    }
}
