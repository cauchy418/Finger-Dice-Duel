using FingerDiceDuel.Assistant;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 血条的数值计算验证。
    ///
    /// 血条外观看不到，但"宽度比例对不对、边界会不会溢出"是可以精确验证的 ——
    /// 算法放在 <see cref="AnimationDirector"/>（不依赖 UnityEngine）。
    /// </summary>
    [TestFixture]
    public class HealthBarTests
    {
        [Test]
        public void Full_health_is_ratio_one()
        {
            Assert.AreEqual(1f, AnimationDirector.HealthRatio(20, 20));
            Assert.AreEqual(1f, AnimationDirector.HealthRatio(25, 25), "野蛮人·野兽道途上限 25");
        }

        [Test]
        public void Half_health_is_ratio_half()
        {
            Assert.AreEqual(0.5f, AnimationDirector.HealthRatio(10, 20));
        }

        [Test]
        public void Zero_health_is_ratio_zero()
        {
            Assert.AreEqual(0f, AnimationDirector.HealthRatio(0, 20));
        }

        [Test]
        public void Negative_health_clamps_to_zero()
        {
            Assert.AreEqual(0f, AnimationDirector.HealthRatio(-5, 20),
                "生命不会为负，但保险起见要夹紧。");
        }

        [Test]
        public void Health_above_max_clamps_to_one()
        {
            Assert.AreEqual(1f, AnimationDirector.HealthRatio(30, 20),
                "回复不会超过上限，但要防止溢出把血条画穿。");
        }

        [Test]
        public void Zero_max_health_does_not_divide_by_zero()
        {
            Assert.AreEqual(0f, AnimationDirector.HealthRatio(10, 0));
        }

        [Test]
        public void Fill_width_scales_with_ratio()
        {
            const float full = 220f;

            Assert.AreEqual(220f, AnimationDirector.HealthBarFillWidth(full, 20, 20));
            Assert.AreEqual(110f, AnimationDirector.HealthBarFillWidth(full, 10, 20));
            Assert.AreEqual(0f, AnimationDirector.HealthBarFillWidth(full, 0, 20));
        }

        [Test]
        public void PathOfTheBeast_25_max_health_bar_uses_its_own_max()
        {
            // 野蛮人·野兽道途上限 25：满血应是满条，而不是 20/25 那种"看起来缺一块"
            const float full = 220f;

            Assert.AreEqual(full, AnimationDirector.HealthBarFillWidth(full, 25, 25));
            // 12/25 = 0.48 -> 105.6
            Assert.AreEqual(full * 0.48f, AnimationDirector.HealthBarFillWidth(full, 12, 25));
        }

        [Test]
        public void Zero_health_means_zero_width_so_the_bar_is_actually_empty()
        {
            // 实测过的 bug：填充曾用"左右拉伸"的锚点，
            // 那种情况下 sizeDelta 是**相对锚点跨度的偏移量**，
            // 设成 0 时实际宽度仍等于锚点跨度 —— 血条永远满格，
            // 血量归零也还剩一整条。
            //
            // 现在填充改用点锚点，sizeDelta.x 就是真实像素宽度，
            // 所以"比例为 0 -> 宽度为 0"必须成立。
            const float full = 220f;

            Assert.AreEqual(0f, AnimationDirector.HealthBarFillWidth(full, 0, 20),
                "血量 0 时填充宽度必须是 0（否则条永远空不掉）。");
        }

        [Test]
        public void Full_health_fills_exactly_the_usable_width()
        {
            const float full = 220f;

            // 满血 == 可用宽度，不多不少（多了会盖住底槽边框）
            Assert.AreEqual(full, AnimationDirector.HealthBarFillWidth(full, 20, 20));
        }

        [Test]
        public void Entering_death_produces_a_lower_ratio_than_before()
        {
            // 1 点血 -> 死亡必须让条变短，而不是原地不动
            float almostDead = AnimationDirector.HealthRatio(1, 20);
            float dead = AnimationDirector.HealthRatio(0, 20);

            Assert.True(dead < almostDead,
                $"死亡比例 {dead} 应小于 1 点血的 {almostDead}。");
        }
        [Test]
        public void Health_ratio_is_monotonic()
        {
            // 血越少条越短，不能出现反直觉的跳变
            float previous = -1f;
            for (int hp = 0; hp <= 20; hp++)
            {
                float ratio = AnimationDirector.HealthRatio(hp, 20);
                Assert.True(ratio > previous || hp == 0,
                    $"血量 {hp} 的比例应大于前一档（{ratio} vs {previous}）。");
                previous = ratio;
            }
        }
    }
}
