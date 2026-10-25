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
    /// ⚠️ 阵营（<see cref="Alignment"/>）以玩家最新确认的分配为准，
    ///    玩家可以随时调整 —— 改动只在这一张表里。
    ///
    /// <b>写新角色前先读这段</b> —— 玩家明确要求「不要长篇大论」，
    /// 并且已经由 <c>CodexTests.Codex_entries_are_not_over_long</c> 强制守住。
    ///
    /// 篇幅基准（中文字数，实测 20 名角色的数据）：
    ///
    ///   段落        机制简单    有机制     硬上限
    ///   技能一句    8 ~ 15     17 ~ 22    45
    ///   操作指南    30 ~ 38    50 ~ 60    80
    ///   如何针对    19 ~ 40    20 ~ 40    80
    ///   交互        仅保留需要解释的例外或优先级，可为0条
    ///
    /// <b>机制简单</b> = 一句能说完（勇士、风流剑客、王冠之誓、野兽道途、巨人道途…）
    /// <b>有机制</b>   = 要说清触发条件与代价（太极宗、盗贼、至高妖精、幽域追踪者、魔术师…）
    ///
    /// 该留的：按钮怎么点、判定看什么、关键代价、唯一不可挡的那一手。
    /// 优先删：重复解释、举不完的例子、玩家自己能推出来的推论。
    ///
    /// 定这条之前，太极宗的「如何针对」曾写到 167 字（中位数的 5 倍）。
    /// 完整说明见仓库根目录的 <c>CODE-STYLE.md</c>。
    /// </summary>
    public static class CharacterCodex
    {
        private static readonly Dictionary<string, CodexEntry> ById =
            new Dictionary<string, CodexEntry>();

        static CharacterCodex()
        {
            Add(CharacterDefinition.ChampionId, "Champion", Alignment.NeutralGood,
                shortSkill: "十点重击：运算后双手和10，发起攻击的基础伤害×1.5。",
                playGuide: "运算后凑4+6或8+2，再发起攻击，基础4/6/8变6/9/12后计算攻防。自动触发，无额外按钮。",
                counter: "留意他另一只手的点数，避免给出能在攻击后凑10的加数；盾、闪避与取消仍能应对重击。",
                interactions: new[] { "与忍宗双将：十点重击先乘基础伤害，再分段计算攻防。" });

            Add(CharacterDefinition.BreakerId, "Breaker", Alignment.LawfulEvil,
                shortSkill: "盾碎可连锁破盾；主动碎盾攻击附流血2回合，碎另一盾可换∞距离。",
                playGuide: "核心是主动合盾。"
                         + "攻击时若另一只手是完整盾，可碎它换本次攻击距离∞ ——"
                         + "够不到远处对手时这是解法；"
                         + "自己的盾本来就要碎时，顺手连锁打碎对手的盾最赚。",
                counter: "别让他合成 5（那是他合盾的唯一来源）。"
                       + "给他的数字里 1/2/3/4/5 都要压低，4/6/8 次之。",
                interactions: new string[0]);

            Add(CharacterDefinition.SchoolOfConjurationId, "School of Conjuration", Alignment.LawfulNeutral,
                shortSkill: "空间刃穿盾、正防与闪避；王车易位：同手值换座并跳过。",
                playGuide: "优先合8/6/4。王车易位：点击技能后选双手值相同的存活角色（4/6同6/4），换座并跳过；不限距离，取消不耗回合，冻结不能用。",
                counter: "盾、闪避、太极与伏击挡不住空间刃，可用潜行或妖精取消。避免同手值被换座，注意换位后的射程变化。",
                interactions: new[] { "与太极宗：空间刃不减半、不反弹，也不消耗太极手。", "与幽域追踪者：空间刃穿透伏击，但不解除暗置。" });

            Add(CharacterDefinition.PathOfTheWardenId, "Path of the Warden", Alignment.ChaoticGood,
                shortSkill: "合出盾时可控制对手一只手，盾碎时解锁。",
                playGuide: "合盾不只是防御，更是控制：锁住对手更值钱的那只手。"
                         + "锁手在盾碎时解除，所以算好盾能撑多久。",
                counter: "别让他合成 5。另外注意："
                       + "他的锁手在场上只剩两人时才有额外限制，人多时更凶。",
                interactions: new string[0]);

            Add(CharacterDefinition.LifeDomainId, "Life Domain", Alignment.LawfulGood,
                shortSkill: "4/6/8 可转回复；回复先净化再回血；9 可给别人，双9再回一次。",
                playGuide: "4/6/8 是二选一，所以它们比别的角色更灵活。"
                         + "危险时才回血，否则全部进攻 —— 只顾回血会打成僵局。"
                         + "2v2 里优先给一队血量最低的人回复。",
                counter: "尽量阻止他合双 9（双 9 = 9 + 9 = 回 18 血）。"
                       + "9 的原料（4+5、3+6、2+7、1+8）都值得压一点。",
                interactions: new string[0]);

            Add(CharacterDefinition.SwashbucklerId, "Swashbuckler", Alignment.ChaoticGood,
                shortSkill: "1→2闪避；2→3强化基础值×1.5、距离∞，命中附流血2回合。",
                playGuide: "1 是最值钱的手牌（能闪避，还能变成 2 再强化）。"
                         + "被攻击时用掉 1 换掉整次攻击永远划算。"
                         + "乘风突刺同时把距离变成∞ —— 够不到远处对手时这是解法。",
                counter: "别给他 1 与 2 —— 那是他闪避与强化的原料。"
                       + "面对咒法学派时他的 1 完全无效（攻击不可闪避）。",
                interactions: new[] { "与太极宗：反弹可以被闪避；多段攻击每段分别消耗一只1。" });

            Add(CharacterDefinition.PathOfTheBeastId, "Path of the Beast", Alignment.ChaoticEvil,
                shortSkill: "运算后双手之和为 7 → 回半血且攻防永久 +1。",
                playGuide: "目标是凑 7（不是手上有 7）。"
                         + "⚠️ 7 对你是死牌：7 + 任何数都回不到 7。"
                         + "所以要留 1/2/3/4 当凑 7 的伙伴。",
                counter: "别让他两手凑成 7（有 1 别给 6、有 2 别给 5、"
                       + "有 3 别给 4）。"
                       + "他每次进化回半血且攻防 +1，拖久了很难消耗。",
                interactions: new string[0]);

            Add(CharacterDefinition.SwordsmanId, "Swordsman", Alignment.LawfulNeutral,
                shortSkill: "双4/6/8时追加固定伤害，命中附流血2回合。",
                playGuide: "要的是双手相同且为 4/6/8，一手做不出来 ——"
                         + "先用一手把一只手做成 4/6/8，下一手凑齐另一只。"
                         + "追加攻击不吃攻防，所以它是一笔稳定的额外伤害。",
                counter: "别给他 4/6/8（那是他双持的原料）。"
                       + "他血量健康时不必太怕，但被他连续双持会很快崩。",
                interactions: new[] { "与至高妖精：追加攻击没有攻击数字，不能被取消。" });

            Add(CharacterDefinition.WayOfTheNinjaId, "Way of the Ninja", Alignment.TrueNeutral,
                shortSkill: "攻击拆成两段、伤害减半，每段各自计算攻防。",
                playGuide: "拆两段后每段各自加一次攻防，"
                         + "4 与 8 享受的增益是一样的，"
                         + "所以 4 不是死牌，只是 8 本身更值钱（排序 8 > 6 > 4）。",
                counter: "别给他高值（ 8 最该挡，6 次之）。"
                       + "一面盾只能挡下他其中一段，所以自己合盾的价值下降。",
                interactions: new string[0]);

            Add(CharacterDefinition.AssassinId, "Assassin", Alignment.ChaoticEvil,
                shortSkill: "运算达成双7潜行，不能被攻击；出潜行攻击＋7，命中附流血2回合。",
                playGuide: "要的是双 7，一手做不出来 ——"
                         + "先保留一只 7，再用另一只去凑 7。"
                         + "⚠️ 开局直接摆出的双 7 不算潜行，必须经运算达成。",
                counter: "别给他 7（那是潜行的种子）。"
                       + "他潜行期间无法被选中，所以趁他没进潜行时压血线。",
                interactions: new[] { "与咒法学派：潜行不能被选为攻击目标；空间刃不能绕过目标选择。" });

            Add(CharacterDefinition.OathOfDevotionId, "Oath of Devotion", Alignment.LawfulGood,
                shortSkill: "4/5/6 都合盾，且合盾 / 碎盾时额外打出 2 点冲击。",
                playGuide: "选自己的手与加数后，4/5/6 直接合盾，只提示选择冲击目标。"
                         + "用完整盾再合盾，会分别触发碎盾和合盾两次冲击。",
                counter: "别让他合成 5，4/6 也要压 —— "
                       + "他每合一次盾就白赚一次冲击。",
                interactions: new string[0]);

            Add(CharacterDefinition.TheArchfeyId, "The Archfey", Alignment.ChaoticEvil,
                shortSkill: "妖精戏法：手上留着与对手攻击数字相同的牌即可取消该攻击。",
                playGuide: "核心是\"手上留着什么\"：能取消就取消。"
                         + "跳过回合能把一人血量改成与自己一致 —— "
                         + "比对方少 8 以上才值得。",
                counter: "别给他 1/2/7（能凑出最多的攻击数字）；"
                       + "或改用盾 / 回复推进局面。",
                interactions: new[] { "与剑士、太极宗：追加与反弹没有攻击数字，不能取消。" });

            Add(CharacterDefinition.ArcaneArcherId, "Arcane Archer", Alignment.LawfulNeutral,
                shortSkill: "攻击距离 3；4 打两段、6 伤害 +2、8 无视护盾。",
                playGuide: "距离 3 让你能打到环上更远的人，先手优势明显。"
                         + "8 是穿透盾牌的答案，4/6 是稳定输出 —— 三者都好用。",
                counter: "与咒法学派同构但要弱很多：只有 8 能穿盾，"
                       + "6 会被完全挡下、4 能挡一半。所以你的 5 只小幅贬值。",
                interactions: new string[0]);

            Add(CharacterDefinition.GamblerId, "Gambler", Alignment.ChaoticNeutral,
                shortSkill: "放弃运算，掷 1d9 直接作为本次行动结果。",
                playGuide: "算不出高价值就赌（4/6/8 才算）。"
                         + "赌博不需要点他人的手，打谁等结果出来再选；"
                         + "正常运算过一回合后强制赌博。",
                counter: "基本不需要针对（与你的手值无关）；"
                       + "他赌博后的下一回合别让他凑出高价值。",
                interactions: new string[0]);

            Add(CharacterDefinition.OathOfTheCrownId, "Oath of the Crown", Alignment.LawfulGood,
                shortSkill: "王冠威仪：自己的盾破碎后，攻击距离与攻击加成永久一起 +1。",
                playGuide: "碎盾是成长，不是损失 —— 碎一次距离变 2、加成变 2。"
                         + "所以主动去打会被破盾的仗，越打越强。",
                counter: "别给他 5（合成 5 就是给他一面可碎的盾），"
                       + "1/2/3/4 这些凑 5 的原料也要压。",
                interactions: new string[0]);

            Add(CharacterDefinition.WayOfTheTaiChiId, "Way of the Tai Chi", Alignment.NeutralGood,
                shortSkill: "5 不合盾而进入太极：被攻击时减半并反弹。",
                playGuide: "5 是高价值（太极攻守兼备，而且开局那个 5 就已经是太极）。"
                         + "被攻击时可以选择发动：把 5 变 0、受一半、反弹一半。"
                         + "自己的 0 被他人当加数触碰后会立刻变成 1。",
                counter: "别给他 5。一个太极只夹住一段，其余照常吃 ——"
                       + "多段攻击打他反而赚；咒法学派打他则完全失效。",
                interactions: new[] { "与咒法学派：空间刃不触发太极，不消耗5。", "与风流剑客：反弹可以被闪避。" });

            Add(CharacterDefinition.PathOfTheGiantId, "Path of the Giant", Alignment.ChaoticEvil,
                shortSkill: "4/6/8 完全无效果、5 不合盾；5 是两段攻击、9 是两段回复。",
                playGuide: "只有 5 与 9 是高价值。"
                         + "打 5 时每段伤害 = 另一只手的点数，"
                         + "所以配一个大数（7/8/9）收益最大；"
                         + "打 9 时同样按另一只手回复。生命上限 25。",
                counter: "别给他 5 与 9；4/6/8 对他完全无效果，"
                       + "所以那三个数字给他也无所谓（算安全牌）。",
                interactions: new string[0]);

            Add(CharacterDefinition.ThiefId, "Thief", Alignment.NeutralEvil,
                shortSkill: "放弃运算改为偷窃：双方点数互换，原先 ≥ 偷来的才生效。",
                playGuide: "点自己的手 → 点【偷窃】→ 点别人的手。"
                         + "判定看\"原先 ≥ 偷来的\"，所以握着大数字才偷得到好东西；"
                         + "∞距离、可以偷队友，但上一次行动偷过就不能再偷。",
                counter: "别给他 8 / 9，4 / 6 次之。"
                       + "递出完整 5 其实不亏（被偷会变成碎盾）。",
                interactions: new[] { "与太极宗：偷得5只形成普通盾，不继承太极；被偷者的手值变化不触发效果。" });

            // ---- 幽域追踪者（混乱善良）----
            Add(CharacterDefinition.GloomStalkerId, "Gloom Stalker", Alignment.ChaoticGood,
                shortSkill: "幽域伏击：运算后两手同为 1/2/3 时，可暗置那只手进入伏击。",
                playGuide: "点【伏击】把参与运算的那只手暗置：它不能再运算、"
                         + "对手也点不到，但数值可见。"
                         + "伏击期间另一只手照常行动，攻击 +1、距离 3；"
                         + "挨打时恢复那只手并挡下伤害。",
                counter: "数值可见，能预判他的伏击回合；"
                       + "伏击只有一次，逼他提早用掉即可。",
                interactions: new[] { "与咒法学派：空间刃穿透伏击，且不解除暗置。", "双将有太极与伏击时：先太极，再伏击。" });

            // ---- 魔术师（混乱中立）----
            Add(CharacterDefinition.MagicianId, "Magician", Alignment.ChaoticNeutral,
                shortSkill: "可以拿自己的手当被加数；"
                          + "运算出 2/4/6/8 都是攻击（伤害 = 牌面数字）。",
                playGuide: "点自己的一只手当加数，再点自己的另一只手当被加数："
                         + "两只手任选方向，变的是先点的那只。"
                         + "重复点同一只手不生效。"
                         + "攻击牌是 2/4/6/8，伤害就是牌面数字，吃攻防加成 ——"
                         + "所以 8 打得最疼、2 只打 2 点。",
                counter: "别让他凑出 2/4/6/8 —— 那四张对他都是攻击。"
                       + "他攻 0 / 防 −1，身材偏软，直接压血线有效。",
                interactions: new string[0]);

            // ---- 塑能学派（绝对中立）----
            Add(CharacterDefinition.SchoolOfEvocationId, "School of Evocation", Alignment.TrueNeutral,
                shortSkill: "运算出 4/6/8 时可放弃这次攻击，"
                          + "给下一次攻击指定一种伤害类型（10 选 1），命中后挂上负面状态。",
                playGuide: "算出 4/6/8 时问是否放弃攻击。选放弃就挑一种伤害类型，"
                         + "这次白扔，下次打中谁就给谁挂状态（持续 2 回合）。"
                         + "被盾挡或太极减免也算命中；被闪避或取消则作废，得重放弃。"
                         + "连续放弃只留最后选的。",
                counter: "他必须先白扔一回合才能挂状态 —— 趁那一回合压血。"
                       + "腐朽（暗蚀）期间不要指望回血，中毒（毒素）2 点不可抵挡，"
                       + "燃烧（火焰）3 点可以用盾扛。"
                       + "他攻 0 / 防 −1，身材很软，抢在他铺垫完之前解决掉。",
                interactions: new[] { "与太极宗：减免仍算命中，状态照常附加；闪避和取消不算。" });

            // ---- 狂野魔法（混乱中立）----
            Add(CharacterDefinition.WildMagicId, "Wild Magic", Alignment.ChaoticNeutral,
                shortSkill: "每次攻击各 8% 变成 10 种元素之一（共 80%），"
                          + "改写后挂对应状态；余 20% 普攻。",
                playGuide: "能力自动发动，不用选。掷到力场穿盾、无视正向防御、不可闪避。"
                         + "普攻距离 1 内是钝击、1 外是穿刺；多段攻击每段各掷一次。",
                counter: "攻 0 防 −1 但射程∞，躲不掉只能硬扛。"
                       + "闪避或取消能让那一段连属性一起作废；盾与太极挡不住状态。",
                interactions: new string[0]);

            // ---- 旧日支配者（中立邪恶）----
            Add(CharacterDefinition.TheGreatOldOneId, "The Great Old One", Alignment.NeutralEvil,
                shortSkill: "异界疏离：命中的攻击令你与目标之间的距离 +2，持续他两个回合；"
                          + "射程∞。",
                playGuide: "射程∞，站位不用管。打中就把他推开两格，"
                         + "他射程不够时这两个回合根本打不到你。"
                         + "类型按距离：1 内挥砍、1 外穿刺。",
                counter: "攻 0 防 −1，很软，抢血就行。"
                       + "闪避与取消能让这一击连带距离一起作废。"
                       + "被推开后先合盾或回复，别硬打。",
                interactions: new string[0]);

            // ---- 勇气学院（守序善良）----
            Add(CharacterDefinition.CollegeOfValorId, "College of Valor", Alignment.LawfulGood,
                shortSkill: "勇气战歌：本回合受伤或实际回血后，可给攻击范围内 1~3 人"
                          + "加/减攻防 ±(4-人数)。",
                playGuide: "每次行动结算完，若本回合受过伤或回过血就会问你要不要发动。"
                         + "先选增幅/削弱 攻击/防御，再选人数（1 人 ±3、2 人 ±2、3 人 ±1），"
                         + "最后点角色。可以选自己，同一人点两只手只算一次。",
                counter: "持续到你下次发动或你死，所以要盯住他给谁加了什么。"
                       + "他攻 0 防 −1、射程 3，抢血就能逼他先给自己加防御。",
                interactions: new string[0]);

            Add(CharacterDefinition.OathbreakerId, "Oathbreaker", Alignment.LawfulEvil,
                shortSkill: "5 改为 5 点钝击攻击；被攻击时可把目标转给持完整盾者。",
                playGuide: "5 既能进攻，也保留转移资格。被攻击时点持盾角色的转移按钮，或选不发动。",
                counter: "先破掉场上的完整盾；他没有5或场上没有合适持盾者时无法转移。",
                interactions: new[] { "与奉献之誓：转移目标后，奉献之誓仍可代挡。" });

            Add(CharacterDefinition.PathOfTheBerserkerId, "Path of the Berserker", Alignment.NeutralEvil,
                shortSkill: "生命上限 +5；9 改为 9 点挥砍攻击；每少 5 血 +1/+1；造成伤害回一半。",
                playGuide: "血量越低越强，但别把自己送进斩杀线；9 现在是主要输出。",
                counter: "爆发要一次打死，别让他反复用伤害回血。",
                interactions: new string[0]);

            Add(CharacterDefinition.TacticianId, "Tactician", Alignment.ChaoticGood,
                shortSkill: "战局推演：按场上最高与最低体力差 ÷3 获得额外攻防；挥砍伤害。",
                playGuide: "场上血量差越大越强，适合在残局或有人被集火时发力。",
                counter: "尽量把双方血量拉平，削弱他的动态加成。",
                interactions: new string[0]);

            Add(CharacterDefinition.SchoolOfDivinationId, "School of Divination", Alignment.TrueNeutral,
                shortSkill: "7 后开启一轮窗口：他人行动时手牌等于你另一手快照则整次行动无效。",
                playGuide: "先用 7 铺设窗口，再围绕快照数字安排自己的手牌；7 本身不产生攻击。",
                counter: "在窗口内避免留下与预言快照相同的手值，或用额外行动/跳过规避。",
                interactions: new[] { "与死灵学派：回合开始的免费暗蚀不是正常行动，不触发预言窗口。" });

            Add(CharacterDefinition.SchoolOfNecromancyId, "School of Necromancy", Alignment.NeutralEvil,
                shortSkill: "2→1 挡一段；回合初有1免费暗蚀附腐朽；有人死亡可交换0～2只手值。",
                playGuide: "保留2防守。回合开始有可用1自动选攻击目标，也可放弃，不用选手。有人死亡后，依次选自己的手和死者的手交换数字，可提前结束。",
                counter: "力场攻击无视 2 抵挡；用多段攻击消耗多个 2。",
                interactions: new[] { "与防护学派：先选择2抵挡，剩余伤害再由屏障吸收。" });

            Add(CharacterDefinition.SchoolOfAbjurationId, "School of Abjuration", Alignment.LawfulGood,
                shortSkill: "2 赋物理整组或五种元素抗性；攻击数字可存屏障，吸收后清空。",
                playGuide: "算出2先选角色，再选物理整组、雷鸣、强酸、火焰、寒冷或闪电。攻击时可改存屏障；抗性在实际受伤后清除，屏障吸收后清空。",
                counter: "用多段攻击/不同伤害类型先消耗抗性与屏障。",
                interactions: new string[0]);

            // ---- 光明领域（中立善良，2026-10-23）----
            Add(CharacterDefinition.LightDomainId, "Light Domain", Alignment.NeutralGood,
                shortSkill: "攻击恒为 4 点光耀伤害；命中附带 2 回合圣光：净化目标状态、压制其回血。",
                playGuide: "4/6/8均造成4点光耀。命中施加圣光，清除其他状态与抗性；"
                         + "持续两回合，回复减半，回合开始失去1生命。"
                         + "盾挡仍附圣光，闪避和取消不附加。",
                counter: "闪避或取消可避免圣光；护盾只挡伤害，仍会中圣光。"
                       + "用净化解除圣光后再回复。",
                interactions: new[] { "与生命领域：回复先净化圣光，再按全额治疗。", "双将的追加、反弹、免费暗蚀不改成固定4点，也不附圣光。" });

            // ---- 战争领域（守序中立，2026-10-23）----
            Add(CharacterDefinition.WarDomainId, "War Domain", Alignment.LawfulNeutral,
                shortSkill: "受到或发起攻击后攻防 +1（可叠加）；自己回合结束未发起攻击则清零。",
                playGuide: "越战越强，每回合都要出手：被集火挨打同样 +1/+1，别怕挨刀。"
                         + "被冻结、眩晕或锁手让他空过一回合，加成就清零重来。"
                         + "距离 1 内钝击、1 外穿刺。",
                counter: "逼他空过回合（冻结 / 眩晕 / 锁手）即可清空加成；"
                       + "他靠滚雪球，前期就要压制，别放任对拼。",
                interactions: new[] { "与多段攻击：一次攻击只叠一层；反弹是独立攻击，另算一次。", "与至高妖精：被取消的攻击不叠战争狂热。" });

            // ---- 战斗大师（守序中立，2026-10-23）----
            Add(CharacterDefinition.BattleMasterId, "Battle Master", Alignment.LawfulNeutral,
                shortSkill: "4=挥砍、6=钝击、8=穿刺；造成伤害后按另一手附加流血/跛脚或震慑/倒伏。",
                playGuide: "攻击命中后看另一只手：4/8→流血或跛脚、6→震慑或倒伏（各 2 回合，可放弃）。"
                         + "先把手凑成「攻击数字 + 4/6/8」再出手；6 的震慑/倒伏最强。",
                counter: "别让他的另一只手停在 4/6/8；"
                       + "闪避与护盾挡住攻击就完全不给附加机会。",
                interactions: new[] { "与太极宗：减免后仍造成伤害才附招式，完全盾挡不附加。", "多段整次结算一次招式；圣光期间不能附加招式状态。" });

            Add(CharacterDefinition.DraconicBloodlineId, "Draconic Bloodline", Alignment.ChaoticNeutral,
                shortSkill: "首次回合选寒冷/火焰/闪电/毒素/强酸；攻击改该元素，永久抗性，状态3回合。",
                playGuide: "根据对手常见元素/队伍构成选择；选定后攻击和抗性都锁定该元素。",
                counter: "用非该元素攻击；或用高防御/护盾处理其元素攻击。",
                interactions: new[] { "与防护学派：两种抗性可叠加保存。" });

            Add(CharacterDefinition.RangerHunterId, "Hunter", Alignment.NeutralGood,
                shortSkill: "7 在射程内标记；攻击标记目标无视正防御并加已损体力/5，消耗标记。",
                playGuide: "合出7后选择距离3内的一名其他角色，也可不标记；优先标记受伤或高防御的敌人。",
                counter: "被标记后尽快离开其射程或逼他先消耗标记。",
                interactions: new[] { "与多目标：同一次攻击可同时结算原目标与一个标记目标。" });

            Add(CharacterDefinition.WayOfTheOpenHandId, "Way of the Open Hand", Alignment.TrueNeutral,
                shortSkill: "气点：4气共鸣、2气倒伏/震慑；攻击共鸣目标引爆半伤真实伤害；混元体。",
                playGuide: "攻击前选择招式或保留气点；2气倒伏/震慑、4气共鸣，按局面留气。",
                counter: "逼他消耗气点；共鸣期间避免被他再次攻击。",
                interactions: new[] { "与倒伏：共鸣引爆为额外真实伤害，仍吃本次攻击的其他结算。" });

            Add(CharacterDefinition.WayOfTheFourElementsId, "Way of the Four Elements", Alignment.TrueNeutral,
                shortSkill: "气点：2气改元素攻击；受到元素攻击时有气则耗1气获得该元素抗性。",
                playGuide: "攻击前从五种元素中选择或保留气点；利用潮湿/覆霜等联动，留气防守。",
                counter: "用物理或非元素攻击绕开抗性；连续元素攻击耗尽其气。",
                interactions: new[] { "与龙族血脉：元素抗性可叠加保存。" });

            Add(CharacterDefinition.CircleOfTheMoonId, "Circle of the Moon", Alignment.ChaoticGood,
                shortSkill: "双手和10进入荒野：熊4合盾、狼吸血、鹰射程3穿刺、蛇毒伤。",
                playGuide: "回合开始凑10再选形态；熊守、狼攻、鹰远程、蛇上毒。",
                counter: "不让他保持10；熊形态避免浪费攻击打盾；鹰形态留意其6无法攻击。",
                interactions: new[] { "与月亮结社分属不同阵营，当前不能组成双将。" });

            Add(CharacterDefinition.CircleOfTheStarsId, "Circle of the Stars", Alignment.LawfulNeutral,
                shortSkill: "双手和10进入星耀：射手光耀、圣杯额外治疗、巨龙两次加手。",
                playGuide: "根据队形选射手/圣杯/巨龙；巨龙需要两次选被加数。",
                counter: "射手防御偏低；圣杯优先保护伤员；巨龙只在第二次加手后结算一次效果。",
                interactions: new[] { "与月亮结社分属不同阵营，当前不能组成双将。" });

            Add(CharacterDefinition.CollegeOfLoreId,"College of Lore",Alignment.LawfulNeutral,
                shortSkill:"记录1～9；集齐可成长，消耗对应记录可减伤。",
                playGuide:"打出新数自动记录；集齐后选成长或保留。受击时选择清除对应记录减半，或保留。",
                counter:"留意其已有记录；换数字攻击，逼其消耗成长进度。",
                interactions:new[]{"风暴余势只改手值，不触发记录。"});
            Add(CharacterDefinition.StormSorceryId,"Storm Sorcery",Alignment.ChaoticNeutral,
                shortSkill:"选永久雷抗；攻击30%闪电、30%雷鸣，行动后可无效果运算。",
                playGuide:"开局选闪电或雷鸣抗性；行动后可选运算手和加数，也可放弃；额外运算只改手值。",
                counter:"额外运算不能直接打伤害或回血；根据其永久抗性调整元素。",
                interactions:new[]{"额外运算不触发数字效果、碎盾或运算后技能，不额外推进回合与状态。"});
            Add(CharacterDefinition.SchoolOfIllusionId,"School of Illusion",Alignment.ChaoticNeutral,
                shortSkill:"持2可随机改换加数手；双手差2可变小手为2减伤。",
                playGuide:"持2自动50%错位，按实际加数结算；受击时双手差2，可选择小手变2减半。",
                counter:"选加数前比较两只手；压制其2或拆开差2，降低减伤机会。",
                interactions:new[]{"替换手也须是合法加数；隐藏或被封锁的另一手不会被强行借用。"});

            Add(CharacterDefinition.CircleOfTheLandId, "Circle of the Land", Alignment.TrueNeutral,
                shortSkill: "结束封一手禁作加数至下回合；3可束缚2回合。",
                playGuide: "3后先选束缚对象或不发动；回合结束再选一只手封锁，可选自己。封锁不影响该手主动运算。",
                counter: "封锁只禁加数，可用该手主动运算；束缚则随机限制运算手。",
                interactions: new[] { "多人封锁同手：各自持续，直到各来源下回合开始解除。" });

            Add(CharacterDefinition.OathOfTheAncientsId, "Oath of the Ancients", Alignment.NeutralGood,
                shortSkill: "盾碎可束缚射程内角色；受伤后按血量分支回复2。",
                playGuide: "主动或被动盾碎后选角色或不发动；受伤后≥10选射程内他人回复2，低于10自动自愈2。",
                counter: "利用射程隔开治疗对象；碎盾会给他控制机会。",
                interactions: new[] { "虚假生命：只扣虚假生命不触发庇护；致死不自愈。" });

            Add(CharacterDefinition.TheFiendId, "The Fiend", Alignment.LawfulEvil,
                shortSkill: "溢出治疗存10点虚假生命；可耗尽令目标失能/恐慌。",
                playGuide: "攻击时选失能、恐慌或保留；虚假生命先吸伤。造成伤害后目标半血以下（含击杀）回复3。",
                counter: "少量伤害先消耗其防护；用腐朽阻止回复与溢出转化。",
                interactions: new[] { "满血治疗仍可转化溢出；虚假生命不被净化或圣光移除。" });

            Add(CharacterDefinition.SchoolOfTransmutationId,"School of Transmutation",Alignment.ChaoticNeutral,
                shortSkill:"结束可将同数角色变羊；独立10生命、距离1、无原技能。",playGuide:"回合结束选无序匹配双手的角色或不发动，可选自己；变羊两回合，形态血池被击破也解除。",counter:"改变自己的手值避开匹配；打破羊形态时溢伤仍会扣原体力。",interactions:new[]{"羊形态保留原状态计时、暂停原技能；解除恢复原生命。"});
            Add(CharacterDefinition.SchoolOfEnchantmentId,"School of Enchantment",Alignment.NeutralEvil,
                shortSkill:"伤害后可施加失能或魅惑；之后受伤解除。",playGuide:"整次攻击结算后选失能、魅惑或不发动；造成伤害选受伤者，受到伤害选来源。",counter:"后续实际掉血能解除惑控；虚假生命吸收不算实际掉血。",interactions:new[]{"只清除惑控来源效果；其他来源的同名状态仍保留。"});
            Add(CharacterDefinition.EldritchKnightId,"Eldritch Knight",Alignment.LawfulGood,
                shortSkill:"碎盾选五元素，受魔法伤可记录该类型，附魔下次攻击。",playGuide:"主动用完整盾运算先选元素，本次算出攻击立即生效；受非物理伤害可记录。附魔覆盖，攻击发起即消耗。",counter:"用盾挡或闪避消耗附魔；留意其记录的伤害类型。",interactions:new[]{"只被虚假生命吸收不触发记录；免疫或零伤不触发。"});
            Add(CharacterDefinition.BladesingingId,"Bladesinging",Alignment.ChaoticGood,
                shortSkill:"双手和等于体力可入剑歌，元素攻击且攻防+1。",playGuide:"回合结束达成条件后选元素或不发动；之后每回合至少攻击、合盾或实际回复一次维持剑歌。",counter:"破坏双手和与体力的等式；冻结或限制有效行动可令剑歌结束。",interactions:new[]{"闪避、盾挡或零伤仍可维持，攻击被取消则不算。"});
            Add(CharacterDefinition.TheHexbladeId,"The Hexblade",Alignment.NeutralEvil,
                shortSkill:"持7可诅咒；每段+2，亲自击杀召唤咒缚幽灵。",playGuide:"回合开始选诅咒对象或不发动。击杀诅咒者后，幽灵占死者座位、继承手值，由AI从下轮行动。",counter:"避开诅咒伤害；击杀召唤者可使幽灵消失。",interactions:new[]{"幽灵占位计距，不计原玩家胜负人数；状态扣血与幽灵击杀不触发召唤。"});
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
            => id != null && ById.TryGetValue(CharacterIdentity.NormalizeId(id), out CodexEntry e) ? e : null;

        /// <summary>按角色定义取资料。</summary>
        public static CodexEntry For(CharacterDefinition def)
            => def != null ? For(def.Id) : null;

        /// <summary>
        /// 按阵营筛选（玩家要求：点守序/中立/混乱 或 善良/中立/邪恶 依次筛选）。
        ///
        /// <paramref name="lawAxis"/> 与 <paramref name="goodAxis"/> 传 null 表示不过滤。
        /// </summary>
        public static List<CharacterDefinition> Filter(string lawAxis, string goodAxis, string professionId = null)
        {
            var list = new List<CharacterDefinition>();
            CharacterDefinition[] all = CharacterDefinition.BuiltIn;

            for (int i = 0; i < all.Length; i++)
            {
                CharacterDefinition def = all[i];
                if (def == null) continue;

                CodexEntry e = For(def);
                if (e == null) continue;
                if (professionId != null && def.Profession?.Id != professionId) continue;

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
