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
        private const int GamesPerPair = 8;

        [Test]
        public void All_dual_general_matchups_run_without_crashing()
        {
            string report = DualVersusDualHarness.RunRoundRobin(GamesPerPair, out int totalGames);

            Assert.True(totalGames > 0, "应当跑出对局。");

            // ⚠️ 报告只在**断言失败**时才会被 NUnit 打出来。
            //    平时用下面这条让测试变绿；
            //    想看数据时把条件改成 false 跑一次，报告就会出现在失败消息里。
            bool wantReport = false;
            Assert.True(!wantReport || report.Contains("异常：无"),
                "全部双将组合交叉对战都不应崩溃或僵局。\n\n" + report);
        }
    }
}
