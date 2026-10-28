using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 角色行为钩子接口，由 CharacterTemplate 及其子类实现，PlayerState 绑定使用。
    /// </summary>
    public interface ICharacterBehavior
    {
        /// <summary>攻击结算前介入，可修改 AttackContext（如无视护盾、增伤）。</summary>
        void OnResolveAttack(AttackContext context);

        /// <summary>某只手合成护盾时触发。</summary>
        void OnShieldFormed(PlayerState owner, Hand shieldHand, GameManager manager);

        /// <summary>
        /// "运算已完成、主效果尚未结算"时触发（"抢跑"阶段）。
        ///
        /// 用途：某些机制必须在主效果之前生效。例如牧师·生命领域的"运算出双 9 时先回自己 9 点，
        /// 然后再结算那次 9 的回复"——顺序反了结果就不同。
        ///
        /// 此时运算源**已经**变成结果值，另一只手未变；
        /// 想让"双手和为 X"这类条件立刻生效，可以直接改另一只手的值。
        /// </summary>
        void OnBeforeEffectResolved(PlayerState owner, Hand actedHand,
                                    int targetValue, HandEffectType effect, GameManager manager,
                                    IPreTurnChoiceSink choiceSink);

        /// <summary>
        /// 某只手参与运算并结算完成后触发（"运算后"机制，例如野蛮人·野兽道途的双手和为 7）。
        ///
        /// <paramref name="choiceSink"/> 是**行动方自己**的抉择接收者，用来回读玩家在运算前的选择。
        /// <paramref name="actedHandValueBefore"/> 是运算源在运算**之前**的数值
        /// （运算后它已变成结果值，需要判断"运算前的双手"时用它）。
        /// </summary>
        void OnOperationResolved(PlayerState owner, Hand actedHand, int actedHandValueBefore,
                                 int targetValue, HandEffectType effect, GameManager manager,
                                 IPreTurnChoiceSink choiceSink);

        /// <summary>自身护盾被击碎时触发（用于战士·破阵者连锁破盾等）。</summary>
        void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager);

        /// <summary>
        /// 自身护盾**被别人打碎**时触发（区别于"自己主动破盾"）。
        ///
        /// 由 <see cref="GameManager.BreakShieldAndNotify"/> 调用 ——
        /// 即"被攻击消耗掉"或"被连锁破盾打碎"。
        /// 圣武士·奉献之誓用它区分两种碎盾（两种都触发冲击，但语义不同）。
        /// </summary>
        void OnOtherShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager);

        /// <summary>
        /// 承受攻击时触发，用于"闪避"类机制（游荡者·风流剑客）。
        /// 实现若决定闪避，需要自行把 <paramref name="context"/> 记为闪避并消耗对应的手。
        ///
        /// <paramref name="hitIndex"/> 是"第几段"（忍耐者的两段会各自调用一次），
        /// 用它区分同类抉择的槽位，避免两段读到同一个答案。
        /// </summary>
        void TryDodge(PreTurnChoiceContext context, IPreTurnChoiceSink sink, int hitIndex);

        /// <summary>
        /// 整场比赛开始时触发（新开一局、重开一局）。
        /// 角色必须在此清空所有跨回合的内部状态，例如野蛮人·守望者道途的锁手映射、野蛮人·野兽道途的成长加成。
        /// <see cref="GameManager.ResetMatch"/> 保证本方法一定被调用。
        /// </summary>
        void OnMatchReset(PlayerState owner, GameManager manager);

        /// <summary>造成伤害修正（白板 = 0）。</summary>
        int AttackBonus { get; }

        /// <summary>受到伤害修正（正数 = 减伤，负数 = 易伤）。</summary>
        int DefenseBonus { get; }
    }

    /// <summary>有玩家死亡后通知（法师·死灵学派的换牌）。对局结束时不调用。</summary>
    public interface INpcCircuitProgress { int CircuitProgress {get;} }

    public interface IPlayerDeathListener
    {
        void OnPlayerDied(PlayerState dead, GameManager manager);
    }

    /// <summary>受到元素攻击时自动反应（武僧·四象宗）。</summary>
    public interface IIncomingElementReaction
    {
        void OnIncomingElementAttack(AttackContext context, GameManager manager);
    }

    /// <summary>在伤害计算完成后吸收伤害（法师·防护学派的奥术屏障）。</summary>
    public interface IIncomingDamageBarrier
    {
        int AbsorbDamage(PlayerState target, int damage, AttackContext context, GameManager manager);
    }

    /// <summary>受到攻击时用一只 2 变 1 完全挡下一段（法师·死灵学派）。</summary>
    public interface IAttackBlocker
    {
        bool TryBlockAttack(AttackContext context, GameManager manager, int hitIndex);
    }

    /// <summary>在运算前判断是否仅改数字、压制本次所有效果（法师·预言学派）。</summary>
    public interface IActionInvalidator
    {
        bool CanInvalidateAction(PlayerState owner, PlayerState actor);
        bool TryInvalidateAction(PlayerState owner, PlayerState actor,
                                 Hand actedHand, Hand targetHand,
                                 GameManager manager, out string reason);
    }

    /// <summary>回合开始通知（法师·预言学派用于结束自己的 7 窗口）。</summary>
    public interface ITurnStartHook
    {
        void OnTurnStarted(PlayerState owner, GameManager manager);
    }

    /// <summary>
    /// 固定普通攻击伤害（如光明领域的4点伤害）。
    ///
    /// 由 <see cref="GameManager.ResolveStatusTurnEnd"/> 在"该玩家的回合结束"
    /// （燃烧 / 状态递减之后）触发 —— 界面与无头两条路径都经过它，
    /// 所以在这里挂不会出现"某条路径忘了清"的漂移。
    /// </summary>
    public interface IFixedNormalAttackDamage
    {
        int NormalAttackDamage { get; }
        AttackDamageType NormalAttackType { get; }
    }

    /// <summary>自己回合结束通知：征服之誓层数衰减、剑歌维持等，界面与无头共用。</summary>
    public interface ITurnEndHook
    {
        void OnTurnEnded(PlayerState owner, GameManager manager);
    }

    /// <summary>整次攻击结算后通知；闪避、盾挡与零伤害也算，取消和持续伤害不算。</summary>
    public interface IAttackFinishedHook
    { void OnAttackFinished(PlayerState owner, AttackContext attack, bool initiated, GameManager game); }

    /// <summary>攻击前的通用目标观察口；结算后成长使用IAttackFinishedHook。</summary>
    public interface IAttackTargetedHook
    {
        void OnTargetedByAttack(PlayerState owner, AttackContext context, GameManager manager);
    }

    /// <summary>
    /// 攻击**实际造成伤害之后**的通知（战士·战斗大师的招式附加）。
    ///
    /// ⚠️ 与"命中"（<see cref="IStatusApplyingAttack"/>）口径**不同**：
    ///    只有 <see cref="AttackContext.AppliedDamage"/> &gt; 0 才触发 ——
    ///    被盾挡（0 伤）、被闪避、被取消、太极减至 0 都不算"造成伤害"。
    /// 调用点在 <see cref="AttackContext.Resolve"/> 的最后，经
    /// <see cref="CharacterBehaviors.Implementers{T}"/> 遍历（双将部件也会收到）。
    /// </summary>
    public interface IOnDamageLanded
    {
        void OnDamageLanded(AttackContext context, GameManager manager);
    }

    /// <summary>
    /// 战士·战斗大师的"招式"选择结果归属（**角色侧持有**，UI / 控制器写入）。
    ///
    /// 为什么需要它：Controller / UI 在 Assistant 与 UI 层，**不能引用 Characters**
    /// （分层铁律）；而"选了哪种状态"既不是 <see cref="Hand"/> 也不是抉择器的枚举，
    /// 只能由角色自己保存。与 <see cref="IAbjurationSkill"/> 的
    /// <c>SelectedResistance</c> 同一模式。
    /// </summary>
    public interface IBattleMasterManeuverChoice
    {
        /// <summary>当前分支可选的招式（4/8 → 流血/跛脚；6 → 眩晕/倒伏；无则长度为 0）。</summary>
        StatusEffect[] ManeuverOptions { get; }

        /// <summary>玩家选定的招式（写入后由结算按当前分支校验）。</summary>
        void SelectManeuver(StatusEffect status);

        /// <summary>玩家明确选择**不发动**（与"没问过"区分，结算时不再走 AI 默认）。</summary>
        void DeclineManeuver();
    }

    /// <summary>
    /// **攻击命中后要给目标挂状态**的角色实现本接口（法师·塑能学派）。
    ///
    /// 为什么单独一个接口：挂状态的时机在 `AttackContext.Resolve` 的最后
    /// ——必须等"闪避 / 取消 / 盾 / 太极"全部判完，才知道**算不算命中**。
    ///
    /// 玩家确认的命中口径：
    ///   · 被盾**抵挡**、被太极**减免** -> **仍算命中**（负面状态附加时长按格挡／对应抗性扣减，最低 0）
    ///   · 被**闪避**、被邪术师·至高妖精**取消** -> **不算命中**（不挂状态）
    /// </summary>
    public interface IStatusApplyingAttack
    {
        /// <summary>
        /// 本次攻击要挂上的状态（<see cref="StatusEffect.None"/> = 不挂）。
        /// 由 <see cref="AttackContext"/> 在确认命中后调用。
        /// </summary>
        StatusEffect StatusToApplyOnHit(AttackContext context);
    }

    /// <summary>
    /// **命中后想做别的事**的角色实现本接口（邪术师·旧日支配者：加距离）。
    ///
    /// 调用时机与 <see cref="IStatusApplyingAttack"/> 完全相同 ——
    /// 闪避 / 取消 / 盾 / 太极都判完了，确认**算命中**之后。
    /// 命中口径沿用同一套：被盾抵挡、被太极减免仍算命中；
    /// 被闪避或被取消不算。
    /// </summary>
    public interface IOnHitLanded
    {
        /// <summary>确认命中后调用。</summary>
        void OnHitLanded(AttackContext context, GameManager manager);
    }

    /// <summary>受到攻击实际伤害后触发（圣武士·古贤之誓）。</summary>
    public interface IPostDamageTaken
    {
        void OnDamageTaken(PlayerState owner, int damage, AttackContext context, GameManager manager);
    }
    /// <summary>一次攻击造成伤害后通知；致死目标也通知，供妖魔汲取等自身收益。</summary>
    public interface IOnDamageDealt
    {
        void OnDamageDealt(AttackContext context, GameManager manager);
    }

    /// <summary>治疗溢出时触发（邪术师·妖魔转虚假生命）。</summary>
    public interface IHealingOverflow
    {
        void OnHealingOverflow(PlayerState owner, int overflow);
    }

    /// <summary>
    /// 攻击**不可被闪避**的角色实现本接口（法师·咒法学派）。
    ///
    /// 为什么要单独一个接口，而不是让游荡者·风流剑客去判断 `is SchoolOfConjurationLogic`：
    /// 角色之间不应该互相认识。谁想让自己的攻击无法被闪避，谁就实现它。
    ///
    /// （曾经我按"伤害数值是 8"来判定 —— 那是错的：法师·咒法学派也能用 4/6 攻击，
    /// 而**其他角色打出 8 反而变得无法闪避**。判定必须看"攻击者是谁"，不是"打了多少"。）
    /// </summary>
    public interface IDodgeBlocker
    {
        /// <summary>true 表示本角色发起的攻击不可被闪避。</summary>
        bool PreventsDodge { get; }
    }

    /// <summary>
    /// 需要**接管"跳过回合"**的角色实现本接口（游荡者·刺客的潜行）。
    ///
    /// "跳过回合"原本只对"双盾在手"开放（见 RULES.md §14）；
    /// 有些角色有自己的跳过条件与副作用，所以由角色自己声明与处理。
    /// </summary>
    public interface ITurnSkipRule
    {
        /// <summary>
        /// 让内部的"跳过机会是否已用掉"与当前状态保持一致。
        /// 在每次询问 <see cref="CanSkip"/> 之前调用，避免依赖"某个钩子一定会被触发"。
        /// </summary>
        void SyncSkipState(PlayerState owner);

        /// <summary>该玩家当前是否允许跳过自己的回合。</summary>
        bool CanSkip(PlayerState owner, GameManager manager);

        /// <summary>跳过回合时的副作用（例如消耗掉潜行的那次跳过机会）。</summary>
        void OnTurnSkipped(PlayerState owner, GameManager manager);
    }

    /// <summary>
    /// "这一手对当前角色值不值"的上下文，交给角色自己判断。
    ///
    /// 只带被动数据，不含任何决策 —— 因此 Core 层可以安全地定义它。
    /// </summary>
    public readonly struct MoveValueContext
    {
        /// <summary>被操作的自己那只手。</summary>
        public Hand ActedHand { get; }

        /// <summary>被操作的那只手**运算后**的值。</summary>
        public int ActedValueAfter { get; }

        /// <summary>被操作的对手那只手。</summary>
        public Hand TargetHand { get; }

        /// <summary>自己**另一只手**的值（运算后不变）。</summary>
        public int OtherHandValue { get; }

        public MoveValueContext(Hand actedHand, int actedValueAfter,
                                Hand targetHand, int otherHandValue)
        {
            ActedHand = actedHand;
            ActedValueAfter = actedValueAfter;
            TargetHand = targetHand;
            OtherHandValue = otherHandValue;
        }
    }

    /// <summary>
    /// 会"自己判断某手棋特别值钱"的角色实现本接口（野蛮人·野兽道途凑 7、战士·剑士双持同值 ……）。
    ///
    /// 为什么反转依赖：AI 在 Assistant 层，按项目约定**不得引用 Characters**。
    /// 所以不是"AI 认识野蛮人·野兽道途"，而是"野蛮人·野兽道途告诉 AI 这一手值多少"。
    /// </summary>
    public interface IAiMoveAdvisor
    {
        /// <summary>给这一手追加的分值（0 表示没有特别价值）。</summary>
        int ScoreMoveBonus(MoveValueContext context);
    }

    /// <summary>
    /// 拥有"**我需要多回合才能达成的目标状态**"的角色实现本接口。
    ///
    /// 场景：游荡者·刺客要"两只手都是 7"才能进潜行。这不是一手能成的 ——
    /// 第一手先把一只手做成 7，**下一手**再把另一只做成 7。
    ///
    /// ⚠️ 为什么需要这个接口：价值表的 `Combos` 只在**已经达成**时给分
    ///    （`DoublesOf` 要求 `actedAfter == otherHand`），
    ///    所以贪心评分**看不到"离目标还差一只手"的价值**。
    ///    实测（游荡者·刺客诊断）：第一手走的是 `1+2=3`，从不保留 7，
    ///    30 局里潜行发动 0 次。
    ///
    /// 有了它，AI 就能"为两步计划铺路"，而不只是"看到成型的奖励"。
    /// </summary>
    public interface ITwoStepGoal
    {
        /// <summary>本回合这一手之后，我离目标状态还差多远（0 = 已达成）。</summary>
        int StepsToGoal(int actedValueAfter, int otherHandValue);

        /// <summary>
        /// 这个手值是否属于"达成目标所必需"（例如游荡者·刺客的 7）。
        ///
        /// 用于：**别把计划要用的那只手拿去做运算** ——
        /// 拿它当运算源就会把它改掉，计划当场作废。
        /// </summary>
        bool IsGoalValue(int handValue);
    }

    /// <summary>
    /// 角色实现"**本回合我需要优先保住某只手**"（目标计划的铺路）。
    ///
    /// 场景：游荡者·刺客已经有一只 7 时，这一手**别去动那只 7**，
    /// 而应该用另一只手朝 7 走。
    ///
    /// 这条信息已经由 <see cref="ITwoStepGoal.IsGoalValue"/> 提供，
    /// 这里不再重复定义接口（避免两个接口说同一件事）。
    /// </summary>

    /// <summary>
    /// 拥有"用某只手换一次完全闪避"能力的角色实现本接口（游荡者·风流剑客的 1 变 2）。
    ///
    /// 上层（BoardController）需要它来**为每一段攻击各准备一次闪避询问**：
    /// 一次攻击可能是多段，而"要闪几段"在执行前必须先问清楚。
    /// 用接口而不是具体类型判断，控制器就不必认识任何角色。
    /// </summary>
    public interface IDodgeCapability
    {
        /// <summary>当前可用于闪避的手（值等于触发值、且未被锁）。</summary>
        System.Collections.Generic.List<Hand> GetDodgeHands(PlayerState owner);

        /// <summary>
        /// 这一击**能不能被闪避**（决定要不要向玩家提问）。
        ///
        /// 非攻击的操作（合盾 / 回复 / 无效果）不需要问；
        /// 法师·咒法学派的攻击也不可闪避。把这条判断也交给角色，
        /// 上层就不必认识任何具体角色类型。
        /// </summary>
        bool CanDodgeAgainst(PreTurnChoiceContext context);
    }

    /// <summary>
    /// 会在主攻击之前追加一次独立攻击的角色实现本接口（战士·剑士）。
    /// 上层用它判断"这一击实际是几次攻击"。
    /// </summary>
    public interface IExtraAttackProvider
    {
        /// <summary>本次运算是否会在主攻击前追加一次攻击。</summary>
        bool WillExtraAttack(PreTurnChoiceContext context);
    }

    /// <summary>
    /// 攻击会拆成多段的角色实现本接口（武僧·忍宗）。
    ///
    /// 上层需要**提前**知道段数，才能为每一段各问一次闪避
    /// （"要闪几段"在执行前必须确定，否则结算中途没法再问玩家）。
    /// </summary>
    public interface IMultiHitAttack
    {
        /// <summary>攻击段数（武僧·忍宗 = 2）。</summary>
        int MaxHits { get; }
    }

    /// <summary>
    /// 角色声明"我现在能不能被选为攻击目标"。
    ///
    /// 由 <see cref="Assistant.BoardController"/> 查询 —— 它是 Assistant 层，
    /// **不能引用 Characters 层**（分层铁律），所以通过本接口反向调用。
    ///
    /// 目前的实现者：游荡者·刺客（潜行中不能被攻击）。
    /// 日后可扩展为"攻击距离不足"等。
    /// </summary>
    public interface IAttackTargetRule
    {
        /// <summary>该玩家当前是否**不能被攻击**。</summary>
        bool IsImmuneToAttack(PlayerState owner);
    }

    /// <summary>
    /// 角色声明自己的**攻击距离**。
    ///
    /// 规则（玩家确认）：
    ///   · 合法攻击目标 = 处于攻击范围内的其他角色（自己除外，**队友也算**）
    ///   · 攻击距离 = 到那位玩家在**座位环**上的最短距离（阵亡者从环上摘掉）
    ///   · 白板与大多数角色是 **1**；**法师·咒法学派、牧师·生命领域是无限**
    ///
    /// 由 <see cref="Assistant.BoardController"/> 查询 —— 它是 Assistant 层，
    /// **不能引用 Characters 层**（分层铁律），所以通过本接口反向调用。
    /// </summary>
    public interface IAttackRangeProvider
    {
        /// <summary>
        /// 基础攻击距离。<see cref="System.Int32.MaxValue"/> 表示**无限**。
        /// </summary>
        int BaseAttackRange { get; }
    }

    /// <summary>
    /// 角色实现"以代价换取**本次攻击距离无限**"的副作用（战士·破阵者碎盾换射程）。
    ///
    /// 为什么单独一个接口：这个副作用必须发生在**距离检查之前**，
    /// 而距离检查在攻击分派的开头。让 <see cref="GameManager"/> 直接调一个
    /// 明确的钩子，比在各个角色里找时机可靠。
    ///
    /// ⚠️ 实现必须**只回读已记录的答案**，不能在结算里向玩家索取输入 ——
    /// 结算一旦开始就一次跑完。
    /// </summary>
    public interface IRangeBreakEffect
    {
        /// <summary>若玩家选择了"换射程"，在这里落实代价（例如打碎一面盾）。</summary>
        void ApplyRangeBreak(PlayerState actor, Hand actedHand, GameManager manager);
    }

    /// <summary>
    /// **把多个角色行为包成一个角色**的容器（双将）。
    ///
    /// ⚠️ 为什么要单独定义在 Core：
    ///    双将时 <c>actor.Character</c> 是**容器**，不是部件本身，
    ///    所以部件判“这次该不该我发动”时**不能**写
    ///    <c>ReferenceEquals(actor.Character, this)</c> —— 那种写法在双将里恒为假，
    ///    技能会**静默失效**（玩家实测：“双将游侠·幽域追踪者没有出现伏击按钮”）。
    ///
    ///    而容器实体（<c>DualGeneralBehavior</c>）住在 Characters 层，
    ///    Core 不允许认识它 —— 所以**契约定义在 Core、实现在 Characters**。
    /// </summary>
    public interface ICharacterBehaviorContainer
    {
        /// <summary>这个容器里是不是有这个部件。</summary>
        bool Contains(ICharacterBehavior part);

        /// <summary>容器里的全部部件（顺序即优先级）。</summary>
        IReadOnlyList<ICharacterBehavior> Parts { get; }
    }

    /// <summary>
    /// 部件侧的统一判断：**我在不在这位角色的技能集合里**。
    ///
    /// 单独角色时就是 <c>actor.Character == part</c>；
    /// 双将时问容器的 <see cref="ICharacterBehaviorContainer.Contains"/>。
    /// </summary>
    public static class CharacterBehaviors
    {
        public static System.Collections.Generic.IEnumerable<T> Implementers<T>(PlayerState actor) where T:class
        {
            if(actor?.Character is ICharacterBehaviorContainer c)
            { foreach(var p in c.Parts) if(p is T t) yield return t; }
            else if(actor?.Character is T direct) yield return direct;
        }
        public static IHandEffectRewriter EffectRewriterFor(PlayerState actor, int result)
        {
            if (actor?.Character is ICharacterBehaviorContainer container)
                return EffectRewriterFor(container.Parts,result);
            var rewriter=actor?.Character as IHandEffectRewriter;
            return rewriter is ISelectiveHandEffectRewriter selective && !selective.HandlesResult(result) ? null : rewriter;
        }

        public static IHandEffectRewriter EffectRewriterFor(IReadOnlyList<ICharacterBehavior> parts, int result)
        {
            if(parts==null) return null;
            foreach(var part in parts)
                if(part is IHandEffectRewriter rewriter
                    && (!(part is ISelectiveHandEffectRewriter selective) || selective.HandlesResult(result)))
                    return rewriter;
            return null;
        }

        public static HandEffectType DisplayedEffectOf(PlayerState actor, int result)
        {
            var rewriter=EffectRewriterFor(actor,result);
            if(rewriter is IHandEffectDisplay display) return display.DisplayedEffectOf(result);
            return FirstImplementer<IHandEffectDisplay>(actor)?.DisplayedEffectOf(result) ?? Hand.GetEffectType(result);
        }

        public static bool ActorHas(PlayerState actor, ICharacterBehavior part)
        {
            if (actor?.Character == null || part == null) return false;
            if (ReferenceEquals(actor.Character, part)) return true;

            return actor.Character is ICharacterBehaviorContainer c && c.Contains(part);
        }

        /// <summary>
        /// **这一次运算是不是"拿自己的手当被加数"**（游荡者·魔术师的特色）。
        ///
        /// 判据：这位角色允许自加，**且**被加数的那只手也是他自己的。
        ///
        /// \u26a0\ufe0f 为什么要这样一个统一助手：游荡者·魔术师的"结果 2 变成攻击"与
        ///    "2 也要问打谁"都依赖这个判断。我原来用了一个
        ///    "上层在运算前告知"的标记（<c>MarkSelfAddend</c>），
        ///    但那个方法**从来没有任何调用点** —— 标记恒为 false，
        ///    于是判定写反、2 既没变成攻击、也没被追问目标。
        ///    改成**直接从手牌归属推出来**，不依赖任何易漏的调用约定。
        /// </summary>
        public static bool ActorHasSelfAddend(PlayerState actor, Hand targetHand)
        {
            if (actor == null || targetHand == null) return false;

            // 被加数必须是他自己的手（否则就是"碰别人的手"，不是自加）
            if (!actor.Owns(targetHand)) return false;

            var self = actor.Character as ISelfAddendProvider;
            return self != null && self.AllowsSelfAddend;
        }

        /// <summary>
        /// **这位角色里第一个实现了 <typeparamref name="T"/> 的部件**（没有则 null）。
        ///
        /// \u26a0\ufe0f \u4e3a\u4ec0\u4e48\u5fc5\u987b\u8d70\u5b83\uff0c\u800c\u4e0d\u80fd\u5199
        ///    <c>actor.Character is IHealOverride</c>\uff1a
        ///    \u53cc\u5c06\u65f6 <c>actor.Character</c> \u662f <c>DualGeneralBehavior</c> **\u5bb9\u5668**\uff0c
        ///    \u5b83\u81ea\u5df1\u5e76\u4e0d\u5b9e\u73b0\u90a3\u4e9b\u80fd\u529b\u63a5\u53e3\uff08\u53ea\u662f\u8f6c\u53d1\uff09\uff0c
        ///    \u6240\u4ee5\u90a3\u79cd\u5199\u6cd5**\u6052\u4e3a\u5047** \u2014\u2014
        ///    \u6280\u80fd\u4f1a\u9759\u9ed8\u5730\u9000\u5316\u6210\u9ed8\u8ba4\u884c\u4e3a\u3002
        ///
        ///    \u5b9e\u6d4b\uff08\u73a9\u5bb6\u62a5\u544a\uff09\uff1a\u53cc\u5c06\u91cc\u7267\u5e08\u7684 9
        ///    \u201c\u53ea\u4ece 2 \u8840\u56de\u5230 11 \u8840\u800c\u4e0d\u662f 20 \u8840\u201d\u2014\u2014
        ///    \u6b63\u662f\u56e0\u4e3a\u8fd9\u91cc\u9000\u5230\u4e86 else \u5206\u652f\uff08\u9ed8\u8ba4\u56de\u590d\uff09\u3002
        /// </summary>
        public static T FirstImplementer<T>(PlayerState actor) where T : class
        {
            if (actor?.Character == null) return null;

            // ⚠️⚠️ **容器必须先往下找部件，不能把自己交出去**。
            //
            //    `DualGeneralBehavior` **自己也实现**了这些能力接口
            //    （它把各部件的结果"取最大 / 取第一个"再暴露出去），
            //    所以下面那句 `is T direct` 对容器**恒为真** ——
            //    我第一版就是先写 direct、结果把容器自己返回了：
            //      FirstImplementer<IHealOverride> = DualGeneralBehavior
            //    于是 GameManager 调的是**容器那个泛型 ApplyHeal**，
            //    而不是**牧师·生命领域的** —— 牧师·生命领域的分支永远进不去，
            //    回复量落到默认值（实测 HealAmount=0、HP 完全不涨）。
            //
            //    所以顺序必须是：**先容器 -> 再 direct**。
            if (actor.Character is ICharacterBehaviorContainer c && c.Parts != null)
            {
                for (int i = 0; i < c.Parts.Count; i++)
                    if (c.Parts[i] is T t) return t;

                // 容器里的部件都不实现 -> 就当没有（**不要回退到容器自己**：
                // 那是容器的聚合实现，语义与"某个角色真的能接管"不同）。
                return null;
            }

            return actor.Character as T;
        }
    }

    /// <summary>
    /// **可以把自己的手当被加数**的角色实现本接口（游荡者·魔术师）。
    ///
    /// 玩家确认："可以选择自己的手当被加数"
    /// （例：左手 3、右手 5 -> 拿左手 3 碰自己的 5 凑出 8）。
    /// 两只手**任选一个方向**，变的是**先点的那只手**（即发起运算的手）。
    ///
    /// 用独立接口而不是硬编码角色类型：分层铁律下
    /// 上层不能认识具体角色（见 <see cref="IPiercesMitigation"/> 同理）。
    /// </summary>
    public interface ISelfAddendProvider
    {
        /// <summary>这一次是否允许拿自己的另一只手当被加数。</summary>
        bool AllowsSelfAddend { get; }
    }

    /// <summary>
    /// 攻击**可以穿透减免**的角色实现本接口（法师·咒法学派）。
    ///
    /// 玩家确认：法师·咒法学派**可以直接穿透伏击**打出伤害，
    /// 即伏击**无法抵挡咒术攻击**，且咒术攻击**不会解除伏击**。
    ///
    /// ⚠️ 为什么要单独一个接口：
    ///    <c>WayOfTheTaiChiLogic</c> 原本写的是 <c>is SchoolOfConjurationLogic</c>（硬编码角色类型），
    ///    而如今**游侠·幽域追踪者也要判同一件事** ——
    ///    再硬编码一次就会出现"两个角色各自认识法师·咒法学派"，
    ///    且以后每加一个“可穿透”角色都要改两处。
    ///    改成接口后只有一个地方声明。
    /// </summary>
    public interface IPiercesMitigation
    {
        /// <summary>true 表示本角色发起的攻击**不受减免影响**。</summary>
        bool PiercesMitigation { get; }
    }

    /// <summary>
    /// 自己要接管"回复效果"结算的角色实现本接口。
    ///
    /// 牧师·生命领域的 9 可以回给对手，而 <see cref="GameManager"/> 的默认实现只会回给行动方自己。
    /// 与其让角色去"撤销"默认治疗（那会在残血时把角色扣死），不如让角色**完全接管**这一步。
    /// </summary>
    public interface IHealOverride
    {
        /// <summary>
        /// 执行本次回复效果，返回**实际被回复的玩家**（<see cref="GameManager"/> 用于记录战报）。
        /// </summary>
        PlayerState ApplyHeal(PlayerState owner, GameManager manager, IPreTurnChoiceSink choiceSink);
    }

    /// <summary>
    /// 角色实现"**本次回复量由我说了算**"（野蛮人·巨人道途的 9：回复量 = 另一只手点数 × 2）。
    ///
    /// 默认回复量是固定的 9（<see cref="Hand.HealAmount"/>），
    /// 牧师·生命领域按效果类型算（<c>LifeDomainLogic.HealAmountFor</c>）。
    /// 野蛮人·巨人道途的回复量取决于**另一只手的当前值**，所以必须由角色自己算。
    ///
    /// 由 <see cref="GameManager"/> 在回复分支调用（与 <see cref="IHealOverride"/> 配合）。
    /// </summary>
    public interface IHealAmountProvider
    {
        /// <summary>本次回复的**总量**（已经包含所有段数）。</summary>
        int HealAmountOf(PlayerState owner, GameManager manager);
    }

    /// <summary>声明真正接管的数字；未接管的普通数字不应挡住双将另一技能。</summary>
    public interface ISelectiveHandEffectRewriter
    {
        bool HandlesResult(int value);
    }

    /// <summary>
    /// 角色实现"**改写本次运算产生的效果类型**"。
    ///
    /// 由 <see cref="GameManager"/> 在运算**之后、效果分派之前**调用
    /// （此时手值已经变成结果值，但护盾状态还没设）。
    ///
    /// 目前的实现者：**圣武士·奉献之誓**（4 与 6 不攻击，改为合出护盾）。
    ///
    /// ⚠️ 调用方必须保证一致性：改写后的效果决定手牌状态
    ///    （例如改成 Shield 就要把手设成 Formed 并触发 OnShieldFormed），
    ///    否则会出现"看起来是盾、实际不是盾"。
    /// </summary>
    public interface IHandEffectRewriter
    {
        /// <summary>
        /// 返回改写后的效果类型；不想改写就原样返回。
        /// </summary>
        HandEffectType RewriteEffect(PlayerState actor, Hand actedHand,
                                     int targetValue, GameManager manager);
    }

    /// <summary>
    /// 角色实现"**取消某次攻击**"（邪术师·至高妖精）。
    ///
    /// 由 <see cref="AttackContext"/> 在**最开头**询问 —— 规则确认它是
    /// **最早结算**的，能取消掉法师·咒法学派那种"攻击方钩子改写伤害"的攻击。
    ///
    /// ⚠️ 取消者不是挨打的人，而是**场上的第三方**：所以判定要遍历全部参战玩家。
    /// </summary>
    public interface IAttackCanceller
    {
        /// <summary>
        /// 返回 true 表示"这次攻击被取消了"（调用方立即中止结算、不造成伤害）。
        ///
        /// <paramref name="attackValue"/> 是**攻击数字**：
        /// 战士·剑士的追加攻击与圣武士·奉献之誓的冲击视为 **0**（因此无法被取消）。
        /// </summary>
        bool TryCancelAttack(PlayerState canceller, PlayerState attacker,
                             PlayerState target, int attackValue, GameManager manager);
    }

    /// <summary>
    /// 角色实现"**改写本次攻击造成的伤害**"（战士·魔射手：6 伤害 +2、8 无视护盾）。
    ///
    /// 由 <see cref="AttackContext"/> 在攻击方钩子阶段调用。
    /// </summary>
    public interface IAttackDamageRule
    {
        /// <summary>在攻击方钩子里改写伤害与护盾规则（直接改 <paramref name="context"/>）。</summary>
        void ApplyAttackRule(AttackContext context, int attackValue, GameManager manager);
    }

    /// <summary>
    /// 角色实现"**化解这次攻击**"（武僧·太极宗的太极）。
    ///
    /// 由 <see cref="AttackContext"/> 在伤害已定、逐段结算**之前**询问。
    ///
    /// 与其他免伤机制的区别：
    ///   · **闪避**是"完全免伤"
    ///   · **护盾**是"用一面盾完全挡下一次"
    ///   · **太极**是"**减半**，并把剩下的一半**反弹**给攻击方"
    ///
    /// 所以它必须能改写成"实际受到的伤害"，并附带一个反弹量。
    /// </summary>
    public interface IDamageMitigator
    {
        /// <summary>
        /// 尝试化解这次攻击。返回 true 表示**已经接管**（调用方必须采用
        /// <paramref name="reducedDamage"/> 与 <paramref name="reflectDamage"/>）。
        /// </summary>
        /// <param name="incomingDamage">原本会造成的伤害（已含攻防加成）。</param>
        /// <param name="reducedDamage">化解后实际承受的伤害。</param>
        /// <param name="reflectDamage">反弹给攻击方的伤害（0 = 不反弹）。</param>
        /// <param name="incomingDamage">整次攻击的总伤害（仅用于判断是否 &gt; 0）。</param>
        /// <param name="perHitDamage">
        /// **单段**伤害。
        ///
        /// ⚠️ 武僧·太极宗的太极只对付**一段** —— 玩家确认：
        ///    "一个太极只能弹一段伤害是对的，不要改成能一次挡多段伤害；
        ///     例如野蛮人·巨人道途的攻击打到太极上，一个太极只能减免反弹一段伤害，
        ///     另一段伤害是要吃的。"
        /// </param>
        /// <param name="segmentDamage">这一段当前的伤害量。</param>
        /// <param name="stages">
        /// 这一段上实际发动了**几次**减免。
        ///
        /// ⚠️ 一般角色只能发动 1 次；武僧·太极宗有**两个太极**时可以发动 2 次：
        ///    "第一次弹一半伤害，第二次弹剩余伤害的一半，
        ///     即只吃 1/4 伤害剩下全部反弹。"
        /// </param>
        /// <param name="totalReduced">这一段上被减免掉的总量。</param>
        /// <param name="totalReflect">这一段上要反弹回去的总量。</param>
        /// <param name="stagesUsed">
        /// 这一段上**已经发动过几次**。
        ///
        /// ⚠️ 玩家要求把**闪避移到太极之前**，于是太极改成**逐段询问**（每个未被闪避的段各问一次）。
        ///    实现方只算**新增那一次**的递减。
        /// </param>
        bool TryMitigate(PlayerState owner, AttackContext context, int segmentDamage,
                         int stagesUsed,
                         out int stages, out int totalReduced, out int totalReflect);
    }

    /// <summary>
    /// 角色实现"**我这张手在界面上该显示成什么效果**"。
    ///
    /// 用途：有些角色会**改写效果类型**，而界面若按通用的
    /// <see cref="Hand.GetEffectType"/> 去上色，就会与实际效果不符。
    ///
    /// 目前的实现者：**野蛮人·巨人道途** —— 它的 4/6/8 是无效果、**5 反而是攻击**，
    /// 与通用规则（5 = 护盾）正好相反，所以界面上必须按它自己的规则显示。
    ///
    /// 由表现层（<c>UI/HandVisual</c>）在绘制手牌时查询。
    /// </summary>
    public interface IHandEffectDisplay
    {
        /// <summary>这张手在界面上应显示成哪种效果（决定配色与状态小字）。</summary>
        HandEffectType DisplayedEffectOf(int resultValue);
    }

    /// <summary>
    /// 角色实现"**我有没有发动过招牌技能**"（供 AI 与测试观测）。
    ///
    /// 为什么需要：有些技能发动后在场上**不留痕迹**（潜行会被后续运算解除、
    /// 锁手会随盾碎解锁、连锁破盾会被别的操作覆盖），于是
    /// "AI 从不使用这个技能"这类问题**根本测不出来** —— 我在这上面卡过。
    ///
    /// 实现者返回"本局发动过几次"（累计值，重开一局清零）。
    /// </summary>
    public interface ISkillUseCounter
    {
        /// <summary>本局发动过几次招牌技能。</summary>
        int SkillUseCount { get; }
    }

    /// <summary>
    /// 角色实现"**AI 应该把这次回复给谁**"。
    ///
    /// 场景：牧师·生命领域在 2v2 里该给**自己这一队血量最低的存活者**回复
    /// （玩家给的规则："目前写成优先给一队血量最低的人回复"）。
    ///
    /// ⚠️ 为什么需要接口：这条规则只有角色自己知道，
    ///    而 AI（Assistant 层）**不能引用 Characters 层**（项目分层约定）。
    ///    由角色声明、AI 调用，依赖方向就反转过来了。
    /// </summary>
    public interface IHealTargetAdvisor
    {
        /// <summary>AI 该把回复给谁（返回 null 表示"没意见，按默认走"）。</summary>
        PlayerState BestHealTarget(PlayerState owner, GameManager manager);
    }

    /// <summary>
    /// 角色实现"**我的斩杀线要按自己的机制调整**"。
    ///
    /// 背景：斩杀线 = 本次攻击的最大伤害 = 基础值 + 攻方攻击加成 − 受方防御加成。
    /// 但有几个角色的实际伤害**不等于**这个式子：
    ///   · **游荡者·风流剑客**：手上有 2 时可以乘风突刺（基础值 ×1.5）
    ///   · **战士·剑士**：双手相同时追加一次 4/6/8 伤害
    ///   · **游荡者·刺客**：潜行中 +7
    ///   · **野蛮人·巨人道途**：打出 5 时是**两段**，每段伤害 = 另一只手的点数
    ///
    /// ⚠️ 为什么必须由角色自己算：这些机制只有角色知道，
    ///    而 AI（Assistant 层）**不能引用 Characters 层**（项目分层约定）。
    ///    不调整的话会算错斩杀线 —— 该杀不杀，或者以为能杀却差 1 点。
    /// </summary>
    public interface ILethalThresholdAdvisor
    {
        /// <summary>
        /// 估算这一手对某人的**总伤害**（用于判断能不能斩杀）。
        ///
        /// ⚠️ 参数给的是**原始值**（结果值 / 攻方攻击加成 / 受方防御加成），
        ///    让角色能按自己的机制重算 —— 因为有些机制作用在
        ///    **叠加攻防之前**。例如游荡者·风流剑客的乘风突刺是"基础值 ×1.5"，
        ///    真实顺序是 `(4 * 1.5) + 攻 - 防`，而不是 `(4 + 攻 - 防) * 1.5`。
        ///
        ///    通用情形下直接返回
        ///    <see cref="AttackContext.ApplyStatBonuses"/> 的结果即可。
        /// </summary>
        int EstimateLethalDamage(LethalContext context, int resultValue,
                                 int attackBonus, int defenseBonus);
    }

    /// <summary>斩杀线估算的上下文。</summary>
    public readonly struct LethalContext
    {
        /// <summary>本次运算源（那只手）。</summary>
        public Hand ActedHand { get; }

        /// <summary>被打的那只手（用它的主人当受害者）。</summary>
        public Hand TargetHand { get; }

        /// <summary>本次运算的结果值。</summary>
        public int ResultValue { get; }

        /// <summary>自己的**另一只手**的点数（野蛮人·巨人道途 / 战士·剑士要用）。</summary>
        public int OtherHandValue { get; }

        public LethalContext(Hand actedHand, Hand targetHand, int resultValue,
                             int otherHandValue)
        {
            ActedHand = actedHand;
            TargetHand = targetHand;
            ResultValue = resultValue;
            OtherHandValue = otherHandValue;
        }
    }

    /// <summary>供状态栏读取武僧当前气点，不提供资源修改入口。</summary>
    public interface IKiResource
    {
        int Ki { get; }
    }

    /// <summary>供状态栏查询该技能是否标记了指定角色。</summary>
    public interface IHunterMarkSource
    {
        bool IsMarked(PlayerState target);
    }

    /// <summary>角色根据战场状态刷新动态攻防（野蛮人·狂战士道途 / 战士·战术家）。</summary>
    public interface IDynamicStats
    {
        void RefreshDynamicStats(PlayerState owner, GameManager manager);
    }

    /// <summary>受攻击者先选择把整次攻击转移给另一名合法角色，再允许圣武士·奉献之誓代挡。</summary>
    public interface IAttackTransfer
    {
        PlayerState TransferAttack(AttackContext context, GameManager manager);
    }

    /// <summary>在判定闪避与护盾前询问圣武士·奉献之誓是否代挡。</summary>
    public interface IProtectionChoiceProvider
    {void CollectRedirectChoice(PlayerState owner,PlayerState attacker,PlayerState target,GameManager game,IList<ChoiceRequest> requests,int attackValue=0);}
    public interface IAttackRedirector
    {
        /// <summary>
        /// 返回 true 表示"这次攻击改由我承受"（调用方负责替换挨打的人）。
        /// </summary>
        bool TryRedirectToSelf(PlayerState owner, PlayerState attacker,
                               PlayerState intendedTarget, GameManager manager);
    }
    /// <summary>已确定防守盾后收集其破碎的选择，避免将加数手误当成防守盾。</summary>
    public interface IShieldBreakChoiceProvider
    {
        void CollectShieldBreakChoices(PlayerState owner, Hand breakingShield, GameManager game, IList<ChoiceRequest> requests);
    }

}
