namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 角色数值（攻防修正 + 可选最大生命）。
    /// 由 <see cref="CharacterDefinition"/> 构建，通过 <see cref="CharacterTemplate.ApplyStats"/>
    /// 注入角色实例，使数值可在数据层调整而不必改代码。
    /// </summary>
    public readonly struct CharacterStats
    {
        public int AttackBonus { get; }
        public int DefenseBonus { get; }

        /// <summary>为 null 表示不覆盖玩家的最大生命。</summary>
        public int? MaxHealth { get; }

        public CharacterStats(int attackBonus = 0, int defenseBonus = 0, int? maxHealth = null)
        {
            AttackBonus = attackBonus;
            DefenseBonus = defenseBonus;
            MaxHealth = maxHealth;
        }

        public override string ToString()
            => $"ATK={AttackBonus:+#;-#;0} DEF={DefenseBonus:+#;-#;0} HP={MaxHealth?.ToString() ?? "-"}";
    }

    /// <summary>
    /// 角色行为模板：所有具体角色逻辑（Curseweaver / Warrior / Barbarian / Priest / ...）的基类。
    /// 默认所有钩子为空实现，子类按需重写。
    /// </summary>
    public abstract class CharacterTemplate : ICharacterBehavior, IPreTurnChoiceProvider,
                                              IResultTargetProvider, ITargetHandChoiceProvider,
                                              IAiMoveAdvisor, IAttackRangeProvider,
                                              IRangeBreakEffect
    {
        // ============================================================
        // ITargetHandChoiceProvider 的默认实现
        // ============================================================

        /// <summary>
        /// 默认没有"选目标手之前"的问题。牧师的 4/6/8 攻击/回复二选一重写它。
        /// </summary>
        public virtual void CollectPreTargetChoices(PreTurnChoiceContext context,
                                                    System.Collections.Generic.IList<ChoiceRequest> requests)
        { }

        // ============================================================
        // IResultTargetProvider 的默认实现
        // ============================================================

        /// <summary>
        /// 默认规则：**只有会指向别人的效果**才需要指定目标 —— 即攻击（4/6/8）。
        ///
        /// 9 的回复**默认自动回给自己**（见 <see cref="GameManager"/> 的默认治疗），
        /// 所以不需要问；能改写回复目标的角色（牧师，实现了
        /// <see cref="IHealOverride"/>）自己重写成需要问。
        ///
        /// 无效果（1/2/3/7）与护盾（5，作用于自己）同样不需要 ——
        /// 否则 1 + 1 = 2 也会被追问"打谁"，纯属打扰。
        /// </summary>
        public virtual bool ResultNeedsTarget(PreTurnChoiceContext context)
        {
            if (context == null) return false;

            switch (context.Effect)
            {
                case HandEffectType.Attack4:
                case HandEffectType.Attack6:
                case HandEffectType.Attack8:
                    return true;

                // 9 默认回自己、5 作用于自己、None 无效果 —— 都不需要指定目标
                default:
                    return false;
            }
        }

        // ============================================================
        // IPreTurnChoiceProvider 的默认实现
        //
        // 放在基类是为了不必让每个角色都写一遍空方法 ——
        // 只有真正需要抉择的（牧师 / 游侠 / 剑客 / 野蛮人）才重写。
        // ============================================================

        /// <summary>默认不提出任何抉择。</summary>
        public virtual void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                  System.Collections.Generic.IList<ChoiceRequest> requests)
        { }

        /// <summary>
        /// 默认没有"依赖前一个答案"的后续问题。
        /// 分阶段抉择的角色（例如牧师：选了回复才问给谁）自己重写。
        /// </summary>
        public virtual void CollectFollowUpChoices(ChoiceRequest answered, Hand answer,
                                                   System.Collections.Generic.IList<ChoiceRequest> requests)
        { }

        private int _attackBonus;
        private int _defenseBonus;
        private int _baseAttackBonus;
        private int _baseDefenseBonus;
        private bool _statsApplied;

        /// <summary>角色标识，便于日志/UI显示，可由子类或数据层覆盖。</summary>
        public virtual string DisplayName => GetType().Name;

        /// <summary>
        /// 额外生命上限加成（狼人 +5）。默认 0。
        /// 由 <see cref="CharacterFactory"/> 在建角色时叠加到玩家最大生命上。
        /// </summary>
        public virtual int MaxHealthBonus => 0;

        /// <summary>攻击结算前介入，可修改 AttackContext（如无视护盾、追加伤害等）。</summary>
        public virtual void OnResolveAttack(AttackContext context) { }

        /// <summary>某只手合成护盾时触发。</summary>
        public virtual void OnShieldFormed(PlayerState owner, Hand shieldHand, GameManager manager) { }

        /// <summary>"运算已完成、主效果尚未结算"时触发。默认什么都不做。</summary>
        public virtual void OnBeforeEffectResolved(PlayerState owner, Hand actedHand,
                                                   int targetValue, HandEffectType effect,
                                                   GameManager manager, IPreTurnChoiceSink choiceSink)
        { }

        /// <summary>某只手参与运算并结算完成后触发（"运算后"的角色机制，例如狼人）。</summary>
        public virtual void OnOperationResolved(PlayerState owner, Hand actedHand, int actedHandValueBefore,
                                                int targetValue, HandEffectType effect, GameManager manager,
                                                IPreTurnChoiceSink choiceSink)
        { }

        /// <summary>自身护盾被击碎时触发（战士连锁破盾等在此实现）。</summary>
        public virtual void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager) { }

        /// <summary>承受攻击时触发。默认不闪避；游侠等角色重写本方法。</summary>
        public virtual void TryDodge(PreTurnChoiceContext context, IPreTurnChoiceSink sink, int hitIndex) { }

        /// <summary>
        /// 整场比赛开始时触发。持有跨回合状态的子类**必须**重写本方法并清空状态，
        /// 否则重开一局会残留上一局的效果（例如野蛮人的锁手、狼人的成长加成）。
        /// </summary>
        public virtual void OnMatchReset(PlayerState owner, GameManager manager) { }

        /// <summary>
        /// 由数据层注入的数值。子类通过 <c>DefaultAttackBonus</c> / <c>DefaultDefenseBonus</c>
        /// 声明自身默认值；一旦 <see cref="ApplyStats"/> 被调用，注入值优先。
        /// </summary>
        public virtual int DefaultAttackBonus => 0;
        public virtual int DefaultDefenseBonus => 0;

        /// <summary>当前攻击修正（含永久成长）。</summary>
        public int AttackBonus => _statsApplied ? _attackBonus : DefaultAttackBonus;

        /// <summary>当前防御修正（含永久成长）。</summary>
        public int DefenseBonus => _statsApplied ? _defenseBonus : DefaultDefenseBonus;

        /// <summary>
        /// 由 <see cref="CharacterFactory"/> 调用，用角色数据覆盖攻防修正。
        /// 也可直接调用以在运行时调整数值。
        /// </summary>
        public void ApplyStats(int attackBonus, int defenseBonus)
        {
            _attackBonus = attackBonus;
            _defenseBonus = defenseBonus;
            _baseAttackBonus = attackBonus;      // 记为基准，供 ResetStats 回到初始值
            _baseDefenseBonus = defenseBonus;
            _statsApplied = true;
        }

        public void ApplyStats(CharacterStats stats) => ApplyStats(stats.AttackBonus, stats.DefenseBonus);

        /// <summary>
        /// 永久提升攻防（狼人的成长机制用）。
        /// 会先确保当前数值已"落定"（未注入过数据时先取子类默认值），再累加。
        /// </summary>
        public void GrowStats(int attackDelta, int defenseDelta)
        {
            if (!_statsApplied)
            {
                _attackBonus = DefaultAttackBonus;
                _defenseBonus = DefaultDefenseBonus;
                _baseAttackBonus = DefaultAttackBonus;
                _baseDefenseBonus = DefaultDefenseBonus;
                _statsApplied = true;
            }

            _attackBonus += attackDelta;
            _defenseBonus += defenseDelta;
        }

        /// <summary>
        /// 把攻防恢复到"本局开始时的基准值"，丢弃所有成长累计。
        /// 持有永久成长的角色（狼人）**必须**在 <see cref="OnMatchReset"/> 里调用它，
        /// 否则重开一局会带着上一局的加成继续长。
        /// </summary>
        public void ResetStats()
        {
            if (!_statsApplied) return;

            _attackBonus = _baseAttackBonus;
            _defenseBonus = _baseDefenseBonus;
        }

        // ============================================================
        // IAiMoveAdvisor 的默认实现：用价值表评估"运算后留下的手值"
        // ============================================================

        /// <summary>
        /// 基础**攻击距离**（只影响 2v2 / 8 人局；1v1 两人相邻，距离恒为 1）。
        ///
        /// 默认 **1**（白板与大多数角色）。咒术师、牧师重写为无限。
        /// </summary>
        public virtual int BaseAttackRange => 1;

        /// <summary>
        /// 默认没有"碎盾换射程"这类副作用。战士重写它。
        /// </summary>
        public virtual void ApplyRangeBreak(PlayerState actor, Hand actedHand, GameManager manager) { }

        /// <summary>无限距离的表示（避免各处写 int.MaxValue 写错）。</summary>
        public const int InfiniteAttackRange = int.MaxValue;

        /// <summary>
        /// "这一手**此刻**触发了什么"的额外分值。
        ///
        /// 默认 0 —— 大多数角色的即时收益已经由 AI 的效果档位分数覆盖了
        /// （4/6/8 = 攻击、5 = 合盾、9 = 回复）。
        /// 狼人（凑 7）与剑客（双持）会重写它。
        /// </summary>
        public virtual int ScoreMoveBonus(MoveValueContext context) => 0;

        /// <summary>
        /// 本角色对应的**数据定义**（由 <see cref="CharacterFactory"/> 注入）。
        /// 价值表按 <see cref="CharacterDefinition.Id"/> 查，所以需要它。
        /// </summary>
        public CharacterDefinition Definition { get; private set; }

        /// <summary>注入角色数据（同时应用攻防修正）。</summary>
        public void ApplyDefinition(CharacterDefinition definition)
        {
            if (definition == null) return;

            Definition = definition;
            ApplyStats(definition.AttackBonus, definition.DefenseBonus);
        }
    }
}
