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
    /// 白板 / 战士，六个新角色的技能**一个都不会用**。
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
        // 牧师：绝不把 9 回给对手
        // ============================================================

        [Test]
        public void Priest_ai_heals_itself_and_never_the_opponent()
        {
            // 这是修掉的真 bug：旧逻辑按"谁最缺血"选目标，
            // 于是对手更残血时，牧师会把 9 送给对手。
            var ai = new GreedyChoiceProvider();
            var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
            var opponent = CharacterFactory.CreatePlayer("乙", CharacterDefinition.Blank);

            // 自己满血、对手残血 —— 旧逻辑一定会选对手
            TestSetup.SetHealth(priest, 20);
            TestSetup.SetHealth(opponent, 3);

            var candidates = new List<Hand> { priest.LeftHand, priest.RightHand, opponent.LeftHand, opponent.RightHand };
            var request = new ChoiceRequest(
                HandEffect.PriestHealTarget, "回给谁？", candidates,
                allowDecline: false, declineResult: priest.LeftHand, decider: priest);

            Hand chosen = ai.ChooseHandFor(request, priest);

            Assert.True(ReferenceEquals(chosen, priest.LeftHand)
                        || ReferenceEquals(chosen, priest.RightHand),
                "牧师 AI 必须回给自己，不能回给对手。");
        }

        // ============================================================
        // 游侠：该闪就闪、该强化就强化
        // ============================================================

        [Test]
        public void Ranger_ai_uses_dodge()
        {
            var ai = new GreedyChoiceProvider();
            var ranger = CharacterFactory.CreatePlayer("游侠", CharacterDefinition.Ranger);
            var opponent = CharacterFactory.CreatePlayer("乙", CharacterDefinition.Blank);

            var candidates = new List<Hand> { ranger.LeftHand, ranger.RightHand };
            var request = new ChoiceRequest(
                HandEffect.RangerDodge, "要闪避吗？", candidates,
                allowDecline: true, declineResult: null, decider: ranger);

            Hand chosen = ai.ChooseHandFor(request, ranger);

            Assert.NotNull(chosen, "被攻击时 AI 应该用掉一只 1 换掉整次攻击。");
            Assert.True(ReferenceEquals(chosen, ranger.LeftHand)
                        || ReferenceEquals(chosen, ranger.RightHand));
        }

        [Test]
        public void Ranger_ai_always_takes_the_power_shot()
        {
            var ai = new GreedyChoiceProvider();
            var ranger = CharacterFactory.CreatePlayer("游侠", CharacterDefinition.Ranger);

            var request = new ChoiceRequest(
                HandEffect.RangerPowerShot, "要强化吗？",
                new List<Hand> { ranger.LeftHand },
                allowDecline: true, declineResult: null, decider: ranger);

            Assert.NotNull(ai.ChooseHandFor(request, ranger), "强化是白赚，必须发动。");
        }

        // ============================================================
        // 狼人：优先凑"双手和 7"
        // ============================================================

        [Test]
        public void Werewolf_ai_actually_picks_a_move_that_triggers_the_sum_seven()
        {
            var (game, ai) = Arrange(CharacterDefinition.Werewolf);
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

            Assert.AreEqual(WerewolfLogic.TriggerSum, result + other,
                "AI 应优先凑出双手和 7（回半血 + 攻防永久 +1）。");
        }

        [Test]
        public void Werewolf_ai_triggers_the_skill_in_a_real_turn()
        {
            var (game, ai) = Arrange(CharacterDefinition.Werewolf);
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

            var logic = wolf.Character as WerewolfLogic;
            Assert.NotNull(logic);
            Assert.True(logic.TriggerCount > 0,
                "狼人 AI 在 20 个回合内应至少触发一次'和 7'。");
            Assert.True(wolf.Character.AttackBonus > attackBefore,
                "触发后攻防应永久成长。");
            _ = healthBefore;
        }

        [Test]
        public void Werewolf_scores_a_sum_seven_move_higher_than_a_plain_attack()
        {
            // 直接验证角色给出的加分：和为 7 应 > 0，普通攻击应为 0
            var wolf = CharacterFactory.CreatePlayer("狼人", CharacterDefinition.Werewolf);

            var triggering = new MoveValueContext(wolf.LeftHand, 5, wolf.RightHand, 2);   // 5 + 2 = 7
            var plain = new MoveValueContext(wolf.LeftHand, 8, wolf.RightHand, 1);        // 8 + 1 = 9

            var advisor = wolf.Character as IAiMoveAdvisor;
            Assert.NotNull(advisor, "狼人应实现 IAiMoveAdvisor。");
            Assert.True(advisor.ScoreMoveBonus(triggering) > 0, "和为 7 应加分。");
            Assert.AreEqual(0, advisor.ScoreMoveBonus(plain), "普通攻击不加分。");
        }

        // ============================================================
        // 剑客：双持同值有加分
        // ============================================================

        [Test]
        public void Swordsman_scores_a_double_handed_move()
        {
            var swordsman = CharacterFactory.CreatePlayer("剑客", CharacterDefinition.Swordsman);
            var advisor = swordsman.Character as IAiMoveAdvisor;

            Assert.NotNull(advisor, "剑客应实现 IAiMoveAdvisor。");

            // 运算后的值 == 另一只手 -> 触发追加
            Assert.True(advisor.ScoreMoveBonus(
                new MoveValueContext(swordsman.LeftHand, 6, swordsman.RightHand, 6)) > 0);

            // 不等 -> 不触发
            Assert.AreEqual(0, advisor.ScoreMoveBonus(
                new MoveValueContext(swordsman.LeftHand, 6, swordsman.RightHand, 3)));
        }

        // ============================================================
        // 剑客：追加攻击的发动门槛
        // ============================================================

        [Test]
        public void Swordsman_ai_skips_a_feeble_follow_up()
        {
            var ai = new GreedyChoiceProvider { SwordsmanFollowUpMinDamage = 3 };
            var swordsman = CharacterFactory.CreatePlayer("剑客", CharacterDefinition.Swordsman);

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
        // 六个新角色都能被 AI 正常操作（不会崩、能打完）
        // ============================================================

        [Test]
        public void All_new_characters_can_be_driven_by_the_ai_without_crashing()
        {
            var defs = new Dictionary<string, CharacterDefinition>
            {
                { "牧师", CharacterDefinition.Priest },
                { "游侠", CharacterDefinition.Ranger },
                { "狼人", CharacterDefinition.Werewolf },
                { "剑客", CharacterDefinition.Swordsman },
                { "忍者", CharacterDefinition.Ninja },
                { "刺客", CharacterDefinition.Assassin },
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
