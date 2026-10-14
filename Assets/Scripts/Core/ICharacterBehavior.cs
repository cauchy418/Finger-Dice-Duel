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
        /// 用途：某些机制必须在主效果之前生效。例如牧师的"运算出双 9 时先回自己 9 点，
        /// 然后再结算那次 9 的回复"——顺序反了结果就不同。
        ///
        /// 此时运算源**已经**变成结果值，另一只手未变；
        /// 想让"双手和为 X"这类条件立刻生效，可以直接改另一只手的值。
        /// </summary>
        void OnBeforeEffectResolved(PlayerState owner, Hand actedHand,
                                    int targetValue, HandEffectType effect, GameManager manager,
                                    IPreTurnChoiceSink choiceSink);

        /// <summary>
        /// 某只手参与运算并结算完成后触发（"运算后"机制，例如狼人的双手和为 7）。
        ///
        /// <paramref name="choiceSink"/> 是**行动方自己**的抉择接收者，用来回读玩家在运算前的选择。
        /// <paramref name="actedHandValueBefore"/> 是运算源在运算**之前**的数值
        /// （运算后它已变成结果值，需要判断"运算前的双手"时用它）。
        /// </summary>
        void OnOperationResolved(PlayerState owner, Hand actedHand, int actedHandValueBefore,
                                 int targetValue, HandEffectType effect, GameManager manager,
                                 IPreTurnChoiceSink choiceSink);

        /// <summary>自身护盾被击碎时触发（用于战士连锁破盾等）。</summary>
        void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager);

        /// <summary>
        /// 自身护盾**被别人打碎**时触发（区别于"自己主动破盾"）。
        ///
        /// 由 <see cref="GameManager.BreakShieldAndNotify"/> 调用 ——
        /// 即"被攻击消耗掉"或"被连锁破盾打碎"。
        /// 圣武士用它区分两种碎盾（两种都触发冲击，但语义不同）。
        /// </summary>
        void OnOtherShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager);

        /// <summary>
        /// 承受攻击时触发，用于"闪避"类机制（游侠）。
        /// 实现若决定闪避，需要自行把 <paramref name="context"/> 记为闪避并消耗对应的手。
        ///
        /// <paramref name="hitIndex"/> 是"第几段"（忍耐者的两段会各自调用一次），
        /// 用它区分同类抉择的槽位，避免两段读到同一个答案。
        /// </summary>
        void TryDodge(PreTurnChoiceContext context, IPreTurnChoiceSink sink, int hitIndex);

        /// <summary>
        /// 整场比赛开始时触发（新开一局、重开一局）。
        /// 角色必须在此清空所有跨回合的内部状态，例如野蛮人的锁手映射、狼人的成长加成。
        /// <see cref="GameManager.ResetMatch"/> 保证本方法一定被调用。
        /// </summary>
        void OnMatchReset(PlayerState owner, GameManager manager);

        /// <summary>造成伤害修正（白板 = 0）。</summary>
        int AttackBonus { get; }

        /// <summary>受到伤害修正（正数 = 减伤，负数 = 易伤）。</summary>
        int DefenseBonus { get; }
    }

    /// <summary>
    /// 攻击**不可被闪避**的角色实现本接口（咒术师）。
    ///
    /// 为什么要单独一个接口，而不是让游侠去判断 `is CurseweaverLogic`：
    /// 角色之间不应该互相认识。谁想让自己的攻击无法被闪避，谁就实现它。
    ///
    /// （曾经我按"伤害数值是 8"来判定 —— 那是错的：咒术师也能用 4/6 攻击，
    /// 而**其他角色打出 8 反而变得无法闪避**。判定必须看"攻击者是谁"，不是"打了多少"。）
    /// </summary>
    public interface IDodgeBlocker
    {
        /// <summary>true 表示本角色发起的攻击不可被闪避。</summary>
        bool PreventsDodge { get; }
    }

    /// <summary>
    /// 需要**接管"跳过回合"**的角色实现本接口（刺客的潜行）。
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
    /// 会"自己判断某手棋特别值钱"的角色实现本接口（狼人凑 7、剑客双持同值 ……）。
    ///
    /// 为什么反转依赖：AI 在 Assistant 层，按项目约定**不得引用 Characters**。
    /// 所以不是"AI 认识狼人"，而是"狼人告诉 AI 这一手值多少"。
    /// </summary>
    public interface IAiMoveAdvisor
    {
        /// <summary>给这一手追加的分值（0 表示没有特别价值）。</summary>
        int ScoreMoveBonus(MoveValueContext context);
    }

    /// <summary>
    /// 拥有"**我需要多回合才能达成的目标状态**"的角色实现本接口。
    ///
    /// 场景：刺客要"两只手都是 7"才能进潜行。这不是一手能成的 ——
    /// 第一手先把一只手做成 7，**下一手**再把另一只做成 7。
    ///
    /// ⚠️ 为什么需要这个接口：价值表的 `Combos` 只在**已经达成**时给分
    ///    （`DoublesOf` 要求 `actedAfter == otherHand`），
    ///    所以贪心评分**看不到"离目标还差一只手"的价值**。
    ///    实测（刺客诊断）：第一手走的是 `1+2=3`，从不保留 7，
    ///    30 局里潜行发动 0 次。
    ///
    /// 有了它，AI 就能"为两步计划铺路"，而不只是"看到成型的奖励"。
    /// </summary>
    public interface ITwoStepGoal
    {
        /// <summary>本回合这一手之后，我离目标状态还差多远（0 = 已达成）。</summary>
        int StepsToGoal(int actedValueAfter, int otherHandValue);

        /// <summary>
        /// 这个手值是否属于"达成目标所必需"（例如刺客的 7）。
        ///
        /// 用于：**别把计划要用的那只手拿去做运算** ——
        /// 拿它当运算源就会把它改掉，计划当场作废。
        /// </summary>
        bool IsGoalValue(int handValue);
    }

    /// <summary>
    /// 角色实现"**本回合我需要优先保住某只手**"（目标计划的铺路）。
    ///
    /// 场景：刺客已经有一只 7 时，这一手**别去动那只 7**，
    /// 而应该用另一只手朝 7 走。
    ///
    /// 这条信息已经由 <see cref="ITwoStepGoal.IsGoalValue"/> 提供，
    /// 这里不再重复定义接口（避免两个接口说同一件事）。
    /// </summary>

    /// <summary>
    /// 拥有"用某只手换一次完全闪避"能力的角色实现本接口（游侠的 1 变 2）。
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
        /// 咒术师的攻击也不可闪避。把这条判断也交给角色，
        /// 上层就不必认识任何具体角色类型。
        /// </summary>
        bool CanDodgeAgainst(PreTurnChoiceContext context);
    }

    /// <summary>
    /// 会在主攻击之前追加一次独立攻击的角色实现本接口（剑客）。
    /// 上层用它判断"这一击实际是几次攻击"。
    /// </summary>
    public interface IExtraAttackProvider
    {
        /// <summary>本次运算是否会在主攻击前追加一次攻击。</summary>
        bool WillExtraAttack(PreTurnChoiceContext context);
    }

    /// <summary>
    /// 攻击会拆成多段的角色实现本接口（忍者）。
    ///
    /// 上层需要**提前**知道段数，才能为每一段各问一次闪避
    /// （"要闪几段"在执行前必须确定，否则结算中途没法再问玩家）。
    /// </summary>
    public interface IMultiHitAttack
    {
        /// <summary>攻击段数（忍者 = 2）。</summary>
        int MaxHits { get; }
    }

    /// <summary>
    /// 角色声明"我现在能不能被选为攻击目标"。
    ///
    /// 由 <see cref="Assistant.BoardController"/> 查询 —— 它是 Assistant 层，
    /// **不能引用 Characters 层**（分层铁律），所以通过本接口反向调用。
    ///
    /// 目前的实现者：刺客（潜行中不能被攻击）。
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
    ///   · 白板与大多数角色是 **1**；**咒术师、牧师是无限**
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
    /// 角色实现"以代价换取**本次攻击距离无限**"的副作用（战士碎盾换射程）。
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
    /// 自己要接管"回复效果"结算的角色实现本接口。
    ///
    /// 牧师的 9 可以回给对手，而 <see cref="GameManager"/> 的默认实现只会回给行动方自己。
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
    /// 角色实现"**本次回复量由我说了算**"（巨人的 9：回复量 = 另一只手点数 × 2）。
    ///
    /// 默认回复量是固定的 9（<see cref="Hand.HealAmount"/>），
    /// 牧师按效果类型算（<c>PriestLogic.HealAmountFor</c>）。
    /// 巨人的回复量取决于**另一只手的当前值**，所以必须由角色自己算。
    ///
    /// 由 <see cref="GameManager"/> 在回复分支调用（与 <see cref="IHealOverride"/> 配合）。
    /// </summary>
    public interface IHealAmountProvider
    {
        /// <summary>本次回复的**总量**（已经包含所有段数）。</summary>
        int HealAmountOf(PlayerState owner, GameManager manager);
    }

    /// <summary>
    /// 角色实现"**改写本次运算产生的效果类型**"。
    ///
    /// 由 <see cref="GameManager"/> 在运算**之后、效果分派之前**调用
    /// （此时手值已经变成结果值，但护盾状态还没设）。
    ///
    /// 目前的实现者：**圣武士**（4 与 6 不攻击，改为合出护盾）。
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
    /// 角色实现"**取消某次攻击**"（邪术师）。
    ///
    /// 由 <see cref="AttackContext"/> 在**最开头**询问 —— 规则确认它是
    /// **最早结算**的，能取消掉咒术师那种"攻击方钩子改写伤害"的攻击。
    ///
    /// ⚠️ 取消者不是挨打的人，而是**场上的第三方**：所以判定要遍历全部参战玩家。
    /// </summary>
    public interface IAttackCanceller
    {
        /// <summary>
        /// 返回 true 表示"这次攻击被取消了"（调用方立即中止结算、不造成伤害）。
        ///
        /// <paramref name="attackValue"/> 是**攻击数字**：
        /// 剑客的追加攻击与圣武士的冲击视为 **0**（因此无法被取消）。
        /// </summary>
        bool TryCancelAttack(PlayerState canceller, PlayerState attacker,
                             PlayerState target, int attackValue, GameManager manager);
    }

    /// <summary>
    /// 角色实现"**改写本次攻击造成的伤害**"（魔射手：6 伤害 +2、8 无视护盾）。
    ///
    /// 由 <see cref="AttackContext"/> 在攻击方钩子阶段调用。
    /// </summary>
    public interface IAttackDamageRule
    {
        /// <summary>在攻击方钩子里改写伤害与护盾规则（直接改 <paramref name="context"/>）。</summary>
        void ApplyAttackRule(AttackContext context, int attackValue, GameManager manager);
    }

    /// <summary>
    /// 角色实现"**化解这次攻击**"（武僧的太极）。
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
        bool TryMitigate(PlayerState owner, AttackContext context, int incomingDamage,
                         out int reducedDamage, out int reflectDamage);
    }

    /// <summary>
    /// 角色实现"**我这张手在界面上该显示成什么效果**"。
    ///
    /// 用途：有些角色会**改写效果类型**，而界面若按通用的
    /// <see cref="Hand.GetEffectType"/> 去上色，就会与实际效果不符。
    ///
    /// 目前的实现者：**巨人** —— 它的 4/6/8 是无效果、**5 反而是攻击**，
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
    /// 场景：牧师在 2v2 里该给**自己这一队血量最低的存活者**回复
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
    ///   · **游侠**：手上有 2 时可以强化射击（基础值 ×1.5）
    ///   · **剑客**：双手相同时追加一次 4/6/8 伤害
    ///   · **刺客**：潜行中 +7
    ///   · **巨人**：打出 5 时是**两段**，每段伤害 = 另一只手的点数
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
        ///    **叠加攻防之前**。例如游侠的强化射击是"基础值 ×1.5"，
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

        /// <summary>自己的**另一只手**的点数（巨人 / 剑客要用）。</summary>
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

    /// <summary>
    /// 角色实现"我要接管某次攻击的结算"（把目标改成自己）。
    ///
    /// 由 <see cref="AttackContext"/> 在**判定闪避与护盾之前**询问 ——
    /// "改目标"必须发生在"谁来承受"这件事确定之前。
    ///
    /// 目前的实现者：**圣武士**（攻击范围内的角色被攻击时可以把目标改成自己）。
    /// </summary>
    public interface IAttackRedirector
    {
        /// <summary>
        /// 返回 true 表示"这次攻击改由我承受"（调用方负责替换挨打的人）。
        /// </summary>
        bool TryRedirectToSelf(PlayerState owner, PlayerState attacker,
                               PlayerState intendedTarget, GameManager manager);
    }
}
