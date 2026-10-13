using System;

namespace FingerDiceDuel.Core
{
    [Serializable]
    public class Hand
    {
        /// <summary>
        /// 手值的合法下界 = **0**。
        ///
        /// ⚠️ 原本是 1（"十进制归一化后不会出现 0"）——
        ///    但**武僧的太极**明确要求"把那张 5 变成 **0**"（玩家规则），
        ///    所以下界必须放开。
        ///
        ///    放开之后的注意点：
        ///      · **开局掷骰的拆分仍然是 1..9**（见 <c>DiceRollSetup</c>）——
        ///        0 只会由太极产生，不是正常的初始值
        ///      · 0 + N 的运算照旧取个位，所以不影响加法规则
        /// </summary>
        public const int MinValue = 0;
        public const int MaxValue = 9;

        /// <summary>运算结果为 9 时的回血量。</summary>
        public const int HealAmount = 9;

        public int Value { get; private set; }

        /// <summary>护盾状态：None / Formed / Broken</summary>
        public ShieldState State { get; private set; }

        /// <summary>兼容旧代码：等价于 State == ShieldState.Formed</summary>
        public bool IsShield => State == ShieldState.Formed;

        /// <summary>
        /// 这只手是否处于**太极形态**（武僧）。
        ///
        /// ⚠️ 与 <see cref="IsShield"/> **互斥**：太极不是盾，
        ///    所以"能不能挡伤害 / 算不算双盾 / 能不能被破"这些判断
        ///    都必须只认 <see cref="IsShield"/>，不要写成"State != None"。
        /// </summary>
        public bool IsTaichi => State == ShieldState.Taichi;

        /// <summary>
        /// 是否被锁定。被锁定的手**不能作为运算源**（RULES.md 5.3），
        /// 但仍可被对手选为运算目标。
        /// </summary>
        public bool IsLocked { get; private set; }

        public Hand(int initialValue = 1)
        {
            Value = Clamp(initialValue);
            State = ShieldState.None;
            IsLocked = false;
        }

        private static int Clamp(int v) => v < MinValue ? MinValue : (v > MaxValue ? MaxValue : v);

        /// <summary>
        /// 十进制归一化：结果取个位数，且和为 10 时变成 1。
        /// 例：1+9=1、2+8=1、5+5=1、3+9=2、4+9=3、9+9=8。
        /// </summary>
        public static int ComputeSum(int a, int b)
        {
            int result = (a + b) % 10;
            if (result == 0) result = 1;
            return result;
        }

        public static HandEffectType GetEffectType(int value)
        {
            switch (value)
            {
                case 5: return HandEffectType.Shield;
                case 4: return HandEffectType.Attack4;
                case 6: return HandEffectType.Attack6;
                case 8: return HandEffectType.Attack8;
                case 9: return HandEffectType.Heal;
                default: return HandEffectType.None;
            }
        }

        public HandEffectType GetCurrentEffectType() => GetEffectType(Value);

        /// <summary>运算前破盾：本手若为护盾状态，立即破碎。</summary>
        public bool BreakShieldBeforeOperation()
        {
            if (State != ShieldState.Formed) return false;
            State = ShieldState.Broken;
            return true;
        }

        /// <summary>
        /// 参与运算并写回自身。若结果为 5，则进入护盾状态。
        ///
        /// 注意：<paramref name="otherHandValue"/> 是**对手**参与运算那只手的数值，
        /// 本方法**不会**修改对手的手。
        /// </summary>
        public HandEffectType PerformOperation(int otherHandValue)
        {
            int result = ComputeSum(Value, otherHandValue);
            Value = Clamp(result);

            HandEffectType effect = GetEffectType(Value);
            State = (effect == HandEffectType.Shield) ? ShieldState.Formed : ShieldState.None;
            return effect;
        }

        /// <summary>抵挡一次攻击。抵挡成功后护盾破碎。</summary>
        public bool TryBlockAttack()
        {
            if (State != ShieldState.Formed) return false;
            State = ShieldState.Broken;
            return true;
        }

        /// <summary>被外部（连锁破盾等）直接破除护盾。</summary>
        public bool ForceBreakShield()
        {
            if (State != ShieldState.Formed) return false;
            State = ShieldState.Broken;
            return true;
        }

        /// <summary>供 GameManager/AttackContext 显式修改护盾状态。</summary>
        public void SetShieldState(ShieldState state) => State = state;

        public void Reset(int initialValue = 1)
        {
            Value = Clamp(initialValue);
            State = ShieldState.None;
            IsLocked = false;
        }

        /// <summary>
        /// 绕过运算直接设置手值，并把护盾状态清为 <see cref="ShieldState.None"/>。
        ///
        /// 用途：测试夹具摆局面、存档读取、调试指令。
        /// 正常对局请不要用它改手值 —— 手值只能通过 <see cref="PerformOperation"/> 改变，
        /// 否则会破坏"只有行动方的手改变"的回合契约。
        /// </summary>
        public void SetValue(int value)
        {
            Value = Clamp(value);
            State = ShieldState.None;
        }

        public void Lock() => IsLocked = true;
        public void Unlock() => IsLocked = false;

        public override string ToString()
            => $"Hand(Value={Value}, State={State}, Locked={IsLocked})";
    }
}
