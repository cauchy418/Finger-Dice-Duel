using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>法师·预言学派：7 后的窗口内，符合手值条件的行动直接无效。</summary>
    public sealed class SchoolOfDivinationLogic : CharacterTemplate,
        IActionInvalidator, ITurnStartHook, IAttackTypeProvider
    {
        private PlayerState _owner;
        private bool _windowActive;
        private int _snapshotValue;

        public override string DisplayName => "法师·预言学派";
        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _owner = owner;
            _windowActive = false;
            _snapshotValue = 0;
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
                                                  int actedHandValueBefore, int targetValue,
                                                  HandEffectType effect, GameManager manager,
                                                  IPreTurnChoiceSink choiceSink)
        {
            if (owner == null || actedHand == null) return;
            if (actedHand.Value != 7) return;

            _owner = owner;
            Hand other = ReferenceEquals(actedHand, owner.LeftHand) ? owner.RightHand : owner.LeftHand;
            _snapshotValue = other?.Value ?? 0;
            _windowActive = true;
            manager?.NoteSkill(owner, "预言", $"7 窗口开启：另一只手 {_snapshotValue}");
        }

        public void OnTurnStarted(PlayerState owner, GameManager manager)
        {
            if (!ReferenceEquals(manager?.CurrentPlayer, owner)) return;
            if (_windowActive && ReferenceEquals(owner, _owner))
            {
                _windowActive = false;
                manager?.NoteSkill(owner, "预言", "7 窗口结束");
            }
        }

        public bool TryInvalidateAction(PlayerState owner, PlayerState actor,
                                        Hand actedHand, Hand targetHand,
                                        GameManager manager, out string reason)
        {
            reason = null;
            if (!_windowActive || owner == null || actor == null || !owner.IsAlive) return false;
            if (ReferenceEquals(actor, owner)) return false; // 自己的行动不无效

            bool matches = (actor.LeftHand != null && actor.LeftHand.Value == _snapshotValue)
                        || (actor.RightHand != null && actor.RightHand.Value == _snapshotValue);
            if (!matches) return false;

            reason = $"{actor.Name} 的手牌 {_snapshotValue} 与预言快照相同";
            manager?.NoteSkill(owner, "预言", reason);
            return true;
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            int distance = context?.Manager?.DistanceBetween(context.Attacker, context.Defender) ?? 1;
            return distance <= 1 ? AttackDamageType.Bludgeoning : AttackDamageType.Piercing;
        }
    }
}
