using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class AdventureGuideHistoryTests
    {
        [Test]
        public void Navigation_only_visits_unlocked_pages_and_returns_to_current()
        {
            var history = new AdventureGuideHistory();
            history.Update("1", "第一步", "合盾");
            Assert.IsFalse(history.CanPrevious || history.CanNext);
            history.Update("2", "第二步", "碎盾");
            Assert.AreEqual(1, history.Index);
            history.Previous(); Assert.AreEqual("合盾", history.Current.Text);
            Assert.IsFalse(history.CanPrevious); Assert.IsTrue(history.CanNext);
            history.Next(); Assert.AreEqual("碎盾", history.Current.Text);
            history.Next(); Assert.AreEqual(1, history.Index);
            history.Previous(); history.Latest(); Assert.AreEqual(1, history.Index);
        }
        [Test]
        public void Refresh_and_new_step_preserve_manual_history_reading()
        {
            var history = new AdventureGuideHistory();
            history.Update("1", "第一步", "甲"); history.Update("2", "第二步", "乙");
            history.Previous(); history.Update("2", "第二步", "乙更新");
            Assert.AreEqual(0, history.Index); Assert.AreEqual(2, history.Count);
            history.Update("3", "第三步", "丙"); Assert.AreEqual(0, history.Index);
            history.Latest(); Assert.AreEqual("丙", history.Current.Text);
            history.Previous(); Assert.AreEqual("乙更新", history.Current.Text);
        }
        [Test]
        public void Reset_between_lessons_clears_history_and_cannot_show_future_guides()
        {
            var history = new AdventureGuideHistory(); history.Update("1", "旧关", "旧文字");
            history.Clear(); Assert.AreEqual(0, history.Count); Assert.Null(history.Current);
            history.Update("1", "新关", "新文字"); Assert.AreEqual(1, history.Count);
            Assert.AreEqual("新文字", history.Current.Text); Assert.IsFalse(history.CanNext);
        }
    }
}
