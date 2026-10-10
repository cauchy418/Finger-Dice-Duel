using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 测试夹具辅助：把对象摆成想要的局面。
    ///
    /// 为什么需要它：规则限定"每回合只有行动方的一只手能改变"，因此想用正常回合
    /// 把某只手凑到指定数值，需要一长串回合，测试会变得很难读、也很难维护。
    /// 这里直接用 <see cref="Hand.PerformOperation"/> 在**测试方法自己新建的对象**上
    /// 摆好初始局面，然后再让被测逻辑去跑。
    ///
    /// 注意：只能在 <see cref="GameManager"/> 开始跑之前使用；游戏进行中摆数值
    /// 会破坏"只有行动方的手改变"这个契约，那样的测试没有意义。
    /// </summary>
    internal static class TestSetup
    {
        /// <summary>把一只手摆成 1..9 的指定数值（护盾状态清为 None）。</summary>
        public static void SetHand(Hand hand, int targetValue)
        {
            if (targetValue < Hand.MinValue || targetValue > Hand.MaxValue)
                throw new System.ArgumentOutOfRangeException(
                    nameof(targetValue), $"手值必须在 {Hand.MinValue}..{Hand.MaxValue}。");

            hand.SetValue(targetValue);

            if (hand.Value != targetValue)
                throw new System.InvalidOperationException(
                    $"TestSetup.SetHand 内部错误：目标 {targetValue}，实际 {hand.Value}。");
        }

        /// <summary>把双方两只手都摆成指定数值。</summary>
        public static void SetBothHands(PlayerState player, int value)
        {
            SetHand(player.LeftHand, value);
            SetHand(player.RightHand, value);
        }

        /// <summary>
        /// 把双方的 4 只手全部摆成指定数值（默认 1，即原始开局状态）。
        /// 需要"完全确定"的局面时先调用它，可以避免依赖前面步骤留下的数值。
        /// </summary>
        public static void SetAllHands(PlayerState a, PlayerState b, int value = 1)
        {
            SetBothHands(a, value);
            SetBothHands(b, value);
        }

        /// <summary>
        /// 断言一只手当前就是指定数值，不是就立刻报错。
        ///
        /// 用途：把"这个测试依赖某只手恰好是几"这件事写成显式检查。
        /// 这样一旦推理有误，失败点会落在真正的错误位置，
        /// 而不是在后面某个断言上以一种难以理解的方式爆掉。
        /// </summary>
        public static void RequireHand(Hand hand, int expected, string what)
        {
            if (hand.Value != expected)
                throw new System.InvalidOperationException(
                    $"测试前置条件不成立：{what} 应为 {expected}，实际 {hand.Value}。");
        }

        /// <summary>把生命调整到指定值（只减不增）。</summary>
        public static void SetHealth(PlayerState player, int health)
        {
            int delta = player.Health - health;
            if (delta > 0) player.TakeDamage(delta);
        }
    }
}
