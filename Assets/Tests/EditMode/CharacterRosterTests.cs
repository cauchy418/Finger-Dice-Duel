using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 角色选择界面的**文案**验证。
    ///
    /// 布局与外观看不到，但"卡片上显示的身材对不对、技能摘要是否可读"
    /// 是纯数据，可以精确验证 —— 尤其是狼人的 +5 生命上限不在数据层，
    /// 显示错了玩家会以为卡片在骗人。
    /// </summary>
    [TestFixture]
    public class CharacterRosterTests
    {
        [Test]
        public void Werewolf_card_shows_the_real_max_health_of_twenty_five()
        {
            // 狼人的 +5 由角色逻辑提供，不在 CharacterDefinition.MaxHealth 里。
            // 卡片必须显示 25，否则和实战不符。
            string stats = CharacterCardText.DescribeStats(CharacterDefinition.Werewolf);

            Assert.True(stats.Contains("25"),
                $"狼人卡片应显示实际生命上限 25，实际：{stats}");
        }

        [Test]
        public void Blank_card_shows_the_default_max_health()
        {
            string stats = CharacterCardText.DescribeStats(CharacterDefinition.Blank);

            Assert.True(stats.Contains("20"),
                $"白板应显示 20 生命，实际：{stats}");
        }

        [Test]
        public void Card_stats_show_attack_and_defense_with_signs()
        {
            // 咒术师 攻 -1 / 防 0
            string stats = CharacterCardText.DescribeStats(CharacterDefinition.Curseweaver);

            Assert.True(stats.Contains("-1"), $"应显示 攻 -1，实际：{stats}");
            Assert.True(stats.Contains("防 0"), $"应显示 防 0，实际：{stats}");
        }

        [Test]
        public void Card_stats_show_positive_bonuses_with_a_plus()
        {
            // 战士 攻 +1 —— 正数必须带 +，否则和 1 分不清
            string stats = CharacterCardText.DescribeStats(CharacterDefinition.Warrior);

            Assert.True(stats.Contains("+1"), $"正加值应带 + 号，实际：{stats}");
        }

        [Test]
        public void Every_built_in_character_produces_a_readable_card()
        {
            foreach (CharacterDefinition def in CharacterDefinition.BuiltIn)
            {
                string stats = CharacterCardText.DescribeStats(def);
                string hint = CharacterCardText.Shorten(def.Description);

                Assert.False(string.IsNullOrWhiteSpace(stats),
                    $"{def.DisplayName} 的身材摘要不应为空。");
                Assert.False(string.IsNullOrWhiteSpace(hint),
                    $"{def.DisplayName} 的技能摘要不应为空。");
                Assert.True(hint.Length <= 28,
                    $"{def.DisplayName} 的技能摘要过长（{hint.Length} 字），会超出卡片。");
            }
        }

        [Test]
        public void Shorten_truncates_a_long_description_with_an_ellipsis()
        {
            string longText = new string('测', 60);

            string short_ = CharacterCardText.Shorten(longText, maxChars: 20);

            Assert.True(short_.Length <= 21, $"应截断到 20 字加省略号，实际 {short_.Length}。");
            Assert.True(short_.EndsWith("…"), $"截断后应带省略号：{short_}");
        }

        [Test]
        public void Shorten_prefers_the_first_sentence()
        {
            string text = "第一句说明。第二句补充说明。";

            string short_ = CharacterCardText.Shorten(text, maxChars: 40);

            Assert.AreEqual("第一句说明", short_,
                "摘要应取第一个句号之前的内容（不含句号）。");
        }

        [Test]
        public void Shorten_keeps_a_short_description_intact()
        {
            string text = "无特殊能力";

            Assert.AreEqual(text, CharacterCardText.Shorten(text, maxChars: 40));
        }

        [Test]
        public void Shorten_tolerates_null_and_empty()
        {
            Assert.AreEqual("", CharacterCardText.Shorten(null));
            Assert.AreEqual("", CharacterCardText.Shorten(""));
        }

        [Test]
        public void MaxHealthOf_matches_what_a_real_player_gets()
        {
            // 界面显示的上限必须与真正建出来的玩家一致，否则卡片在骗人
            foreach (CharacterDefinition def in CharacterDefinition.BuiltIn)
            {
                PlayerState player = CharacterFactory.CreatePlayer("测试", def);

                Assert.AreEqual(player.MaximumHealth, CharacterFactory.MaxHealthOf(def),
                    $"{def.DisplayName} 的卡片上限与实际不符。");
            }
        }
    }
}
