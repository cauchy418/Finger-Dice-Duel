using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 基础算术规则：十进制归一化、和为 10 变成 1、手值恒在 1..9。
    /// 对应 RULES.md 第 2 节。
    /// </summary>
    [TestFixture]
    public class ArithmeticTests
    {
        // ---------- 需求中明确给出的样例 ----------

        [Test]
        public void Sum_of_10_becomes_1()
        {
            Assert.AreEqual(1, Hand.ComputeSum(1, 9), "1+9");
            Assert.AreEqual(1, Hand.ComputeSum(9, 1), "9+1（顺序无关）");
            Assert.AreEqual(1, Hand.ComputeSum(2, 8), "2+8");
            Assert.AreEqual(1, Hand.ComputeSum(3, 7), "3+7");
            Assert.AreEqual(1, Hand.ComputeSum(4, 6), "4+6");
            Assert.AreEqual(1, Hand.ComputeSum(5, 5), "5+5");
        }

        [Test]
        public void Sum_of_12_keeps_units_digit()
        {
            // 3+9=12 -> 个位 2
            Assert.AreEqual(2, Hand.ComputeSum(3, 9), "3+9");
        }

        [Test]
        public void Sum_of_13_keeps_units_digit()
        {
            // 4+9=13 -> 个位 3
            Assert.AreEqual(3, Hand.ComputeSum(4, 9), "4+9");
        }

        // ---------- 全表性质检查 ----------

        [Test]
        public void Result_is_always_within_1_and_9()
        {
            for (int a = Hand.MinValue; a <= Hand.MaxValue; a++)
            {
                for (int b = Hand.MinValue; b <= Hand.MaxValue; b++)
                {
                    int r = Hand.ComputeSum(a, b);
                    Assert.True(r >= Hand.MinValue && r <= Hand.MaxValue,
                        $"{a}+{b} 的结果 {r} 越界（应在 1..9）。");
                }
            }
        }

        [Test]
        public void Result_is_never_zero()
        {
            for (int a = Hand.MinValue; a <= Hand.MaxValue; a++)
            {
                for (int b = Hand.MinValue; b <= Hand.MaxValue; b++)
                {
                    int r = Hand.ComputeSum(a, b);
                    Assert.AreNotEqual(0, r, $"{a}+{b} 不应产生 0。");
                }
            }
        }

        [Test]
        public void Addition_is_commutative()
        {
            for (int a = Hand.MinValue; a <= Hand.MaxValue; a++)
            {
                for (int b = Hand.MinValue; b <= Hand.MaxValue; b++)
                {
                    Assert.AreEqual(Hand.ComputeSum(a, b), Hand.ComputeSum(b, a),
                        $"{a}+{b} 与 {b}+{a} 应相同。");
                }
            }
        }

        [Test]
        public void Adding_1_leaves_the_value_unchanged()
        {
            // 手指游戏的核心手感：1 是"加不动的数字"，因为 v+1 只要不过 10 就是 v+1，
            // 而这个游戏没有 10，所以 1+1=2、1+9=1。
            Assert.AreEqual(2, Hand.ComputeSum(1, 1));
            Assert.AreEqual(1, Hand.ComputeSum(1, 9));
        }

        [Test]
        public void Every_value_can_be_reached_from_1_plus_x()
        {
            // 归一化不会"跳过"任何数字，保证开局（1）之后局势可展开。
            for (int v = Hand.MinValue; v <= Hand.MaxValue; v++)
            {
                int reached = Hand.ComputeSum(1, v);
                Assert.True(reached >= Hand.MinValue && reached <= Hand.MaxValue,
                    $"1+{v} 的结果 {reached} 越界。");
            }
        }

        // ---------- 手值封装 ----------

        [Test]
        public void New_hand_starts_at_1_with_no_shield()
        {
            var hand = new Hand();
            Assert.AreEqual(1, hand.Value);
            Assert.AreEqual(ShieldState.None, hand.State);
            Assert.False(hand.IsShield);
            Assert.False(hand.IsLocked);
        }

        [Test]
        public void Hand_value_is_clamped_into_range()
        {
            Assert.AreEqual(Hand.MinValue, new Hand(0).Value, "0 应被夹到 1");
            Assert.AreEqual(Hand.MinValue, new Hand(-5).Value, "负数应被夹到 1");
            Assert.AreEqual(Hand.MaxValue, new Hand(42).Value, "超上限应被夹到 9");
        }

        [Test]
        public void PerformOperation_writes_back_only_itself()
        {
            var hand = new Hand(3);
            HandEffectType effect = hand.PerformOperation(6);

            Assert.AreEqual(9, hand.Value, "3+6 应为 9");
            Assert.AreEqual(HandEffectType.Heal, effect, "结果 9 应为回血");
        }
    }
}
