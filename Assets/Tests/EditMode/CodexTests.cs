using System.Collections.Generic;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **角色图鉴**的数据完整性。
    ///
    /// 图鉴是纯数据 + 界面，所以这里只钉"每个角色都有完整资料" ——
    /// 缺项会在界面上表现为空白，很难发现，所以用测试守住。
    /// </summary>
    [TestFixture]
    public class CodexTests
    {
        [Test]
        public void Every_built_in_character_has_a_codex_entry()
        {
            CharacterDefinition[] all = CharacterDefinition.BuiltIn;
            var missing = new List<string>();

            for (int i = 0; i < all.Length; i++)
            {
                if (CharacterCodex.For(all[i]) == null) missing.Add(all[i].DisplayName);
            }

            Assert.AreEqual(0, missing.Count,
                "这些角色没有图鉴资料：" + string.Join("、", missing));
        }

        [Test]
        public void Codex_entries_have_all_required_fields()
        {
            CharacterDefinition[] all = CharacterDefinition.BuiltIn;
            var bad = new List<string>();

            for (int i = 0; i < all.Length; i++)
            {
                CodexEntry e = CharacterCodex.For(all[i]);
                if (e == null) continue;

                bool ok = !string.IsNullOrEmpty(e.EnglishName)
                          && !string.IsNullOrEmpty(e.ShortSkill)
                          && !string.IsNullOrEmpty(e.DetailSkill)
                          && !string.IsNullOrEmpty(e.PlayGuide)
                          && !string.IsNullOrEmpty(e.Counter)
                          && e.Interactions.Count > 0;

                if (!ok) bad.Add(all[i].DisplayName);
            }

            Assert.AreEqual(0, bad.Count,
                "这些角色的图鉴资料有缺项：" + string.Join("、", bad));
        }

        [Test]
        public void Codex_short_skill_is_short_enough_for_a_card()
        {
            // 玩家要求"技能先给一句精简描述，然后换行给出详细描述"——
            // 所以精简那句必须**足够短**（一行放得下），否则界面就没意义了。
            //
            // ⚠️ 我第一版断言的是"精简描述必须短于详细描述"，
            //    结果有一个角色失败 —— 但那是**测试太严**：
            //    有几个角色的详细描述本来就只有一句（例如士兵的"攻防各 +1"），
            //    这种情况下"精简比详细短"根本不成立。
            //    真正的意图是"精简要短"，所以改成限制绝对长度。
            const int maxShortLen = 80;

            CharacterDefinition[] all = CharacterDefinition.BuiltIn;
            var bad = new List<string>();

            for (int i = 0; i < all.Length; i++)
            {
                CodexEntry e = CharacterCodex.For(all[i]);
                if (e == null) continue;

                if (e.ShortSkill.Length > maxShortLen)
                    bad.Add($"{all[i].DisplayName}({e.ShortSkill.Length})");
            }

            Assert.AreEqual(0, bad.Count,
                $"这些角色的精简描述超过 {maxShortLen} 字，卡片放不下："
                + string.Join("、", bad));
        }

        [Test]
        public void Alignments_cover_the_nine_grid()
        {
            // 玩家计划用阵营九宫格分类，所以每个角色都必须落在九格里，
            // 而且两条轴各自只能是三种取值之一。
            CharacterDefinition[] all = CharacterDefinition.BuiltIn;

            for (int i = 0; i < all.Length; i++)
            {
                CodexEntry e = CharacterCodex.For(all[i]);
                if (e == null) continue;

                string law = Alignments.LawAxisOf(e.Alignment);
                string good = Alignments.GoodAxisOf(e.Alignment);

                Assert.True(law == "守序" || law == "中立" || law == "混乱",
                    $"{all[i].DisplayName} 的守序轴取值异常：{law}");
                Assert.True(good == "善良" || good == "中立" || good == "邪恶",
                    $"{all[i].DisplayName} 的善良轴取值异常：{good}");
            }
        }

        [Test]
        public void Filtering_by_one_axis_returns_a_subset()
        {
            List<CharacterDefinition> all =
                CharacterCodex.Filter(null, null);
            List<CharacterDefinition> lawful =
                CharacterCodex.Filter("守序", null);

            Assert.True(lawful.Count > 0, "守序阵营不该是空的。");
            Assert.True(lawful.Count < all.Count, "筛选后应该比全集少。");

            // 筛出来的人确实都属于该阵营
            for (int i = 0; i < lawful.Count; i++)
            {
                CodexEntry e = CharacterCodex.For(lawful[i]);
                Assert.AreEqual("守序", Alignments.LawAxisOf(e.Alignment),
                    $"{lawful[i].DisplayName} 不该出现在守序筛选里。");
            }
        }

        [Test]
        public void Filtering_by_two_axes_stacks()
        {
            // 玩家要求：两个轴可以**依次叠加**筛选
            List<CharacterDefinition> both =
                CharacterCodex.Filter("守序", "善良");

            for (int i = 0; i < both.Count; i++)
            {
                CodexEntry e = CharacterCodex.For(both[i]);
                Assert.AreEqual("守序", Alignments.LawAxisOf(e.Alignment));
                Assert.AreEqual("善良", Alignments.GoodAxisOf(e.Alignment));
            }

            List<CharacterDefinition> one =
                CharacterCodex.Filter("守序", null);

            Assert.True(both.Count <= one.Count,
                "两个轴叠加后不可能比单轴更多。");
        }
    }
}
