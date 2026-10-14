using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 2v2 的循环赛统计（用重写后的 <see cref="TwoVersusTwoHarness"/>）。
    /// </summary>
    [TestFixture]
    public class TwoVersusTwoBalanceTests
    {
        /// <summary>自检：测试台必须给每个座位自己的 AI，且跑得起来。</summary>
        [Test]
        public void Harness_dispatches_every_seat_to_its_own_ai()
        {
            int errors = 0;

            const int games = 20;
            for (int g = 0; g < games; g++)
            {
                TwoVersusTwoHarness.Outcome r = TwoVersusTwoHarness.Play(
                    CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                    CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                    g, 400, out _);

                if (r.Error != null) errors++;
            }

            Assert.AreEqual(0, errors,
                "测试台出错（座位没有可用选择器 / 无合法步 / 抛异常）。");
        }

        /// <summary>
        /// **2v2 必须能分出胜负**（回归测试）。
        /// 钉住的是 GetLegalMoves 漏掉 IsAlive 那个真实 bug（详见该方法注释）。
        /// </summary>
        [Test]
        public void Two_versus_two_reaches_a_result_instead_of_stalling()
        {
            int decided = 0, drew = 0, errors = 0;

            const int games = 6;
            for (int g = 0; g < games; g++)
            {
                TwoVersusTwoHarness.Outcome r = TwoVersusTwoHarness.Play(
                    CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                    CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                    g, 400, out _);

                if (r.Error != null) errors++;
                else if (r.Seat14Won || r.Seat23Won) decided++;
                else drew++;
            }

            Assert.AreEqual(0, errors, "测试台出错。");
            Assert.True(decided > 0,
                $"四个士兵对打 {games} 局里一局都没分出胜负（平 {drew}）");
        }

    }
}
