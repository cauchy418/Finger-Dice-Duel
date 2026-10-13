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

        // ============ 便捷查询 ============
        public bool HasAnyShield =>
            LeftHand.State == ShieldState.Formed || RightHand.State == ShieldState.Formed;

        public bool HasDoubleShield =>
            LeftHand.State == ShieldState.Formed && RightHand.State == ShieldState.Formed;

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
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            Health = Math.Min(MaximumHealth, Health + amount);
        }

        /// <summary>
        /// **精确设置**生命值（不夹到上限、也不做治疗/伤害的语义）。
        ///
        /// 用途：邪术师的"把场上一人的体力改成与自己一致" ——
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
