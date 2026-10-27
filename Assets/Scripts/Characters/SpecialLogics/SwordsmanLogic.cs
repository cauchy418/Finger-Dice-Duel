using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 战士·剑士 Swordsman（攻防 +2 / −1）
    ///
    /// 技能：运算出 4 / 6 / 8（攻击）时，若**另一只手恰好和运算源数字相同**
    /// （即双 4 / 双 6 / 双 8），则在主攻击**之前**先追加一次攻击，
    /// 追加攻击的伤害固定为 4 / 6 / 8，且**无视双方的攻防加成**。
    ///
    /// 追加攻击与主攻击是**两次独立结算**：护盾只能挡下其中一次
    /// （见 RULES.md §6.3）。
    /// </summary>
    public sealed class SwordsmanLogic : CharacterTemplate, IPreTurnChoiceProvider, IExtraAttackProvider, IAiMoveAdvisor,
                                         ILethalThresholdAdvisor, ITwoStepGoal
    {
        public override string DisplayName => "战士·剑士";

        // ============================================================
        // 双 4/6/8 的**两回合计划**
        // ============================================================
        //
        // ⚠️ 为什么需要：战士·剑士的追加攻击要求**双手相同且结果是 4/6/8**，
        //    这也是一手做不出来的（和游荡者·刺客的双 7 一样）。
        //    价值表里的 DoublesOf 只在**已经成了**时给分，
        //    所以贪心评分看不到"我手上已经有一个 8，下一手凑齐就成"。
        //    实测发动率只有 20% —— 加上这条之后明显上升。

        /// <summary>离"双手相同且为 4/6/8"还差几步（0 = 已成）。</summary>
        public int StepsToGoal(int actedValueAfter, int otherHandValue)
        {
            bool a = IsFollowUpValue(actedValueAfter);
            bool b = IsFollowUpValue(otherHandValue);

            if (a && b && actedValueAfter == otherHandValue) return 0;   // 已成
            if (a && b) return 1;                                        // 同档但不同值
            if (a || b) return 1;                                        // 只差另一只手
            return 2;                                                    // 两只都要做
        }

        /// <summary>该手值是否属于追加攻击的触发值（4/6/8）。</summary>
        public bool IsGoalValue(int handValue) => IsFollowUpValue(handValue);

        private static bool IsFollowUpValue(int v) => v == 4 || v == 6 || v == 8;

        /// <summary>
        /// 给这一手加分，让 AI **愿意为双 4/6/8 铺路**。
        ///
        /// 差一步给 45（比游荡者·刺客的 55 略低：战士·剑士的追加攻击已经是"锦上添花"，
        /// 而游荡者·刺客的潜行还附带"不能被攻击"，价值更高）。
        /// </summary>
        public override int ScoreMoveBonus(MoveValueContext context)
        {
            int steps = StepsToGoal(context.ActedValueAfter, context.OtherHandValue);
            return steps == 1 ? 45 : 0;
        }

        public override int DefaultAttackBonus => 2;

        /// <summary>
        /// **战士·剑士的斩杀线**（玩家要求"战士·剑士的斩杀线也要根据不同手的情况写"）。
        ///
        /// = 主攻击伤害 + （若会触发追加攻击）追加伤害。
        ///
        /// 追加攻击的伤害是 **固定 4 / 6 / 8**（与结果值相同），
        /// 而且它 **IgnoreStatBonuses** —— 不吃攻防加成
        /// （见 SwordsmanLogic 里构造追加攻击的 AttackContext）。
        ///
        /// 触发条件是"双手相同且结果是 4/6/8"，用 ShouldFollowUp 判定，
        /// 保证与真实触发**同一份逻辑**。
        /// </summary>
        public int EstimateLethalDamage(LethalContext context, int resultValue,
                                        int attackBonus, int defenseBonus)
        {
            int main = AttackContext.ApplyStatBonuses(resultValue, attackBonus, defenseBonus,
                                                      ignorePositiveDefense: false);

            // 追加攻击：双手相同（另一只手 == 结果值）且结果是 4/6/8
            bool followUp = context.OtherHandValue == resultValue
                            && (resultValue == 4 || resultValue == 6 || resultValue == 8);

            return followUp ? main + resultValue : main;
        }
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
        // 现在统一由价值表表达（见 Assistant.HandValueProfiles 的战士·剑士档案：
        // 双 4/双 6/双 8 加 130）。

        /// <summary>供上层查询"这次运算会不会追加一次攻击"。</summary>
        public bool WillExtraAttack(PreTurnChoiceContext context) => ShouldFollowUp(context);

        /// <summary>
        /// 取"另一只**自己的**手"。
        ///
        /// ⚠️ 必须确认 <paramref name="actedHand"/> 确实是行动方自己的手 ——
        /// 规则允许**被加数是任何其他人的手**，而 <c>context.ActedHand</c>
        /// 在某些调用点承载的是"参与运算的那只手"（可能是别人的）。
        /// 不加这个判断会把**别人的**手误认成自己的，返回一只错的手。
        /// </summary>
        private static Hand GetOtherHand(PlayerState actor, Hand actedHand)
        {
            if (actor == null || actedHand == null) return null;
            if (!actor.Owns(actedHand)) return null;

            return ReferenceEquals(actedHand, actor.LeftHand) ? actor.RightHand : actor.LeftHand;
        }

        // ============================================================
        // 运算前的抉择：是否发动追加攻击
        // ============================================================

        public override void CollectPreTurnChoices(PreTurnChoiceContext context, IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;

            // ⚠️ 同 GloomStalkerLogic：双将时 `actor.Character` 是容器，
            //    `!= this` 会**恒为真**，追加攻击在双将里静默失效。
            //    （这是与"双将游侠·幽域追踪者没有伏击按钮"同一类的坑，
            //     由 GloomStalkerLogic 那次修复一并发现并改正。）
            if (!CharacterBehaviors.ActorHas(context.Actor, this)) return;
            if (!ShouldFollowUp(context)) return;

            // ⚠️ 提示里的数值必须用**运算结果**（ResultValue），
            //    不能用 context.ActedHand.Value —— 收集抉择时运算还没执行，
            //    那个值还是运算前的（5 + 1 = 6 时它是 5），提示就会显示"双 5、固定 5"。
            int value = context.ResultValue;

            requests.Add(new ChoiceRequest(
                HandEffect.SwordsmanFollowUp,
                $"战士·剑士双 {value}：可以在主攻击之前追加一次" +
                $"固定 {value} 点伤害的攻击（无视双方攻防加成，命中附流血 2 回合）。要发动吗？",
                new List<Hand>(1) { GetOtherHand(context.Actor, context.ActedHand) },
                allowDecline: true,
                declineResult: null,             // 放弃 = 不追加
                decider: context.Actor,
                acceptLabel: $"发动追加（{value} 点）", resultValue: value));
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
            //
            // ⚠️ 目标必须是**本次运算真正要打的人**（`DamageTargetPlayer`），
            //    不能用 `GetOpponent(owner)`（那是"第一位对手"）。
            //    玩家确认的交互里第 ③ 步会单独选目标玩家，可以不是第一位对手 ——
            //    用 GetOpponent 会让追加攻击打到**另一个人**身上。
            PlayerState defender = manager.DamageTargetPlayer ?? manager.GetOpponent(owner);
            if (defender == null) return;

            var ctx = new AttackContext(owner, defender, actedHand, (int)effect)
            {
                IgnoreStatBonuses = true,
                InflictsBleed = true,
                AutoResolveDodge = true,

                // ⚠️ 追加攻击的"攻击数字视为 0"（玩家确认）——
                //    因此**邪术师·至高妖精无法取消它**（任何手值都不可能等于 0）。
                IsSpecialAttack = true,
            };
            // 追加攻击发生在主攻击之前，玩家还没来得及做闪避选择，
            // 所以它的闪避**自动结算**（有几只"1"就闪几段），不弹提示。
            void ResolveFollowUp(){
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
            if(manager.HasTempestDomain()&&manager.DeferWizardAttack?.Invoke(ctx,ResolveFollowUp)==true)return;
            ResolveFollowUp();
        }

        private static Hand ReadChoice(PlayerState owner, GameManager manager,
                                       IPreTurnChoiceSink sink, Hand actedHand, Hand other)
        {
            var request = new ChoiceRequest(
                HandEffect.SwordsmanFollowUp,
                "战士·剑士：要追加一次固定伤害、命中附流血2回合的攻击吗？",
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
