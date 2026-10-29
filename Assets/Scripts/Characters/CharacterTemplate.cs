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
    /// 角色行为模板：所有具体角色逻辑（SchoolOfConjuration / Breaker / PathOfTheWarden / LifeDomain / ...）的基类。
    /// 默认所有钩子为空实现，子类按需重写。
    /// </summary>
    public abstract class CharacterTemplate : ICharacterBehavior, IPreTurnChoiceProvider,
                                              IResultTargetProvider, ITargetHandChoiceProvider,
                                              IAiMoveAdvisor, IAttackRangeProvider,
                                              IRangeBreakEffect, IAttackTypeProvider
    {
        // ============================================================
        // ITargetHandChoiceProvider 的默认实现
        // ============================================================

        /// <summary>
        /// 默认没有"选目标手之前"的问题。牧师·生命领域的 4/6/8 攻击/回复二选一重写它。
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
        /// 所以不需要问；能改写回复目标的角色（牧师·生命领域，实现了
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
        // 只有真正需要抉择的（牧师·生命领域 / 游荡者·风流剑客 / 战士·剑士 / 野蛮人·守望者道途）才重写。
        // ============================================================

        /// <summary>默认不提出任何抉择。</summary>
        public virtual void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                  System.Collections.Generic.IList<ChoiceRequest> requests)
        { }

        /// <summary>
        /// 默认没有"依赖前一个答案"的后续问题。
        /// 分阶段抉择的角色（例如牧师·生命领域：选了回复才问给谁）自己重写。
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
        /// 额外生命上限加成（野蛮人·野兽道途 +5）。默认 0。
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

        /// <summary>某只手参与运算并结算完成后触发（"运算后"的角色机制，例如野蛮人·野兽道途）。</summary>
        public virtual void OnOperationResolved(PlayerState owner, Hand actedHand, int actedHandValueBefore,
                                                int targetValue, HandEffectType effect, GameManager manager,
                                                IPreTurnChoiceSink choiceSink)
        { }

        /// <summary>自身护盾被击碎时触发（战士·破阵者破阵强袭等在此实现）。</summary>
        public virtual void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager) { }

        /// <summary>承受攻击时触发。默认不闪避；游荡者·风流剑客等角色重写本方法。</summary>
        public virtual void TryDodge(PreTurnChoiceContext context, IPreTurnChoiceSink sink, int hitIndex) { }

        /// <summary>
        /// 整场比赛开始时触发。持有跨回合状态的子类**必须**重写本方法并清空状态，
        /// 否则重开一局会残留上一局的效果（例如野蛮人·守望者道途的锁手、野蛮人·野兽道途的成长加成）。
        /// </summary>
        public virtual void OnMatchReset(PlayerState owner, GameManager manager) { }

        /// <summary>
        /// 由数据层注入的数值。子类通过 <c>DefaultAttackBonus</c> / <c>DefaultDefenseBonus</c>
        /// 声明自身默认值；一旦 <see cref="ApplyStats"/> 被调用，注入值优先。
        /// </summary>
        public virtual int DefaultAttackBonus => 0;
        public virtual int DefaultDefenseBonus => 0;

        /// <summary>当前攻击修正（含永久成长）。**子类可重写**（见下）。</summary>
        public virtual int AttackBonus
            => AdjustAttackBonus(_statsApplied ? _attackBonus : DefaultAttackBonus);

        /// <summary>
        /// 角色可以**接管**攻击加成的计算（默认不介入）。
        ///
        /// 用途：圣武士·王冠之誓的"攻击加成恒等于攻击距离" —— 那个值随碎盾次数变化，
        /// 不是一个固定数字，所以要在每次读取时算。
        ///
        /// ⚠️ 与 <see cref="DefaultAttackBonus"/> 的区别：
        ///    那个只在 `ApplyStats` **之前**生效；一旦工厂注入了数据
        ///    （`_statsApplied = true`），注入值优先。
        ///    所以"动态加成"必须走这个钩子，否则会被注入值覆盖 ——
        ///    我第一版就是用 DefaultAttackBonus 做的，测试立刻抓到（恒为 0）。
        /// </summary>
        protected virtual int AdjustAttackBonus(int baseBonus) => baseBonus;

        /// <summary>当前防御修正（含永久成长）。</summary>
        public virtual int DefenseBonus => _statsApplied ? _defenseBonus : DefaultDefenseBonus;

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
        /// 永久提升攻防（野蛮人·野兽道途的成长机制用）。
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
        /// 持有永久成长的角色（野蛮人·野兽道途）**必须**在 <see cref="OnMatchReset"/> 里调用它，
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
        /// 默认 **1**（白板与大多数角色）。法师·咒法学派、牧师·生命领域重写为无限。
        /// </summary>
        public virtual int BaseAttackRange => 1;

        /// <summary>
        /// 自身护盾**被别人打碎**时触发（区别于 <see cref="OnOwnShieldBroken"/>
        /// 的"自己主动破盾"）。
        ///
        /// 目的是让"被动碎盾也要触发效果"的角色（圣武士·奉献之誓的冲击）有地方挂。
        /// </summary>
        public virtual void OnOtherShieldBroken(PlayerState owner, Hand brokenHand,
                                               GameManager manager) { }

        /// <summary>
        /// 默认没有"无畏冲锋"这类副作用。战士·破阵者重写它。
        /// </summary>
        public virtual void ApplyRangeBreak(PlayerState actor, Hand actedHand, GameManager manager) { }

        /// <summary>无限距离的表示（避免各处写 int.MaxValue 写错）。</summary>
        public const int InfiniteAttackRange = int.MaxValue;

        /// <summary>
        /// "这一手**此刻**触发了什么"的额外分值。
        ///
        /// 默认 0 —— 大多数角色的即时收益已经由 AI 的效果档位分数覆盖了
        /// （4/6/8 = 攻击、5 = 合盾、9 = 回复）。
        /// 野蛮人·野兽道途（凑 7）与战士·剑士（双持）会重写它。
        /// </summary>
        public virtual int ScoreMoveBonus(MoveValueContext context) => 0;

        // ============================================================
        // IAttackTypeProvider：**本次攻击的伤害类型**
        // ============================================================

        /// <summary>
        /// 本次攻击的伤害类型。
        ///
        /// ⚠️ 玩家要求"把所有攻击都标记上攻击类型"，并给了每个角色的规则。
        ///    默认给**挥砍**（刀剑类最普遍，也是玩家规则里出现最多的），
        ///    只有规则特殊的角色才覆写它 —— 这样 21 个角色里
        ///    需要写代码的只有十来个，其余自动正确。
        /// </summary>
        public virtual AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Slashing;

        /// <summary>
        /// **最近一次攻击的距离**（由 <see cref="GameManager"/> 在结算时设置）。
        ///
        /// ⚠️ 为什么要从外面设：`AttackContext` 刻意**不持有对局引用**
        ///    （它只是"这一击"的数据），而"距离 1 内 / 外"是玩家规则里
        ///    好几个角色的分型依据（牧师·生命领域 / 邪术师·至高妖精 / 法师·塑能学派）。
        ///    让 GameManager 在发起攻击时写一次距离，比给上下文塞
        ///    GameManager 依赖干净得多。
        /// </summary>
        public int LastAttackDistance { get; set; } = 1;

        /// <summary>按距离分型：距离 1 内用 <paramref name="near"/>，否则用 <paramref name="far"/>。</summary>
        protected AttackDamageType ByDistance(AttackDamageType near, AttackDamageType far)
            => LastAttackDistance <= 1 ? near : far;

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
