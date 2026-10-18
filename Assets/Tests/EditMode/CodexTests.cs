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

        /// <summary>
        /// **图鉴篇幅约定**（玩家要求，2026-02 定）。
        ///
        /// 玩家原话："把写图鉴这个记住，有机制的大概写多长，没机制的大概写多长，
        /// 之后不要再特别容易出现长篇大论的情况。"
        ///
        /// 实测（精简后 20 名角色的中位数与区间）：
        ///
        ///   段落        机制简单        有机制        硬上限（本测试）
        ///   技能一句    ~8~15           ~17~22       45
        ///   操作指南    ~30~38          ~50~60       80
        ///   如何针对    ~19~40          ~20~40       80
        ///
        /// **机制简单** = 一句能说完（士兵、游侠、骑士、狼人、魔射手、巨人…）
        /// **有机制**   = 需要说清触发条件与代价（武僧、盗贼、妖术师、猎人、魔术师…）
        ///
        /// \u26a0\ufe0f 上限只挡"长篇大论"，不追求人人等长 ——
        ///    因为"把简单角色也写长"和"把有机制的角色写短"都是错的方向。
        ///    定这条之前，武僧的"如何针对"曾写到 167 字（中位数的 5 倍）。
        ///
        /// \u26a0\ufe0f 例外机制：若某段确实**重要到无法删**，把它加进下面的
        ///    <see cref="LongButJustified"/> 并**写清理由** ——
        ///    这样例外是显式的、会被复核的，而不是悄悄漂长。
        ///    （玩家："若实在重要无法删除可以例外。"）
        /// </summary>
        private static readonly Dictionary<string, string> LongButJustified =
            new Dictionary<string, string>();

        /// <summary>
        /// **必须写进图鉴的机制**（玩家纠正："不要把这种重要的机制从图鉴里删掉"）。
        ///
        /// 起因：我精简图鉴时，把战士的"碎盾换无限距离"从技能里删掉了，
        /// 而游侠的"强化射击使本次攻击距离无限"**从来就没写过**。
        /// 玩家指出：攻击距离什么时候变成无限，是玩家做决策必须知道的事。
        ///
        /// \u26a0\ufe0f 所以**篇幅上限不能成为删掉机制的理由**。
        ///    这条测试用来守住"机制没被删掉"：
        ///    每个角色的图鉴（技能 + 指南 + 针对 + 交互）里，
        ///    必须提到下面的关键词之一。
        /// </summary>
        private static readonly Dictionary<string, string[]> MustMention =
            new Dictionary<string, string[]>
            {
                // 战士：碎盾换射程 -> 本次攻击距离无限
                { CharacterDefinition.FighterId, new[] { "无限" } },
                // 游侠：强化射击 -> 本次攻击距离无限
                { CharacterDefinition.RangerId, new[] { "无限" } },
                // 盗贼：偷窃是无限距离
                { CharacterDefinition.ThiefId, new[] { "无限" } },
            };

        [Test]
        public void Important_mechanics_are_never_dropped_from_the_codex()
        {
            var bad = new List<string>();

            foreach (KeyValuePair<string, string[]> kv in MustMention)
            {
                CodexEntry e = CharacterCodex.For(kv.Key);
                if (e == null) { bad.Add(kv.Key + "(无资料)"); continue; }

                // 图鉴玩家能看到的所有文字
                string all = e.ShortSkill + "\n" + e.DetailSkill + "\n"
                             + e.PlayGuide + "\n" + e.Counter + "\n"
                             + string.Join("\n", e.Interactions);

                for (int i = 0; i < kv.Value.Length; i++)
                {
                    if (all.IndexOf(kv.Value[i], System.StringComparison.Ordinal) < 0)
                        bad.Add($"{kv.Key} 的图鉴里没提到「{kv.Value[i]}」");
                }
            }

            Assert.AreEqual(0, bad.Count,
                "这些重要机制从图鉴里丢了（玩家明确要求不能删）：\n"
                + string.Join("\n", bad));
        }

        [Test]
        public void Codex_entries_are_not_over_long()
        {
            const int maxSkill = 45;
            const int maxGuide = 80;
            const int maxCounter = 80;

            CharacterDefinition[] all = CharacterDefinition.BuiltIn;
            var bad = new List<string>();

            for (int i = 0; i < all.Length; i++)
            {
                CodexEntry e = CharacterCodex.For(all[i]);
                if (e == null) continue;

                // 只统计中文字数 —— 更贴近"读起来有多长"
                int skill = ChineseCount(e.ShortSkill);
                int guide = ChineseCount(e.PlayGuide);
                int counter = ChineseCount(e.Counter);

                bool exempt = LongButJustified.ContainsKey(all[i].Id);

                if (!exempt && skill > maxSkill)
                    bad.Add($"{all[i].DisplayName} 技能 {skill} > {maxSkill}");
                if (!exempt && guide > maxGuide)
                    bad.Add($"{all[i].DisplayName} 操作指南 {guide} > {maxGuide}");
                if (!exempt && counter > maxCounter)
                    bad.Add($"{all[i].DisplayName} 如何针对 {counter} > {maxCounter}");
            }

            Assert.AreEqual(0, bad.Count,
                "图鉴段落过长（玩家要求不要长篇大论；"
                + "确有必要就加进 LongButJustified 并写明理由）：\n"
                + string.Join("\n", bad));
        }

        /// <summary>统计中文字符数（读起来多长，而不是字节数）。</summary>
        private static int ChineseCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int n = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '\u4e00' && c <= '\u9fff') n++;
            }
            return n;
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
        public void Alignments_match_the_players_assignment()
        {
            // 玩家**逐个指定**了九宫格分配 —— 这条把它钉住，
            // 以后不小心改动会被捕到。
            var expect = new Dictionary<string, Alignment>
            {
                // 守序善良：圣武士 牧师 骑士
                { CharacterDefinition.PaladinId, Alignment.LawfulGood },
                { CharacterDefinition.PriestId, Alignment.LawfulGood },
                { CharacterDefinition.KnightId, Alignment.LawfulGood },
                // 守序中立：剑客 战士
                { CharacterDefinition.SwordsmanId, Alignment.LawfulNeutral },
                { CharacterDefinition.FighterId, Alignment.LawfulNeutral },
                // 守序邪恶：魔射手
                { CharacterDefinition.ArcaneArcherId, Alignment.LawfulEvil },
                // 中立善良：游侠 武僧
                { CharacterDefinition.RangerId, Alignment.NeutralGood },
                { CharacterDefinition.MonkId, Alignment.NeutralGood },
                // 绝对中立：忍者 士兵
                { CharacterDefinition.NinjaId, Alignment.TrueNeutral },
                { CharacterDefinition.SoldierId, Alignment.TrueNeutral },
                // 中立邪恶：盗贼
                { CharacterDefinition.ThiefId, Alignment.NeutralEvil },
                // 混乱善良：野蛮人
                { CharacterDefinition.BarbarianId, Alignment.ChaoticGood },
                // 混乱中立：刺客 赌徒 咒术师
                { CharacterDefinition.AssassinId, Alignment.ChaoticNeutral },
                { CharacterDefinition.GamblerId, Alignment.ChaoticNeutral },
                { CharacterDefinition.CurseweaverId, Alignment.ChaoticNeutral },
                // 混乱邪恶：狼人 巨人 妖术师（妖术师由中立邪恶移来）
                { CharacterDefinition.WerewolfId, Alignment.ChaoticEvil },
                { CharacterDefinition.GiantId, Alignment.ChaoticEvil },
                { CharacterDefinition.SorcererId, Alignment.ChaoticEvil },
                // 守序邪恶：猎人（玩家指定）
                { CharacterDefinition.HunterId, Alignment.LawfulEvil },
                // 混乱善良：魔术师（玩家指定）
                { CharacterDefinition.MagicianId, Alignment.ChaoticGood },
            };

            var wrong = new List<string>();
            foreach (KeyValuePair<string, Alignment> kv in expect)
            {
                CodexEntry e = CharacterCodex.For(kv.Key);
                if (e == null) { wrong.Add(kv.Key + "(无资料)"); continue; }
                if (e.Alignment != kv.Value)
                    wrong.Add($"{kv.Key}: 期望 {kv.Value} 实际 {e.Alignment}");
            }

            Assert.AreEqual(0, wrong.Count,
                "阵营与玩家指定不一致：" + string.Join(" / ", wrong));

            // 九宫格里每一格都要有人（否则筛选会出现空白页）
            for (int i = 0; i <= 8; i++)
            {
                Alignment a = (Alignment)i;
                List<CharacterDefinition> list =
                    CharacterCodex.Filter(Alignments.LawAxisOf(a), Alignments.GoodAxisOf(a));

                // 注意：两个轴只能定位到"格"，
                // 所以每格至少有一人才算完整。
                Assert.True(list.Count > 0,
                    $"阵营 {Alignments.NameOf(a)} 这一格是空的。");
            }
        }

        [Test]
        public void True_neutral_reads_as_absolute_neutral()
        {
            // 玩家要求："中立中立"改为**"绝对中立"**。
            //
            // 因为名字是两条轴拼出来的，直接拼会得到"中立中立"。
            Assert.AreEqual("绝对中立", Alignments.NameOf(Alignment.TrueNeutral));

            // 其余八格仍然是拼接（防止改动误伤它们）
            Assert.AreEqual("守序善良", Alignments.NameOf(Alignment.LawfulGood));
            Assert.AreEqual("守序中立", Alignments.NameOf(Alignment.LawfulNeutral));
            Assert.AreEqual("守序邪恶", Alignments.NameOf(Alignment.LawfulEvil));
            Assert.AreEqual("中立善良", Alignments.NameOf(Alignment.NeutralGood));
            Assert.AreEqual("中立邪恶", Alignments.NameOf(Alignment.NeutralEvil));
            Assert.AreEqual("混乱善良", Alignments.NameOf(Alignment.ChaoticGood));
            Assert.AreEqual("混乱中立", Alignments.NameOf(Alignment.ChaoticNeutral));
            Assert.AreEqual("混乱邪恶", Alignments.NameOf(Alignment.ChaoticEvil));
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
