using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 双将交叉对战测试台。
    ///
    /// ⚠️ Tests 层<b>不能引用 UnityEngine</b>（分层铁律），所以报告不用 Debug.Log，
    ///    而是**刻意让断言失败一次**把报告打出来 —— 与 2v2 测试台同一做法。
    ///
    /// 想让报告出来时把 <see cref="GamesPerPair"/> 调大（轮数越多越可信）。
    /// </summary>
    [TestFixture]
    public class DualVersusDualTests
    {
        /// <summary>每个配对跑多少轮（每轮含交换座位，即 2 局）。</summary>
        private const int GamesPerPair = 12;

        [Test]
        public void All_dual_general_matchups_run_without_crashing()
        {
            string report = DualVersusDualHarness.RunRoundRobin(GamesPerPair, out int totalGames);

            Assert.True(totalGames > 0, "应当跑出对局。");

            // 报告写成文件 —— NUnit 只在断言失败时打消息，而且很长的消息会被截断，
            // 所以**不靠断言传数据**，直接落盘更好用。
            WriteReport(report);

            // 这里只断言"没有真异常"：模拟器被迫中断、或组合构建失败。
            // ⚠️ 打满回合上限**不算异常** —— 按血量判定，只有血量相同才平局（巨人系本来就拖）。
            Assert.True(report.Contains("异常：无"),
                "全部双将组合交叉对战都不应崩溃。\n\n" + report);
        }

        /// <summary>把报告写到 Tools 下（无头运行时的工作目录就是项目根）。</summary>
        private static void WriteReport(string report)
        {
            try
            {
                System.IO.File.WriteAllText("Tools/dual-report.txt", report);
            }
            catch (System.Exception)
            {
                // 写不出来也不该让测试失败
            }
        }

        [Test]
        public void Seed_21639_skipping_turns_still_advances_rounds()
        {
            var a = FindDual("咒术师", "巨人");
            var b = FindDual("野蛮人", "妖术师");

            for (int swap = 0; swap < 2; swap++)
            {
                var outcome = DualVersusDualHarness.Play(a, b, swap == 0, 21639);
                Assert.True(outcome.Error == null, "该种子不应抛异常：" + outcome.Error);
                Assert.False(outcome.HitLimit,
                    $"座位方向 {swap} 不应被 MaxTurns 强停；turns={outcome.Turns}, reached={outcome.ReachedLimit}");
            }
        }

        private static DualVersusDualHarness.Dual FindDual(string primary, string secondary)
        {
            var all = DualVersusDualHarness.AllDuals();
            for (int i = 0; i < all.Count; i++)
                if (all[i].Primary.DisplayName == primary
                    && all[i].Secondary.DisplayName == secondary)
                    return all[i];
            throw new AssertionException($"找不到双将 {primary}+{secondary}");
        }
    }
}
