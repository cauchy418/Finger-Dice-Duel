using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 剑客 Swordsman（攻防 +2 / −1）
    ///
    /// 技能：运算出 4 / 6 / 8（攻击）时，若**另一只手恰好和运算源数字相同**
    /// （即双 4 / 双 6 / 双 8），则在主攻击**之前**先追加一次攻击，
    /// 追加攻击的伤害固定为 4 / 6 / 8，且**无视双方的攻防加成**。
    ///
    /// 追加攻击与主攻击是**两次独立结算**：护盾只能挡下其中一次
    /// （见 RULES.md §6.3）。
    /// </summary>
    public sealed class SwordsmanLogic : CharacterTemplate, IPreTurnChoiceProvider, IExtraAttackProvider, IAiMoveAdvisor
    {
        public override string DisplayName => "剑客";

        public override int DefaultAttackBonus => 2;
        public override int DefaultDefenseBonus => -1;

        /// <summary>触发追加攻击需要"另一只手与运算源数字相同"。</summary>
        public static bool IsDoubleHanded(Hand actedHand, Hand otherHand)
            => actedHand != null && otherHand != null && actedHand.Value == otherHand.Value;

        /// <summary>
        /// 本次运算会不会触发追加攻击。
        ///
        /// ⚠️ 比较的是"**运算源运算后的值**"（= 运算结果 <see cref="PreTurnChoiceContext.ResultValue"/>）
        /// 与另一只手，**不是** <c>ActedHand.Value</c>。
        ///
        /// 因为在**收集阶段**运算还没执行，`ActedHand.Value` 仍然是运算前的值
        /// （例如 5 + 1 = 6 时它还是 5），拿它去比会永远判不出"双 6"。
        /// </summary>
        public static bool ShouldFollowUp(PreTurnChoiceContext context)
        {
            if (context == null || !context.IsAttack) return false;

            Hand other = GetOtherHand(context.Actor, context.ActedHand);
            return other != null && context.ResultValue == other.Value;
        }

        // 「双持同值特别值钱」原本在这里硬编码（+40）。
        // 现在统一由价值表表达（见 Assistant.HandValueProfiles 的剑客档案：
        // 双 4/双 6/双 8 加 130）。

        /// <summary>供上层查询"这次运算会不会追加一次攻击"。</summary>
        public bool WillExtraAttack(PreTurnChoiceContext context) => ShouldFollowUp(context);

        private static Hand GetOtherHand(PlayerState actor, Hand actedHand)
        {
            if (actor == null || actedHand == null) return null;
            return ReferenceEquals(actedHand, actor.LeftHand) ? actor.RightHand : actor.LeftHand;
        }

        // ============================================================
        // 运算前的抉择：是否发动追加攻击
        // ============================================================

        public override void CollectPreTurnChoices(PreTurnChoiceContext context, IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;

            if (context.Actor?.Character != this) return;
            if (!ShouldFollowUp(context)) return;

            // ⚠️ 提示里的数值必须用**运算结果**（ResultValue），
            //    不能用 context.ActedHand.Value —— 收集抉择时运算还没执行，
            //    那个值还是运算前的（5 + 1 = 6 时它是 5），提示就会显示"双 5、固定 5"。
            int value = context.ResultValue;

            requests.Add(new ChoiceRequest(
                HandEffect.SwordsmanFollowUp,
                $"剑客双 {value}：可以在主攻击之前追加一次" +
                $"固定 {value} 点伤害的攻击（无视双方攻防加成）。要发动吗？",
                new List<Hand>(1) { GetOtherHand(context.Actor, context.ActedHand) },
                allowDecline: true,
                declineResult: null,             // 放弃 = 不追加
                decider: context.Actor,
                acceptLabel: $"发动追加（{value} 点）"));
        }

        // ============================================================
        // 结算：主攻击之前追加一击
        // ============================================================

        public override void OnBeforeEffectResolved(PlayerState owner, Hand actedHand,
                                                    int targetValue, HandEffectType effect,
                                                    GameManager manager, IPreTurnChoiceSink choiceSink)
        {
            if (owner == null || manager == null) return;
            if (!IsAttackEffect(effect)) return;

            Hand other = GetOtherHand(owner, actedHand);
            if (other == null) return;

            // 结算阶段运算源已经被改写成结果值，所以这里直接用 actedHand.Value 比较
            if (actedHand.Value != other.Value) return;

            // 回读玩家的选择（没记录则当场问抉择器）
            Hand chosen = ReadChoice(owner, manager, choiceSink, actedHand, other);
            if (chosen == null) return;

            // 追加攻击：固定伤害、无视双方攻防加成
            PlayerState defender = manager.GetOpponent(owner);
            if (defender == null) return;

            var ctx = new AttackContext(owner, defender, actedHand, (int)effect)
            {
                IgnoreStatBonuses = true,
            };
            // 追加攻击发生在主攻击之前，玩家还没来得及做闪避选择，
            // 所以它的闪避**自动结算**（有几只"1"就闪几段），不弹提示。
            manager.ResolvingExtraAttack = true;
            try
            {
                ctx.Resolve(manager.GetChoiceProvider(defender), manager);
            }
            finally
            {
                manager.ResolvingExtraAttack = false;
            }

            // 登记到 GameManager，让战报能单独显示这一行（追加攻击发生在主效果之前，
            // 此时还没有 TurnResult 可以写）。
            int fixedDamage = (int)effect;
            manager.PendingExtraAttack = new ExtraAttackInfo(
                owner.Name, fixedDamage, ctx.AppliedDamage, ctx.WasBlocked);
        }

        private static Hand ReadChoice(PlayerState owner, GameManager manager,
                                       IPreTurnChoiceSink sink, Hand actedHand, Hand other)
        {
            var request = new ChoiceRequest(
                HandEffect.SwordsmanFollowUp,
                "剑客：要追加一次固定伤害的攻击吗？",
                new List<Hand>(1) { other },
                allowDecline: true,
                declineResult: null,
                decider: owner,
                acceptLabel: "发动追加");

            return PreTurnChoiceReader.Read(sink, manager.GetChoiceProvider(owner), request, owner);
        }

        private static bool IsAttackEffect(HandEffectType effect)
            => effect == HandEffectType.Attack4
               || effect == HandEffectType.Attack6
               || effect == HandEffectType.Attack8;

        public override void OnMatchReset(PlayerState owner, GameManager manager) { }
    }
}
