using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **战报里的算式必须自洽**：`A + B = C`，其中 C 必须等于 A 与 B 归一化后的和。
    ///
    /// 玩家实测（附日志）：
    ///   "战士 左手：1 + 4 = 2（无效果）"  —— 1 + 4 应当是 5，不是 2。
    /// </summary>
    [TestFixture]
    public class TurnLogArithmeticTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        [Test]
        public void Basic_operation_log_is_self_consistent()
        {
            PlayerState a = Make("战士", CharacterDefinition.Fighter);
            PlayerState b = Make("法师", CharacterDefinition.Wizard);

            var game = new GameManager(a, b, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            a.LeftHand.SetValue(1);
            b.LeftHand.SetValue(4);

            TurnResult t = game.ExecuteTurn(a, a.LeftHand, b.LeftHand);

            string line = BoardController.DescribeTurn(t);

            Assert.AreEqual(1, t.ActedHandValueBefore, "运算前的手值。");
            Assert.AreEqual(4, t.TargetHandValue, "被加数的手值。");
            Assert.AreEqual(5, t.ResultValue, "1 + 4 应当归一化成 5。");

            Assert.IsTrue(line.Contains("1 + 4 = 5"),
                "战报应当写 `1 + 4 = 5`，实际是：" + line);
        }

        [Test]
        public void Log_arithmetic_holds_for_many_combinations()
        {
            // 遍历一批组合，检查"算式自洽" —— 这是最容易在改动后悄悄坏掉的地方
            for (int left = 1; left <= 9; left++)
            {
                for (int right = 1; right <= 9; right++)
                {
                    PlayerState a = Make("甲", CharacterDefinition.Fighter);
                    PlayerState b = Make("乙", CharacterDefinition.Wizard);

                    var game = new GameManager(a, b, new SimpleChoiceProvider(),
                                               new SimpleChoiceProvider());

                    a.LeftHand.SetValue(left);
                    a.RightHand.SetValue(1);
                    b.LeftHand.SetValue(right);
                    b.RightHand.SetValue(1);

                    TurnResult t = game.ExecuteTurn(a, a.LeftHand, b.LeftHand);

                    int expected = (left + right) % 10;
                    if (expected == 0) expected = 1;      // 归一化：0 -> 1

                    Assert.AreEqual(expected, t.ResultValue,
                        $"{left} + {right} 应当归一化成 {expected}，"
                        + $"实际 {t.ResultValue}。");

                    string line = BoardController.DescribeTurn(t);
                    Assert.IsTrue(line.Contains($"{left} + {right} = {expected}"),
                        "战报算式与规则不一致：" + line);
                }
            }
        }
    }
}
