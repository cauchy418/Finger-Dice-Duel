using System.IO;
using FingerDiceDuel.Assistant;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 界面流程的不变量。
    ///
    /// 玩家实测 bug：对局中"返回标题"或结算后"再来一局"，
    /// **整个 UI 消失**，只剩一片纯色（相机背景），看起来像崩了。
    ///
    /// 根因：`OnStartClicked()` 把 `_selectRoot`（**所有界面共用的画布**）
    /// 整个 `SetActive(false)`，而 `ShowScreen()` 只切分组显隐、
    /// 从不重新激活画布 —— 于是再也回不来。
    ///
    /// ⚠️ 这类问题"肉眼才能发现"，所以这里用两种方式钉住：
    ///   ① 源码守卫：禁止任何地方把共用画布 `SetActive(false)`；
    ///   ② 不变量：切到菜单类界面时，画布必须是激活的。
    /// </summary>
    [TestFixture]
    public class ScreenFlowTests
    {
        /// <summary>
        /// 源码守卫：`_selectRoot` 是三个界面共用的画布。
        /// 任何地方 `SetActive(false)` 它，都会让"切回菜单"永久黑屏 ——
        /// 因为没有任何代码会重新激活它。
        /// </summary>
        [Test]
        public void Shared_canvas_is_never_deactivated_anywhere()
        {
            string path = FindSource("GameBootstrap.cs");
            Assert.NotNull(path, "找不到 GameBootstrap.cs，无法做源码守卫。");

            string source = File.ReadAllText(path);

            Assert.False(source.Contains("_selectRoot.gameObject.SetActive(false)"),
                "禁止把共用画布 _selectRoot SetActive(false) —— " +
                "切回菜单时没有任何代码会重新激活它，会导致整片 UI 消失。" +
                "隐藏界面请用 ShowScreen(...) 切分组显隐。");
        }

        /// <summary>切到菜单类界面时，必须确保共用画布是激活的。</summary>
        [Test]
        public void ShowScreen_reactivates_the_shared_canvas_for_menu_screens()
        {
            string path = FindSource("GameBootstrap.cs");
            Assert.NotNull(path);

            string source = File.ReadAllText(path);

            // ShowScreen 里应有"画布没激活就激活"的保险
            Assert.True(
                source.Contains("_selectRoot.gameObject.SetActive(true)"),
                "ShowScreen 必须包含'画布未激活就重新激活'的保险，" +
                "否则一旦画布被别处关掉，切屏就什么都不显示。");
        }

        /// <summary>对局结束/退出后必须能回到菜单类界面（三个入口都在）。</summary>
        [Test]
        public void All_menu_screens_are_reachable_after_a_match()
        {
            string path = FindSource("GameBootstrap.cs");
            Assert.NotNull(path);

            string source = File.ReadAllText(path);

            // 退出对局 -> 主菜单
            Assert.True(source.Contains("ShowScreen(GameScreen.MainMenu)"),
                "应有回到主菜单的路径。");
            // 重开 -> 角色选择
            Assert.True(source.Contains("ShowScreen(GameScreen.CharacterSelect)"),
                "应有回到角色选择（再来一局）的路径。");
            // 对局中 -> 只切界面，不结束进程
            Assert.True(source.Contains("ShowScreen(GameScreen.InMatch)"),
                "开始对局时应切到对局界面。");
        }

        /// <summary>
        /// 开局掷骰界面必须：
        ///   · 在 ShowScreen 里被纳入"分组显隐"（否则它要么永不显示、要么永不隐藏）；
        ///   · 被排进"弹窗置顶"的那组（否则规则弹窗会被它盖住）。
        /// </summary>
        [Test]
        public void Dice_roll_screen_is_wired_into_the_screen_switch()
        {
            string path = FindSource("GameBootstrap.cs");
            Assert.NotNull(path);
            string source = File.ReadAllText(path);

            Assert.True(source.Contains("ShowScreen(GameScreen.DiceRoll)"),
                "点开始后应先进入掷骰界面。");
            Assert.True(source.Contains("_diceRoll?.SetVisible(screen == GameScreen.DiceRoll)"),
                "ShowScreen 必须显隐掷骰分组 —— 漏掉它会永不显示或永不隐藏。");
            Assert.True(source.Contains("screen == GameScreen.DiceRoll"),
                "掷骰界面也应参与'弹窗置顶'。");
        }

        /// <summary>
        /// 新增的两个选择界面（玩法 / 难度）也必须接进切屏开关。
        ///
        /// 漏掉 `SetVisible` 的后果很直观：界面**永不显示**或**永不隐藏**
        /// （后者会几张界面叠在一起）。
        /// </summary>
        [Test]
        public void Mode_and_difficulty_screens_are_wired_into_the_screen_switch()
        {
            string path = FindSource("GameBootstrap.cs");
            Assert.NotNull(path);
            string source = File.ReadAllText(path);

            Assert.True(source.Contains("_screenMode?.SetVisible(screen == GameScreen.ModeMenu)"),
                "玩法选择界面必须参与显隐。");
            Assert.True(source.Contains("_screenDifficulty?.SetVisible(screen == GameScreen.DifficultyMenu)"),
                "难度选择界面必须参与显隐。");

            // 流程：1v1 -> 玩法 -> 难度 -> 角色选择
            Assert.True(source.Contains("ShowScreen(GameScreen.ModeMenu)"),
                "人机对战应先进入玩法选择。");
            Assert.True(source.Contains("ShowScreen(GameScreen.DifficultyMenu)"),
                "入门玩法应先进入难度选择。");
        }

        /// <summary>
        /// 难度**只**在入门玩法生效 —— 玩家要求经典保持不变。
        /// </summary>        [Test]
        public void Difficulty_only_applies_in_intro_mode()
        {
            string path = FindSource("GameBootstrap.cs");
            Assert.NotNull(path);
            string source = File.ReadAllText(path);

            Assert.True(source.Contains("Mode == PlayMode.Intro") && source.Contains("ApplyDifficulty"),
                "难度应只在入门玩法里配置 AI。");
        }

        /// <summary>
        /// 2v2 的**先攻界面**也必须接进切屏开关。
        ///
        /// 我加界面时反复漏掉这一处（新分组没 SetVisible -> 永远不显示，
        /// 或者切屏后还留在屏幕上）。这条守卫把它变成编译期之外能自动发现的问题。
        /// </summary>
        [Test]
        public void Two_versus_two_initiative_screen_is_wired_into_the_screen_switch()
        {
            string path = FindSource("GameBootstrap.cs");
            Assert.NotNull(path);
            string source = File.ReadAllText(path);

            Assert.True(
                source.Contains("_screenTwoOnTwoInitiative?.SetVisible(screen == GameScreen.TwoOnTwoInitiative)"),
                "2v2 先攻界面必须参与显隐。");

            Assert.True(source.Contains("ShowScreen(GameScreen.TwoOnTwoInitiative)"),
                "经典玩法应先进入先攻界面。");

            // 先攻完成后必须真的开局（否则玩家会停在先攻界面）
            Assert.True(source.Contains("OnTwoOnTwoInitiativeFinished"),
                "先攻完成后必须接上开局流程。");
        }

        /// <summary>
        /// 掷骰界面**只能**用 controller 给的候选生成按钮。
        ///
        /// 玩家明确要求过"不要让不合法的选项给到玩家"；
        /// 界面自己拼算拆分是最容易违反这条的写法，所以用源码守卫钉住。
        /// </summary>
        [Test]
        public void Dice_roll_screen_only_uses_controller_candidates()
        {
            string path = FindSource("DiceRollScreen.cs");
            Assert.NotNull(path);
            string source = File.ReadAllText(path);

            Assert.True(source.Contains("_controller.Candidates"),
                "候选必须来自 MatchStartController.Candidates。");
            Assert.False(source.Contains("new HandSplit("),
                "界面不得自行构造 HandSplit —— 那会把不合法的手值给到玩家。");
        }

        /// <summary>
        /// 掷骰界面的**提示文案**必须取自控制器，不得自己拼。
        ///
        /// ⚠️ 真实 bug 的回归：界面曾经自己拼"顺序提示"与"本次抉择说明"，
        /// 把 decider 的名字/点数两边都用上，于是实机出现
        /// 「战士·破阵者投出 19（大失败）」这种**点数与身份都不对**的提示
        /// （那次实际是 A 投 17、B 投 19，19 还是小成功不是大失败）。
        ///
        /// 控制器侧已有测试钉住 `OrderText` / `DecisionText` 的内容，
        /// 这条守卫保证界面**用的是它们**而不是自己编。
        /// </summary>
        [Test]
        public void Dice_roll_screen_uses_controller_provided_text()
        {
            string path = FindSource("DiceRollScreen.cs");
            Assert.NotNull(path);
            string source = File.ReadAllText(path);

            Assert.True(source.Contains("_controller.OrderText"),
                "顺序提示必须取自 controller.OrderText。");
            Assert.True(source.Contains("_controller.DecisionText") || source.Contains("_controller.Hint"),
                "本次抉择说明必须取自控制器。");

            Assert.False(source.Contains("DiceRollText.OrderNote("),
                "界面不得自己调 OrderNote —— 它拿不到正确的名字（真实 bug 的来源）。");
            Assert.False(source.Contains("DiceRollText.AssignHint("),
                "界面不得自己调 AssignHint —— 以上同理。");
        }

        /// <summary>对局中的按钮不许调用结束进程。</summary>
        [Test]
        public void In_match_exit_does_not_quit_the_process()
        {
            string path = FindSource("GameBootstrap.cs");
            Assert.NotNull(path);

            string source = File.ReadAllText(path);

            // 结束进程只能出现在 QuitGame() 里（主菜单入口）
            int quitCalls = CountOccurrences(source, "Application.Quit()");
            Assert.AreEqual(1, quitCalls,
                $"Application.Quit() 只应在 QuitGame()（主菜单入口）出现一次，实际 {quitCalls} 次。");

            // 且 OnExitClicked 不得直接结束进程
            int exitIndex = source.IndexOf("private void OnExitClicked()");
            Assert.True(exitIndex >= 0, "应存在 OnExitClicked。");

            int nextMethod = source.IndexOf("private ", exitIndex + 10);
            string body = nextMethod > exitIndex
                ? source.Substring(exitIndex, nextMethod - exitIndex)
                : source.Substring(exitIndex);

            Assert.False(body.Contains("Quit"),
                "对局中的\"返回标题\"不得结束进程，只能切回主菜单。");
        }

        // ============================================================

        private static string FindSource(string fileName)
        {
            // 从当前工作目录向上找 Assets/Scripts/UI
            string dir = Directory.GetCurrentDirectory();

            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                string candidate = Path.Combine(dir, "Assets", "Scripts", "UI", fileName);
                if (File.Exists(candidate)) return candidate;

                string ignored = Path.Combine(dir, fileName);
                if (File.Exists(ignored)) return ignored;

                dir = Path.GetDirectoryName(dir);
            }

            return null;
        }

        private static int CountOccurrences(string text, string needle)
        {
            int count = 0;
            int at = 0;

            while ((at = text.IndexOf(needle, at, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }

            return count;
        }
    }
}
