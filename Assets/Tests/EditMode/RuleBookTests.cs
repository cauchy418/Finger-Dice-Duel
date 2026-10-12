using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 规则说明的验证。
    ///
    /// 为什么重要：**规则书写错比代码写错更误导玩家**。
    /// 所以这里不是检查文案好不好看，而是逐条对照**引擎的实际行为**：
    /// 攻击点数、护盾值、和 10 变 1、生命上限、回合上限都必须真的成立。
    /// </summary>
    [TestFixture]
    public class RuleBookTests
    {
        /// <summary>
        /// **玩家可见的文本里不许出现 Markdown 的 `**`。**
        ///
        /// 为什么：游戏用 uGUI 的 <c>Text</c> 渲染，它**不认 Markdown** ——
        /// 写进去的星号会**原样显示**（玩家实测看到"投出 **12**"这种文字）。
        /// 注释里的 `**` 无害（那是给读代码的人看的），所以只查非注释行。
        /// </summary>
        [Test]
        public void No_markdown_bold_in_player_visible_strings()
        {
            string root = FindScriptsRoot();
            Assert.NotNull(root, "找不到 Assets/Scripts 目录。");

            var offenders = new List<string>();

            foreach (string path in System.IO.Directory.GetFiles(root, "*.cs",
                                                                 System.IO.SearchOption.AllDirectories))
            {
                string[] lines = System.IO.File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();

                    // 注释里的 ** 无害（是给读代码的人看的）
                    if (trimmed.StartsWith("//")) continue;

                    // 只关心"字符串里"的 **：这一行得同时含 ** 与引号
                    if (lines[i].Contains("**") && lines[i].Contains("\""))
                        offenders.Add($"{System.IO.Path.GetFileName(path)}:{i + 1}: {trimmed}");
                }
            }

            Assert.AreEqual(0, offenders.Count,
                "玩家可见文本里不能出现 Markdown 的 **（uGUI Text 不会加粗，星号会原样显示）：\n" +
                string.Join("\n", offenders));
        }

        /// <summary>从测试文件位置往上找 Assets/Scripts。</summary>
        private static string FindScriptsRoot()
        {
            var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);

            while (dir != null)
            {
                string candidate = System.IO.Path.Combine(dir.FullName, "Assets", "Scripts");
                if (System.IO.Directory.Exists(candidate)) return candidate;

                // 也可能测试是从 Assets 下面被发现的
                if (System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "Scripts"))
                    && System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "Tests")))
                    return System.IO.Path.Combine(dir.FullName, "Scripts");

                dir = dir.Parent;
            }

            return null;
        }

        [Test]
        public void Attack_values_in_the_rules_match_the_engine()
        {
            string text = RuleBook.AttackValues();

            // 引擎里 4 / 6 / 8 是攻击
            Assert.AreEqual("4 / 6 / 8", text,
                $"规则里的攻击点数应与引擎一致，实际：{text}");
        }

        [Test]
        public void Shield_value_in_the_rules_matches_the_engine()
        {
            Assert.AreEqual(5, RuleBook.ShieldValue(), "合盾的数字应是 5。");
            Assert.AreEqual(HandEffectType.Shield, Hand.GetEffectType(5));
        }

        [Test]
        public void Heal_value_in_the_rules_matches_the_engine()
        {
            Assert.AreEqual(9, Hand.HealAmount);
            Assert.AreEqual(HandEffectType.Heal, Hand.GetEffectType(Hand.HealAmount));
        }

        [Test]
        public void The_ten_becomes_one_claim_in_the_rules_is_true()
        {
            // 规则里写"和为 10 则变成 1"，必须真的成立
            Assert.AreEqual(1, Hand.ComputeSum(1, 9));
            Assert.AreEqual(1, Hand.ComputeSum(2, 8));
            Assert.AreEqual(1, Hand.ComputeSum(5, 5));
        }

        [Test]
        public void Every_arithmetic_example_in_the_rules_is_correct()
        {
            // 规则里举的例子：3 + 9 = 12 → 2
            Assert.AreEqual(2, Hand.ComputeSum(3, 9));
        }

        [Test]
        public void The_turn_limit_in_the_rules_matches_the_engine()
        {
            string text = RuleBook.BasicRules();

            Assert.True(text.Contains(GameManager.DefaultTurnLimit.ToString()),
                $"规则应写明回合上限 {GameManager.DefaultTurnLimit}。");
        }

        [Test]
        public void Basic_rules_cover_the_four_core_topics()
        {
            string text = RuleBook.BasicRules();

            foreach (string topic in new[] { "运算", "攻击", "护盾", "胜负" })
            {
                Assert.True(text.Contains(topic),
                    $"基础规则应包含'{topic}'一节。\n{text}");
            }
        }

        [Test]
        public void Character_list_mentions_every_character_with_its_skill()
        {
            var roster = new List<CharacterDefinition>(CharacterDefinition.BuiltIn);

            string text = RuleBook.CharacterList(roster);

            foreach (CharacterDefinition def in roster)
            {
                Assert.True(text.Contains(def.DisplayName),
                    $"角色一览应包含 {def.DisplayName}。");
                Assert.True(text.Contains(def.Description),
                    $"{def.DisplayName} 的技能说明应完整出现。");
            }
        }

        [Test]
        public void Character_list_shows_the_real_max_health_for_the_werewolf()
        {
            // 狼人上限 25，规则里若写 20 就是骗人
            var roster = new List<CharacterDefinition> { CharacterDefinition.Werewolf };

            string text = RuleBook.CharacterList(roster);

            Assert.True(text.Contains("25"),
                $"狼人的生命上限应写 25（实际由 WerewolfLogic 提供）。\n{text}");
        }

        [Test]
        public void Full_rules_contain_both_basic_and_character_sections()
        {
            var roster = new List<CharacterDefinition>(CharacterDefinition.BuiltIn);

            string text = RuleBook.Full(roster);

            Assert.True(text.Contains("一回合做什么"), "应含基础规则。");
            Assert.True(text.Contains("角色技能"), "应含角色一览。");
        }

        [Test]
        public void Coming_soon_text_names_the_feature_and_the_available_mode()
        {
            string text = RuleBook.ComingSoon("2 v 2 组队模式");

            Assert.True(text.Contains("2 v 2 组队模式"), "应说明是哪个功能。");
            Assert.True(text.Contains("敬请期待"), "应说明尚未开放。");
            Assert.True(text.Contains("人机"), "应告诉玩家现在能玩什么。");
        }

        [Test]
        public void Rules_tolerate_an_empty_roster()
        {
            Assert.AreEqual("", RuleBook.CharacterList(null));
            Assert.AreEqual("", RuleBook.CharacterList(new List<CharacterDefinition>()));
        }

        // ============================================================
        // 规则里描述的角色行为，逐条对照引擎
        // ============================================================

        [Test]
        public void Rule_stated_character_numbers_match_the_logic_classes()
        {
            // 规则一栏里出现的数字（如"和 7"、"潜行 7"、"追加"）都应与逻辑类一致
            Assert.AreEqual(7, WerewolfLogic.TriggerSum, "狼人触发和应是 7。");
            Assert.AreEqual(7, AssassinLogic.StealthValue, "刺客潜行值应是 7。");
            Assert.AreEqual(9, PriestLogic.DoubleNineValue, "牧师双 9 判定值是 9。");
            Assert.AreEqual(1, RangerLogic.DodgeFromValue, "游侠闪避的来源值是 1。");
            Assert.AreEqual(2, RangerLogic.PowerFromValue, "游侠强化的来源值是 2。");
            Assert.AreEqual(2, NinjaLogic.HitCount, "忍者攻击是两段。");
        }

        [Test]
        public void Every_character_description_is_the_same_text_the_card_shows()
        {
            // 规则弹窗与角色卡必须用同一份说明，不能各写一套
            foreach (CharacterDefinition def in CharacterDefinition.BuiltIn)
            {
                var roster = new List<CharacterDefinition> { def };
                string rules = RuleBook.CharacterList(roster);

                Assert.True(rules.Contains(def.Description),
                    $"{def.DisplayName} 在规则里应与卡片用同一份说明。");
            }
        }
    }
}
