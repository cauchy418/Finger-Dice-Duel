using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 狼人 Werewolf（攻防 -2 / -2，生命上限 +5）
    ///
    /// 技能：运算后若**两只手数字之和为 7**，则
    ///   · 回复"生命上限一半"的血量（向下取整，且不超过上限）；
    ///   · 攻防属性**永久**各 +1（可叠加）。
    ///
    /// 即：一次触发后 -1/-1，两次后 0/0，三次后 +1/+1 ……
    /// 生命上限 20 + 5 = 25，因此每次回复 25 / 2 = 12 点（向下取整）。
    ///
    /// 成长是跨回合状态，因此 <see cref="OnMatchReset"/> **必须**把它清掉，
    /// 否则重开一局会带着上一局的加成继续长。
    /// </summary>
    public sealed class WerewolfLogic : CharacterTemplate, IAiMoveAdvisor
    {
        /// <summary>触发条件：双手数字之和。</summary>
        public const int TriggerSum = 7;

        /// <summary>生命上限加成。</summary>
        public const int HealthBonus = 5;

        /// <summary>每次触发永久提升的攻防。</summary>
        public const int GrowthPerTrigger = 1;

        public override string DisplayName => "狼人";

        public override int DefaultAttackBonus => -2;
        public override int DefaultDefenseBonus => -2;

        public override int MaxHealthBonus => HealthBonus;

        /// <summary>本局已经触发过几次（仅用于日志 / 测试断言）。</summary>
        public int TriggerCount { get; private set; }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand, int actedHandValueBefore,
                                                 int targetValue, HandEffectType effect, GameManager manager,
                                                 IPreTurnChoiceSink choiceSink)
        {
            if (owner == null) return;

            // 判定用的是"两只手**运算后**的数字之和"
            int sum = owner.LeftHand.Value + owner.RightHand.Value;
            if (sum != TriggerSum) return;

            TriggerCount++;

            // 回复：生命上限的一半，向下取整；Heal 内部会夹到上限
            int healAmount = owner.MaximumHealth / 2;
            owner.Heal(healAmount);

            // 永久成长
            GrowStats(GrowthPerTrigger, GrowthPerTrigger);
        }

        /// <summary>
        /// 告诉 AI：这一手若能让双手和变成 7，就非常值钱。
        ///
        /// 运算后被操作的手变成结果值、另一只手不变，
        /// 所以判断用的是 (运算后的值 + 另一只手)。
        /// </summary>
        public int ScoreMoveBonus(MoveValueContext context)
            => context.ActedValueAfter + context.OtherHandValue == TriggerSum ? 200 : 0;

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            TriggerCount = 0;
            ResetStats();   // 丢弃所有成长累计，回到 -2/-2
        }
    }
}
