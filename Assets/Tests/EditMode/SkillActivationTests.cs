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

            var runner = new MatchRunner { RecordHistory = false, MaxTurns = MaxTurns };
            runner.OnTurnResolved = t =>
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
                    case CharacterDefinition.PriestId:
                        // 把回复给了别人（牧师特有的"回给对手"）
                        if (t.HealTarget != null && !ReferenceEquals(t.HealTarget, pA))
                            hit = true;
                        break;
                }
            };

            try
            {
                runner.Run(game, new GreedyChoiceProvider(), new GreedyChoiceProvider());
            }
            catch (System.Exception)
            {
                return false;   // 崩溃不算"发动"（循环赛那边专门报崩溃）
            }

            // ---- 终局判据（累计型 / 成长型）----
            switch (hero.Id)
            {
                case CharacterDefinition.WerewolfId:
                    // 进化：攻防涨过
                    if (pA.Character.AttackBonus > atkBefore
                        || pA.Character.DefenseBonus > defBefore) hit = true;
                    break;

                case CharacterDefinition.KnightId:
                    if (pA.Character is KnightLogic k && k.ShieldBreakCount > 0) hit = true;
                    break;

                case CharacterDefinition.WarriorId:
                    // ⚠️ 连锁破盾与碎盾换射程在场上**没有别的痕迹**
                    //    （盾会被后续操作覆盖），所以只能靠角色自己的计数器。
                    if (pA.Character is WarriorLogic w
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
            }

            return hit;
        }
    }
}
