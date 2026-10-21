using System;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 管理单个玩家的状态：生命值、双手（Hand）、绑定的角色行为。
    /// 是连接 Hand（数值运算层）与 GameManager（流程控制层）的核心容器。
    /// </summary>
    public class PlayerState
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
        /// **赌徒专用**：本回合是否**必须赌博**。
        ///
        /// 玩家确认的规则："若赌徒上一个自己的回合进行了正常的运算（非赌博），
        /// 那么这一回合就必须赌博"；"赌完就解除，下一回合可以正常运算，
        /// 当然也可以继续赌，但下一回合正常运算后再下一个回合又得强制赌"。
        ///
        /// 由 <see cref="Assistant.BoardController"/> 在回合边界维护 ——
        /// 只有它知道"上一个自己的回合做了什么"。
        /// </summary>
        public bool MustGamble { get; set; }

        // ============ 法师的负面状态 ============
        /// <summary>
        /// 身上的**负面状态**（法师体系）。
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

        /// <summary>身上所有负面状态（按枚举顺序，便于界面稳定显示）。</summary>
        public System.Collections.Generic.IReadOnlyList<StatusEffect> Statuses
        {
            get
            {
                var list = new System.Collections.Generic.List<StatusEffect>();
                for (int i = 1; i <= MaxStatusValue; i++)
                {
                    var s = (StatusEffect)i;
                    if (_statuses.ContainsKey(s)) list.Add(s);
                }
                return list;
            }
        }

        private const int MaxStatusValue = 18;

        /// <summary>是否带有某个状态。</summary>
        public bool HasStatus(StatusEffect status)
            => status != StatusEffect.None && _statuses.ContainsKey(status);

        /// <summary>剩余"目标自己的回合数"（没有该状态时为 0）。</summary>
        public int StatusTurnsLeft(StatusEffect status)
            => _statuses.TryGetValue(status, out int n) ? n : 0;

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
            _statuses[status] = turns;
            if (source != null) _statusSources[status] = source;
        }

        private readonly System.Collections.Generic.Dictionary<StatusEffect, PlayerState>
            _statusSources = new System.Collections.Generic.Dictionary<StatusEffect, PlayerState>();

        /// <summary>某个状态是谁挂上的（没有记录时返回 null）。</summary>
        public PlayerState StatusSourceOf(StatusEffect status)
            => _statusSources.TryGetValue(status, out PlayerState p) ? p : null;

        /// <summary>移除一个状态。</summary>
        public void RemoveStatus(StatusEffect status)
        {
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
                int left = _statuses[keys[i]] - 1;
                if (left <= 0)
                {
                    _statuses.Remove(keys[i]);
                    _statusSources.Remove(keys[i]);
                    expired.Add(keys[i]);
                    if (keys[i] == StatusEffect.Frost)
                        ApplyStatus(StatusEffect.Wet, StatusDurationTurns);
                }
                else
                {
                    _statuses[keys[i]] = left;
                }
            }

            return expired;
        }

        /// <summary>清空所有状态（开局复位用）。</summary>
        public void ClearStatuses()
        {
            _statuses.Clear();
            _statusSources.Clear();
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
        ///   ① 两只手**都被锁定**（野蛮人锁手）；
        ///   ② **一只手被暗置**（猎人的伏击），另一只手又被锁
        ///      —— 这正是玩家早就指出的场景：
        ///      "若猎人处于潜伏状态，剩余的那只手被野蛮人控制住也要给出跳过回合按钮"。
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
            MaximumHealth = SanitizeMaxHealth(value);
            if (Health > MaximumHealth) Health = MaximumHealth;
        }

        private static int SanitizeMaxHealth(int value)
            => value < 1 ? DefaultMaxHealth : value;

        // ============ 生命值操作 ============
        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            Health = Math.Max(0, Health - amount);
            TookDamageThisTurn = true;
        }

        /// <summary>
        /// 回复生命值。
        ///
        /// ⚠️ **腐朽**（法师的暗蚀状态）在这里拦截：带着腐朽的人**完全不能回血**。
        ///
        /// 为什么放在这个最底层的方法里、而不是逐个回复技能去判断：
        /// 回血的入口不止一处（牧师、巨人、狼人的成长回血…），
        /// 逐个判断必然会漏；而这里是**唯一**真正改生命值上行的出口。
        /// （<see cref="SetHealth"/> 是"赋值"语义 —— 妖术师那种不算治疗，不受腐朽影响。）
        /// </summary>
        public void Heal(int amount)
        {
            if (amount <= 0) return;
            if (HasStatus(StatusEffect.Decay)) return;      // 腐朽：禁止回血

            int before = Health;
            Health = Math.Min(MaximumHealth, Health + amount);

            // ⚠️ 吟游诗人的触发条件是"**实际**回过体力" —— 满血回复不算，
            //    所以这里按**真实增量**判断，而不是看有没有调用过 Heal。
            if (Health > before) HealedThisTurn = true;
        }

        // ============ 吟游诗人：本回合的"是否受过伤 / 回过血" ============

        /// <summary>
        /// 本回合是否**受过伤害**（吟游诗人的触发条件之一）。
        /// 每次行动开始时由 `GameManager.BeginTurn` 复位。
        /// </summary>
        public bool TookDamageThisTurn { get; set; }

        /// <summary>
        /// 本回合是否**实际回复过体力**（满血回复不算 —— 见 <see cref="Heal"/>）。
        /// 每次行动开始时由 `GameManager.BeginTurn` 复位。
        /// </summary>
        public bool HealedThisTurn { get; set; }

        /// <summary>
        /// **吟游诗人给的攻防加成**（正数 = 增幅、负数 = 削弱）。
        ///
        /// 与"角色自身的攻防加成"相加（见 `StatusRules.EffectiveAttackBonus`），
        /// 所以和强酸的 −2 防御是同一层，会一起被算进去。
        /// </summary>
        public int BardAttackOffset { get; private set; }
        public int BardDefenseOffset { get; private set; }

        /// <summary>这些加成是哪位吟游诗人给的（用于"下次发动时清除旧的"与"死亡清除"）。</summary>
        public PlayerState BardOffsetSource { get; private set; }

        /// <summary>设置/覆盖吟游诗人的加成（<paramref name="source"/> 为 null 时等于清除）。</summary>
        public void SetBardOffset(PlayerState source, int attackOffset, int defenseOffset)
        {
            BardOffsetSource = source;
            BardAttackOffset = attackOffset;
            BardDefenseOffset = defenseOffset;
        }

        /// <summary>清除由某位吟游诗人给的加成（他再次发动 / 他死亡时调用）。</summary>
        public void ClearBardOffsetFrom(PlayerState source)
        {
            if (source == null) return;
            if (!ReferenceEquals(BardOffsetSource, source)) return;
            SetBardOffset(null, 0, 0);
        }

        /// <summary>
        /// 本回合**是否已经发动过**吟游诗人的技能。
        ///
        /// ⚠️ 没有这个标记时：确认之后 `TookDamageThisTurn` 仍是 true
        ///    （要到下一次行动才复位），于是界面刷新时又满足"可以发动"
        ///    -> **面板重新弹出、反复询问**（玩家实测）。
        /// 一次行动只能发动一次，所以答完（含"不发动"）就置位。
        /// </summary>
        public bool BardActivatedThisTurn { get; set; }

        /// <summary>复位"本回合"标记（每次行动开始时调用）。</summary>
        public void ResetTurnFlags()
        {
            TookDamageThisTurn = false;
            HealedThisTurn = false;
            BardActivatedThisTurn = false;
        }

        /// <summary>
        /// **精确设置**生命值（不夹到上限、也不做治疗/伤害的语义）。
        ///
        /// 用途：妖术师的"把场上一人的体力改成与自己一致" ——
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
            Health = MaximumHealth;
            LeftHand.Reset(1);
            RightHand.Reset(1);
        }

        public override string ToString() => $"{Name}(HP={Health}/{MaximumHealth})";
    }
}
