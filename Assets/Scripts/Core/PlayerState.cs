using System;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 管理单个玩家的状态：生命值、双手（Hand）、绑定的角色行为。
    /// 是连接 Hand（数值运算层）与 GameManager（流程控制层）的核心容器。
    /// </summary>
    public partial class PlayerState
    {
        // ============ 常量 ============
        /// <summary>默认最大生命。实际数值由 <see cref="MaximumHealth"/> 决定，可按角色配置。</summary>
        public const int DefaultMaxHealth = 20;

        /// <summary>兼容旧代码：等价于 <see cref="DefaultMaxHealth"/>（即白板角色的最大生命）。</summary>
        public const int MaxHealth = DefaultMaxHealth;

        // ============ 基本属性 ============
        public string Name { get; private set; }

        /// <summary>当前生命值（对外命名保持 Health，与 GameManager / AttackContext 一致）</summary>
        public int Health { get; private set; }

        /// <summary>当前最大生命。默认 20，可由角色数据（CharacterDefinition.MaxHealth）覆盖。</summary>
        public int MaximumHealth { get; private set; }

        public bool IsAlive => Health > 0;
        public bool IsDead => Health <= 0;

        /// <summary>
        /// **游荡者·赌徒专用**：本回合是否**必须赌博**。
        ///
        /// 玩家确认的规则："若游荡者·赌徒上一个自己的回合进行了正常的运算（非赌博），
        /// 那么这一回合就必须赌博"；"赌完就解除，下一回合可以正常运算，
        /// 当然也可以继续赌，但下一回合正常运算后再下一个回合又得强制赌"。
        ///
        /// 由 <see cref="Assistant.BoardController"/> 在回合边界维护 ——
        /// 只有它知道"上一个自己的回合做了什么"。
        /// </summary>
        public bool MustGamble { get; set; }

        // ============ 计时状态（负面状态与角色状态） ============
        /// <summary>
        /// 身上的**计时状态**（负面状态、圣光与战恩）。
        ///
        /// 以 <see cref="StatusEffect"/> 为键，值 = 剩余"目标自己的回合数"
        /// （玩家确认的持续时间为 **2**）。
        ///
        /// ⚠️ 用**计数**而不是"记录到第几回合"：这样与座位、先手、
        ///    2v2 的轮转顺序都无关，也不会因为"命中发生在两种回合交替处"
        ///    而多算或少算一次。
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<StatusEffect, int> _statuses
            = new System.Collections.Generic.Dictionary<StatusEffect, int>();

        /// <summary>身上所有计时状态（按枚举顺序，便于界面稳定显示）。</summary>
        public System.Collections.Generic.IReadOnlyList<StatusEffect> Statuses
        {
            get
            {
                var list = new System.Collections.Generic.List<StatusEffect>();
                for (int i = 1; i <= MaxStatusValue; i++)
                {
                    var s = (StatusEffect)i;
                    if (HasStatus(s)) list.Add(s);
                }
                return list;
            }
        }

        /// <summary>枚举上限包含束缚、油腻；用于稳定顺序显示状态。</summary>
        private const int MaxStatusValue = (int)StatusEffect.Tinnitus;

        /// <summary>是否带有某个状态。</summary>
        public bool HasStatus(StatusEffect status)
            => status != StatusEffect.None && (_statuses.ContainsKey(status)||_enchantments.ContainsKey(status));

        /// <summary>剩余"目标自己的回合数"（没有该状态时为 0）。</summary>
        public int StatusTurnsLeft(StatusEffect status)
            => Math.Max(_statuses.TryGetValue(status,out int n)?n:0,_enchantments.TryGetValue(status,out int e)?e:0);

        /// <summary>
        /// 施加（或**刷新**）一个状态。
        ///
        /// 玩家确认：**同一种效果再次施加时只刷新持续时间，不叠加强度**。
        /// 所以这里直接覆盖计数，而不是累加。
        ///
        /// <paramref name="source"/> 记录**施加者** —— 只有"震慑"用得到
        /// （被震慑者不能攻击施加者），所以允许为 null。
        /// </summary>
        public void ApplyStatus(StatusEffect status, int turns = StatusDurationTurns,
                                PlayerState source = null)
        {
            if (status == StatusEffect.None || turns <= 0) return;

            // ⚠️ **圣光（牧师·光明领域）的两个特例**（玩家口径，2026-10-23）：
            //    ① 施加圣光时，先清除目标身上**其他全部状态**与**全部抗性**
            //       —— 勇气战歌增益与奥术屏障是独立系统，不受影响；
            //    ② 圣光存在期间，**阻止**其他任何状态被附加；
            //       再施加圣光本身 = 刷新时长，允许。
            //
            // 放在这个唯一入口：塑能 / 狂野魔法 / 技能流血 / 战斗招式等
            // 所有附加路径都经过这里，不需要逐个判断（与腐朽拦截回血同一思路）。
            if (status == StatusEffect.HolyLight)
            {
                ClearStatuses();
                ClearTemporaryDamageAffinities();
                foreach (var song in CharacterBehaviors.Implementers<IBladesong>(this)) song.EndSong();
            }
            else if (HasStatus(StatusEffect.HolyLight))
            {
                return;
            }

            _statuses[status] = turns;
            if (source != null) _statusSources[status] = source;
        }

        private readonly System.Collections.Generic.Dictionary<StatusEffect, PlayerState>
            _statusSources = new System.Collections.Generic.Dictionary<StatusEffect, PlayerState>();

        /// <summary>某个状态是谁挂上的（没有记录时返回 null）。</summary>
        public PlayerState StatusSourceOf(StatusEffect status)
            => _enchantmentSources.TryGetValue(status,out PlayerState e)?e:_statusSources.TryGetValue(status, out PlayerState p) ? p : null;

        /// <summary>移除一个状态。</summary>
        public void RemoveStatus(StatusEffect status)
        {
            if (status == StatusEffect.Restrained) ClearRestraintHands();
            if (status == StatusEffect.KnockedDown) _deferOilProneTick = false;
            _enchantments.Remove(status); _enchantmentSources.Remove(status);
            _statuses.Remove(status);
            _statusSources.Remove(status);
        }

        /// <summary>玩家确认的持续时间：**目标自己的 2 个回合**。</summary>
        public const int StatusDurationTurns = 2;

        /// <summary>
        /// 在**该玩家自己的回合结束**时递减所有状态计数，归零即移除。
        ///
        /// 返回**本次过期的状态**（供战报 / 测试）。
        /// </summary>
        public System.Collections.Generic.List<StatusEffect> TickStatuses()
        {
            var expired = new System.Collections.Generic.List<StatusEffect>();
            var keys = new System.Collections.Generic.List<StatusEffect>(_statuses.Keys);

            for (int i = 0; i < keys.Count; i++)
            {
                if (SkipFreshOilProneTick(keys[i])) continue;
                int left = _statuses[keys[i]] - 1;
                if (left <= 0)
                {
                    _statuses.Remove(keys[i]);_statusSources.Remove(keys[i]);
                    if(keys[i]==StatusEffect.Restrained)ClearRestraintHands();
                    if(keys[i]==StatusEffect.KnockedDown)_deferOilProneTick=false;
                    expired.Add(keys[i]);
                    if (keys[i] == StatusEffect.Frost)
                        ApplyStatus(StatusEffect.Wet, StatusDurationTurns);
                }
                else
                {
                    _statuses[keys[i]] = left;
                }
            }

            TickEnchantments(); TickPolymorph();
            return expired;
        }

        /// <summary>清空所有状态（开局复位用）。</summary>
        public void ClearStatuses()
        {
            EndPolymorph(); ClearEnchantments();
            ClearRestraintHands();
            _deferOilProneTick = false;
            _statuses.Clear();
            _statusSources.Clear();
            ClearVulnerabilities();
        }

        // ============ 双手 ============
        public Hand LeftHand { get; }
        public Hand RightHand { get; }

        // ============ 角色绑定 ============
        public ICharacterBehavior Character { get; private set; }

        /// <summary>
        /// 该玩家所用的**角色数据**（数值 + 显示名 + <see cref="CharacterDefinition.Id"/>）。
        ///
        /// 为什么要有它：<see cref="Character"/> 是**行为**，只暴露能力接口
        /// （`IDodgeCapability` 之类），拿不到 Id —— 而开局选点策略需要按
        /// Id 查价值表（见 Assistant.HandValueProfiles）。
        /// 由 <see cref="CharacterFactory"/> 创建/配置玩家时一并写入。
        /// </summary>
        public CharacterDefinition Definition { get; private set; }

        /// <summary>
        /// UI 显示名覆盖（双将用："主将 + 副将"）。
        /// 战报仍只显示主将；这里只影响棋盘面板 / 标题。
        /// </summary>
        public string DisplayNameOverride { get; set; }

        /// <summary>身份局附加攻击 / 防御（主公、亮身份内奸）。</summary>
        public int IdentityAttackBonus { get; private set; }
        public int IdentityDefenseBonus { get; private set; }

        /// <summary>角色按血量 / 场上差值实时计算出的攻防。</summary>
        public int DynamicAttackBonus { get; private set; }
        public int DynamicDefenseBonus { get; private set; }

        /// <summary>身份局主公的明主/霸王/暴君动态攻防（不覆盖角色自身动态攻防）。</summary>
        public int LordAttackBonus { get; private set; }
        public int LordDefenseBonus { get; private set; }

        private bool _physicalResistance;
        private readonly System.Collections.Generic.HashSet<AttackDamageType> _elementResistances
            = new System.Collections.Generic.HashSet<AttackDamageType>();
        private readonly System.Collections.Generic.HashSet<AttackDamageType> _permanentResistances
            = new System.Collections.Generic.HashSet<AttackDamageType>();

        public bool HasPhysicalResistance => _physicalResistance;

        /// <summary>武僧·散打宗的共鸣之气标记。</summary>
        public bool HasResonance { get; private set; }
        public void SetResonance(bool value) => HasResonance = value;

        /// <summary>邪术师·妖魔的虚假生命，先于真实生命扣除。</summary>
        public int FalseLife { get; private set; }
        public void GainFalseLife(int amount)
        {
            if (amount <= 0) return;
            FalseLife = System.Math.Min(10, FalseLife + amount);
        }
        public void ClearFalseLife() => FalseLife = 0;
        public bool HasResistance(AttackDamageType type)
        {
            if (type == AttackDamageType.Bludgeoning
                || type == AttackDamageType.Piercing
                || type == AttackDamageType.Slashing)
                return _physicalResistance || _permanentResistances.Contains(AttackDamageType.Bludgeoning);
            return _elementResistances.Contains(type) || _permanentResistances.Contains(type);
        }

        public void AddResistance(AttackDamageType type)
        {
            // **圣光存在期间抗性不可附加**（与状态附加同一套拦截口径）。
            if (HasStatus(StatusEffect.HolyLight)) return;

            if (type == AttackDamageType.Bludgeoning
                || type == AttackDamageType.Piercing
                || type == AttackDamageType.Slashing)
                _physicalResistance = true;
            else if (type != AttackDamageType.None)
                _elementResistances.Add(type);
        }

        public bool HasPermanentResistance(AttackDamageType type)=>_permanentResistances.Contains(type)
            ||(type==AttackDamageType.Slashing||type==AttackDamageType.Piercing)&&_permanentResistances.Contains(AttackDamageType.Bludgeoning);

        public void AddPermanentResistance(AttackDamageType type)
        {
            if (type != AttackDamageType.None) _permanentResistances.Add(type);
        }

        /// <summary>临时抗性在受到实际伤害后清除；永久抗性保留整场。</summary>
        public void ClearResistances()
        {
            _physicalResistance = false;
            _elementResistances.Clear();
        }

        public void ClearAllResistances()
        {
            ClearResistances();
            _permanentResistances.Clear();
        }

        public void SetLordBonuses(int attack, int defense)
        {
            LordAttackBonus = attack;
            LordDefenseBonus = defense;
        }

        public void SetDynamicBonuses(int attack, int defense)
        {
            DynamicAttackBonus = attack;
            DynamicDefenseBonus = defense;
        }

        public void AddIdentityBonus(int attack, int defense)
        {
            IdentityAttackBonus += attack;
            IdentityDefenseBonus += defense;
        }

        // ============ 便捷查询 ============
        public bool HasAnyShield =>
            LeftHand.State == ShieldState.Formed || RightHand.State == ShieldState.Formed;

        public bool HasDoubleShield =>
            LeftHand.State == ShieldState.Formed && RightHand.State == ShieldState.Formed;

        /// <summary>
        /// **两只手都不能作为运算源**。
        ///
        /// 两种情况：
        ///   ① 两只手**都被锁定**（野蛮人·守望者道途锁手）；
        ///   ② **一只手被暗置**（游侠·幽域追踪者的伏击），另一只手又被锁
        ///      —— 这正是玩家早就指出的场景：
        ///      "若游侠·幽域追踪者处于潜伏状态，剩余的那只手被野蛮人·守望者道途控制住也要给出跳过回合按钮"。
        ///
        /// ⚠️ 判据必须与 `GetLegalMoves` 的过滤口径**一致**（那里用的是
        ///    `Hand.CanBeOperated` = 未锁 **且** 未暗置）。
        ///    我第一版这里只查 `IsLocked`，于是"一暗置 + 一被锁"时：
        ///    **合法步为 0，但这个属性说 false -> 跳过按钮不出现 -> 对局卡死**
        ///    （全角色循环赛实测 5 处卡死，双将与 2v2 也被连带）。
        /// </summary>
        public bool HasNoUsableHand =>
            !LeftHand.CanBeOperated && !RightHand.CanBeOperated;

        // ============ 构造 ============
        public PlayerState(string name = "Player", ICharacterBehavior character = null,
                           int maxHealth = DefaultMaxHealth)
        {
            Name = name;
            Character = character;
            MaximumHealth = SanitizeMaxHealth(maxHealth);
            Health = MaximumHealth;
            LeftHand = new Hand(1);
            RightHand = new Hand(1);
        }

        // ============ 角色管理 ============
        public void BindCharacter(ICharacterBehavior character) => Character = character;

        /// <summary>绑定角色数据（由 <see cref="CharacterFactory"/> 调用）。</summary>
        public void BindDefinition(CharacterDefinition definition) => Definition = definition;

        /// <summary>设置显示名（角色选择界面确认后使用）。</summary>
        public void Rename(string name)
        {
            if (!string.IsNullOrEmpty(name)) Name = name;
        }

        // ============ 最大生命 ============
        /// <summary>
        /// 设置最大生命。会同时把当前生命夹到 [0, 新上限]。
        /// 一般只在开局/换角色时调用，比赛进行中不要调用。
        /// </summary>
        public void SetMaxHealth(int value)
        {
            if(IsPolymorphed){_untransformedMaximum=SanitizeMaxHealth(value);_untransformedHealth=Math.Min(_untransformedHealth,_untransformedMaximum);return;}
            MaximumHealth = SanitizeMaxHealth(value);
            if (Health > MaximumHealth) Health = MaximumHealth;
        }

        private static int SanitizeMaxHealth(int value)
            => value < 1 ? DefaultMaxHealth : value;

        // ============ 生命值操作 ============
        public void TakeDamage(int amount)
        {
            TakeDamageResolved(amount);
        }

        /// <summary>
        /// 回复生命值。
        ///
        /// ⚠️ **腐朽**（法师·塑能学派的暗蚀状态）在这里拦截：带着腐朽的人**完全不能回血**。
        ///
        /// 为什么放在这个最底层的方法里、而不是逐个回复技能去判断：
        /// 回血的入口不止一处（牧师·生命领域、野蛮人·巨人道途、野蛮人·野兽道途的成长回血…），
        /// 逐个判断必然会漏；而这里是**唯一**真正改生命值上行的出口。
        /// （<see cref="SetHealth"/> 是"赋值"语义 —— 邪术师·至高妖精那种不算治疗，不受腐朽影响。）
        /// </summary>
        public void Heal(int amount)
        {
            if (amount <= 0) return;
            if (!StatusRules.CanHeal(this)) return;      // 腐朽／耳鸣：禁止回血

            // **圣光**：回复量减半（向上取整 —— 与抗性减半同一取整口径）。
            // 放在最底层的原因与腐朽一致：回血入口不止一处，逐个判断必漏。
            if (HasStatus(StatusEffect.HolyLight)) amount = (amount + 1) / 2;

            int before = Health;
            int allowed = MaximumHealth - before;
            int actual = Math.Min(Math.Max(0, amount), allowed);
            Health = Math.Min(MaximumHealth, Health + actual);
            int overflow = Math.Max(0, amount - actual);
            if (overflow > 0)
                foreach(var overflowHook in CharacterBehaviors.Implementers<IHealingOverflow>(this))
                    overflowHook.OnHealingOverflow(this, overflow);

            // ⚠️ 吟游诗人·勇气学院的触发条件是"**实际**回过体力" —— 满血回复不算，
            //    所以这里按**真实增量**判断，而不是看有没有调用过 Heal。
            if (Health > before) HealedThisTurn = true;
        }

        // ============ 吟游诗人·勇气学院：本回合的"是否受过伤 / 回过血" ============

        /// <summary>
        /// 本回合是否**受过伤害**（吟游诗人·勇气学院的触发条件之一）。
        /// 每次行动开始时由 `GameManager.BeginTurn` 复位。
        /// </summary>
        public bool TookDamageThisTurn { get; set; }

        /// <summary>
        /// 本回合是否**实际回复过体力**（满血回复不算 —— 见 <see cref="Heal"/>）。
        /// 每次行动开始时由 `GameManager.BeginTurn` 复位。
        /// </summary>
        public bool HealedThisTurn { get; set; }

        /// <summary>
        /// **吟游诗人·勇气学院给的攻防加成**（正数 = 增幅、负数 = 削弱）。
        ///
        /// 与"角色自身的攻防加成"相加（见 `StatusRules.EffectiveAttackBonus`），
        /// 所以和强酸的 −2 防御是同一层，会一起被算进去。
        /// </summary>
        public int CollegeOfValorAttackOffset { get; private set; }
        public int CollegeOfValorDefenseOffset { get; private set; }

        /// <summary>这些加成是哪位吟游诗人·勇气学院给的（用于"下次发动时清除旧的"与"死亡清除"）。</summary>
        public PlayerState CollegeOfValorOffsetSource { get; private set; }

        /// <summary>设置/覆盖吟游诗人·勇气学院的加成（<paramref name="source"/> 为 null 时等于清除）。</summary>
        public void SetCollegeOfValorOffset(PlayerState source, int attackOffset, int defenseOffset)
        {
            CollegeOfValorOffsetSource = source;
            CollegeOfValorAttackOffset = attackOffset;
            CollegeOfValorDefenseOffset = defenseOffset;
        }

        /// <summary>清除由某位吟游诗人·勇气学院给的加成（他再次发动 / 他死亡时调用）。</summary>
        public void ClearCollegeOfValorOffsetFrom(PlayerState source)
        {
            if (source == null) return;
            if (!ReferenceEquals(CollegeOfValorOffsetSource, source)) return;
            SetCollegeOfValorOffset(null, 0, 0);
        }

        /// <summary>
        /// 本回合**是否已经发动过**吟游诗人·勇气学院的技能。
        ///
        /// ⚠️ 没有这个标记时：确认之后 `TookDamageThisTurn` 仍是 true
        ///    （要到下一次行动才复位），于是界面刷新时又满足"可以发动"
        ///    -> **面板重新弹出、反复询问**（玩家实测）。
        /// 一次行动只能发动一次，所以答完（含"不发动"）就置位。
        /// </summary>
        public bool CollegeOfValorActivatedThisTurn { get; set; }

        /// <summary>复位"本回合"标记（每次行动开始时调用）。</summary>
        public void ResetTurnFlags()
        {
            BeganAttackThisTurn=false; FormedShieldThisTurn=false;
            TookDamageThisTurn = false;
            HealedThisTurn = false;
            CollegeOfValorActivatedThisTurn = false;
        }

        /// <summary>
        /// **精确设置**生命值（不夹到上限、也不做治疗/伤害的语义）。
        ///
        /// 用途：邪术师·至高妖精的"把场上一人的体力改成与自己一致" ——
        /// 那是**赋值**而不是治疗，用 <see cref="Heal"/> 会被上限截断，
        /// 用 <see cref="TakeDamage"/> 又只能减。
        ///
        /// 允许超过上限（规则没有禁止，且"改成一致"就该是一致）。
        /// </summary>
        public void SetHealthExact(int value)
        {
            Health = Math.Max(0, value);
        }

        // ============ 手部访问 ============
        public Hand GetHand(int index)
        {
            if (index == 0) return LeftHand;
            if (index == 1) return RightHand;
            throw new ArgumentOutOfRangeException(
                nameof(index), "手的索引只能是 0（左）或 1（右）。");
        }

        public Hand GetOtherHand(Hand hand)
        {
            if (hand == LeftHand) return RightHand;
            if (hand == RightHand) return LeftHand;
            throw new ArgumentException("传入的 Hand 不属于该玩家。", nameof(hand));
        }

        /// <summary>该 Hand 是否属于本玩家。</summary>
        public bool Owns(Hand hand) => ReferenceEquals(hand, LeftHand) || ReferenceEquals(hand, RightHand);

        /// <summary>枚举本玩家的两只手（左手在前）。</summary>
        public Hand[] Hands => new[] { LeftHand, RightHand };

        // ============ 重置 ============
        /// <summary>
        /// 恢复开局状态：生命、双手数值与护盾状态。
        /// 注意：本方法**不**清理角色内部状态，请通过 <see cref="GameManager.ResetMatch"/> 调用，
        /// 由它负责触发 <see cref="ICharacterBehavior.OnMatchReset"/>。
        /// </summary>
        public void Reset()
        {
            RestoreDeadSeat(); EndPolymorph(); ClearEnchantments();
            Health = MaximumHealth;
            LeftHand.Reset(1);
            RightHand.Reset(1);
            ClearAllResistances();
            ClearVulnerabilities();
            ClearImmunities(); _permanentImmunities.Clear();
            SetResonance(false);
            ClearFalseLife();
        }

        public override string ToString() => $"{Name}(HP={Health}/{MaximumHealth})";
    }
}
