using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters
{
    /// <summary>
    /// 角色图鉴：每个角色的完整资料。
    ///
    /// 内容按玩家要求组织：名称（中英文）→ 攻防距离生命 → 技能（一句 + 详细）
    /// → 操作指导 → 如何针对 → 与其他角色的特殊交互。
    ///
    /// ⚠️ 阵营（<see cref="Alignment"/>）是我按角色气质拟定的，
    ///    玩家可以随时调整 —— 改动只在这一张表里。
    /// </summary>
    public static class CharacterCodex
    {
        private static readonly Dictionary<string, CodexEntry> ById =
            new Dictionary<string, CodexEntry>();

        static CharacterCodex()
        {
            Add(CharacterDefinition.SoldierId, "Soldier", Alignment.TrueNeutral,
                shortSkill: "攻防各 +1 的白板基准。",
                playGuide: "没有技能，全靠攻防各 +1 的底子。用它练基本功："
                         + "优先凑 4/6/8 打输出，血量健康时不必急着合盾。",
                counter: "没有可针对的机制，正常对拼即可。"
                       + "注意他的攻防各 +1，换血时你会略亏，尽量用技能换回节奏。",
                interactions: new[]
                {
                    "与咒术师：他的攻击无视护盾与正向防御 —— 士兵的防 +1 会被吃掉。",
                    "与巨人：巨人攻 +2 且两段，士兵的防 +1 吃不消。",
                });

            Add(CharacterDefinition.WarriorId, "Warrior", Alignment.LawfulNeutral,
                shortSkill: "自己的盾碎时可连锁打碎场上一面盾；也可碎盾换无限距离。",
                playGuide: "两个技能都以盾为前提，所以核心是主动合盾。"
                         + "合出盾后：想够到远处的对手就碎盾换射程；"
                         + "自己的盾本来就要碎时，顺手连锁打碎对手的盾最赚。",
                counter: "别让他合成 5（那是他合盾的唯一来源）。"
                       + "给他的数字里 1/2/3/4/5 都要压低，4/6/8 次之。",
                interactions: new[]
                {
                    "与咒术师：咒术师无视护盾，战士的盾与两个技能都会失效。",
                    "与圣武士 / 野蛮人 / 骑士：场上盾越多，连锁破盾的选择越多。",
                });

            Add(CharacterDefinition.CurseweaverId, "Curseweaver", Alignment.ChaoticNeutral,
                shortSkill: "攻击无视护盾，并无视对手的正向防御加成。",
                playGuide: "出手就是收益：4/6/8 打出去没人能靠盾挡住。"
                         + "不必花回合合盾（你自己防 −1，盾的价值也低）。",
                counter: "合盾几乎没用（他不消耗、不破碎你的盾，只是无视它）——"
                       + "所以你的 5 与游侠的闪避都大幅贬值。"
                       + "反过来靠高攻防硬换，或用潜行 / 太极这类非盾防御。",
                interactions: new[]
                {
                    "与武僧：太极对他不减半、不反弹、5 也不消耗。",
                    "与游侠：他的攻击不可闪避。",
                    "与战士：战士的盾与两个技能都失效。",
                    "与圣武士：圣武士的盾不挡他的伤害，但合盾仍会触发冲击。",
                });

            Add(CharacterDefinition.BarbarianId, "Barbarian", Alignment.ChaoticGood,
                shortSkill: "合出盾时可控制对手一只手，盾碎时解锁。",
                playGuide: "合盾不只是防御，更是控制：锁住对手更值钱的那只手。"
                         + "锁手在盾碎时解除，所以算好盾能撑多久。",
                counter: "别让他合成 5。另外注意："
                       + "他的锁手在场上只剩两人时才有额外限制，人多时更凶。",
                interactions: new[]
                {
                    "与巨人：巨人靠另一只手决定伤害，锁住那只手收益最大。",
                    "与咒术师：咒术师无视护盾，但锁手仍然生效。",
                });

            Add(CharacterDefinition.PriestId, "Priest", Alignment.LawfulGood,
                shortSkill: "9 回复（可给别人）；4/6/8 可选当攻击或回复；双 9 再回一次。",
                playGuide: "4/6/8 是二选一，所以它们比别的角色更灵活。"
                         + "危险时才回血，否则全部进攻 —— 只顾回血会打成僵局。"
                         + "2v2 里优先给一队血量最低的人回复。",
                counter: "尽量阻止他合双 9（双 9 = 9 + 9 = 回 18 血）。"
                       + "9 的原料（4+5、3+6、2+7、1+8）都值得压一点。",
                interactions: new[]
                {
                    "与武僧：他的回复可以给别人，但 0 只在武僧手上。",
                    "与巨人：巨人的 9 是两段回复，牧师是一段但可指定目标。",
                });

            Add(CharacterDefinition.RangerId, "Ranger", Alignment.NeutralGood,
                shortSkill: "1 → 2 完全闪避一次攻击；2 → 3 让攻击基础值 ×1.5。",
                playGuide: "1 是最值钱的手牌（能闪避，还能变成 2 再强化）。"
                         + "被攻击时用掉 1 换掉整次攻击永远划算。",
                counter: "别给他 1 与 2 —— 那是他闪避与强化的原料。"
                       + "面对咒术师时他的 1 完全无效（攻击不可闪避）。",
                interactions: new[]
                {
                    "与咒术师：闪避无效。",
                    "与武僧：太极反弹可以被闪避 —— 所以他的 1 反而是应对手段。",
                    "与忍者 / 巨人：多段攻击时，一只 1 只能闪掉其中一段。",
                });

            Add(CharacterDefinition.WerewolfId, "Werewolf", Alignment.ChaoticEvil,
                shortSkill: "运算后双手之和为 7 → 回半血且攻防永久 +1。",
                playGuide: "目标是凑 7（不是手上有 7）。"
                         + "⚠️ 7 对你是死牌：7 + 任何数都回不到 7。"
                         + "所以要留 1/2/3/4 当凑 7 的伙伴。",
                counter: "别给他 7（那是他凑 7 的种子），"
                       + "但更要注意别让他用小数凑到 7 —— 他进化两次就很强。",
                interactions: new[]
                {
                    "与牧师：他回半血，牧师回固定 9 —— 血量上限 25 的狼人更耐打。",
                    "与巨人：两者生命上限都是 25。",
                });

            Add(CharacterDefinition.SwordsmanId, "Swordsman", Alignment.LawfulNeutral,
                shortSkill: "双手相同时，追加一次 4/6/8 的固定伤害。",
                playGuide: "要的是双手相同且为 4/6/8，一手做不出来 ——"
                         + "先用一手把一只手做成 4/6/8，下一手凑齐另一只。"
                         + "追加攻击不吃攻防，所以它是一笔稳定的额外伤害。",
                counter: "别给他 4/6/8（那是他双持的原料）。"
                       + "他血量健康时不必太怕，但被他连续双持会很快崩。",
                interactions: new[]
                {
                    "与咒术师：追加攻击的\"攻击数字视为 0\"，邪术师无法取消它。",
                    "与忍者：忍者拆两段各吃攻防，剑客是一次固定追加 —— 算法不同。",
                });

            Add(CharacterDefinition.NinjaId, "Ninja", Alignment.TrueNeutral,
                shortSkill: "攻击拆成两段、伤害减半，每段各自计算攻防。",
                playGuide: "高值受益更大：8 → 4+4 两段各吃加成；4 → 2+2 最亏"
                         + "（攻 +1 被吃了两次）。所以优先凑 6 与 8。",
                counter: "尽量别给他高值：8 最该挡，其次 6，4 相对无所谓。",
                interactions: new[]
                {
                    "与游侠：两段是两次独立结算，一面盾 / 一只 1 只能挡一段。",
                    "与巨人：巨人的 5 也是两段，但每段伤害取决于另一只手。",
                });

            Add(CharacterDefinition.AssassinId, "Assassin", Alignment.ChaoticNeutral,
                shortSkill: "经运算达成双 7 → 进入潜行：不能被攻击，出潜行 +7 伤害。",
                playGuide: "要的是双 7，一手做不出来 ——"
                         + "先保留一只 7，再用另一只去凑 7。"
                         + "⚠️ 开局直接摆出的双 7 不算潜行，必须经运算达成。",
                counter: "别给他 7（那是潜行的种子）。"
                       + "他潜行期间无法被选中，所以趁他没进潜行时压血线。",
                interactions: new[]
                {
                    "与武僧：潜行中的攻击照样会被太极化解。",
                    "与咒术师：潜行只挡\"被选中\"，取消攻击仍然生效。",
                });

            Add(CharacterDefinition.PaladinId, "Paladin", Alignment.LawfulGood,
                shortSkill: "4/5/6 都合盾，且合盾 / 碎盾时额外打出 2 点冲击。",
                playGuide: "4/5/6 三条路都通向护盾 + 冲击，所以你的选择比谁都多。"
                         + "已经有一面完整的盾时，拿它当运算源去碰出下一面盾最赚"
                         + "（一次运算换一次免费伤害）。",
                counter: "别让他合成 5，4/6 也要压 —— "
                       + "他每合一次盾就白赚一次冲击。",
                interactions: new[]
                {
                    "与咒术师：盾不挡伤害，但合盾仍会触发冲击。",
                    "与战士 / 野蛮人 / 骑士：他们也会合盾，场上盾多时双方技能都更活跃。",
                });

            Add(CharacterDefinition.WarlockId, "Warlock", Alignment.NeutralEvil,
                shortSkill: "手上留着与对手攻击数字相同的牌即可取消该次攻击（那只手 +1，永久）。",
                playGuide: "核心不是\"打出什么\"，而是\"手上留着什么\"。"
                         + "只要能取消敌人的攻击就取消。"
                         + "跳过回合可以把一人的血量改成与自己一致 —— "
                         + "比敌人少 8 以上时才值得，自己血太少时别跳。"
                         + "2v2 里队友处于危险值且自己血多 9 以上，优先拉队友。",
                counter: "别给他覆盖面广的低值（1/2/7 能凑出最多的攻击数字）。"
                       + "或者干脆避开他用攻击数字出手，改用盾 / 回复推进局面。",
                interactions: new[]
                {
                    "与巨人：巨人的 5 是两段攻击、是高价值数字，"
                        + "所以邪术师手上 5 的价值对巨人时更高（取消它收益更大）。",
                    "与剑客：追加攻击\"攻击数字视为 0\"，无法被取消。",
                    "与武僧：太极反弹\"攻击数字视为 0\"，同样无法被取消。",
                });

            Add(CharacterDefinition.ArcaneArcherId, "ArcaneArcher", Alignment.LawfulEvil,
                shortSkill: "攻击距离 3；4 打两段、6 伤害 +2、8 无视护盾。",
                playGuide: "距离 3 让你能打到环上更远的人，先手优势明显。"
                         + "8 是穿透盾牌的答案，4/6 是稳定输出 —— 三者都好用。",
                counter: "与咒术师同构但要弱很多：只有 8 能穿盾，"
                       + "6 会被完全挡下、4 能挡一半。所以你的 5 只小幅贬值。",
                interactions: new[]
                {
                    "与咒术师：咒术师无视全部护盾，魔射手只有 8 能穿。",
                    "与巨人 / 巨人：双方距离不同，巨人只有 1。",
                });

            Add(CharacterDefinition.GamblerId, "Gambler", Alignment.ChaoticNeutral,
                shortSkill: "放弃运算，掷 1d9 直接作为本次行动结果。",
                playGuide: "算不出高价值点数就赌（4/6/8 才是高价值；"
                         + "合盾与纯回复不算）。"
                         + "赌博不需要点他人的手，打谁在结果出来之后再选。"
                         + "上一回合做了正常运算，这一回合强制赌博。",
                counter: "基本不需要针对，因为赌博掷的是 d9、与你的手值无关。"
                       + "唯一要注意的是：他赌博过的下一个回合他会正常运算，"
                       + "那时尽量别让他凑出高价值；"
                       + "而他正常运算过的下一个回合他强制赌博，怎么给都无所谓。",
                interactions: new[]
                {
                    "与所有角色：赌博结果与双方手值完全无关。",
                    "与武僧：赌出 5 时，武僧进入太极形态而非合盾。",
                });

            Add(CharacterDefinition.KnightId, "Knight", Alignment.LawfulGood,
                shortSkill: "自己的盾破碎后，攻击距离与攻击加成永久一起 +1。",
                playGuide: "碎盾是成长，不是损失 —— 碎一次距离变 2、加成变 2。"
                         + "所以主动去打会被破盾的仗，越打越强。",
                counter: "别给他 5（合成 5 就是给他一面可碎的盾），"
                       + "1/2/3/4 这些凑 5 的原料也要压。",
                interactions: new[]
                {
                    "与战士：战士想碎盾换射程，正好帮骑士成长。",
                    "与邪术师：骑士的攻击加成随距离成长，被取消时损失更大。",
                });

            Add(CharacterDefinition.MonkId, "Monk", Alignment.NeutralGood,
                shortSkill: "5 不合盾而进入太极：被攻击时减半并把剩余反弹给攻击方。",
                playGuide: "5 是高价值（太极攻守兼备，而且开局那个 5 就已经是太极）。"
                         + "被攻击时可以选择发动：把 5 变 0、受一半、反弹一半。"
                         + "自己的 0 被他人当加数触碰后会立刻变成 1。",
                counter: "别给他 5。反弹无视距离、可被盾挡、可被闪避，"
                       + "但不能被邪术师取消。"
                       + "用咒术师打他则太极完全失效（不减半、不反弹、5 也不消耗）。",
                interactions: new[]
                {
                    "与咒术师：太极完全失效，而且那张 5 不会被消耗。",
                    "与盗贼：盗贼偷武僧的 5 只会拿到普通盾 / 碎盾（不继承太极）；"
                        + "反之武僧收到 5 会变成太极。盗贼偷武僧的 0 只能偷到 1。",
                    "与邪术师：反弹无法被取消。",
                    "与游侠：反弹可以被闪避。",
                });

            Add(CharacterDefinition.GiantId, "Giant", Alignment.ChaoticEvil,
                shortSkill: "4/6/8 完全无效果、5 不合盾；5 是两段攻击、9 是两段回复。",
                playGuide: "只有 5 与 9 是高价值。"
                         + "打 5 时每段伤害 = 另一只手的点数，"
                         + "所以配一个大数（7/8/9）收益最大；"
                         + "打 9 时同样按另一只手回复。生命上限 25。",
                counter: "别给他 5 与 9；4/6/8 对他完全无效果，"
                       + "所以那三个数字给他也无所谓（算安全牌）。",
                interactions: new[]
                {
                    "与邪术师：巨人打 5 是两段，但攻击数字就是 5 ——"
                        + "邪术师可以用 5 一次性取消两段。",
                    "与武僧：巨人两段攻击会被太极整体减半并反弹。",
                    "与忍者：两者都是多段，但巨人每段伤害取决于另一只手。",
                });

            Add(CharacterDefinition.ThiefId, "Thief", Alignment.NeutralEvil,
                shortSkill: "放弃运算改为偷窃：双方点数互换，原先 ≥ 偷来的才生效。",
                playGuide: "流程是点自己的手 → 点【偷窃】→ 点别人的手。"
                         + "判定看\"原先 ≥ 偷来的\"：所以握着大数字才偷得到好东西。"
                         + "偷到 4/6/8 会再点一次目标才结算伤害。"
                         + "偷到 5 时：原先 ≥ 5 得完整盾，原先 1~4 只得碎盾。"
                         + "他自己的上一次行动偷过，本回合就不能再偷。"
                         + "无限距离、可以偷队友。",
                counter: "别给他 8 / 9（偷到就是高价值攻击 / 回复），4/6 次之。"
                       + "5 只小幅压：被偷的完整盾会变成碎盾，"
                       + "所以递出完整 5 其实不亏。"
                       + "治本的一手是让他握着低数字 —— 那样他根本偷不到高值。",
                interactions: new[]
                {
                    "与武僧：偷他的 5 只得普通盾 / 碎盾（不继承太极）；"
                        + "武僧收到 5 则变成太极；偷他的 0 只能得到 1。",
                    "与骑士 / 战士 / 野蛮人：偷走他们的 5 会让他们的盾"
                        + "换成碎盾，等于顺手破坏了他们的技能前提。",
                    "与所有角色：被偷的人不触发效果、也不形成盾。",
                });
        }

        private static void Add(string id, string englishName, Alignment alignment,
                               string shortSkill, string playGuide, string counter,
                               IReadOnlyList<string> interactions)
        {
            // 详细技能描述直接用数据层里那句长的 —— 保证只有一处真相
            CharacterDefinition def = FindDefinition(id);
            string detail = def != null ? def.Description : string.Empty;

            ById[id] = new CodexEntry(id, englishName, alignment,
                                      shortSkill, detail,
                                      playGuide, counter, interactions);
        }

        private static CharacterDefinition FindDefinition(string id)
        {
            CharacterDefinition[] all = CharacterDefinition.BuiltIn;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].Id == id) return all[i];

            return null;
        }

        /// <summary>按角色 Id 取资料（找不到返回 null）。</summary>
        public static CodexEntry For(string id)
            => id != null && ById.TryGetValue(id, out CodexEntry e) ? e : null;

        /// <summary>按角色定义取资料。</summary>
        public static CodexEntry For(CharacterDefinition def)
            => def != null ? For(def.Id) : null;

        /// <summary>
        /// 按阵营筛选（玩家要求：点守序/中立/混乱 或 善良/中立/邪恶 依次筛选）。
        ///
        /// <paramref name="lawAxis"/> 与 <paramref name="goodAxis"/> 传 null 表示不过滤。
        /// </summary>
        public static List<CharacterDefinition> Filter(string lawAxis, string goodAxis)
        {
            var list = new List<CharacterDefinition>();
            CharacterDefinition[] all = CharacterDefinition.BuiltIn;

            for (int i = 0; i < all.Length; i++)
            {
                CharacterDefinition def = all[i];
                if (def == null) continue;

                CodexEntry e = For(def);
                if (e == null) continue;

                if (!string.IsNullOrEmpty(lawAxis)
                    && Alignments.LawAxisOf(e.Alignment) != lawAxis) continue;
                if (!string.IsNullOrEmpty(goodAxis)
                    && Alignments.GoodAxisOf(e.Alignment) != goodAxis) continue;

                list.Add(def);
            }

            return list;
        }
    }
}
