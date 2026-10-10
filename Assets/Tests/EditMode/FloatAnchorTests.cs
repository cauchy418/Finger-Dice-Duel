using FingerDiceDuel.Assistant;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 飘字**落点**的验证。
    ///
    /// 为什么可以测：落点换算被抽成了 <see cref="AnimationDirector.CanvasLocalToAnchored"/>
    /// 这个纯浮点函数（不依赖 UnityEngine），所以不需要画面就能确认
    /// "飘字确实落在名字那一行、且以画布中心为原点"。
    /// UI 层只做一层薄包装。
    /// </summary>
    [TestFixture]
    public class FloatAnchorTests
    {
        private const float W = 1920f;
        private const float H = 1080f;

        private static void Convert(float localX, float localY,
                                    out float anchoredX, out float anchoredY)
            => AnimationDirector.CanvasLocalToAnchored(W, H, localX, localY,
                                                       out anchoredX, out anchoredY);

        [Test]
        public void Canvas_center_maps_to_origin()
        {
            Convert(W * 0.5f, H * 0.5f, out float x, out float y);

            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);
        }

        [Test]
        public void Canvas_corners_map_to_half_extents()
        {
            Convert(0f, 0f, out float blx, out float bly);
            Assert.AreEqual(-960f, blx, "左下角 x");
            Assert.AreEqual(-540f, bly, "左下角 y");

            Convert(W, H, out float trx, out float trY);
            Assert.AreEqual(960f, trx, "右上角 x");
            Assert.AreEqual(540f, trY, "右上角 y");
        }

        [Test]
        public void A_name_row_somewhere_high_maps_upward()
        {
            // 名字行若在画布本地 y = 820（画布高 1080），应落在中心之上
            Convert(300f, 820f, out float x, out float y);

            Assert.AreEqual(300f - 960f, x, "x 应相对中心偏移");
            Assert.AreEqual(820f - 540f, y, "y 应相对中心偏移");
            Assert.True(y > 0f, $"名字行应在画布中心之上，实际 y={y}。");
        }

        [Test]
        public void Conversion_is_stable_across_repeated_calls()
        {
            Convert(300f, 820f, out float x1, out float y1);
            Convert(300f, 820f, out float x2, out float y2);

            Assert.AreEqual(x1, x2);
            Assert.AreEqual(y1, y2);
        }

        [Test]
        public void Stagger_constant_is_the_agreed_value()
        {
            // "先按这组值做、之后再调" —— 钉住，避免被意外改坏
            Assert.AreEqual(0.25f, AnimationDirector.StaggerSeconds);
        }
    }
}
