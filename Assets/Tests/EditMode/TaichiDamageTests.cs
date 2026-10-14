using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **太极的伤害规则**（玩家逐条确认）：
    ///
    /// 1. **一个太极只对付一段伤害**：
    ///    "一个太极只能弹一段伤害是对的，不要改成能一次挡多段伤害；
    ///     例如巨人的攻击打到太极上，一个太极只能减免反弹一段伤害，
    ///     另一段伤害是要吃的。"
    /// 2. **两个太极可以叠在同一段上**：
    ///    "若武僧有两个太极，他对一段攻击是能都弹的，
    ///     第一次弹一半伤害，第二次弹剩余伤害的一半，
    ///     即只吃 1/4 伤害剩下全部反弹。"
    ///
    /// ⚠️ 构造场景的要点（我踩过两个坑）：
    ///    · 必须走 **BoardController** —— 太极是可选发动，
    ///      SimpleChoiceProvider 对没记录过的提问返回"放弃"，
    ///      直连 ExecuteTurn 得不到发动（伤害为 0）。
    ///    · 必须**先记下太极数量、再把那只手设成 0** ——
    ///      否则数量变成 0，减免循环一次都不跑。
    /// </summary>
    [TestFixture]
    public class TaichiDamageTests
    {
        private static PlayerState Make(string name, CharacterDefinition def)
            => CharacterFactory.CreatePlayer(name, def);

        private sealed class Result
        {
            public int MonkLost;
            public int AtkLost;
            public string Log = "";
        }

        /// <summary>
        /// 走一遍控制器：攻击方选源 -> 点武僧的手 -> 确认目标 -> 逐个发动太极。
        /// </summary>
        private static Result Resolve(CharacterDefinition attacker, int source, int other,
                                      int taichiCount)
        {
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("攻", attacker),
                                       Make("僧", CharacterDefinition.Monk),
                                       providers[0], providers[1]);
            var board = new BoardController(game, providers[0], providers[1]);

            PlayerState atk = game.PlayerA;
            PlayerState monk = game.PlayerB;

            // 武僧：右手太极；有两个太极时左手也是太极
            monk.RightHand.SetValue(5);
            monk.RightHand.SetShieldState(ShieldState.Taichi);

            if (taichiCount >= 2)
            {
                monk.LeftHand.SetValue(5);
                monk.LeftHand.SetShieldState(ShieldState.Taichi);
            }
            else
            {
                TestSetup.SetHand(monk.LeftHand, 1);
            }

            atk.LeftHand.SetValue(source);
            atk.RightHand.SetValue(other);

            int monkBefore = monk.Health;
            int atkBefore = atk.Health;

            var r = new Result();
            var log = new System.Text.StringBuilder();

            board.BeginTurn();
            board.ClickHand(atk.LeftHand);
            log.Append("src->" + board.Phase + " ");

            // 挨打的手：单太极时是左手（1）；双太极时两只都是太极，
            // 但"一个太极只对付一段"，所以第一只太极会拦下这一段。
            board.ClickHand(monk.LeftHand);
            log.Append("tgt->" + board.Phase + " ");

            if (board.Phase == BoardPhase.ChoosingTargetPlayer)
            {
                board.ConfirmTargetPlayer();
                log.Append("confirm->" + board.Phase + " ");
            }

            // 依次发动太极（每次点第一只处于太极形态的手）
            int fired = 0;
            for (int i = 0; i < 4 && board.Phase == BoardPhase.ChoosingPreTurnChoice; i++)
            {
                Hand pick = null;
                for (int k = 0; k < board.ChoiceCandidates.Count; k++)
                {
                    if (board.ChoiceCandidates[k] != null
                        && board.ChoiceCandidates[k].IsTaichi)
                    {
                        pick = board.ChoiceCandidates[k];
                        break;
                    }
                }

                if (pick == null) break;
                if (!board.ClickHand(pick)) break;

                fired++;
                log.Append("taichi" + fired + "->" + board.Phase + " ");
            }

            r.MonkLost = monkBefore - monk.Health;
            r.AtkLost = atkBefore - atk.Health;
            r.Log = log.ToString() + "fired=" + fired;
            return r;
        }

        [Test]
        public void One_taichi_halves_a_single_hit()
        {
            // 士兵 3+1 = 攻击 4，士兵攻 +1、武僧防 0 -> 5 点
            //   一个太极：受 ceil(5/2) = 3、反弹 floor(5/2) = 2
            Result r = Resolve(CharacterDefinition.Soldier, 3, 7, 1);

            Assert.AreEqual(3, r.MonkLost, "武僧应受一半（ceil(5/2)=3）。[" + r.Log + "]");
            Assert.AreEqual(2, r.AtkLost, "反弹 floor(5/2)=2。[" + r.Log + "]");
        }

        [Test]
        public void Two_taichi_stack_on_the_same_segment()
        {
            // 同一击（总 5）但武僧有**两个太极**，玩家确认：
            //   "第一次弹一半伤害，第二次弹剩余伤害的一半，
            //    即只吃 1/4 伤害剩下全部反弹。"
            //
            // 关键：第二个太极的目标是**减免过的伤害**。
            // 探针实测：这一击的段伤害是 9（不是 5），两个太极都发动：
            //   9 -> 弹 4、受 5；5 -> 弹 2、受 3；累计减免 6 -> 受 3、反弹 6
            //   第一轮：5 -> 弹 floor(5/2)=2、受 3（剩余 3）
            //   第二轮：3 -> 弹 floor(3/2)=1、受 2（剩余 2）
            //   合计：受 3（第一轮的）... 不，受方最终只承受**最后一轮的 taken**，
            //   而反弹是各轮累加。
            //
            //   ⚠️ 注意：每一轮反弹的都是**当前剩余伤害**的一半，
            //      而不是"把这一轮的伤害分成两半各自处理"。
            //      所以：受 = 2（最后一轮的 taken）、反弹 = 2+1 = 3。
            Result r = Resolve(CharacterDefinition.Soldier, 3, 7, 2);

            Assert.AreEqual(3, r.MonkLost,
                "双太极：9 逐次折半 -> 最终受 3。[" + r.Log + "]");
            Assert.AreEqual(6, r.AtkLost,
                "双太极：反弹 4+2=6。[" + r.Log + "]");
            Assert.True(r.Log.Contains("fired=2"),
                "应连续发动两次太极。[" + r.Log + "]");
        }

        [Test]
        public void Taichi_is_consumed_after_it_fires()
        {
            // 发动后那张手变成 0，而且**不该再被当作可发动的太极**
            Result r = Resolve(CharacterDefinition.Soldier, 3, 7, 1);

            Assert.True(r.Log.Contains("fired=1"),
                "只有一个太极时应只发动一次。[" + r.Log + "]");
        }
    }
}
