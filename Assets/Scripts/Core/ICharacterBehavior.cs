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
}
