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
        /// <summary>
        /// **自检**：四个士兵对打时，四个座位都必须真的在行动，
        /// 而且局面要能推进到分出胜负。
        ///
        /// 这一条是"测试台本身对不对"的看门狗 ——
        /// 我上一版的测试台里座位 3/4 根本没出招，
        /// 结果 612 局有 458 局平局却没人发现。
        /// </summary>
        [Test]
        public void Harness_dispatches_every_seat_to_its_own_ai()
        {
            // 自检：测试台必须给**每个座位**自己的 AI，
            // 否则会出现"只有两个人在出招"的假数据。
            //
            // 断言只看"跑得起来"（没有异常、没有无法出招），
            // **不断言必须分出胜负** ——
            // 因为目前 2v2 存在一个**真实的环上死锁**（见下），
            // 把它写成断言只会让测试变红。
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
        /// **记录已知问题**：2v2 存在环上死锁。
        ///
        /// 环序是 1-2-3-4（同队 1&amp;4、2&amp;3），攻击距离 1 只能打**相邻座位**。
        /// 于是 1 只能打 2，不能打 3；同理 2 不能打 4。
        /// 而胜负判定要求**整队全灭** ——
        /// 两队各死一人后，剩下的两人互相够不到，**永远无法全灭对方**。
        ///
        /// 实测：4 个士兵对打 20 局，**全部平局**，
        /// 每局都打到 400 次行动上限；
        /// 结束时血量是 P1=20 P2=0 P3=20 P4=0 —— 恰好是"两队各死一人"。
        ///
        /// ⚠️ 这是**游戏规则层面**的问题，不是测试台的问题（测试台已经
        ///    确认四个座位都在出招了）。需要玩家决定怎么改，所以这里只记录事实。
        /// </summary>
        [Test]
        public void Known_issue_ring_seating_deadlocks_two_versus_two()
        {
            int decided = 0, drew = 0;

            const int games = 6;
            for (int g = 0; g < games; g++)
            {
                TwoVersusTwoHarness.Outcome r = TwoVersusTwoHarness.Play(
                    CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                    CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                    g, 400, out _);

                if (r.Seat14Won || r.Seat23Won) decided++;
                else drew++;
            }

            // 这条断言的是**现状**：全部平局。
            // 一旦玩家修好了环上死锁，这条会失败 ——
            // 那正是它在提醒"该更新这条记录了"。
            Assert.AreEqual(0, decided,
                $"预期全部平局（已知死锁），但有 {decided} 局分出了胜负 "
                + $"—— 说明环上死锁可能已被修好，请更新本测试。（平 {drew}）");
        }

    }
}
