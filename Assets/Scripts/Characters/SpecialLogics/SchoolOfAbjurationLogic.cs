using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 法师·防护学派：
    /// 2 给目标抗性；攻击数字可存为奥术屏障；受伤前消耗全部屏障吸收。
    /// </summary>
    public sealed class SchoolOfAbjurationLogic : CharacterTemplate,
        IAttackTypeProvider, IIncomingDamageBarrier
    {
        private PlayerState _owner;

        public override string DisplayName => "法师·防护学派";
        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;

        /// <summary>当前累计的奥术屏障点数（无上限，重开清空）。</summary>
        public int BarrierPoints { get; private set; }

        public void StoreBarrier(int points)
        {
            if (points > 0) BarrierPoints += points;
        }

        public void GrantResistance(PlayerState target, AttackDamageType type)
        {
            target?.AddResistance(type);
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _owner = owner;
            BarrierPoints = 0;
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
                                                  int actedHandValueBefore, int targetValue,
                                                  HandEffectType effect, GameManager manager,
                                                  IPreTurnChoiceSink choiceSink)
        {
            _owner = owner;
            if (owner == null || actedHand == null) return;

            // 2：默认把物理抗性给自己；UI 的目标/类型选择在后续接入。
            if (actedHand.Value == 2)
            {
                owner.AddResistance(AttackDamageType.Bludgeoning);
                manager?.NoteSkill(owner, "防护抗性", "获得物理抗性（挥砍/钝击/穿刺）");
            }
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            int distance = context?.Manager?.DistanceBetween(context.Attacker, context.Defender) ?? 1;
            return distance <= 1 ? AttackDamageType.Bludgeoning : AttackDamageType.Piercing;
        }

        public int AbsorbDamage(PlayerState target, int damage, AttackContext context, GameManager manager)
        {
            if (damage <= 0 || BarrierPoints <= 0) return damage;

            int absorbed = System.Math.Min(damage, BarrierPoints);
            BarrierPoints = 0; // 使用即全部消耗，不保留
            return damage - absorbed;
        }
    }
}
