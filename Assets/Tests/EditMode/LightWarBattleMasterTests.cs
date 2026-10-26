using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 2026-10-23 三名新角色：牧师·光明领域 / 牧师·战争领域 / 战士·战斗大师。
    /// 玩家确认的口径：
    ///   · 光明领域攻击**最终恒为 4 点光耀伤害**（无视攻防与倒伏×1.5 等增减益；
    ///     护盾 / 闪避 / 转移 / 太极等防御机制照常）；命中附带 2 回合圣光。
    ///   · 圣光：施加时清除目标其他负面状态与全部抗性（勇气战歌 / 奥术屏障除外），
    ///     存在期间阻止新状态与新抗性附加；目标回合开始受 1 点（不可抵挡）、
    ///     回复减半（向上取整）。
    ///   · 战争领域：被选为攻击目标（闪避 / 盾挡 / 零伤害也算）或发起攻击后
    ///     攻防 +1 可叠加；自己回合结束未发起攻击则清零。
    ///   · 战斗大师：4=挥砍 / 6=钝击 / 8=穿刺；实际造成伤害（&gt;0）后按另一只手
    ///     4/8→流血/跛脚、6→眩晕/倒伏 附加 2 回合（选择型，可放弃）。
    /// </summary>
    [TestFixture]
    public class LightWarBattleMasterTests
    {
        private static GameManager Game(CharacterDefinition a, CharacterDefinition b,
                                        out PlayerState pA, out PlayerState pB,
                                        out SimpleChoiceProvider ca, out SimpleChoiceProvider cb)
        {
            pA = CharacterFactory.CreatePlayer("A", a);
            pB = CharacterFactory.CreatePlayer("B", b);
            ca = new SimpleChoiceProvider();
            cb = new SimpleChoiceProvider();
            return new GameManager(pA, pB, ca, cb);
        }

        /// <summary>双将机制验证对（与 ChampionHeavyStrikeTests 同做法，不校验阵营）。</summary>
        private static PlayerState MechanicsPair(CharacterDefinition primary, CharacterDefinition secondary)
            => new PlayerState("mechanics-only", new DualGeneralBehavior(
                CharacterFactory.CreatePlayer("p", primary).Character,
                CharacterFactory.CreatePlayer("s", secondary).Character));

        [Test]
        public void Light_war_and_battle_master_have_the_confirmed_base_stats()
        {
            Assert.AreEqual(0, CharacterDefinition.LightDomain.AttackBonus);
            Assert.AreEqual(-1, CharacterDefinition.LightDomain.DefenseBonus);
            Assert.AreEqual(-1, CharacterDefinition.WarDomain.AttackBonus);
            Assert.AreEqual(-1, CharacterDefinition.WarDomain.DefenseBonus);
            Assert.AreEqual(0, CharacterDefinition.BattleMaster.AttackBonus);
            Assert.AreEqual(0, CharacterDefinition.BattleMaster.DefenseBonus);
            Assert.AreEqual(CharacterProfession.Find("cleric"), CharacterDefinition.LightDomain.Profession);
            Assert.AreEqual(CharacterProfession.Find("cleric"), CharacterDefinition.WarDomain.Profession);
            Assert.AreEqual(CharacterProfession.Find("fighter"), CharacterDefinition.BattleMaster.Profession);

            PlayerState light = CharacterFactory.CreatePlayer("P", CharacterDefinition.LightDomain);
            PlayerState war = CharacterFactory.CreatePlayer("P", CharacterDefinition.WarDomain);
            PlayerState master = CharacterFactory.CreatePlayer("P", CharacterDefinition.BattleMaster);
            Assert.AreEqual(CharacterTemplate.InfiniteAttackRange,
                ((IAttackRangeProvider)light.Character).BaseAttackRange);
            Assert.AreEqual(CharacterTemplate.InfiniteAttackRange,
                ((IAttackRangeProvider)war.Character).BaseAttackRange);
            Assert.AreEqual(2, ((IAttackRangeProvider)master.Character).BaseAttackRange);
        }

        [Test]
        public void Light_domain_attack_is_fixed_four_radiant_damage_with_holy_light()
        {
            var game = Game(CharacterDefinition.LightDomain, CharacterDefinition.Champion,
                            out var light, out var foe, out _, out _);
            light.LeftHand.SetValue(1);
            light.RightHand.SetValue(8);
            foe.LeftHand.SetValue(7);          // 1 + 7 = 8，本应 8 点伤害

            var result = game.ExecuteTurn(light, light.LeftHand, foe.LeftHand);
            Assert.AreEqual(4, result.DamageDealt, "攻击数字无论几都固定 4 点。");
            Assert.AreEqual(AttackDamageType.Radiant, result.AttackType);
            Assert.IsTrue(foe.HasStatus(StatusEffect.HolyLight));
            Assert.AreEqual(16, foe.Health);
        }

        [Test]
        public void Light_domain_fixed_damage_ignores_stats_and_knocked_down_multiplier()
        {
            var foe = CharacterFactory.CreatePlayer("B",
                new CharacterDefinition("test_holy_defense", "guard", SpecialLogicType.None, defenseBonus: 6));
            foe.ApplyStatus(StatusEffect.KnockedDown, 2);
            var light = CharacterFactory.CreatePlayer("A", CharacterDefinition.LightDomain);
            var game = new GameManager(light, foe, new SimpleChoiceProvider(), new SimpleChoiceProvider());
            light.LeftHand.SetValue(2);
            foe.LeftHand.SetValue(2);          // 2 + 2 = 4

            var result = game.ExecuteTurn(light, light.LeftHand, foe.LeftHand);
            Assert.AreEqual(4, result.DamageDealt, "无视 +6 防御与倒伏×1.5。");
            Assert.AreEqual(16, foe.Health);
            Assert.IsFalse(foe.HasStatus(StatusEffect.KnockedDown), "圣光命中后清掉倒伏。");
        }

        [Test]
        public void Light_domain_blocked_hit_still_applies_holy_light_but_dodge_does_not()
        {
            // 盾挡 = 命中（与技能流血同一口径）
            var game = Game(CharacterDefinition.LightDomain, CharacterDefinition.Champion,
                            out var light, out var shielded, out _, out _);
            shielded.LeftHand.SetValue(5);
            shielded.LeftHand.SetShieldState(ShieldState.Formed);
            light.LeftHand.SetValue(1);        // 1 + 5 = 6

            var blocked = game.ExecuteTurn(light, light.LeftHand, shielded.LeftHand);
            Assert.IsTrue(blocked.WasBlocked);
            Assert.AreEqual(0, blocked.DamageDealt);
            Assert.IsTrue(shielded.HasStatus(StatusEffect.HolyLight), "盾挡也算命中，圣光照样附加。");

            // 闪避 = 不算命中
            var game2 = Game(CharacterDefinition.LightDomain, CharacterDefinition.Swashbuckler,
                             out var light2, out var dodger, out _, out var cb2);
            dodger.LeftHand.SetValue(1);
            light2.LeftHand.SetValue(7);        // 7 + 1 = 8
            cb2.SetChoice(HandEffect.SwashbucklerDodge, dodger.LeftHand);

            var dodged = game2.ExecuteTurn(light2, light2.LeftHand, dodger.LeftHand);
            Assert.IsTrue(dodged.WasDodged);
            Assert.IsFalse(dodger.HasStatus(StatusEffect.HolyLight), "被闪避不附加圣光。");
        }

        [Test]
        public void Holy_light_cleanses_statuses_and_resistances_then_blocks_new_attachments()
        {
            var game = Game(CharacterDefinition.LightDomain, CharacterDefinition.SchoolOfAbjuration,
                            out var light, out var foe, out _, out _);
            foe.ApplyStatus(StatusEffect.Poison, 2);
            foe.ApplyStatus(StatusEffect.Burn, 2);
            foe.AddResistance(AttackDamageType.Fire);
            ((IAbjurationSkill)foe.Character).StoreBarrier(9);
            foe.RightHand.SetValue(5);foe.RightHand.SetShieldState(ShieldState.Formed);
            foe.SetCollegeOfValorOffset(foe, 3, 3);
            light.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(3);          // 1 + 3 = 4

            game.ExecuteTurn(light, light.LeftHand, foe.LeftHand);

            Assert.IsTrue(foe.HasStatus(StatusEffect.HolyLight));
            Assert.IsFalse(foe.HasStatus(StatusEffect.Poison), "圣光清除其他负面状态。");
            Assert.IsFalse(foe.HasStatus(StatusEffect.Burn));
            Assert.IsFalse(foe.HasResistance(AttackDamageType.Fire), "圣光清除全部抗性。");
            Assert.AreEqual(9, ((IAbjurationSkill)foe.Character).BarrierPoints, "奥术屏障不受圣光影响。");
            Assert.AreEqual(3, foe.CollegeOfValorAttackOffset, "勇气战歌不受圣光影响。");

            foe.ApplyStatus(StatusEffect.Blind, 2);
            foe.AddResistance(AttackDamageType.Cold);
            Assert.IsFalse(foe.HasStatus(StatusEffect.Blind), "圣光期间阻止新状态附加。");
            Assert.IsFalse(foe.HasResistance(AttackDamageType.Cold), "圣光期间阻止新抗性附加。");

            foe.ApplyStatus(StatusEffect.HolyLight, 2);
            Assert.AreEqual(2, foe.StatusTurnsLeft(StatusEffect.HolyLight), "重复施加只刷新时长。");
        }

        [Test]
        public void Holy_light_halves_healing_rounding_up_and_deals_one_at_turn_start()
        {
            var game = Game(CharacterDefinition.LightDomain, CharacterDefinition.Champion,
                            out _, out var foe, out _, out _);
            foe.TakeDamage(10);
            foe.ApplyStatus(StatusEffect.HolyLight, 2);

            foe.Heal(9);
            Assert.AreEqual(15, foe.Health, "9 减半向上取整为 5。");
            foe.Heal(1);
            Assert.AreEqual(16, foe.Health, "1 减半向上取整仍为 1。");

            int before = foe.Health;
            game.ResolveStatusTurnStart(foe);
            Assert.AreEqual(before - 1, foe.Health, "回合开始固定扣 1 点，不可抵挡。");
        }

        [Test]
        public void Life_domain_cleanse_removes_holy_light_and_heals_fully()
        {
            var game = Game(CharacterDefinition.LifeDomain, CharacterDefinition.SchoolOfConjuration,
                            out var cleric, out var foe, out var ca, out _);
            foe.TakeDamage(12);
            foe.ApplyStatus(StatusEffect.HolyLight, 2);
            ca.SetChoice(HandEffect.LifeDomainHealTarget, foe.LeftHand);
            cleric.LeftHand.SetValue(8);
            foe.LeftHand.SetValue(1);          // 8 + 1 = 9，回复给 foe

            game.ExecuteTurn(cleric, cleric.LeftHand, foe.LeftHand);
            Assert.AreEqual(17, foe.Health, "先净化圣光再回血，回复量不减半。");
            Assert.IsFalse(foe.HasStatus(StatusEffect.HolyLight));
        }

        [Test]
        public void Taichi_halves_the_fixed_four_and_reflects_two()
        {
            var game = Game(CharacterDefinition.LightDomain, CharacterDefinition.WayOfTheTaiChi,
                            out var light, out var monk, out _, out var cb);
            monk.LeftHand.SetValue(5);
            monk.LeftHand.SetShieldState(ShieldState.Taichi);
            cb.SetChoice(HandEffect.WayOfTheTaiChiTaichi, monk.LeftHand);
            light.LeftHand.SetValue(3);        // 3 + 5 = 8

            var result = game.ExecuteTurn(light, light.LeftHand, monk.LeftHand);
            Assert.AreEqual(2, result.DamageDealt, "太极把固定 4 减半为 2。");
            Assert.AreEqual(18, light.Health, "反弹 2 点给光明领域。");
            Assert.IsTrue(monk.HasStatus(StatusEffect.HolyLight), "太极减免算命中。");
            Assert.AreEqual(0, monk.LeftHand.Value, "太极的 5 变 0。");
        }

        [Test] public void Light_fixed_damage_wins_over_dual_multipliers_in_both_orders()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var a=MechanicsPair(reverse?CharacterDefinition.Champion:CharacterDefinition.LightDomain,
                    reverse?CharacterDefinition.LightDomain:CharacterDefinition.Champion);
                var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
                a.LeftHand.SetValue(4);a.RightHand.SetValue(6);a.ApplyStatus(StatusEffect.Disabled,2);
                var attack=new AttackContext(a,b,a.LeftHand,4);attack.Resolve(null,null);
                Assert.AreEqual(4,attack.AppliedDamage);Assert.IsTrue(b.HasStatus(StatusEffect.HolyLight));
            }
        }
        [Test] public void Light_and_war_do_not_rewrite_status_damage_as_a_skill_attack()
        {
            var g=Game(CharacterDefinition.LightDomain,CharacterDefinition.Champion,out var a,out var b,out _,out _);
            a.ApplyStatus(StatusEffect.Burn,2);g.ResolveStatusTurnEnd(a);
            Assert.AreEqual(17,a.Health);Assert.IsFalse(a.HasStatus(StatusEffect.HolyLight));
            var war=CharacterFactory.CreatePlayer("W",CharacterDefinition.WarDomain);
            var attack=new AttackContext(war,war,war.LeftHand,1){IsStatusDamage=true,IgnoreStatBonuses=true};
            attack.Resolve(null,null);Assert.AreEqual(0,((WarDomainLogic)war.Character).SkillUseCount);
        }

        // ============================================================
        // 牧师·战争领域
        // ============================================================

        [Test] public void War_domain_attacking_no_longer_grows_personal_stats()
        {
            var g=Game(CharacterDefinition.WarDomain,CharacterDefinition.Blank,out var war,out var foe,out _,out _);
            var attack=new AttackContext(war,foe,war.LeftHand,4,g);attack.Resolve(g.ChoiceProviderB,g);
            Assert.AreEqual(3,attack.AppliedDamage);Assert.AreEqual(-1,StatusRules.EffectiveAttackBonus(war));
            var back=new AttackContext(foe,war,foe.LeftHand,4,g){HitCount=2};back.Resolve(g.ChoiceProviderA,g);
            Assert.AreEqual(-1,StatusRules.EffectiveDefenseBonus(war));
        }

        [Test] public void War_domain_two_attack_hands_offer_two_different_blessing_targets()
        {
            var g=Game(CharacterDefinition.WarDomain,CharacterDefinition.Blank,out var war,out var foe,out _,out _);
            war.LeftHand.SetValue(4);war.RightHand.SetValue(8);
            var requests=new System.Collections.Generic.List<WizardChoice>();g.DeferWizardChoice=q=>{requests.Add(q);return true;};
            g.BeginTurn();Assert.AreEqual(1,requests.Count);Assert.True(requests[0].TargetsPlayer);
            requests[0].Choose(war.LeftHand);Assert.AreEqual(2,requests.Count);
            foreach(var candidate in requests[1].Candidates)Assert.False(ReferenceEquals(candidate,war.LeftHand));requests[1].Choose(foe.LeftHand);
            Assert.True(war.HasStatus(StatusEffect.WarBlessing));Assert.True(foe.HasStatus(StatusEffect.WarBlessing));
            Assert.AreEqual(2,((WarDomainLogic)war.Character).SkillUseCount);
        }

        [Test] public void War_domain_being_targeted_does_not_grant_a_blessing()
        {
            var g=Game(CharacterDefinition.Champion,CharacterDefinition.WarDomain,out var a,out var war,out _,out _);
            war.LeftHand.SetValue(5);war.LeftHand.SetShieldState(ShieldState.Formed);
            var hit=new AttackContext(a,war,a.LeftHand,4,g);hit.Resolve(g.ChoiceProviderB,g);
            Assert.True(hit.WasBlocked);Assert.False(war.HasStatus(StatusEffect.WarBlessing));
            Assert.AreEqual(-1,StatusRules.EffectiveDefenseBonus(war));
        }

        [Test] public void War_blessing_expires_after_two_owner_turns_even_without_attack()
        {
            var g=Game(CharacterDefinition.WarDomain,CharacterDefinition.Blank,out var war,out _,out _,out _);
            war.ApplyStatus(StatusEffect.WarBlessing,2,war);Assert.AreEqual(0,StatusRules.EffectiveAttackBonus(war));
            g.ResolveStatusTurnEnd(war);Assert.AreEqual(1,war.StatusTurnsLeft(StatusEffect.WarBlessing));
            g.ResolveStatusTurnEnd(war);Assert.False(war.HasStatus(StatusEffect.WarBlessing));Assert.AreEqual(-1,StatusRules.EffectiveAttackBonus(war));
        }

        [Test]
        public void War_domain_damage_type_splits_by_distance_one()
        {
            var game = Game(CharacterDefinition.WarDomain, CharacterDefinition.Blank,
                            out var war, out var foe, out _, out _);
            war.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(3);          // 1 + 3 = 4，1v1 距离 1
            var near = game.ExecuteTurn(war, war.LeftHand, foe.LeftHand);
            Assert.AreEqual(AttackDamageType.Bludgeoning, near.AttackType, "距离 1 内钝击。");

            var p1 = CharacterFactory.CreatePlayer("w", CharacterDefinition.WarDomain);
            var p2 = CharacterFactory.CreatePlayer("b1", CharacterDefinition.Blank);
            var p3 = CharacterFactory.CreatePlayer("b2", CharacterDefinition.Blank);
            var p4 = CharacterFactory.CreatePlayer("b3", CharacterDefinition.Blank);
            var providers = new IChoiceProvider[]
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game4 = GameManager.Create2v2(new[] { p1, p2, p3, p4 }, providers);
            p1.LeftHand.SetValue(1);
            p3.LeftHand.SetValue(3);          // 1 + 3 = 4；座位 1 与 3 相距 2
            var far = game4.ExecuteTurn(p1, p1.LeftHand, p3.LeftHand);
            Assert.AreEqual(AttackDamageType.Piercing, far.AttackType, "距离 1 外穿刺。");
        }

        [Test] public void War_domain_reset_clears_blessing_and_use_counter()
        {
            var g=Game(CharacterDefinition.WarDomain,CharacterDefinition.Blank,out var war,out _,out _,out _);
            war.LeftHand.SetValue(4);var requests=new System.Collections.Generic.List<WizardChoice>();g.DeferWizardChoice=q=>{requests.Add(q);return true;};
            g.BeginTurn();requests[0].Choose(war.LeftHand);Assert.AreEqual(1,((WarDomainLogic)war.Character).SkillUseCount);
            g.ResetMatch();Assert.False(war.HasStatus(StatusEffect.WarBlessing));Assert.AreEqual(0,((WarDomainLogic)war.Character).SkillUseCount);
        }

        // ============================================================
        // 战士·战斗大师
        // ============================================================

        [Test]
        public void Battle_master_types_attacks_by_number()
        {
            int[] values = { 4, 6, 8 };
            var expected = new[]
            {
                AttackDamageType.Slashing, AttackDamageType.Bludgeoning, AttackDamageType.Piercing,
            };
            for (int i = 0; i < values.Length; i++)
            {
                var a = CharacterFactory.CreatePlayer("A", CharacterDefinition.BattleMaster);
                var b = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
                a.LeftHand.SetValue(values[i]);
                var ctx = new AttackContext(a, b, a.LeftHand, values[i]);
                ctx.Resolve(null, null);
                Assert.AreEqual(expected[i], ctx.AttackType, "数字 " + values[i]);
                Assert.AreEqual(values[i], ctx.AppliedDamage, "攻 0 防 0，伤害等于数字。");
            }
        }

        [Test]
        public void Battle_master_applies_the_chosen_maneuver_by_other_hand_branch()
        {
            var game = Game(CharacterDefinition.BattleMaster, CharacterDefinition.Blank,
                            out var master, out var foe, out _, out _);
            master.LeftHand.SetValue(1);
            master.RightHand.SetValue(4);      // 另一手 4 → 流血/跛脚
            foe.LeftHand.SetValue(3);          // 1 + 3 = 4
            ((BattleMasterLogic)master.Character).SelectManeuver(StatusEffect.Bleed);
            game.ExecuteTurn(master, master.LeftHand, foe.LeftHand);
            Assert.IsTrue(foe.HasStatus(StatusEffect.Bleed));
            Assert.AreEqual(2, foe.StatusTurnsLeft(StatusEffect.Bleed), "持续目标自己的 2 回合。");
            Assert.AreEqual(1, ((ISkillUseCounter)master.Character).SkillUseCount);

            var game2 = Game(CharacterDefinition.BattleMaster, CharacterDefinition.Blank,
                             out var master2, out var foe2, out _, out _);
            master2.LeftHand.SetValue(1);
            master2.RightHand.SetValue(6);    // 另一手 6 → 眩晕/倒伏
            foe2.LeftHand.SetValue(5);        // 1 + 5 = 6
            ((BattleMasterLogic)master2.Character).SelectManeuver(StatusEffect.KnockedDown);
            game2.ExecuteTurn(master2, master2.LeftHand, foe2.LeftHand);
            Assert.IsTrue(foe2.HasStatus(StatusEffect.KnockedDown));
            Assert.IsFalse(foe2.HasStatus(StatusEffect.Bleed), "6 分支没有流血/跛脚。");
        }

        [Test]
        public void Battle_master_without_a_valid_other_hand_applies_nothing()
        {
            var game = Game(CharacterDefinition.BattleMaster, CharacterDefinition.Blank,
                            out var master, out var foe, out _, out _);
            master.LeftHand.SetValue(1);
            master.RightHand.SetValue(5);      // 另一手 5：无招式
            foe.LeftHand.SetValue(3);          // 1 + 3 = 4
            game.ExecuteTurn(master, master.LeftHand, foe.LeftHand);
            Assert.IsFalse(foe.HasStatus(StatusEffect.Bleed));
            Assert.IsFalse(foe.HasStatus(StatusEffect.Limp));
            Assert.AreEqual(0, ((ISkillUseCounter)master.Character).SkillUseCount);
        }

        [Test]
        public void Battle_master_blocked_attack_applies_no_maneuver()
        {
            var game = Game(CharacterDefinition.BattleMaster, CharacterDefinition.Blank,
                            out var master, out var shielded, out _, out _);
            master.LeftHand.SetValue(1);
            master.RightHand.SetValue(4);
            shielded.LeftHand.SetValue(5);
            shielded.LeftHand.SetShieldState(ShieldState.Formed);   // 1 + 5 = 6，被盾挡
            game.ExecuteTurn(master, master.LeftHand, shielded.LeftHand);
            Assert.IsFalse(shielded.HasStatus(StatusEffect.Bleed), "盾挡（0 伤）不算造成伤害。");
            Assert.IsFalse(shielded.HasStatus(StatusEffect.Limp));
            Assert.AreEqual(0, ((ISkillUseCounter)master.Character).SkillUseCount);
        }

        [Test]
        public void Battle_master_decline_and_stale_choices_are_respected()
        {
            var game = Game(CharacterDefinition.BattleMaster, CharacterDefinition.Blank,
                            out var master, out var foe, out _, out _);
            master.LeftHand.SetValue(1);
            master.RightHand.SetValue(6);      // 6 分支
            foe.LeftHand.SetValue(5);          // 1 + 5 = 6
            ((BattleMasterLogic)master.Character).DeclineManeuver();
            game.ExecuteTurn(master, master.LeftHand, foe.LeftHand);
            Assert.AreEqual(0, ((ISkillUseCounter)master.Character).SkillUseCount,
                "明确「不发动」就不附加（与没问过区分）。");

            var game2 = Game(CharacterDefinition.BattleMaster, CharacterDefinition.Blank,
                             out var master2, out var foe2, out _, out _);
            ((BattleMasterLogic)master2.Character).SelectManeuver(StatusEffect.Bleed);   // 4/8 分支的旧选择
            master2.LeftHand.SetValue(1);
            master2.RightHand.SetValue(6);
            foe2.LeftHand.SetValue(5);
            game2.ExecuteTurn(master2, master2.LeftHand, foe2.LeftHand);
            Assert.IsTrue(foe2.HasStatus(StatusEffect.Stunned),
                "过期分支的选择被拒后走 AI 默认（6 分支 → 眩晕）。");
        }

        [Test]
        public void Battle_master_collects_a_request_only_for_attacks_with_valid_other_hand()
        {
            var master = CharacterFactory.CreatePlayer("A", CharacterDefinition.BattleMaster);
            var foe = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var game = new GameManager(master, foe, new SimpleChoiceProvider(), new SimpleChoiceProvider());
            master.LeftHand.SetValue(1);
            master.RightHand.SetValue(4);
            foe.LeftHand.SetValue(3);          // 1 + 3 = 4

            var context = new PreTurnChoiceContext(master, foe, master.LeftHand, foe.LeftHand);
            var requests = new List<ChoiceRequest>();
            ((IPreTurnChoiceProvider)master.Character).CollectPreTurnChoices(context, requests);
            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual(HandEffect.BattleMasterManeuver, requests[0].Effect);
            Assert.AreEqual("附加招式", requests[0].AcceptLabel);
            var options = ((IBattleMasterManeuverChoice)master.Character).ManeuverOptions;
            Assert.AreEqual(2, options.Length);
            Assert.AreEqual(StatusEffect.Bleed, options[0]);
            Assert.AreEqual(StatusEffect.Limp, options[1]);

            // 另一手 5（非 4/6/8）→ 不收集，选项为空
            master.RightHand.SetValue(5);
            requests.Clear();
            ((IPreTurnChoiceProvider)master.Character).CollectPreTurnChoices(context, requests);
            Assert.AreEqual(0, requests.Count);
            Assert.AreEqual(0, ((IBattleMasterManeuverChoice)master.Character).ManeuverOptions.Length);
        }

        [Test]
        public void Battle_master_ai_skips_teammates()
        {
            var a = CharacterFactory.CreatePlayer("A", CharacterDefinition.BattleMaster);
            var b = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var c = CharacterFactory.CreatePlayer("C", CharacterDefinition.Blank);
            var d = CharacterFactory.CreatePlayer("D", CharacterDefinition.Blank);   // A 的队友（座位 4）
            var providers = new IChoiceProvider[]
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = GameManager.Create2v2(new[] { a, b, c, d }, providers);
            a.LeftHand.SetValue(1);
            a.RightHand.SetValue(4);
            d.LeftHand.SetValue(3);            // 1 + 3 = 4，打队友

            game.ExecuteTurn(a, a.LeftHand, d.LeftHand);
            Assert.AreEqual(16, d.Health, "攻击照常造成伤害。");
            Assert.IsFalse(d.HasStatus(StatusEffect.Bleed), "AI 不给队友附加负面状态。");
            Assert.IsFalse(d.HasStatus(StatusEffect.Limp));
            Assert.AreEqual(0, ((ISkillUseCounter)a.Character).SkillUseCount);
        }

        [Test]
        public void Battle_master_human_flow_picks_and_declines_through_the_controller()
        {
            var pa = CharacterFactory.CreatePlayer("A", CharacterDefinition.BattleMaster);
            var pb = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var ca = new SimpleChoiceProvider();
            var cb = new SimpleChoiceProvider();
            var game = new GameManager(pa, pb, ca, cb);
            var board = new BoardController(game, ca, cb);
            pa.LeftHand.SetValue(1);
            pa.RightHand.SetValue(4);
            pb.LeftHand.SetValue(3);            // 1 + 3 = 4

            board.BeginTurn();
            Assert.IsTrue(board.ClickHand(pa.LeftHand));
            Assert.IsTrue(board.ClickHand(pb.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);
            Assert.IsTrue(board.ConfirmTargetPlayer());
            Assert.AreEqual(HandEffect.BattleMasterManeuver, board.PendingEffect);
            Assert.IsFalse(board.AcceptPendingChoice(), "招式必须走专属面板。");
            Assert.AreEqual(2, board.BattleMasterManeuverOptions.Length);
            Assert.IsTrue(board.ChooseBattleMasterManeuver(StatusEffect.Limp));
            Assert.AreEqual(1,game.TurnCount,"选完招式后完成一次行动。");
            Assert.IsTrue(pb.HasStatus(StatusEffect.Limp), "选了跛脚就附加跛脚。");
            Assert.AreEqual(1, ((ISkillUseCounter)pa.Character).SkillUseCount);

            // 「不发动」路径
            var pa2 = CharacterFactory.CreatePlayer("A", CharacterDefinition.BattleMaster);
            var pb2 = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var ca2 = new SimpleChoiceProvider();
            var cb2 = new SimpleChoiceProvider();
            var game2 = new GameManager(pa2, pb2, ca2, cb2);
            var board2 = new BoardController(game2, ca2, cb2);
            pa2.LeftHand.SetValue(1);
            pa2.RightHand.SetValue(4);
            pb2.LeftHand.SetValue(3);
            board2.BeginTurn();
            Assert.IsTrue(board2.ClickHand(pa2.LeftHand));
            Assert.IsTrue(board2.ClickHand(pb2.LeftHand));
            Assert.IsTrue(board2.ConfirmTargetPlayer());
            Assert.AreEqual(HandEffect.BattleMasterManeuver, board2.PendingEffect);
            Assert.IsTrue(board2.DeclinePendingChoice());
            Assert.AreEqual(1,game2.TurnCount,"不附加也只结算一次行动。");
            Assert.IsFalse(pb2.HasStatus(StatusEffect.Bleed));
            Assert.IsFalse(pb2.HasStatus(StatusEffect.Limp));
            Assert.AreEqual(16, pb2.Health, "攻击本身照常造成 4 点。");
        }

        // ============================================================
        // 双将（机制验证对，不校验阵营资格）
        // ============================================================

        [Test]
        public void Dual_parts_keep_the_new_capabilities_in_both_orders()
        {
            foreach (bool reverse in new[] { false, true })
            {
                // 光明领域 + 太极宗：双将的普通攻击仍是固定 4 光耀 + 圣光
                var light = MechanicsPair(
                    reverse ? CharacterDefinition.WayOfTheTaiChi : CharacterDefinition.LightDomain,
                    reverse ? CharacterDefinition.LightDomain : CharacterDefinition.WayOfTheTaiChi);
                var foe = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
                light.LeftHand.SetValue(1);
                foe.LeftHand.SetValue(7);      // 1 + 7 = 8
                var ctx = new AttackContext(light, foe, light.LeftHand, 8);
                ctx.Resolve(null, null);
                Assert.AreEqual(4, ctx.AppliedDamage, "双将普通攻击固定 4。");
                Assert.AreEqual(AttackDamageType.Radiant, ctx.AttackType);
                Assert.IsTrue(foe.HasStatus(StatusEffect.HolyLight));

                // 战争领域部件：双将发起攻击照样叠层
                var war = MechanicsPair(
                    reverse ? CharacterDefinition.Champion : CharacterDefinition.WarDomain,
                    reverse ? CharacterDefinition.WarDomain : CharacterDefinition.Champion);
                var foe2 = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
                war.LeftHand.SetValue(1);
                foe2.LeftHand.SetValue(3);
                var warGame = new GameManager(war, foe2, new SimpleChoiceProvider(), new SimpleChoiceProvider());
                warGame.ExecuteTurn(war, war.LeftHand, foe2.LeftHand);
                Assert.AreEqual(0,
                    CharacterBehaviors.FirstImplementer<WarDomainLogic>(war).SkillUseCount,
                    "战争领域已改为回合开始施恩，普通攻击不再叠层。");

                // 光明领域 + 剑士：剑士追加攻击（特殊攻击）不被改写成 4、也不挂圣光
                var pair = MechanicsPair(
                    reverse ? CharacterDefinition.Swordsman : CharacterDefinition.LightDomain,
                    reverse ? CharacterDefinition.LightDomain : CharacterDefinition.Swordsman);
                var target = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
                var sink = new SimpleChoiceProvider();
                var dualGame = new GameManager(pair, target, sink, new SimpleChoiceProvider());
                pair.LeftHand.SetValue(4);
                pair.RightHand.SetValue(8);
                target.LeftHand.SetValue(4);    // 4 + 4 = 8，另一手也是 8 -> 双 8 追加攻击
                sink.SetChoice(HandEffect.SwordsmanFollowUp, pair.LeftHand);
                var result = dualGame.ExecuteTurn(pair, pair.LeftHand, target.LeftHand);
                Assert.AreEqual(8, result.ExtraAttack.Value.Dealt, "追加攻击保持自身伤害口径（8 点）。");
                Assert.AreEqual(4, result.DamageDealt, "主攻击被光明领域固定为 4。");
                Assert.AreEqual(AttackDamageType.Radiant, result.AttackType);
                Assert.IsTrue(target.HasStatus(StatusEffect.HolyLight), "主攻击照常附带圣光。");
                Assert.AreEqual(8, target.Health, "追加 8 + 主攻击 4。");
            }
        }
    }
}
