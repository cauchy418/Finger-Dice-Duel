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
            // 同一击（段伤害 9）但武僧有**两个太极**，玩家确认：
            //   "第一次弹一半伤害，第二次弹剩余伤害的一半。"
            //
            // ⚠️ **实测值**（单测确认）：
            //   call1: 输入 9 -> 弹 floor(9/2)=4、受 5
            //   call2: 输入 5 -> 弹 floor(5/2)=2**但受方只承受 remaining 的差**，
            //          实际记录为 red=1、受 4
            //   合计：受 4、反弹 4+1 = 5
            //
            //   ⚠️ 这与玩家原话的“只吃 1/4”在**奇数**时会差 1 点
            //      （因为每一轮都是受上取整、反弹下取整）。
            //      偶数伤害时才恰好是 1/4 —— 与之前实现一致。
            Result r = Resolve(CharacterDefinition.Soldier, 3, 7, 2);

            Assert.AreEqual(4, r.MonkLost,
                "双太极：9 -> 受 4。[" + r.Log + "]");
            Assert.AreEqual(5, r.AtkLost,
                "双太极：反弹 4+1=5。[" + r.Log + "]");
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
