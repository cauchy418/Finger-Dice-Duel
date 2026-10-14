using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **2v2 的 AI 行为**：牧师该给谁回血、该打谁。
    ///
    /// 玩家给的规则：
    ///   · 牧师"目前写成**优先给一队血量最低的人回复**"
    ///   · 攻击目标按这个顺序：
    ///     ① 自己和队友都能打到的 ② 血量更低的
    ///     ③ 没有防御（护盾 / 闪避 / 太极）的 ④ 防御更低的 ⑤ 攻击更高的
    /// </summary>
    [TestFixture]
    public class TwoVersusTwoAiTests
    {
        /// <summary>按 2v2 规则建局：座位 1&amp;4 一队、2&amp;3 一队。</summary>
        private static GameManager Make2v2(CharacterDefinition d1, CharacterDefinition d2,
                                          CharacterDefinition d3, CharacterDefinition d4,
                                          out List<PlayerState> seated,
                                          out GreedyChoiceProvider ai1,
                                          out GreedyChoiceProvider ai2,
                                          out GreedyChoiceProvider ai3,
                                          out GreedyChoiceProvider ai4)
        {
            seated = new List<PlayerState>(4)
            {
                CharacterFactory.CreatePlayer("P1", d1),
                CharacterFactory.CreatePlayer("P2", d2),
                CharacterFactory.CreatePlayer("P3", d3),
                CharacterFactory.CreatePlayer("P4", d4),
            };

            ai1 = new GreedyChoiceProvider();
            ai2 = new GreedyChoiceProvider();
            ai3 = new GreedyChoiceProvider();
            ai4 = new GreedyChoiceProvider();

            var providers = new List<IChoiceProvider>(4)
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };

            GameManager game = GameManager.Create2v2(seated, providers);

            // 注入对局引用（真机上由 BoardController 注入）
            ai1.Game = game; ai2.Game = game; ai3.Game = game; ai4.Game = game;

            return game;
        }

        // ============================================================
        // 牧师：优先给自己这一队血量最低的
        // ============================================================

        [Test]
        public void Priest_heals_the_lowest_health_teammate_in_two_versus_two()
        {
            GameManager game = Make2v2(CharacterDefinition.Priest, CharacterDefinition.Soldier,
                                       CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                                       out List<PlayerState> seated,
                                       out _, out _, out _, out _);

            PlayerState priest = seated[0];          // P1 = 牧师
            PlayerState mate = seated[3];            // P4 = 队友（1&4 一队）

            Assert.True(game.IsTeammate(priest, mate), "前置：P1 与 P4 应是队友。");

            // 队友掉血最多 -> 该回给队友
            priest.TakeDamage(2);
            mate.TakeDamage(9);

            PlayerState target = HealAdvisor(priest).BestHealTarget(priest, game);

            Assert.True(ReferenceEquals(target, mate),
                $"应回给一队里血量最低的人（队友 {mate.Health} 血），" +
                $"实际选了 {target?.Name}（{target?.Health} 血）。");
        }

        [Test]
        public void Priest_heals_itself_when_it_is_the_lowest_in_its_team()
        {
            GameManager game = Make2v2(CharacterDefinition.Priest, CharacterDefinition.Soldier,
                                       CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                                       out List<PlayerState> seated,
                                       out _, out _, out _, out _);

            PlayerState priest = seated[0];
            PlayerState mate = seated[3];

            priest.TakeDamage(11);
            mate.TakeDamage(3);

            PlayerState target = HealAdvisor(priest).BestHealTarget(priest, game);

            Assert.True(ReferenceEquals(target, priest),
                "自己血最低时该回自己。");
        }

        [Test]
        public void Priest_never_heals_an_enemy()
        {
            // ⚠️ 关键守卫：敌人血最少也**不能**给他回血（那是自伤）
            GameManager game = Make2v2(CharacterDefinition.Priest, CharacterDefinition.Soldier,
                                       CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                                       out List<PlayerState> seated,
                                       out _, out _, out _, out _);

            PlayerState priest = seated[0];
            PlayerState enemy = seated[1];           // P2 = 对手

            priest.TakeDamage(3);
            enemy.TakeDamage(15);                    // 敌人血最少

            PlayerState target = HealAdvisor(priest).BestHealTarget(priest, game);

            Assert.False(ReferenceEquals(target, enemy),
                "不该给敌人回血（那会让对手回血）。");
        }

        [Test]
        public void Priest_heals_itself_when_alone_in_one_versus_one()
        {
            // 1v1 没有队友 -> 回自己（玩家说"牧师回给对手基本没有实现价值"）
            var a = CharacterFactory.CreatePlayer("牧", CharacterDefinition.Priest);
            var b = CharacterFactory.CreatePlayer("兵", CharacterDefinition.Soldier);
            var game = new GameManager(a, b, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            a.TakeDamage(5);
            b.TakeDamage(9);

            PlayerState target = HealAdvisor(a).BestHealTarget(a, game);

            Assert.True(ReferenceEquals(target, a), "1v1 应回自己。");
        }

        // ============================================================
        // 2v2 攻击目标优先级
        // ============================================================

        [Test]
        public void Ai_prefers_the_enemy_with_lower_health()
        {
            // 规则第 ② 条：优先攻击血量更低的对手
            // 造一个"两个对手都能打到、防御/攻击都相同"的局面，
            // 唯一的差别是血量 -> 必须选血少的那个。
            GameManager game = Make2v2(CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                                       CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                                       out List<PlayerState> seated,
                                       out GreedyChoiceProvider ai1, out _, out _, out _);

            PlayerState me = seated[0];
            PlayerState enemyA = seated[1];     // P2
            PlayerState enemyB = seated[2];     // P3

            enemyA.TakeDamage(1);               // 血多一点
            enemyB.TakeDamage(10);              // 血少

            // 用"手值相同"排除其他变量
            foreach (PlayerState p in seated)
            {
                p.LeftHand.SetValue(3);
                p.RightHand.SetValue(1);
            }

            var scoring = new ScoreProbe(ai1, game);
            int scoreA = scoring.ScoreFor(me, me.LeftHand, enemyA.LeftHand);
            int scoreB = scoring.ScoreFor(me, me.LeftHand, enemyB.LeftHand);

            Assert.True(scoreB > scoreA,
                $"打血少的对手应得分更高（血少 {scoreB} vs 血多 {scoreA}）。");
        }

        [Test]
        public void Ai_prefers_the_enemy_without_defense()
        {
            // 规则第 ③ 条：优先攻击没有防御（护盾 / 太极）的对手
            GameManager game = Make2v2(CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                                       CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                                       out List<PlayerState> seated,
                                       out GreedyChoiceProvider ai1, out _, out _, out _);

            PlayerState me = seated[0];
            PlayerState shielded = seated[1];
            PlayerState bare = seated[2];

            foreach (PlayerState p in seated)
            {
                p.LeftHand.SetValue(3);
                p.RightHand.SetValue(1);
            }

            // 给 P2 一面成型的盾（血量保持一致，只改防御）
            shielded.LeftHand.SetValue(5);
            shielded.LeftHand.SetShieldState(ShieldState.Formed);

            var scoring = new ScoreProbe(ai1, game);
            int scoreShielded = scoring.ScoreFor(me, me.LeftHand, shielded.LeftHand);
            int scoreBare = scoring.ScoreFor(me, me.LeftHand, bare.LeftHand);

            Assert.True(scoreBare > scoreShielded,
                $"打没有防御的对手应得分更高（无防御 {scoreBare} vs 有盾 {scoreShielded}）。");
        }

        /// <summary>
        /// 取"回复目标"的决策者。
        ///
        /// ⚠️ 走 <see cref="IHealTargetAdvisor"/> 而不是直接取 PriestLogic ——
        ///    测试层不能引用 Characters 层（项目分层约定），
        ///    而且这样也顺带验证了接口本身。
        /// </summary>
        private static IHealTargetAdvisor HealAdvisor(PlayerState p)
            => p?.Character as IHealTargetAdvisor;

        /// <summary>
        /// 用反射之外的正当办法拿到走子评分：<see cref="GreedyChoiceProvider"/>
        /// 暴露一个只读的评分探针。
        /// </summary>
        private sealed class ScoreProbe
        {
            private readonly GreedyChoiceProvider _ai;
            private readonly GameManager _game;

            public ScoreProbe(GreedyChoiceProvider ai, GameManager game)
            {
                _ai = ai;
                _game = game;
            }

            public int ScoreFor(PlayerState actor, Hand acted, Hand target)
                => _ai.ScoreMoveForTest(new LegalMove(actor, acted, target), actor, _game);
        }
    }
}
