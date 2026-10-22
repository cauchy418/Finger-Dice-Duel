using System.Collections.Generic;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **所有攻击都要有伤害类型**（玩家要求）——逐条钉住每个角色的规则。
    ///
    /// 玩家原话：
    ///   "现在把所有攻击都标记上攻击类型，战士·勇士攻击是挥砍攻击，
    ///    战士·破阵者攻击和碎盾无距离限制攻击都是挥砍，法师·咒法学派是力场伤害，
    ///    野蛮人·守望者道途是钝击伤害，牧师·生命领域对攻击距离为1的是钝击伤害，对攻击距离1以外的是穿刺伤害，
    ///    游荡者·风流剑客普通情况是挥砍，用2强化的攻击是穿刺伤害，野蛮人·野兽道途是挥砍，
    ///    战士·剑士攻击和追加攻击都是挥砍，武僧·忍宗是挥砍，游荡者·刺客是穿刺，
    ///    圣武士·奉献之誓攻击和盾牌冲击都是钝击，邪术师·至高妖精对距离为1是钝击，1以外是穿刺，
    ///    战士·魔射手是穿刺，游荡者·赌徒是挥砍，圣武士·王冠之誓是穿刺，武僧·太极宗是钝击，
    ///    太极反弹的伤害类型由该伤害决定，野蛮人·巨人道途是钝击，游荡者·盗贼是挥砍，
    ///    游侠·幽域追踪者非伏击是挥砍，伏击是穿刺，游荡者·魔术师是穿刺，
    ///    法师·塑能学派附加效果后由附加决定，没附加距离1是钝击，1以外是穿刺。"
    /// </summary>
    [TestFixture]
    public class AttackTypeTests
    {
        private static CharacterDefinition Find(string id)
        {
            for (int i = 0; i < CharacterDefinition.BuiltIn.Length; i++)
                if (CharacterDefinition.BuiltIn[i].Id == id)
                    return CharacterDefinition.BuiltIn[i];
            return null;
        }

        /// <summary>造一个角色 + 一个对手，并返回它的类型（距离可指定）。</summary>
        private static AttackDamageType TypeOf(string id, int distance = 1)
        {
            CharacterDefinition def = Find(id);
            Assert.NotNull(def, "找不到角色：" + id);

            PlayerState self = CharacterFactory.CreatePlayer("自己", def);
            PlayerState foe = CharacterFactory.CreatePlayer("对手", CharacterDefinition.Champion);

            var tmpl = (CharacterTemplate)self.Character;
            tmpl.LastAttackDistance = distance;

            var ctx = new AttackContext(self, foe, self.LeftHand, 4);
            return tmpl.AttackTypeOf(ctx);
        }

        // ---------------- 挥砍（默认）----------------

        [Test]
        public void Slashing_characters()
        {
            // 玩家明确点名的挥砍角色
            Assert.AreEqual(AttackDamageType.Slashing, TypeOf(CharacterDefinition.ChampionId),
                "战士·勇士 = 挥砍");
            Assert.AreEqual(AttackDamageType.Slashing, TypeOf(CharacterDefinition.BreakerId),
                "战士·破阵者（含碎盾无距离限制攻击）= 挥砍");
            Assert.AreEqual(AttackDamageType.Slashing, TypeOf(CharacterDefinition.PathOfTheBeastId),
                "野蛮人·野兽道途 = 挥砍");
            Assert.AreEqual(AttackDamageType.Slashing, TypeOf(CharacterDefinition.SwordsmanId),
                "战士·剑士（含追加攻击）= 挥砍");
            Assert.AreEqual(AttackDamageType.Slashing, TypeOf(CharacterDefinition.WayOfTheNinjaId),
                "武僧·忍宗 = 挥砍");
            Assert.AreEqual(AttackDamageType.Slashing, TypeOf(CharacterDefinition.GamblerId),
                "游荡者·赌徒 = 挥砍");
            Assert.AreEqual(AttackDamageType.Slashing, TypeOf(CharacterDefinition.ThiefId),
                "游荡者·盗贼 = 挥砍");
        }

        // ---------------- 钝击 ----------------

        [Test]
        public void Bludgeoning_characters()
        {
            Assert.AreEqual(AttackDamageType.Bludgeoning, TypeOf(CharacterDefinition.PathOfTheWardenId),
                "野蛮人·守望者道途 = 钝击");
            Assert.AreEqual(AttackDamageType.Bludgeoning, TypeOf(CharacterDefinition.OathOfDevotionId),
                "圣武士·奉献之誓（含盾牌冲击）= 钝击");
            Assert.AreEqual(AttackDamageType.Bludgeoning, TypeOf(CharacterDefinition.WayOfTheTaiChiId),
                "武僧·太极宗 = 钝击（太极反弹继承同类型）");
            Assert.AreEqual(AttackDamageType.Bludgeoning, TypeOf(CharacterDefinition.PathOfTheGiantId),
                "野蛮人·巨人道途 = 钝击");
        }

        // ---------------- 穿刺 ----------------

        [Test]
        public void Piercing_characters()
        {
            Assert.AreEqual(AttackDamageType.Piercing, TypeOf(CharacterDefinition.AssassinId),
                "游荡者·刺客 = 穿刺");
            Assert.AreEqual(AttackDamageType.Piercing, TypeOf(CharacterDefinition.ArcaneArcherId),
                "战士·魔射手 = 穿刺");
            Assert.AreEqual(AttackDamageType.Piercing, TypeOf(CharacterDefinition.OathOfTheCrownId),
                "圣武士·王冠之誓 = 穿刺");
            Assert.AreEqual(AttackDamageType.Piercing, TypeOf(CharacterDefinition.MagicianId),
                "游荡者·魔术师 = 穿刺");
        }

        // ---------------- 力场 ----------------

        [Test]
        public void SchoolOfConjuration_is_force()
        {
            Assert.AreEqual(AttackDamageType.Force, TypeOf(CharacterDefinition.SchoolOfConjurationId),
                "法师·咒法学派 = 力场伤害");
        }

        // ---------------- 按距离分型 ----------------

        [Test]
        public void LifeDomain_is_bludgeoning_near_and_piercing_far()
        {
            Assert.AreEqual(AttackDamageType.Bludgeoning,
                TypeOf(CharacterDefinition.LifeDomainId, 1), "牧师·生命领域 距离 1 = 钝击");
            Assert.AreEqual(AttackDamageType.Piercing,
                TypeOf(CharacterDefinition.LifeDomainId, 2), "牧师·生命领域 距离 2 = 穿刺");
        }

        [Test]
        public void TheArchfey_is_bludgeoning_near_and_piercing_far()
        {
            Assert.AreEqual(AttackDamageType.Bludgeoning,
                TypeOf(CharacterDefinition.TheArchfeyId, 1), "邪术师·至高妖精 距离 1 = 钝击");
            Assert.AreEqual(AttackDamageType.Piercing,
                TypeOf(CharacterDefinition.TheArchfeyId, 2), "邪术师·至高妖精 距离 2 = 穿刺");
        }

        [Test]
        public void SchoolOfEvocation_falls_back_to_distance_when_nothing_is_pending()
        {
            CharacterDefinition def = Find(CharacterDefinition.SchoolOfEvocationId);
            PlayerState self = CharacterFactory.CreatePlayer("法", def);
            PlayerState foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);
            var tmpl = (CharacterTemplate)self.Character;

            // 没有附加类型 -> 按距离
            tmpl.LastAttackDistance = 1;
            Assert.AreEqual(AttackDamageType.Bludgeoning,
                tmpl.AttackTypeOf(new AttackContext(self, foe, self.LeftHand, 4)),
                "法师·塑能学派 无附加 + 距离 1 = 钝击");

            tmpl.LastAttackDistance = 2;
            Assert.AreEqual(AttackDamageType.Piercing,
                tmpl.AttackTypeOf(new AttackContext(self, foe, self.LeftHand, 4)),
                "法师·塑能学派 无附加 + 距离 2 = 穿刺");
        }

        [Test]
        public void SchoolOfEvocation_uses_the_pending_type_when_it_has_one()
        {
            CharacterDefinition def = Find(CharacterDefinition.SchoolOfEvocationId);
            PlayerState self = CharacterFactory.CreatePlayer("法", def);
            PlayerState foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);
            var logic = (SchoolOfEvocationLogic)self.Character;

            // 每个魔法类型都要变成对应的攻击类型
            AttackDamageType[] all = AttackDamageTypes.AllMagic;
            for (int i = 0; i < all.Length; i++)
            {
                AttackDamageType expected = all[i];
                logic.SetPendingDamageType(AttackDamageTypes.ToDamageType(expected));

                // 距离故意设成 2：有附加类型时距离**不该**影响结果
                logic.LastAttackDistance = 2;

                Assert.AreEqual(expected,
                    logic.AttackTypeOf(new AttackContext(self, foe, self.LeftHand, 4)),
                    "法师·塑能学派有附加类型时，由附加的类型决定（" + expected + "）");
            }
        }

        // ---------------- 游荡者·风流剑客 / 游侠·幽域追踪者 ----------------

        [Test]
        public void Swashbuckler_is_slashing_normally_and_piercing_when_empowered()
        {
            CharacterDefinition def = Find(CharacterDefinition.SwashbucklerId);
            PlayerState self = CharacterFactory.CreatePlayer("游", def);
            PlayerState foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);
            var tmpl = (CharacterTemplate)self.Character;

            var normal = new AttackContext(self, foe, self.LeftHand, 4);
            Assert.AreEqual(AttackDamageType.Slashing, tmpl.AttackTypeOf(normal),
                "游荡者·风流剑客 普通 = 挥砍");

            var empowered = new AttackContext(self, foe, self.LeftHand, 4)
            {
                DamageMultiplier = 1.5f,      // 乘风突刺就是这么标记的
            };
            Assert.AreEqual(AttackDamageType.Piercing, tmpl.AttackTypeOf(empowered),
                "游荡者·风流剑客 用 2 强化 = 穿刺");
        }

        [Test]
        public void GloomStalker_is_slashing_normally_and_piercing_when_ambushing()
        {
            CharacterDefinition def = Find(CharacterDefinition.GloomStalkerId);
            PlayerState self = CharacterFactory.CreatePlayer("猎", def);
            PlayerState foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);
            var logic = (CharacterTemplate)self.Character;
            var ctx = new AttackContext(self, foe, self.LeftHand, 4);

            Assert.AreEqual(AttackDamageType.Slashing, logic.AttackTypeOf(ctx),
                "游侠·幽域追踪者 非伏击 = 挥砍");
        }

        // ---------------- 颜色的唯一真相 ----------------

        // ---------------- 太极反弹继承"打来那次攻击"的类型 ----------------

        [Test]
        public void A_preset_attack_type_is_not_overridden_by_the_attacker()
        {
            // 玩家原话：
            //   "武僧·太极宗的太极反弹根据反弹的攻击定，
            //    比如打来的攻击时寒冰伤害，反弹也就是寒冰伤害。"
            //
            // 实现要点：反弹是"内部再开一个 AttackContext"，
            // 那一刻**武僧·太极宗成了攻击方**。若弹回去时再问一次攻击方类型，
            // 问到的是武僧·太极宗自己的**钝击** —— 那就永远弹回去钝击了。
            // 所以 `Resolve` 里加了「已经带了类型就不再推导」的守卫。
            //
            // 这条测试直接钉住那个守卫：把一个【寒冷】预置给
            // **以武僧·太极宗为攻击方**的上下文，结算后它必须还是寒冷。
            PlayerState wayOfTheTaiChi = CharacterFactory.CreatePlayer(
                "僧", Find(CharacterDefinition.WayOfTheTaiChiId));
            PlayerState foe = CharacterFactory.CreatePlayer(
                "敌", Find(CharacterDefinition.ChampionId));

            var game = new GameManager(wayOfTheTaiChi, foe,
                                       new FingerDiceDuel.Assistant.SimpleChoiceProvider(),
                                       new FingerDiceDuel.Assistant.SimpleChoiceProvider());

            var ctx = new AttackContext(wayOfTheTaiChi, foe, wayOfTheTaiChi.LeftHand, 4)
            {
                AttackType = AttackDamageType.Cold,     // 模拟"打来的那次是寒冷"
            };

            ctx.Resolve(game.GetChoiceProvider(foe), game);

            Assert.AreEqual(AttackDamageType.Cold, ctx.AttackType,
                "预置的攻击类型不能被攻击方覆盖 —— 否则太极反弹"
                + "永远会变成武僧·太极宗自己的钝击。");
        }

        [Test]
        public void WayOfTheTaiChi_attacking_normally_is_still_bludgeoning()
        {
            // 反向保证：武僧·太极宗**自己**出手时仍然是钝击
            // （守卫只影响"已经预置类型"的那种上下文，不影响正常攻击）。
            Assert.AreEqual(AttackDamageType.Bludgeoning,
                TypeOf(CharacterDefinition.WayOfTheTaiChiId),
                "武僧·太极宗主动攻击 = 钝击");
        }

        [Test]
        public void Physical_types_share_one_grey_and_magic_use_their_own_colours()        {
            Assert.AreEqual(AttackDamageTypes.PhysicalColor,
                AttackDamageTypes.ColorOf(AttackDamageType.Slashing));
            Assert.AreEqual(AttackDamageTypes.PhysicalColor,
                AttackDamageTypes.ColorOf(AttackDamageType.Bludgeoning));
            Assert.AreEqual(AttackDamageTypes.PhysicalColor,
                AttackDamageTypes.ColorOf(AttackDamageType.Piercing));

            // 魔法：与状态色同源（力场是深红）
            AttackDamageType[] all = AttackDamageTypes.AllMagic;
            for (int i = 0; i < all.Length; i++)
            {
                int c = AttackDamageTypes.ColorOf(all[i]);
                Assert.AreNotEqual(AttackDamageTypes.PhysicalColor, c,
                    "魔法类型不该是物理的灰色：" + all[i]);
            }

            Assert.AreEqual(StatusNames.ForceColor,
                AttackDamageTypes.ColorOf(AttackDamageType.Force),
                "力场用共享的深红");
        }

        [Test]
        public void Every_type_has_a_name_and_an_icon_key()
        {
            var all = new List<AttackDamageType>
            {
                AttackDamageType.Slashing, AttackDamageType.Bludgeoning,
                AttackDamageType.Piercing,
            };
            all.AddRange(AttackDamageTypes.AllMagic);

            for (int i = 0; i < all.Count; i++)
            {
                AttackDamageType t = all[i];
                Assert.IsFalse(string.IsNullOrEmpty(AttackDamageTypes.NameOf(t)),
                    "缺少名称：" + t);
                Assert.IsFalse(string.IsNullOrEmpty(AttackDamageTypes.IconKeyOf(t)),
                    "缺少图标键：" + t);
                Assert.IsTrue(AttackDamageTypes.ColorOf(t) != 0,
                    "缺少颜色：" + t);
            }
        }

        // ---------------- 飘字颜色跟着类型走 ----------------

        [Test]
        public void Damage_and_block_floats_take_the_attack_type_colour()
        {
            PlayerState actor = CharacterFactory.CreatePlayer(
                "甲", CharacterDefinition.Champion);
            PlayerState defender = CharacterFactory.CreatePlayer(
                "乙", CharacterDefinition.Champion);

            var result = new TurnResult
            {
                Actor = actor,
                DamageDealt = 4,
                LandedHits = 1,
                BlockedHits = 1,
            };

            FingerDiceDuel.Assistant.AnimationPlan plan =
                FingerDiceDuel.Assistant.AnimationDirector.Build(
                    result, defender, AttackDamageType.Piercing);
            int expected = AttackDamageTypes.ColorOf(AttackDamageType.Piercing);
            int damageFloats = 0, blockFloats = 0;

            for (int i = 0; i < plan.Floats.Count; i++)
            {
                FingerDiceDuel.Assistant.FloatTextSpec f = plan.Floats[i];
                if (f.Kind == FingerDiceDuel.Assistant.FloatKind.Damage)
                {
                    Assert.AreEqual(expected, f.ColorRgb,
                        "伤害飘字要用本次攻击类型的颜色");
                    damageFloats++;
                }
                else if (f.Kind == FingerDiceDuel.Assistant.FloatKind.Blocked)
                {
                    Assert.AreEqual(expected, f.ColorRgb,
                        "被盾牌抵挡也要用本次攻击类型的颜色");
                    blockFloats++;
                }
            }

            Assert.IsTrue(damageFloats > 0, "应当有伤害飘字");
            Assert.IsTrue(blockFloats > 0, "应当有格挡飘字");
        }
    }
}
