using System.IO;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **源码守卫**：飘字的颜色必须一路传到界面。
    ///
    /// ⚠️ 为什么用"读源码"而不是断言行为：
    ///    测试项目**引用不到界面程序集**（UI 是叶子层），
    ///    所以 `GamePresentation` / `FloatingTextLayer` 在测试里无法直接构造。
    ///
    /// 背景（真实发生过的 bug）：
    ///    `/GamePresentation.cs` 把 `FloatTextSpec` 转成 `FloatSpawn` 时
    ///   **漏传了颜色**，于是 `FloatSpawn.ColorRgb` 恒为 0、界面退回默认色 ——
    ///   伤害飘字变了色、**格挡还是灰白**，玩家报"格挡弹的颜色也要和攻击属性一致"。
    /// </summary>
    [TestFixture]
    public class FloatColorWiringTests
    {
        private static string Root()
        {
            // 从测试程序集所在目录往上找仓库根（含 Assets 的那一层）
            string dir = System.AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "Assets"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static string Read(string relative)
        {
            string root = Root();
            if (root == null) return null;

            string p = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        [Test]
        public void Presentation_must_pass_the_float_colour_through()
        {
            string src = Read("Assets/Scripts/UI/GamePresentation.cs");
            if (src == null) return;      // 非源码目录运行：跳过

            Assert.IsTrue(src.Contains("spec.ColorRgb"),
                "GamePresentation 把 FloatTextSpec 转成 FloatSpawn 时，"
                + "**必须把 ColorRgb 一起传下去** —— "
                + "否则格挡/伤害会退回默认色，玩家会报"
                + "「格挡弹的颜色也要和攻击属性一致」。");
        }

        [Test]
        public void Float_layer_must_prefer_the_override_colour()
        {
            string src = Read("Assets/Scripts/UI/FloatingTextLayer.cs");
            if (src == null) return;

            Assert.IsTrue(src.Contains("spawn.ColorRgb != 0"),
                "FloatingTextLayer 应当**优先用 ColorRgb**（有覆盖时），"
                + "而不是一律按 Kind 取默认色。");
        }

        [Test]
        public void Blocked_floats_carry_the_attack_type_colour()
        {
            string src = Read("Assets/Scripts/Assistant/AnimationDirector.cs");
            if (src == null) return;

            // ⚠️ 不能只找第一个"格挡" —— 那是枚举 FloatKind.Blocked 的注释。
            //    要定位**真正生成飘字的那一行**：带 FloatKind.Blocked 的 Add(...)。
            int i = src.IndexOf("FloatKind.Blocked, typeColor");
            Assert.IsTrue(i > 0,
                "「格挡」飘字必须带上本次攻击类型的颜色"
                + "（应为 `Add(..., FloatKind.Blocked, typeColor)`）。");

            // 伤害那一条也要带
            int d = src.IndexOf("\"-\" + perHit");
            Assert.IsTrue(d > 0, "找不到伤害飘字的生成处。");

            string dw = src.Substring(d, System.Math.Min(120, src.Length - d));
            Assert.IsTrue(dw.Contains("typeColor"),
                "伤害飘字必须带上本次攻击类型的颜色（typeColor）。");
        }
    }
}
