using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **游侠·幽域追踪者**（攻 0 / 防 −1，攻击距离 1）。
    ///
    /// 玩家给出的规则：
    ///   · **运算后**，若两只手**同时是 1/2/3 且数字相同**，
    ///     可以（**主动选择**）把**参与运算的那只手**暗置，进入**伏击**状态。
    ///   · 暗置的手：不能发起运算（类似被锁），
    ///     也**不能被当成运算数字**（对手点不到它）—— 但**数值可见**。
    ///   · 伏击状态：**攻击 +1**、**攻击距离 3**。
    ///   · 即将受到伤害时若处于伏击，则**恢复暗置的手**（解除伏击）并**抵挡此次伤害**；
    ///     触发时机在**武僧·太极宗太极之后**。
    ///   · **法师·咒法学派可以穿透伏击**：伏击挡不住咒术攻击，咒术攻击也**不会解除**伏击。
    ///
    /// 玩家补充（与跳过回合有关）：
    ///   "若游侠·幽域追踪者处于潜伏状态，剩余的那只手被野蛮人·守望者道途控制住也要给出跳过回合按钮。"
    ///   —— 即**两只手都无法主动运算时，右侧都要有跳过按钮**。
    ///   （由 <see cref="PlayerState.HasNoUsableHand"/> 与
    ///     <see cref="GameManager.CanSkipTurnFor"/> 统管。）
    /// </summary>
    public sealed class GloomStalkerLogic : CharacterTemplate,
        IHandEffectRewriter, ISelectiveHandEffectRewriter, IHandEffectDisplay, IDamageMitigator,
        IAttackRangeProvider, ITwoStepGoal, IPreTurnChoiceProvider
    {
        /// <summary>伏击要求两只手都是 1/2/3 —— 上界（含）。</summary>
        public const int AmbushMaxValue = 3;

        /// <summary>伏击状态下的攻击加成。</summary>
        public const int AmbushAttackBonus = 1;

        /// <summary>伏击状态下的攻击距离。</summary>
        public const int AmbushRange = 3;

        public bool HandlesResult(int value) => false;

        public override string DisplayName => "游侠·幽域追踪者";

        // ⚠️ `DefaultAttackBonus` 在下面重写为"基础 0 + 伏击加成"（见伏击加成一节），
        //    所以这里**不能**再声明一次，否则重复定义。
        public override int DefaultDefenseBonus => -1;

        public override int BaseAttackRange => 1;

        /// <summary>处于伏击状态时暗置的那只手；未伏击为 null。</summary>
        private Hand _ambushHand;

        /// <summary>是否处于伏击状态。</summary>
        public bool IsAmbushing => _ambushHand != null && _ambushHand.IsConcealed;

        /// <summary>处于伏击时暗置的手（供界面与诊断）。</summary>
        public Hand AmbushHand => IsAmbushing ? _ambushHand : null;

        // ---------------------------------------------------------------
        // 伏击条件
        // ---------------------------------------------------------------

        /// <summary>
        /// 这次运算之后是否**满足伏击条件**：
        /// 两只手**同时是 1/2/3 且数字相同**。
        ///
        /// 玩家原话："运算后，若两只手同时是 1/2/3 且数字相同，可以……进入伏击状态。"
        /// </summary>
        public static bool MeetsAmbushCondition(PlayerState owner)
        {
            if (owner == null) return false;

            Hand left = owner.LeftHand;
            Hand right = owner.RightHand;
            if (left == null || right == null) return false;

            if (left.Value < 1 || left.Value > AmbushMaxValue) return false;
            if (right.Value < 1 || right.Value > AmbushMaxValue) return false;

            return left.Value == right.Value;
        }

        /// <summary>
        /// **玩家给出的判定**（这是权威版本）：
        /// 运算之后，**发起运算的那只手**（它的值已经变成结果值）与**另一只手**
        /// 数值相同，且两者都在 1/2/3。
        ///
        /// 玩家原话与例子：
        ///   "指的是我运算后两只手数值相同，例如左手是 9，右手是 2，
        ///    这回合我用 9+3=2，触发伏击。"
        ///
        /// 拆开看：左手 9 发起运算、加数是 3 -> 9 + 3 = 12 -> 归一化 **2**，
        /// 于是左手变成 2，而右手本来就是 2 -> 两手同为 2 -> 触发。
        ///
        /// \u26a0\ufe0f 我第一版写错过：当时用"两只手**都** <= 3"去判，
        ///    等于要求**原来那只 9 就 <= 3** —— 那当然永远不成立，
        ///    我因此误判成"条件不可达"。真正要比的是**运算之后**的两只手。
        /// </summary>
        public static bool MeetsAmbushCondition(PlayerState owner, Hand actedHand, int resultValue)
        {
            if (owner == null || actedHand == null) return false;
            if (!owner.Owns(actedHand)) return false;

            Hand other = ReferenceEquals(actedHand, owner.LeftHand) ? owner.RightHand : owner.LeftHand;
            if (other == null) return false;

            // 运算后：发起运算的那只手 = 结果值；另一只手不变
            if (resultValue < 1 || resultValue > AmbushMaxValue) return false;
            if (other.Value < 1 || other.Value > AmbushMaxValue) return false;

            return resultValue == other.Value;
        }

        /// <summary>
        /// 这次运算之后可以暗置哪只手（不满足则 null）。
        ///
        /// <paramref name="resultValue"/> = 发起运算那只手的**新值**（结果值）。
        /// </summary>
        public Hand AmbushCandidate(PlayerState owner, Hand actedHand, int resultValue)
        {
            if (!MeetsAmbushCondition(owner, actedHand, resultValue)) return null;
            if (actedHand.IsLocked || actedHand.IsConcealed) return null;
            if (actedHand.State != ShieldState.None) return null;
            return actedHand;
        }

        /// <summary>
        /// **本局进入伏击几次**（供"技能发动率"统计）。
        ///
        /// ⚠️ 为什么要计数器：伏击会被"抵伤"**消耗掉**，
        ///    所以终局时看 <see cref="IsAmbushing"/> 常常已经是 false ——
        ///    只有累计次数才反映"这一局到底用过没有"。
        /// </summary>
        public int AmbushCount { get; private set; }

        /// <summary>进入伏击：暗置这只手。</summary>
        public void EnterAmbush(Hand hand)
        {
            if (hand == null) return;
            hand.Conceal();
            _ambushHand = hand;
            AmbushCount++;
        }

        /// <summary>解除伏击：恢复被暗置的手。返回被恢复的那只手（没在伏击则 null）。</summary>
        public Hand ReleaseAmbush()
        {
            Hand hand = AmbushHand;
            if (hand == null) return null;

            hand.Reveal();
            _ambushHand = null;
            return hand;
        }

        // ---------------------------------------------------------------
        // 伏击带来的加成
        // ---------------------------------------------------------------

        /// <summary>
        /// 伏击状态下**攻击加成 +1**（玩家确认）。
        ///
        /// ⚠️ 用 <c>DefaultAttackBonus</c> 而不是 <c>AdjustAttackBonus</c>：
        ///    后者只在“数据层没注入过攻防”时才生效
        ///    （实际对局里 <c>CharacterFactory</c> 一定会注入），
        ///    于是伏击的 +1 永远生效不了。
        ///    写成 <c>DefaultAttackBonus</c> 就能让注入的基础值上再加伏击加成。
        /// </summary>
        // ⚠️ 必须走 `AdjustAttackBonus`，**不能**走 `DefaultAttackBonus`。
        //
        //    `AttackBonus` 的读法是
        //      AdjustAttackBonus(_statsApplied ? _attackBonus : DefaultAttackBonus)
        //    而实际对局里 `CharacterFactory` **一定会注入**攻防（于是 `_statsApplied == true`），
        //    这时读到的是注入值 `_attackBonus` —— 我第一版把 +1 写在
        //    `DefaultAttackBonus` 上，**在注入路径上根本不会被读到**，
        //    伏击的 +1 等于没加（测试当场抓到：期望 1、实际 0）。
        //
        //    写在 `AdjustAttackBonus` 里就能让"注入的基础值 + 伏击加成"。
        protected override int AdjustAttackBonus(int baseBonus)
            => IsAmbushing ? baseBonus + AmbushAttackBonus : baseBonus;

        /// <summary>未注入时的默认值仍是 0（伏击加成由上一条统一叠加）。</summary>
        public override int DefaultAttackBonus => 0;

        /// <summary>伏击状态下攻击距离 3；否则 1。</summary>
        public int BaseAttackRangeEffective => IsAmbushing ? AmbushRange : BaseAttackRange;

        int IAttackRangeProvider.BaseAttackRange => BaseAttackRangeEffective;

        // ---------------------------------------------------------------
        // 效果改写：让上层知道"这次运算之后可以伏击"
        // ---------------------------------------------------------------

        /// <summary>
        /// 不改写效果类型 —— 游侠·幽域追踪者没有把某个数字变成别的东西。
        /// 他只需要一个"运算之后"的时机来询问是否伏击，那走
        /// <see cref="OnOperationResolved"/>（与野蛮人·野兽道途的"和为 7"同一处）。
        /// </summary>
        public HandEffectType RewriteEffect(PlayerState actor, Hand actedHand,
                                            int targetValue, GameManager manager)
            => Hand.GetEffectType(actedHand?.Value ?? 0);

        public HandEffectType DisplayedEffectOf(int resultValue)
            => Hand.GetEffectType(resultValue);

        // ---------------------------------------------------------------
        // 抵伤：伏击挡下一次伤害（在太极之后）
        // ---------------------------------------------------------------

        /// <summary>
        /// 伏击抵伤。
        ///
        /// \u26a0\ufe0f 触发时机**在武僧·太极宗太极之后**（玩家确认）：
        ///    本方法是 <see cref="IDamageMitigator"/>，被 `AttackContext` 在
        ///    **逐段结算之前**询问；而双将容器按"主将在前"的顺序询问，
        ///    所以"武僧·太极宗主将 + 游侠·幽域追踪者副将"天然就是太极先、
        ///    只有太极没吃下这次伤害时才会问到游侠·幽域追踪者。
        ///
        /// \u26a0\ufe0f 法师·咒法学派的攻击**穿透**伏击：既不抵挡、也**不解除**伏击。
        /// </summary>
        bool IDamageMitigator.TryMitigate(PlayerState owner, AttackContext context,
                                          int segmentDamage, int stagesUsed,
                                          out int stages, out int totalReduced,
                                          out int totalReflect)
        {
            stages = 0;
            totalReduced = 0;
            totalReflect = 0;

            if (!IsAmbushing) return false;
            if (segmentDamage <= 0) return false;

            // 咒术攻击穿透：不抵挡、也不解除伏击
            if (WayOfTheTaiChiLogic.PiercesMitigation(context)) return false;

            // 伏击抵挡**整次伤害**（这是一个"消耗掉伏击"的免伤，不是减免）
            stages = 1;
            totalReduced = segmentDamage;
            totalReflect = 0;

            ReleaseAmbush();
            context.Manager?.NoteSkill(owner, "伏击免伤", $"抵消本段 {segmentDamage} 伤害，解除伏击");
            return true;
        }

        // ---------------------------------------------------------------
        // 伏击的"主动选择"：与战士·剑士追加攻击**同一条通道**
        // ---------------------------------------------------------------

        /// <summary>
        /// 收集阶段：满足条件就提一条“要不要伏击”的拉择。
        ///
        /// ⚠️ 收集期**只读** —— 不碰任何手的状态。
        ///    （上一次尝试引发过偶发崩溃，所以这里严格保持只读。）
        /// </summary>
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                   IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;

            // ⚠️ **不能**写 `ReferenceEquals(context.Actor?.Character, this)`。
            //
            //    双将时 `actor.Character` 是 `DualGeneralBehavior` **容器**，
            //    永远不等于部件本身 —— 那种写法在双将里恒为假，
            //    游侠·幽域追踪者的伏击按钮就永远不出现（玩家实测报告）。
            //    而容器的 `CollectPreTurnChoices` 会转给**每一个**部件，
            //    所以这里必须用"我在不在这位角色的技能集合里"来判断。
            if (!CharacterBehaviors.ActorHas(context.Actor, this)) return;

            // ① 伏击只发生在**非攻击**的运算上
            if (context.IsAttack) return;

            Hand acted = context.ActedHand;
            if (acted == null || !context.Actor.Owns(acted)) return;

            // ② 较的是**运算结果**与另一只手
            //    （收集阶段运算还没执行，所以不能读 acted.Value）
            if (!MeetsAmbushCondition(context.Actor, acted, context.ResultValue)) return;

            int value = context.ResultValue;
            requests.Add(new ChoiceRequest(
                HandEffect.GloomStalkerAmbush,
                $"游侠·幽域追踪者：运算后两手同为 {value}，"
                + $"可以把这只手暗置进入伏击"
                + $"（攻击 +{AmbushAttackBonus}、距离 {AmbushRange}；"
                + $"被攻击时抵挡一次伤害并恢复）。要伏击吗？",
                new List<Hand>(1) { acted },
                allowDecline: true,
                declineResult: null,
                decider: context.Actor,
                acceptLabel: "伏击"));
        }

        /// <summary>
        /// **同一时机落实伏击**（与战士·剑士追加攻击同一个钩子）。
        ///
        /// 玩家确认："战士·剑士什么时候能触发追加攻击，
        /// 游侠·幽域追踪者什么时候就能触发伏击"。
        /// 此时运算已完成（actedHand.Value 已是结果值）、主效果尚未分派。
        /// </summary>
        public override void OnBeforeEffectResolved(PlayerState owner, Hand actedHand,
                                                    int targetValue, HandEffectType effect,
                                                    GameManager manager, IPreTurnChoiceSink choiceSink)
        {
            if (owner == null || manager == null) return;
            if (actedHand == null || !owner.Owns(actedHand)) return;
            if (IsAmbushing) return;

            // 攻击不伏击（那只手会被覆盖成输出手）
            if (IsAttackEffect(effect)) return;

            // 此刻 actedHand.Value 已经是结果值
            Hand candidate = AmbushCandidate(owner, actedHand, actedHand.Value);
            if (candidate == null) return;

            var request = new ChoiceRequest(
                HandEffect.GloomStalkerAmbush,
                "游侠·幽域追踪者：要伏击吗？",
                new List<Hand>(1) { candidate },
                allowDecline: true,
                declineResult: null,
                decider: owner,
                acceptLabel: "伏击");

            Hand chosen = PreTurnChoiceReader.Read(choiceSink, manager.GetChoiceProvider(owner),
                                                   request, owner);
            if (chosen == null) return;

            EnterAmbush(candidate);
            manager.NoteSkill(owner, "伏击", $"暗置{CombatText.HandName(owner, candidate)}，进入伏击");

            // 记到战报里 —— 否则玩家会看到"（无效果）"，而实际上他进了伏击。
            if (manager.LastTurnResult != null) manager.LastTurnResult.EnteredAmbush = true;
        }

        private static bool IsAttackEffect(HandEffectType effect)
            => effect == HandEffectType.Attack4
               || effect == HandEffectType.Attack6
               || effect == HandEffectType.Attack8;

        // ---------------------------------------------------------------
        // AI：为"凑 1/2/3"铺路（玩家要求仿照游荡者·刺客 / 战士·剑士的两步凑数，但值低一点）
        // ---------------------------------------------------------------

        /// <summary>
        /// 离"两手都是 1/2/3"还差几步：0 = 都就位；1 = 一只手就位；2 = 都要做。
        ///
        /// ⚠️ 与游荡者·刺客的"双 7"不同：目标是**两手同值且都 &lt;= 3**，
        ///    所以"就位"= 这只手落在 1..3 内。
        ///    至于"两手还要相等"，交给价值表的同值对加成去抓 ——
        ///    这里只负责把两只手都推进 1..3。
        /// </summary>
        public int StepsToGoal(int actedValueAfter, int otherHandValue)
        {
            int have = 0;
            if (actedValueAfter >= 1 && actedValueAfter <= AmbushMaxValue) have++;
            if (otherHandValue >= 1 && otherHandValue <= AmbushMaxValue) have++;

            return 2 - have;
        }

        /// <summary>
        /// 1/2/3 是达成伏击必需的值 —— **别拿它去做运算**（会把它改掉）。
        ///
        /// 与游荡者·刺客同一个用途：已经有一只 1/2/3 时，应该用**另一只手**
        /// 朝 1..3 走，而不是把就位的那只改掉。
        /// </summary>
        public bool IsGoalValue(int handValue)
            => handValue >= 1 && handValue <= AmbushMaxValue;

        /// <summary>
        /// 给这一手加分，让 AI 愿意为伏击铺路。
        ///
        /// ⚠️ **分值比游荡者·刺客低**（玩家明确要求："值要低一点"）：
        ///    游荡者·刺客是 90 / 15，这里用 60 / 10。
        ///    理由：伏击的收益是"一次免伤 + 攻击 +1 / 距离 3"，
        ///    比游荡者·刺客的潜行（完全闪避）轻；而且它对**手值范围**有要求
        ///    （必须都 <= 3），硬凑的代价比凑 7 更大 ——
        ///    分值给太高会让 AI 为了伏击放弃明显更好的进攻。
        /// </summary>
        public override int ScoreMoveBonus(MoveValueContext context)
        {
            int steps = StepsToGoal(context.ActedValueAfter, context.OtherHandValue);

            switch (steps)
            {
                case 1: return 60;      // 另一只手已就位，这一手把它补齐
                // ⚠️ 从 10 提到 45（玩家要求"调高两段到达 123 的价值"）。
                //    实测：10 的时候 30 局一次都没凑成伏击 ——
                //    因为"还差两步"是这个计划的**常见起点**（开局两手往往是 1/1，
                //    那本身就是 0 步；但被打散之后多半是"差两步"），
                //    给 10 分等于**几乎不驱动**。45 分足以让它愿意为伏击让路，
                //    又仍低于"差一步"的 60（越接近目标越该抓紧）。
                case 2: return 45;
                default: return 0;      // 已成：交给价值表的同值对加成
            }
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _ambushHand = null;
            AmbushCount = 0;      // 计数也是"每局重置"的一部分

            // 重开一局必须把手上的暗置状态也清掉 ——
            // 否则第二局开局那只手仍然是"不能碰"的，而伏击状态已经没了。
            if (owner != null)
            {
                foreach (Hand h in owner.Hands) h?.Reveal();
            }
        }

        /// <summary>
        /// 游侠·幽域追踪者：非伏击是**挥砍**，**伏击**是穿刺（玩家指定）。
        /// </summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => IsAmbushing ? AttackDamageType.Piercing : AttackDamageType.Slashing;

}
}
