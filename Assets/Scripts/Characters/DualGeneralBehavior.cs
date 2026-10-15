using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters
{
    /// <summary>
    /// **双将**：一个玩家同时拥有**两名同阵营角色**的技能。
    ///
    /// 为什么是"容器"而不是两个独立职业：
    ///   上层到处是 <c>player.Character is IX</c> 的**单次 cast**
    ///   （共 16 个接口）。一个玩家只能有一个 <c>Character</c>，
    ///   所以双将**必须自己实现那些接口**，再把调用**转发**给子角色。
    ///
    /// 玩家确认的合并规则：
    ///   · 攻/防加成：**相加**
    ///   · 攻击距离 / 体力上限 / 攻击段数 / 斩杀线：**取最大**
    ///   · AI 评分：**两表平均后取最高**
    ///   · “接管同一步骤”型接口（改写效果 / 取消 / 闪避等）：**取第一个能处理的子角色**
    ///
    /// ⚠️ 为何"同阵营"能避开碰撞：
    ///   会互相覆盖的 5 个"接管型"接口，在**守序 / 中立 / 混乱**
    ///   三组内都**不重叠**（已逐个核对）。
    /// </summary>
    public sealed class DualGeneralBehavior : CharacterTemplate,
        IMultiHitAttack, IAttackRangeProvider, ITurnSkipRule, IDamageMitigator,
        IHandEffectRewriter, IHealAmountProvider, IHealOverride, IHealTargetAdvisor,
        ILethalThresholdAdvisor, IAttackDamageRule, IAttackCanceller, IAttackRedirector,
        IDodgeBlocker, IRangeBreakEffect, IExtraAttackProvider, IAiMoveAdvisor,
        IAttackTargetRule, IDodgeCapability, ISkillUseCounter, ITwoStepGoal,
        IHandEffectDisplay, IResultTargetProvider, ITargetHandChoiceProvider
    {
        // ⚠️ 存成 `ICharacterBehavior`（Core 层接口）而不是 `CharacterTemplate`：
        //    容器本身在 Characters 层，但构造参数不应强制调用方持有具体类型。
        private readonly List<ICharacterBehavior> _parts = new List<ICharacterBehavior>(2);

        public DualGeneralBehavior(ICharacterBehavior primary, ICharacterBehavior secondary)
        {
            Primary = primary;
            Secondary = secondary;

            if (primary != null) _parts.Add(primary);
            if (secondary != null) _parts.Add(secondary);
        }

        /// <summary>**主将** —— 战报只显示他的名字（玩家确认）。</summary>
        public ICharacterBehavior Primary { get; }

        /// <summary>副将。</summary>
        public ICharacterBehavior Secondary { get; }

        /// <summary>两名子角色（主将在前）。</summary>
        public IReadOnlyList<ICharacterBehavior> Parts => _parts;

        /// <summary>
        /// **主将的显示名** —— 战报与 UI 只认这个（玩家确认）。
        ///
        /// ⚠️ 不能用 <c>CharacterTemplate.DisplayName</c>：它默认是 <c>GetType().Name</c>，
        ///    对子角色来说就是英文类名（实测：期望“战士”、实际“Warrior”）。
        ///    所以由工厂在创建时把名字传进来。
        /// </summary>
        public string PrimaryName { get; private set; }

        /// <summary>副将的显示名（供 UI 展示主将加副将）。</summary>
        public string SecondaryName { get; private set; }

        /// <summary>由工厂调用，设定主副将的显示名。</summary>
        public void SetNames(string primaryName, string secondaryName)
        {
            PrimaryName = primaryName;
            SecondaryName = secondaryName;
        }

        /// <summary>显示名：主将名（战报只认这个）。</summary>
        public override string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(PrimaryName)) return PrimaryName;
                if (Primary is CharacterTemplate t) return t.DisplayName;
                return Primary != null ? Primary.GetType().Name : "DualGeneral";
            }
        }

        /// <summary>
        /// 体力上限加成：取**最大**（玩家确认）。
        /// </summary>
        public override int MaxHealthBonus
        {
            get
            {
                int best = 0;
                for (int i = 0; i < _parts.Count; i++)
                {
                    if (!(_parts[i] is CharacterTemplate t)) continue;
                    if (t.MaxHealthBonus > best) best = t.MaxHealthBonus;
                }
                return best;
            }
        }

        /// <summary>
        /// 攻击加成：两名子角色**相加**。
        ///
        /// ⚠️ 读的是 <see cref="ICharacterBehavior.AttackBonus"/> 而不是 <c>DefaultAttackBonus</c>：
        ///    前者才含"CharacterFactory 注入的角色数据值"。
        ///    我第一版用了 Default*，于是相加恒为 0（测试抳到：期望 1、实际 0）。
        /// </summary>
        public override int DefaultAttackBonus
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _parts.Count; i++)
                    sum += _parts[i].AttackBonus;
                return sum;
            }
        }

        /// <summary>防御加成：两名子角色**相加**（同上，读 AttackBonus 那一类）。</summary>
        public override int DefaultDefenseBonus
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _parts.Count; i++)
                    sum += _parts[i].DefenseBonus;
                return sum;
            }
        }

        /// <summary>攻击距离：取**最大**。</summary>
        public override int BaseAttackRange
        {
            get
            {
                int best = 1;
                for (int i = 0; i < _parts.Count; i++)
                {
                    if (!(_parts[i] is CharacterTemplate t)) continue;
                    if (t.BaseAttackRange > best) best = t.BaseAttackRange;
                }
                return best;
            }
        }

        /// <summary>找出第一个实现了 <typeparamref name="T"/> 的子角色。</summary>
        private T First<T>() where T : class
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is T t) return t;
            return null;
        }

        // ---------------------------------------------------------------
        // 生命周期钩子：**两名子角色都要跑**
        // ---------------------------------------------------------------
        //
        // ⚠️ 这些不是"取一个"，而是**逐个转发**——
        //    骑士的碎盾计数、巨人的另一只手记录、狼人的成长
        //    都依赖自己收到事件，只转发一个会静默地少算一半。
        public override void OnResolveAttack(AttackContext context)
        {
            for (int i = 0; i < _parts.Count; i++) _parts[i].OnResolveAttack(context);
        }

        public override void OnShieldFormed(PlayerState owner, Hand shieldHand, GameManager manager)
        {
            for (int i = 0; i < _parts.Count; i++) _parts[i].OnShieldFormed(owner, shieldHand, manager);
        }

        public override void OnBeforeEffectResolved(PlayerState owner, Hand actedHand,
            int targetValue, HandEffectType effect, GameManager manager, IPreTurnChoiceSink choiceSink)
        {
            for (int i = 0; i < _parts.Count; i++)
                _parts[i].OnBeforeEffectResolved(owner, actedHand, targetValue, effect, manager, choiceSink);
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
            int actedHandValueBefore, int targetValue, HandEffectType effect,
            GameManager manager, IPreTurnChoiceSink choiceSink)
        {
            for (int i = 0; i < _parts.Count; i++)
                _parts[i].OnOperationResolved(owner, actedHand, actedHandValueBefore,
                                              targetValue, effect, manager, choiceSink);
        }

        public override void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager)
        {
            for (int i = 0; i < _parts.Count; i++) _parts[i].OnOwnShieldBroken(owner, brokenHand, manager);
        }

        public override void OnOtherShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager)
        {
            for (int i = 0; i < _parts.Count; i++) _parts[i].OnOtherShieldBroken(owner, brokenHand, manager);
        }

        public override void TryDodge(PreTurnChoiceContext context, IPreTurnChoiceSink sink, int hitIndex)
        {
            for (int i = 0; i < _parts.Count; i++) _parts[i].TryDodge(context, sink, hitIndex);
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            for (int i = 0; i < _parts.Count; i++) _parts[i].OnMatchReset(owner, manager);
        }

        // ---------------------------------------------------------------
        // 抉择收集：两名子角色都要收集（一次运算可能弹多个问询）
        // ---------------------------------------------------------------
        public override void CollectPreTargetChoices(PreTurnChoiceContext context,
                                                     IList<ChoiceRequest> requests)
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is ITargetHandChoiceProvider p) p.CollectPreTargetChoices(context, requests);
        }

        public override bool ResultNeedsTarget(PreTurnChoiceContext context)
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is IResultTargetProvider r && r.ResultNeedsTarget(context)) return true;
            return false;
        }

        public override void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                   IList<ChoiceRequest> requests)
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is IPreTurnChoiceProvider p) p.CollectPreTurnChoices(context, requests);
        }

        public override void CollectFollowUpChoices(ChoiceRequest answered, Hand answer,
                                                    IList<ChoiceRequest> requests)
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is IPreTurnChoiceProvider p) p.CollectFollowUpChoices(answered, answer, requests);
        }

        // ---------------------------------------------------------------
        // “取最大”类
        // ---------------------------------------------------------------
        int IMultiHitAttack.MaxHits
        {
            get
            {
                int best = 1;
                for (int i = 0; i < _parts.Count; i++)
                    if (_parts[i] is IMultiHitAttack m && m.MaxHits > best) best = m.MaxHits;
                return best;
            }
        }

        int IAttackRangeProvider.BaseAttackRange => BaseAttackRange;

        int ISkillUseCounter.SkillUseCount
        {
            get
            {
                int best = 0;
                for (int i = 0; i < _parts.Count; i++)
                    if (_parts[i] is ISkillUseCounter c && c.SkillUseCount > best) best = c.SkillUseCount;
                return best;
            }
        }

        int ILethalThresholdAdvisor.EstimateLethalDamage(LethalContext context, int resultValue,
                                                         int attackBonus, int defenseBonus)
        {
            int best = 0;
            for (int i = 0; i < _parts.Count; i++)
            {
                if (!(_parts[i] is ILethalThresholdAdvisor a)) continue;
                int v = a.EstimateLethalDamage(context, resultValue, attackBonus, defenseBonus);
                if (v > best) best = v;
            }
            return best;
        }

        /// <summary>
        /// AI 评分：两个子角色的建议**取平均**，再取较高者。
        ///
        /// 玩家确认："双将将评分表平均后 AI 取评分最高"。
        /// 实现上就是：两边各给一个建议值，取它们的平均与较高者中的**较高者**——
        /// 即"不会因为另一半拉低而变笨"，同时保留两边都认同时的加成。
        /// </summary>
        int IAiMoveAdvisor.ScoreMoveBonus(MoveValueContext context)
        {
            int a = 0, b = 0;
            bool hasA = false, hasB = false;

            if (_parts.Count > 0 && _parts[0] is IAiMoveAdvisor m0)
            {
                a = m0.ScoreMoveBonus(context);
                hasA = true;
            }
            if (_parts.Count > 1 && _parts[1] is IAiMoveAdvisor m1)
            {
                b = m1.ScoreMoveBonus(context);
                hasB = true;
            }

            if (hasA && hasB)
            {
                int avg = (a + b) / 2;
                int hi = a > b ? a : b;
                return avg > hi ? avg : hi;
            }
            if (hasA) return a;
            if (hasB) return b;
            return 0;
        }

        // ---------------------------------------------------------------
        // “取第一个能处理的”（接管型）
        // ---------------------------------------------------------------
        HandEffectType IHandEffectRewriter.RewriteEffect(PlayerState actor, Hand actedHand,
                                                         int targetValue, GameManager manager)
        {
            // ⚠⚠ **没有子角色会改写时，必须返回原效果，不是 None。**
            //
            //    <see cref="GameManager"/> 用 <c>rewritten != effect</c> 来判“是否被改写”，
            //    所以 <c>None</c> 的含义是**“把效果改成无效果”**
            //    （巨人的 4/6/8 正是这么用的），而不是“没改写”。
            //
            //    我第一版在没有改写器时返回了 None —— 于是
            //    **游侠+邪术师这种“两个都不实现改写器”的组合，
            //    所有数字全变成了无效果**（玩家实测：8 不攻击）。
            //    巨人只是**恰好确实想把 8 变成 None**，所以看起来是对的。
            HandEffectType original = actedHand != null
                ? Hand.GetEffectType(actedHand.Value)
                : HandEffectType.None;

            for (int i = 0; i < _parts.Count; i++)
            {
                if (!(_parts[i] is IHandEffectRewriter r)) continue;

                // 取**第一个实现了改写器的子角色的答案**，
                // 包括它返回的 None（=“我要把它变成无效果”）。
                // 各子角色在“不想改写”时会自己返回原效果。
                return r.RewriteEffect(actor, actedHand, targetValue, manager);
            }

            return original;
        }

        HandEffectType IHandEffectDisplay.DisplayedEffectOf(int resultValue)
        {
            // 同上：**没有子角色会改显示时，返回原效果**。
            // 巨人的 4/6/8 确实要显示成“不红”，所以它的 None 是有意义的。
            for (int i = 0; i < _parts.Count; i++)
            {
                if (!(_parts[i] is IHandEffectDisplay d)) continue;
                return d.DisplayedEffectOf(resultValue);
            }

            return Hand.GetEffectType(resultValue);
        }

        bool IDamageMitigator.TryMitigate(PlayerState owner, AttackContext context, int segmentDamage,
                                          int stagesUsed,
                                          out int stages, out int totalReduced, out int totalReflect)
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                if (!(_parts[i] is IDamageMitigator m)) continue;
                if (m.TryMitigate(owner, context, segmentDamage, stagesUsed,
                                  out stages, out totalReduced, out totalReflect))
                {
                    return true;
                }
            }

            stages = 0;
            totalReduced = 0;
            totalReflect = 0;
            return false;
        }

        void IAttackDamageRule.ApplyAttackRule(AttackContext context, int attackValue, GameManager manager)
        {
            var rule = First<IAttackDamageRule>();
            rule?.ApplyAttackRule(context, attackValue, manager);
        }

        bool IAttackCanceller.TryCancelAttack(PlayerState canceller, PlayerState attacker,
                                              PlayerState target, int attackValue, GameManager manager)
        {
            var c = First<IAttackCanceller>();
            return c != null && c.TryCancelAttack(canceller, attacker, target, attackValue, manager);
        }

        bool IAttackRedirector.TryRedirectToSelf(PlayerState owner, PlayerState attacker,
                                                 PlayerState intendedTarget, GameManager manager)
        {
            var r = First<IAttackRedirector>();
            return r != null && r.TryRedirectToSelf(owner, attacker, intendedTarget, manager);
        }

        bool IDodgeBlocker.PreventsDodge
        {
            get
            {
                for (int i = 0; i < _parts.Count; i++)
                    if (_parts[i] is IDodgeBlocker b && b.PreventsDodge) return true;
                return false;
            }
        }

        void IRangeBreakEffect.ApplyRangeBreak(PlayerState actor, Hand actedHand, GameManager manager)
        {
            var e = First<IRangeBreakEffect>();
            e?.ApplyRangeBreak(actor, actedHand, manager);
        }

        bool IExtraAttackProvider.WillExtraAttack(PreTurnChoiceContext context)
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is IExtraAttackProvider p && p.WillExtraAttack(context)) return true;
            return false;
        }

        bool IAttackTargetRule.IsImmuneToAttack(PlayerState owner)
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is IAttackTargetRule r && r.IsImmuneToAttack(owner)) return true;
            return false;
        }

        List<Hand> IDodgeCapability.GetDodgeHands(PlayerState owner)
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                if (!(_parts[i] is IDodgeCapability c)) continue;
                List<Hand> hands = c.GetDodgeHands(owner);
                if (hands != null && hands.Count > 0) return hands;
            }
            return new List<Hand>();
        }

        bool IDodgeCapability.CanDodgeAgainst(PreTurnChoiceContext context)
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is IDodgeCapability c && c.CanDodgeAgainst(context)) return true;
            return false;
        }

        void ITurnSkipRule.SyncSkipState(PlayerState owner)
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is ITurnSkipRule r) r.SyncSkipState(owner);
        }

        bool ITurnSkipRule.CanSkip(PlayerState owner, GameManager manager)
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is ITurnSkipRule r && r.CanSkip(owner, manager)) return true;
            return false;
        }

        void ITurnSkipRule.OnTurnSkipped(PlayerState owner, GameManager manager)
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is ITurnSkipRule r) r.OnTurnSkipped(owner, manager);
        }

        PlayerState IHealOverride.ApplyHeal(PlayerState owner, GameManager manager,
                                            IPreTurnChoiceSink choiceSink)
        {
            var h = First<IHealOverride>();
            return h?.ApplyHeal(owner, manager, choiceSink) ?? owner;
        }

        int IHealAmountProvider.HealAmountOf(PlayerState owner, GameManager manager)
        {
            var h = First<IHealAmountProvider>();
            return h != null ? h.HealAmountOf(owner, manager) : 0;
        }

        PlayerState IHealTargetAdvisor.BestHealTarget(PlayerState owner, GameManager manager)
        {
            var a = First<IHealTargetAdvisor>();
            return a?.BestHealTarget(owner, manager);
        }

        int ITwoStepGoal.StepsToGoal(int actedValueAfter, int otherHandValue)
        {
            int best = int.MaxValue;
            for (int i = 0; i < _parts.Count; i++)
            {
                if (!(_parts[i] is ITwoStepGoal g)) continue;
                int v = g.StepsToGoal(actedValueAfter, otherHandValue);
                if (v < best) best = v;
            }
            return best == int.MaxValue ? 0 : best;
        }

        bool ITwoStepGoal.IsGoalValue(int handValue)
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] is ITwoStepGoal g && g.IsGoalValue(handValue)) return true;
            return false;
        }
    }
}
