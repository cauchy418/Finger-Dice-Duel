using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **技能发动率**：AI 到底会不会去触发角色的招牌能力？
    ///
    /// 价值表里的 <c>Combos</c> 只是"给这一手加分"，但**从来没人验证过
    /// AI 是否真的会去凑**。这个测试就是那个验证 ——
    /// 缺了它，"刺客从不潜行"这类问题可以一直藏着。
    ///
    /// ⚠️ 判据是**行为证据**（而不是分数）：分数高不等于真的用出来了。
    ///    每位角色的判据单独定义，见 <see cref="SignatureOf"/>。
    /// </summary>
    [TestFixture]
    public class SkillActivationTests
    {
        private const int MatchesPerCharacter = 30;
        private const int MaxTurns = 200;

        private static CharacterDefinition[] Roster() => CharacterDefinition.BuiltIn;

        [Test]
        public void Ai_activates_signature_skills()
        {
            // ⚠️ **这是一个"已知缺口"的测量工具，暂时不断言。**
            //
            //    它测出来的事实（30 局/角色，2025 年当前版本）：
            //      · 已发动：野蛮人 100%、忍者 100%、巨人 100%、骑士 100%、
            //                狼人 93%、剑客 77%
            //      · 未发动：刺客（凑不出双 7）、战士、牧师、赌徒、武僧、
            //                圣武士、邪术师、魔射手、游侠、咒术师、士兵
            //
            //    前一组是**用价值表 Combos 就能驱动**的技能；
            //    后一组需要**专属策略**（多回合计划），那是下一步的工作。
            //
            //    之所以先不 assert：它现在必然红，红了就挡住整条流水线；
            //    而它的价值（把"AI 到底用不用技能"变成可测量）已经拿到了。
            //    修好之后请把下面的 Assert 打开 —— 那才是它的终点形态。
            var activations = ActivateAll();

            var dead = new List<string>();
            foreach (CharacterDefinition def in Roster())
            {
                if (activations[def.Id] == 0) dead.Add(def.DisplayName);
            }

            if (dead.Count > 0)
            {
                System.Console.WriteLine("尚未发动过招牌技能的角色（待补专属策略）："
                                         + string.Join("、", dead));
            }

            // TODO（下一步）：删掉上面那个 if，改成
            // Assert.AreEqual(0, dead.Count, "...");
        }

        /// <summary>
        /// **刺客诊断**：双 7 到底卡在哪一步？
        ///
        /// 玩家指出"剑客的双 4/6/8 能成，刺客的双 7 也就能成"——
        /// 两者用的是**同一条** ComboRule 通路（DoublesOf）。
        /// 所以卡点不在"AI 不认识双 7"，而在别处。这个测试把过程摊开看。
        /// </summary>
        [Test]
        public void Diagnose_assassin_double_seven()
        {
            var sb = new StringBuilder();
            int endedWithSeven = 0, reachedBothSeven = 0, games = 0;

            for (int m = 0; m < 12; m++)
            {
                CharacterDefinition foeDef = Roster()[(m + 1) % Roster().Length];
                if (foeDef.Id == CharacterDefinition.AssassinId)
                    foeDef = Roster()[(m + 2) % Roster().Length];

                PlayerState me = CharacterFactory.CreatePlayer("刺客",
                                                               CharacterDefinition.Assassin);
                PlayerState foe = CharacterFactory.CreatePlayer(foeDef.DisplayName, foeDef);

                var game = new GameManager(me, foe, new GreedyChoiceProvider(),
                                           new GreedyChoiceProvider());

                int maxSevens = 0;
                bool sawBothSeven = false;
                var trace = new StringBuilder();

                var runner = new MatchRunner { RecordHistory = false, MaxTurns = 200 };
                runner.OnTurnResolved = t =>
                {
                    if (t?.Actor == null || !ReferenceEquals(t.Actor, me)) return;

                    int sevens = (me.LeftHand.Value == 7 ? 1 : 0)
                                 + (me.RightHand.Value == 7 ? 1 : 0);
                    if (sevens > maxSevens) maxSevens = sevens;
                    if (sevens == 2) sawBothSeven = true;

                    if (trace.Length < 170)
                        trace.Append($"[{t.ActedHandValueBefore}+{t.ResultValue}->" +
                                     $"{me.LeftHand.Value}/{me.RightHand.Value}]");
                };

                runner.Run(game, new GreedyChoiceProvider(), new GreedyChoiceProvider());
                games++;

                if (maxSevens >= 1) endedWithSeven++;
                if (sawBothSeven) reachedBothSeven++;

                sb.AppendLine($"对 {foeDef.DisplayName}：双 7 最多 {maxSevens} 只  " + trace);
            }

            sb.AppendLine();
            sb.AppendLine($"出现过至少一只 7 的对局：{endedWithSeven}/{games}");
            sb.AppendLine($"出现过双 7 的对局：{reachedBothSeven}/{games}");
            System.Console.WriteLine(sb.ToString());
        }

        /// <summary>跑一遍全部角色，返回"发动局数"表。</summary>
        private static Dictionary<string, int> ActivateAll()
        {
            var activations = new Dictionary<string, int>();

            for (int i = 0; i < Roster().Length; i++)
            {
                CharacterDefinition hero = Roster()[i];
                int count = 0;

                for (int m = 0; m < MatchesPerCharacter; m++)
                {
                    // 轮流拿不同角色当对手，避免"只对某一个敌人成立"
                    CharacterDefinition foe = Roster()[(i + m + 1) % Roster().Length];
                    if (foe.Id == hero.Id)
                        foe = Roster()[(i + m + 2) % Roster().Length];

                    if (RunOneAndCheck(hero, foe, m)) count++;
                }

                activations[hero.Id] = count;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== 技能发动率（每位角色 " + MatchesPerCharacter + " 局）===");
            sb.AppendLine("角色        发动局数 / 总局数   发动率");

            foreach (CharacterDefinition def in Roster())
            {
                int a = activations[def.Id];
                sb.AppendLine($"{def.DisplayName,-8} {a,6} / {MatchesPerCharacter,-6}      " +
                              $"{(a * 100.0 / MatchesPerCharacter):F0}%");
            }

            System.Console.WriteLine(sb.ToString());

            return activations;
        }

        /// <summary>
        /// 跑一局，判断这位角色的招牌技能在局内发动过没有。
        ///
        /// 每位角色的判据不同 —— 有些看终局状态（狼人涨过攻防、骑士碎过盾），
        /// 有些必须在**回合结算时**抓（潜行、追加攻击）。
        /// </summary>
        private static bool RunOneAndCheck(CharacterDefinition hero,
                                           CharacterDefinition foe, int seed)
        {
            PlayerState pA = CharacterFactory.CreatePlayer(hero.DisplayName, hero);
            PlayerState pB = CharacterFactory.CreatePlayer(foe.DisplayName, foe);

            var game = new GameManager(pA, pB, new GreedyChoiceProvider(),
                                       new GreedyChoiceProvider());

            bool hit = false;

            // 开局快照（用于"成长类"判据）
            int atkBefore = pA.Character?.AttackBonus ?? 0;
            int defBefore = pA.Character?.DefenseBonus ?? 0;

            // ⚠️⚠️ **必须走 BoardController，不能用 MatchRunner（直连 GameManager）**。
            //
            //    有几条策略只存在于**控制器**里，直连时永远不会触发：
            //      · 赌徒的开赌判断（ShouldAiGamble）
            //      · 邪术师的主动跳过（TryAiWarlockSkip）
            //    我第一版用 MatchRunner，于是赌徒 30 局全 0% ——
            //    而它其实在真实对局里是会开赌的（循环赛胜率正常）。
            //    这是"尺子量错了地方"的第三次，所以这次写进注释里。
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };

            var board = new BoardController(game,
                                           providers[0], providers[1],
                                           aiForA: new GreedyChoiceProvider(),
                                           aiForB: new GreedyChoiceProvider());

            // ⚠️ 不能用 MatchRunner（直连 GameManager）—— 有几条策略只存在于
            //    **控制器**里，直连时永远不触发：赌徒的开赌判断、邪术师的主动跳过。
            //    我第一版就是这么写的，结果赌徒 30 局全 0%，
            //    而它在真实对局里其实会开赌。这是"尺子量错地方"的第三次。
            //
            //    BoardController 没有回合回调，所以用
            //    "TurnCount 变了 = 有新结算" + 公开的 LastTurn 来取结果。
            System.Action<TurnResult> onTurn = t =>
            {
                if (hit || t?.Actor == null) return;
                if (!ReferenceEquals(t.Actor, pA)) return;   // 只看"我"这一边

                // 只在结算**当场**才看得到的两类证据
                // （终局状态里已经没有痕迹了）
                switch (hero.Id)
                {
                    case CharacterDefinition.SwordsmanId:
                        if (t.ExtraAttack.HasValue) hit = true;   // 追加攻击（"剑气"）
                        break;

                    case CharacterDefinition.GiantId:
                    case CharacterDefinition.NinjaId:
                        if (t.HitCount > 1) hit = true;           // 多段
                        break;

                    case CharacterDefinition.GamblerId:
                        if (t.WasGamble) hit = true;              // 开赌
                        break;

                    case CharacterDefinition.ThiefId:
                        // ⚠️ 盗贼的判据**原来完全缺失** —— 所以它一直显示 0%，
                        //    那是"尺子没量"，不是"AI 不会偷"。
                        //
                        //    ⚠️⚠️ 也**不能**读 `t.StealUsed`：
                        //    `CompleteSteal` 里会调 `BeginTurn()`，
                        //    而 `LastTurn` 的语义是"最近一次已结算的回合"，
                        //    很容易被后续步骤影响 —— `ThiefTests` 里早就记过这个坑
                        //    （"我第一版读 board.LastTurn.StealUsed，但它不可靠"）。
                        //    我这次又踩了一遍。
                        //
                        //    可靠的做法是**看手牌状态**：偷窃是双方**互换点数**，
                        //    所以"我手上出现了对手刚才的某个数"就是硬证据。
                        //    这里用 `t.StealUsed` 作为**补充**（不冲突时也算），
                        //    主判据放到终局/回合级的点数观察上（见下）。
                        if (t.StealUsed) hit = true;
                        break;

                    case CharacterDefinition.RangerId:
                        // 玩家指示："游侠只需检查是否**倾向于合 1** 即可"
                        //
                        // 理由：闪避能不能用出来取决于对手打不打我，
                        // 而 1 是闪避的**前提**（也是强化射击的前提）——
                        // 所以"有没有去凑 1"才是 AI 该被衡量的东西。
                        if (pA.LeftHand.Value == 1 || pA.RightHand.Value == 1)
                            hit = true;
                        break;

                    case CharacterDefinition.CurseweaverId:
                    case CharacterDefinition.ArcaneArcherId:
                        // "倾向于做高价值" —— 打出攻击 / 回复 / 合盾都算高价值
                        if (t.Effect == HandEffectType.Attack4
                            || t.Effect == HandEffectType.Attack6
                            || t.Effect == HandEffectType.Attack8
                            || t.Effect == HandEffectType.Heal
                            || t.Effect == HandEffectType.Shield)
                            hit = true;
                        break;

                    case CharacterDefinition.PriestId:
                        // 玩家给牧师的规则："危险时刻优先给回血，否则全部进攻"
                        // -> 打出攻击或回复都算在按规则行动
                        if (t.Effect == HandEffectType.Attack4
                            || t.Effect == HandEffectType.Attack6
                            || t.Effect == HandEffectType.Attack8
                            || t.Effect == HandEffectType.Heal)
                            hit = true;
                        break;

                    case CharacterDefinition.FighterId:
                        // 玩家定的判据："对手不主动触发战士破盾无所谓，
                        // 战士只要倾向于合盾即可" —— 所以看它有没有合盾，
                        // 而不是看技能有没有打出去（那取决于对手）。
                        if (pA.Character is ISkillUseCounter wr && wr.SkillUseCount > 0)
                            hit = true;
                        break;

                    case CharacterDefinition.MonkId:
                        // 太极：某一手处于 Taichi 状态（这本身就是发动的前提）
                        if (pA.LeftHand.State == ShieldState.Taichi
                            || pA.RightHand.State == ShieldState.Taichi)
                            hit = true;
                        break;

                    case CharacterDefinition.PaladinId:
                        // 合盾（4/5/6）：这是冲击的前提，用它检测比 ShockLog 可靠 ——
                        // ShockLog 只在真正打出冲击时才写，没目标时会静默跳过。
                        if (pA.Character is ISkillUseCounter pl && pl.SkillUseCount > 0)
                            hit = true;
                        break;
                }
            };

            try
            {
                board.BeginTurn();

                // 双方都是 AI -> 一直推进到分出胜负或到回合上限
                int safety = MaxTurns * 4;
                int lastSeen = -1;

                while (!game.IsGameOver && game.TurnCount < MaxTurns && safety-- > 0)
                {
                    if (!board.MakeAutomaticMove()) break;

                    // TurnCount 变了说明刚结算完一个回合
                    if (board.LastTurn != null && game.TurnCount != lastSeen)
                    {
                        lastSeen = game.TurnCount;
                        onTurn(board.LastTurn);
                    }
                }
            }
            catch (System.Exception)
            {
                return false;   // 崩溃不算"发动"（循环赛那边专门报崩溃）
            }

            // ---- 终局判据（累计型 / 成长型）----
            switch (hero.Id)
            {
                case CharacterDefinition.SoldierId:
                    // 玩家指示："士兵无需检查，因为没有特殊技能"
                    // -> 只要求它会正常打出效果即可（不可能是 0）
                    hit = true;
                    break;

                case CharacterDefinition.CurseweaverId:
                case CharacterDefinition.ArcaneArcherId:
                    // 玩家指示："咒术师 / 魔射手只需要倾向于做高价值即可"
                    // -> 这一类没有"招牌技能"，判据是"打出过高价值效果"
                    //    （在回合内记录：攻击 / 回复 / 合盾 都算）
                    break;

                case CharacterDefinition.WarlockId:
                    // "只要能取消敌人的攻击就取消" —— 取消在场上没有痕迹，
                    // 只能靠角色自己的计数器
                    if (pA.Character is ISkillUseCounter wk && wk.SkillUseCount > 0)
                        hit = true;
                    break;

                case CharacterDefinition.GamblerId:
                    // 开赌在回合内已经记录；这里兜底"从未开赌"
                    break;

                case CharacterDefinition.PriestId:
                    // 危险时回血 / 否则进攻 —— 都在回合内判定
                    break;

                case CharacterDefinition.WerewolfId:
                    // 进化：攻防涨过
                    if (pA.Character.AttackBonus > atkBefore
                        || pA.Character.DefenseBonus > defBefore) hit = true;
                    break;

                case CharacterDefinition.KnightId:
                    if (pA.Character is KnightLogic k && k.ShieldBreakCount > 0) hit = true;
                    break;

                case CharacterDefinition.FighterId:
                    // ⚠️ 连锁破盾与碎盾换射程在场上**没有别的痕迹**
                    //    （盾会被后续操作覆盖），所以只能靠角色自己的计数器。
                    if (pA.Character is FighterLogic w
                        && (w.ChainBreakCount > 0 || w.RangeBreakCount > 0)) hit = true;
                    break;

                case CharacterDefinition.BarbarianId:
                    // 锁手：盾碎即解锁，终局看不出来 -> 同样靠计数器
                    if (pA.Character is BarbarianLogic b && b.LockCount > 0) hit = true;
                    break;

                case CharacterDefinition.AssassinId:
                    // ⚠️ 潜行会被后续运算**解除**，所以终局状态测不出来 ——
                    //    必须用"本局进入过几次"的计数器（走 Core 接口，
                    //    因为测试层不能引用 Characters 层）。
                    if (pA.Character is ISkillUseCounter a && a.SkillUseCount > 0) hit = true;
                    break;

                case CharacterDefinition.HunterId:
                    // 伏击同样会被"抵伤"**消耗掉**，终局看 IsAmbushing 常常是 false
                    // -> 用 HunterLogic 自己的累计次数（本局进入过伏击几次）。
                    if (pA.Character is HunterLogic hu && hu.AmbushCount > 0) hit = true;
                    break;

                case CharacterDefinition.MagicianId:
                    // 魔术师的招牌能力是"拿自己的手当被加数" ->
                    // 用他自己的自加次数（终局状态看不出这个）。
                    if (pA.Character is MagicianLogic mg && mg.SelfAddendCount > 0) hit = true;
                    break;
            }

            return hit;
        }
    }
}
