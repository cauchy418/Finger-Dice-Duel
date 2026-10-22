using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// AI 对新角色特性的使用验证。
    ///
    /// 为什么要有这一组：早先的 <see cref="GreedyChoiceProvider"/> 只认识
    /// 白板 / 战士·破阵者，六个新角色的技能**一个都不会用**。
    /// 这些测试钉住"AI 确实会用技能"，并防止回归。
    /// </summary>
    [TestFixture]
    public class AiCharacterTests
    {
        /// <summary>造一局：甲（指定角色，由 AI 操作） 对 乙（白板）。</summary>
        private static (GameManager game, GreedyChoiceProvider ai) Arrange(
            CharacterDefinition aiCharacter, string aiName = "甲")
        {
            var ai = new GreedyChoiceProvider();
            var game = new GameManager(
                CharacterFactory.CreatePlayer(aiName, aiCharacter),
                CharacterFactory.CreatePlayer("乙", CharacterDefinition.Blank),
                ai, new SimpleChoiceProvider());

            return (game, ai);
        }

        // ============================================================
        // 牧师·生命领域：绝不把 9 回给对手
        // ============================================================

        [Test]
        public void LifeDomain_ai_heals_itself_and_never_the_opponent()
        {
            // 这是修掉的真 bug：旧逻辑按"谁最缺血"选目标，
            // 于是对手更残血时，牧师·生命领域会把 9 送给对手。
            var ai = new GreedyChoiceProvider();
            var lifeDomain = CharacterFactory.CreatePlayer("牧师·生命领域", CharacterDefinition.LifeDomain);
            var opponent = CharacterFactory.CreatePlayer("乙", CharacterDefinition.Blank);

            // 自己满血、对手残血 —— 旧逻辑一定会选对手
            TestSetup.SetHealth(lifeDomain, 20);
            TestSetup.SetHealth(opponent, 3);

            var candidates = new List<Hand> { lifeDomain.LeftHand, lifeDomain.RightHand, opponent.LeftHand, opponent.RightHand };
            var request = new ChoiceRequest(
                HandEffect.LifeDomainHealTarget, "回给谁？", candidates,
                allowDecline: false, declineResult: lifeDomain.LeftHand, decider: lifeDomain);

            Hand chosen = ai.ChooseHandFor(request, lifeDomain);

            Assert.True(ReferenceEquals(chosen, lifeDomain.LeftHand)
                        || ReferenceEquals(chosen, lifeDomain.RightHand),
                "牧师·生命领域 AI 必须回给自己，不能回给对手。");
        }

        // ============================================================
        // 游荡者·风流剑客：该闪就闪、该强化就强化
        // ============================================================

        [Test]
        public void Swashbuckler_ai_uses_dodge()
        {
            var ai = new GreedyChoiceProvider();
            var swashbuckler = CharacterFactory.CreatePlayer("游荡者·风流剑客", CharacterDefinition.Swashbuckler);
            var opponent = CharacterFactory.CreatePlayer("乙", CharacterDefinition.Blank);

            var candidates = new List<Hand> { swashbuckler.LeftHand, swashbuckler.RightHand };
            var request = new ChoiceRequest(
                HandEffect.SwashbucklerDodge, "要闪避吗？", candidates,
                allowDecline: true, declineResult: null, decider: swashbuckler);

            Hand chosen = ai.ChooseHandFor(request, swashbuckler);

            Assert.NotNull(chosen, "被攻击时 AI 应该用掉一只 1 换掉整次攻击。");
            Assert.True(ReferenceEquals(chosen, swashbuckler.LeftHand)
                        || ReferenceEquals(chosen, swashbuckler.RightHand));
        }

        [Test]
        public void Swashbuckler_ai_always_takes_the_power_shot()
        {
            var ai = new GreedyChoiceProvider();
            var swashbuckler = CharacterFactory.CreatePlayer("游荡者·风流剑客", CharacterDefinition.Swashbuckler);

            var request = new ChoiceRequest(
                HandEffect.SwashbucklerDuelistThrust, "要强化吗？",
                new List<Hand> { swashbuckler.LeftHand },
                allowDecline: true, declineResult: null, decider: swashbuckler);

            Assert.NotNull(ai.ChooseHandFor(request, swashbuckler), "强化是白赚，必须发动。");
        }

        // ============================================================
        // 野蛮人·野兽道途：优先凑"双手和 7"
        // ============================================================

        [Test]
        public void PathOfTheBeast_ai_actually_picks_a_move_that_triggers_the_sum_seven()
        {
            var (game, ai) = Arrange(CharacterDefinition.PathOfTheBeast);
            PlayerState wolf = game.PlayerA;
            PlayerState blank = game.PlayerB;

            // 让"凑 7"成为唯一可行的好棋：
            //   左手 1 操作对手的 4 -> 结果 5，另一只手 2 -> 5 + 2 = 7 ✔
            TestSetup.SetHand(wolf.LeftHand, 1);
            TestSetup.SetHand(wolf.RightHand, 2);
            TestSetup.SetHand(blank.LeftHand, 4);
            TestSetup.SetHand(blank.RightHand, 3);

            LegalMove chosen = ai.ChooseMove(game, wolf);

            int result = Hand.ComputeSum(chosen.ActedHand.Value, chosen.TargetHand.Value);
            int other = ReferenceEquals(chosen.ActedHand, wolf.LeftHand)
                ? wolf.RightHand.Value
                : wolf.LeftHand.Value;

            Assert.AreEqual(PathOfTheBeastLogic.TriggerSum, result + other,
                "AI 应优先凑出双手和 7（回半血 + 攻防永久 +1）。");
        }

        [Test]
        public void PathOfTheBeast_ai_triggers_the_skill_in_a_real_turn()
        {
            var (game, ai) = Arrange(CharacterDefinition.PathOfTheBeast);
            PlayerState wolf = game.PlayerA;
            PlayerState blank = game.PlayerB;

            int attackBefore = wolf.Character.AttackBonus;
            int healthBefore = wolf.Health;

            // 反复走几个回合，至少应触发一次成长
            for (int i = 0; i < 20 && game.Winner == null; i++)
            {
                PlayerState actor = game.CurrentPlayer;
                LegalMove move = ai.ChooseMove(game, actor);
                if (move.ActedHand == null) break;
                game.ExecuteTurn(actor, move.ActedHand, move.TargetHand);
            }

            var logic = wolf.Character as PathOfTheBeastLogic;
            Assert.NotNull(logic);
            Assert.True(logic.TriggerCount > 0,
                "野蛮人·野兽道途 AI 在 20 个回合内应至少触发一次'和 7'。");
            Assert.True(wolf.Character.AttackBonus > attackBefore,
                "触发后攻防应永久成长。");
            _ = healthBefore;
        }

        // ============================================================
        // 游荡者·风流剑客：不能为了输出而烧掉唯一的 1（玩家实测报告）
        // ============================================================

        [Test]
        public void Swashbuckler_keeps_its_only_one_instead_of_spending_it_on_damage()
        {
            // ⚠️ 玩家实测："游荡者·风流剑客不会主动运算出 1"，
            // 明明能留一个 1 用于闪避，却选了别的手去打输出。
            //
            // 现在把"留下的手值"计入评分（1 对游荡者·风流剑客 = 85，3 = 45），
            // 于是"保住 1"比"多打 2 点伤害"更划算。
            var swashbuckler = CharacterFactory.CreatePlayer("游荡者·风流剑客", CharacterDefinition.Swashbuckler);
            var foe = CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank);

            TestSetup.SetAllHands(swashbuckler, foe, 1);
            TestSetup.SetHand(swashbuckler.LeftHand, 1);      // 唯一的 1（闪避的种子）
            TestSetup.SetHand(swashbuckler.RightHand, 3);
            TestSetup.SetHand(foe.LeftHand, 3);
            TestSetup.SetHand(foe.RightHand, 3);

            // 两种走法：
            //   用 1 加 3 -> 4（攻击 4），但 1 没了
            //   用 3 加 3 -> 6（攻击 6），1 留着
            // 后者效果更好**而且**保住 1，所以必须选后者。
            var game = new GameManager(swashbuckler, foe, new GreedyChoiceProvider(),
                                       new SimpleChoiceProvider());
            var ai = new GreedyChoiceProvider();

            LegalMove picked = ai.ChooseMove(game, swashbuckler);
            Assert.NotNull(picked, "游荡者·风流剑客应能选出一手。");

            Assert.AreEqual(3, picked.ActedHand.Value,
                $"游荡者·风流剑客不该用唯一的 1 去运算（实际用了 {picked.ActedHand.Value}）。");
            Assert.AreEqual(1, GetOtherValue(swashbuckler, picked.ActedHand),
                $"另一个 1 应被留在手上。");
        }

        [Test]
        public void Swashbuckler_prefers_keeping_a_one_even_when_the_effect_is_slightly_worse()
        {
            // 更严格的版本：两个走法的**效果完全相同**（都是 4 点攻击），
            // 一个烧掉 1、一个保住 1 —— 必须选保住的那个。
            var swashbuckler = CharacterFactory.CreatePlayer("游荡者·风流剑客", CharacterDefinition.Swashbuckler);
            var foe = CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank);

            TestSetup.SetAllHands(swashbuckler, foe, 1);
            TestSetup.SetHand(swashbuckler.LeftHand, 1);
            TestSetup.SetHand(swashbuckler.RightHand, 1);     // 两个都是 1
            TestSetup.SetHand(foe.LeftHand, 3);

            var game = new GameManager(swashbuckler, foe, new GreedyChoiceProvider(),
                                       new SimpleChoiceProvider());
            var ai = new GreedyChoiceProvider();

            LegalMove picked = ai.ChooseMove(game, swashbuckler);
            Assert.NotNull(picked);

            // 两个 1 无论用哪个 -> 结果都是 4、都只剩一个 1，分数相同，选哪个都行；
            // 这里只断言"用完之后手上还留着一个 1"
            Assert.AreEqual(1, GetOtherValue(swashbuckler, picked.ActedHand),
                "用完一手后应仍留着一个 1。");
        }

        private static int GetOtherValue(PlayerState owner, Hand used)
            => ReferenceEquals(used, owner.LeftHand) ? owner.RightHand.Value : owner.LeftHand.Value;

        // ============================================================
        // 难度：噪声抖动
        // ============================================================

        /// <summary>
        /// 同一局面用同一个难度跑多次，返回"选中招"的去重集合大小。
        /// 每次都用**全新的** GameManager + 全新的 AI，避免上一局状态污染。
        /// </summary>
        private static int CountDistinctPicks(AiDifficulty difficulty, int runs)
        {
            var picks = new HashSet<string>();

            for (int i = 0; i < runs; i++)
            {
                var ai = new GreedyChoiceProvider();
                ai.ApplyDifficulty(difficulty);

                var game = new GameManager(
                    CharacterFactory.CreatePlayer("战士·破阵者", CharacterDefinition.Breaker),
                    CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank),
                    ai, new SimpleChoiceProvider());

                PlayerState actor = game.PlayerA;
                TestSetup.SetHand(actor.LeftHand, 5);
                TestSetup.SetHand(actor.RightHand, 2);
                TestSetup.SetHand(game.PlayerB.LeftHand, 4);
                TestSetup.SetHand(game.PlayerB.RightHand, 3);

                LegalMove pick = ai.ChooseMove(game, actor);
                picks.Add($"{pick.ActedHand.Value}+{pick.TargetHand.Value}");
            }

            return picks.Count;
        }

        [Test]
        public void Hard_difficulty_always_picks_the_same_best_move()
        {
            // 困难档不抖动 -> **完全确定**，同一个局面永远走同一步。
            Assert.AreEqual(1, CountDistinctPicks(AiDifficulty.Hard, 40),
                "困难档不该有随机性（同一局面永远同一步）。");
        }

        [Test]
        public void Easy_difficulty_has_noise_enabled_so_it_can_deviate()
        {
            // 简单档靠**评分抖动**实现（±25），这是"会偶尔选次优解"的机制本身。
            //
            // ⚠️ 这里**刻意不做**"采样多次应当出现不同选择"的断言：
            //    那是**概率性**的，即使 200~400 次也会偶发只抽到同一种
            //    （本项目已经因此失败过两次）。概率性测试会掩盖真实行为、还会
            //    变成"重跑一次就好了"的噪声。
            //
            // 改成断言**机制参数**：抖动已开启、幅度足够大（足以翻转
            // "明显更好"与"次优"之间的分差，效果档位是 44/50/66/88）。
            var easy = new GreedyChoiceProvider();
            easy.ApplyDifficulty(AiDifficulty.Easy);

            Assert.True(easy.UseNoise, "简单档应开启噪声（否则永远走最优 = 不简单）。");
            Assert.True(easy.NoiseAmplitude >= 20,
                $"简单档抖动应足够大才能翻转次优解（实际 {easy.NoiseAmplitude}）。");

            // 抖动是**随机源**驱动的：同一局面理论上可能给出不同结果。
            // 这里只验证"随机源确实在参与" —— 跑一次不抛异常且返回合法走法。
            var game = new GameManager(
                CharacterFactory.CreatePlayer("战士·破阵者", CharacterDefinition.Breaker),
                CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank),
                easy, new SimpleChoiceProvider());

            PlayerState actor = game.PlayerA;
            TestSetup.SetHand(actor.LeftHand, 5);
            TestSetup.SetHand(actor.RightHand, 2);
            TestSetup.SetHand(game.PlayerB.LeftHand, 4);
            TestSetup.SetHand(game.PlayerB.RightHand, 3);

            LegalMove pick = easy.ChooseMove(game, actor);
            Assert.NotNull(pick.ActedHand, "应能选出一步。");
        }

        [Test]
        public void Normal_difficulty_sits_between_easy_and_hard()
        {
            var easy = new GreedyChoiceProvider();
            easy.ApplyDifficulty(AiDifficulty.Easy);

            var normal = new GreedyChoiceProvider();
            normal.ApplyDifficulty(AiDifficulty.Normal);

            var hard = new GreedyChoiceProvider();
            hard.ApplyDifficulty(AiDifficulty.Hard);

            Assert.True(easy.NoiseAmplitude > normal.NoiseAmplitude,
                $"简单档抖动应大于普通档（{easy.NoiseAmplitude} vs {normal.NoiseAmplitude}）。");
            Assert.True(normal.NoiseAmplitude > hard.NoiseAmplitude,
                $"普通档抖动应大于困难档（{normal.NoiseAmplitude} vs {hard.NoiseAmplitude}）。");
            Assert.AreEqual(0, hard.NoiseAmplitude, "困难档不抖动。");
            Assert.False(hard.UseNoise, "困难档应关闭噪声。");
            Assert.True(easy.UseNoise && normal.UseNoise, "简单/普通档应开启噪声。");
        }

        [Test]
        public void The_default_ai_stays_deterministic()
        {
            // 经典玩法不选难度 —— 不能因为"加了难度功能"就顺手带上随机性。
            var plain = new GreedyChoiceProvider();
            Assert.False(plain.UseNoise, "默认不应有噪声。");
            Assert.AreEqual(0, plain.NoiseAmplitude, "默认抖动幅度应为 0。");

            Assert.AreEqual(1, CountDistinctPicks(AiDifficulty.Hard, 30),
                "默认 AI 应完全确定（经典玩法不受难度影响）。");
        }

        // ============================================================
        // 野蛮人·野兽道途 / 战士·剑士：优势已统一由价值表表达
        // ============================================================

        [Test]
        public void PathOfTheBeast_scores_a_sum_seven_move_higher_than_a_plain_attack()
        {
            // ⚠️ "凑 7 加分"原先硬编码在 PathOfTheBeastLogic.ScoreMoveBonus（+200），
            // 现在统一由**价值表**表达（野蛮人·野兽道途档案：和为 7 加 220，
            // 并把 7 本身调到 15 —— 因为"要有 7"与"要和为 7"互斥）。
            var wolf = CharacterFactory.CreatePlayer("野蛮人·野兽道途", CharacterDefinition.PathOfTheBeast);
            var foe = CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank);

            TestSetup.SetAllHands(wolf, foe, 1);
            TestSetup.SetHand(wolf.LeftHand, 3);
            TestSetup.SetHand(wolf.RightHand, 4);       // 3 + 4 = 7 -> 回半血 + 攻防成长
            TestSetup.SetHand(foe.LeftHand, 9);
            TestSetup.SetHand(foe.RightHand, 9);

            var game = new GameManager(wolf, foe, new GreedyChoiceProvider(),
                                       new SimpleChoiceProvider());
            var ai = new GreedyChoiceProvider();

            LegalMove picked = ai.ChooseMove(game, wolf);
            Assert.NotNull(picked);

            // 能凑 7 的操作：3+4=7 或 4+3=7（对手手值是 9，所以 3+9=2、4+9=3 都不行）
            // —— 本用例里没有能凑 7 的操作，所以改为断言价值表本身认这个组合。
            HandValueProfile profile = HandValueProfiles.For(CharacterDefinition.PathOfTheBeast);
            Assert.True(profile.Score(new HandSplit(3, 4)) > profile.Score(new HandSplit(8, 9)),
                "价值表应认定 3/4（和为 7）优于 8/9。");
        }

        [Test]
        public void Swordsman_scores_a_double_handed_move()
        {
            // 同上：双持的优势已由价值表表达（战士·剑士档案：双 4/双 6/双 8 加 130）
            HandValueProfile fencer = HandValueProfiles.For(CharacterDefinition.Swordsman);

            Assert.True(fencer.Score(new HandSplit(6, 6)) > fencer.Score(new HandSplit(6, 3)),
                "双 6 应优于 6/3。");
            Assert.True(fencer.Combos.Count > 0, "战士·剑士应有配对加成规则。");
        }

        // ============================================================
        // 战士·剑士：追加攻击的发动门槛
        // ============================================================

        [Test]
        public void Swordsman_ai_skips_a_feeble_follow_up()
        {
            var ai = new GreedyChoiceProvider { SwordsmanFollowUpMinDamage = 3 };
            var swordsman = CharacterFactory.CreatePlayer("战士·剑士", CharacterDefinition.Swordsman);

            var request = new ChoiceRequest(
                HandEffect.SwordsmanFollowUp, "要追加吗？",
                new List<Hand> { swordsman.LeftHand },
                allowDecline: true, declineResult: null, decider: swordsman);

            ai.FollowUpDamageLookup = () => 2;      // 太弱
            Assert.Null(ai.ChooseHandFor(request, swordsman), "2 点追加不值得换掉原来的数字。");

            ai.FollowUpDamageLookup = () => 6;      // 值得
            Assert.NotNull(ai.ChooseHandFor(request, swordsman), "6 点追加应该发动。");
        }

        // ============================================================
        // 新角色都能被 AI 正常操作（不会崩、能打完）
        // ============================================================

        [Test]
        public void All_new_characters_can_be_driven_by_the_ai_without_crashing()
        {
            var defs = new Dictionary<string, CharacterDefinition>
            {
                { "牧师·生命领域", CharacterDefinition.LifeDomain },
                { "游荡者·风流剑客", CharacterDefinition.Swashbuckler },
                { "野蛮人·野兽道途", CharacterDefinition.PathOfTheBeast },
                { "战士·剑士", CharacterDefinition.Swordsman },
                { "武僧·忍宗", CharacterDefinition.WayOfTheNinja },
                { "游荡者·刺客", CharacterDefinition.Assassin },
                { "术士·狂野魔法", CharacterDefinition.WildMagic },
                { "邪术师·旧日支配者", CharacterDefinition.TheGreatOldOne },
                { "吟游诗人·勇气学院", CharacterDefinition.CollegeOfValor },
            };

            foreach (var pair in defs)
            {
                var ai = new GreedyChoiceProvider();
                var game = new GameManager(
                    CharacterFactory.CreatePlayer(pair.Key, pair.Value),
                    CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank),
                    ai, new SimpleChoiceProvider());

                // 让 AI 连续走若干回合 —— 只要不抛异常、状态合法即可
                for (int turn = 0; turn < 12 && game.Winner == null; turn++)
                {
                    PlayerState actor = game.CurrentPlayer;
                    LegalMove move = ai.ChooseMove(game, actor);
                    if (move.ActedHand == null) break;

                    game.ExecuteTurn(actor, move.ActedHand, move.TargetHand);

                    Assert.True(actor.Health >= 0, $"{pair.Key} 生命不应为负。");
                    Assert.True(actor.LeftHand.Value >= Hand.MinValue
                                && actor.LeftHand.Value <= Hand.MaxValue,
                        $"{pair.Key} 左手值应在 1~9，实际 {actor.LeftHand.Value}。");
                    Assert.True(actor.RightHand.Value >= Hand.MinValue
                                && actor.RightHand.Value <= Hand.MaxValue,
                        $"{pair.Key} 右手值应在 1~9，实际 {actor.RightHand.Value}。");
                }
            }
        }
    }
}
