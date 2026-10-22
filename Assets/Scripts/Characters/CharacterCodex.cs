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
    ///   交互        每角色 2 ~ 3 条
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
                interactions: new[]
                {
                    "与咒法学派：勇士防0，没有正向防御可被无视；护盾仍挡不住空间刃。",
                    "与忍宗双将：十点重击先乘基础伤害，再分段并逐段计算攻防；与风流剑客现为跨阵营，不能同选。",
                });

            Add(CharacterDefinition.BreakerId, "Breaker", Alignment.LawfulNeutral,
                shortSkill: "盾碎可连锁破盾；主动碎盾攻击附流血2回合，碎另一盾可换无限距离。",
                playGuide: "核心是主动合盾。"
                         + "攻击时若另一只手是完整盾，可碎它换本次攻击距离无限 ——"
                         + "够不到远处对手时这是解法；"
                         + "自己的盾本来就要碎时，顺手连锁打碎对手的盾最赚。",
                counter: "别让他合成 5（那是他合盾的唯一来源）。"
                       + "给他的数字里 1/2/3/4/5 都要压低，4/6/8 次之。",
                interactions: new[]
                {
                    "与咒法学派：他无视护盾，所以你的盾挡不住他的伤害；"
                        + "但你的两个技能照样能发动 —— "
                        + "他只是不消耗、不破碎你的盾，"
                        + "你主动碎盾换射程依然成立，也能连锁打碎他的盾。",
                    "与奉献之誓 / 守望者道途 / 王冠之誓：场上盾越多，连锁破盾的选择越多。",
                });

            Add(CharacterDefinition.SchoolOfConjurationId, "School of Conjuration", Alignment.LawfulNeutral,
                shortSkill: "空间刃穿盾、正防与闪避；王车易位：同手值换座并跳过。",
                playGuide: "优先合8/6/4。王车易位：点击技能后选双手值相同的存活角色（4/6同6/4），换座并跳过；不限距离，取消不耗回合，冻结不能用。",
                counter: "盾、闪避、太极与伏击挡不住空间刃，可用潜行或妖精取消。避免同手值被换座，注意换位后的射程变化。",
                interactions: new[]
                {
                    "王车易位：可换队友或对手，队伍和身份不变；距离立即变化，本轮按原顺序，下一轮按新座位行动。",
                    "与太极宗：太极对他不减半、不反弹、5 也不消耗 —— "
                        + "所以太极宗的 5 面对他时也要降价值，它只是普通的一张 5。",
                    "与风流剑客：他的攻击不可闪避。",
                    "与破阵者：他的盾挡不住你，但他的两个技能照样能发动"
                        + "（主动碎盾换射程 / 连锁打碏你的盾）。",
                    "与奉献之誓：奉献之誓的盾不挡他的伤害，但合盾仍会触发冲击。",
                });

            Add(CharacterDefinition.PathOfTheWardenId, "Path of the Warden", Alignment.ChaoticGood,
                shortSkill: "合出盾时可控制对手一只手，盾碎时解锁。",
                playGuide: "合盾不只是防御，更是控制：锁住对手更值钱的那只手。"
                         + "锁手在盾碎时解除，所以算好盾能撑多久。",
                counter: "别让他合成 5。另外注意："
                       + "他的锁手在场上只剩两人时才有额外限制，人多时更凶。",
                interactions: new[]
                {
                    "与巨人道途：巨人道途靠另一只手决定伤害，锁住那只手收益最大。",
                    "与咒法学派：咒法学派无视护盾，但锁手仍然生效。",
                });

            Add(CharacterDefinition.LifeDomainId, "Life Domain", Alignment.LawfulGood,
                shortSkill: "4/6/8 可转回复；回复先净化再回血；9 可给别人，双9再回一次。",
                playGuide: "4/6/8 是二选一，所以它们比别的角色更灵活。"
                         + "危险时才回血，否则全部进攻 —— 只顾回血会打成僵局。"
                         + "2v2 里优先给一队血量最低的人回复。",
                counter: "尽量阻止他合双 9（双 9 = 9 + 9 = 回 18 血）。"
                       + "9 的原料（4+5、3+6、2+7、1+8）都值得压一点。",
                interactions: new[]
                {
                    "与太极宗：他的回复可以给别人，但 0 只在太极宗手上。",
                    "与巨人道途：巨人道途的 9 是两段回复，生命领域是一段但可指定目标。",
                });

            Add(CharacterDefinition.SwashbucklerId, "Swashbuckler", Alignment.ChaoticGood,
                shortSkill: "1→2闪避；2→3强化基础值×1.5、距离无限，命中附流血2回合。",
                playGuide: "1 是最值钱的手牌（能闪避，还能变成 2 再强化）。"
                         + "被攻击时用掉 1 换掉整次攻击永远划算。"
                         + "乘风突刺同时把距离变成无限 —— 够不到远处对手时这是解法。",
                counter: "别给他 1 与 2 —— 那是他闪避与强化的原料。"
                       + "面对咒法学派时他的 1 完全无效（攻击不可闪避）。",
                interactions: new[]
                {
                    "与咒法学派：闪避无效。",
                    "与太极宗：太极反弹可以被闪避 —— 所以他的 1 反而是应对手段。",
                    "与忍宗 / 巨人道途：多段攻击时，一只 1 只能闪掉其中一段。",
                });

            Add(CharacterDefinition.PathOfTheBeastId, "Path of the Beast", Alignment.ChaoticEvil,
                shortSkill: "运算后双手之和为 7 → 回半血且攻防永久 +1。",
                playGuide: "目标是凑 7（不是手上有 7）。"
                         + "⚠️ 7 对你是死牌：7 + 任何数都回不到 7。"
                         + "所以要留 1/2/3/4 当凑 7 的伙伴。",
                counter: "别让他两手凑成 7（有 1 别给 6、有 2 别给 5、"
                       + "有 3 别给 4）。"
                       + "他每次进化回半血且攻防 +1，拖久了很难消耗。",
                interactions: new[]
                {
                    "与生命领域：他回半血，生命领域回固定 9 —— 血量上限 25 的野兽道途更耐打。",
                    "与巨人道途：两者生命上限都是 25。",
                });

            Add(CharacterDefinition.SwordsmanId, "Swordsman", Alignment.LawfulNeutral,
                shortSkill: "双4/6/8时追加固定伤害，命中附流血2回合。",
                playGuide: "要的是双手相同且为 4/6/8，一手做不出来 ——"
                         + "先用一手把一只手做成 4/6/8，下一手凑齐另一只。"
                         + "追加攻击不吃攻防，所以它是一笔稳定的额外伤害。",
                counter: "别给他 4/6/8（那是他双持的原料）。"
                       + "他血量健康时不必太怕，但被他连续双持会很快崩。",
                interactions: new[]
                {
                    "与咒法学派：追加攻击的\"攻击数字视为 0\"，至高妖精无法取消它。",
                    "与忍宗：忍宗拆两段各吃攻防，剑士是一次固定追加 —— 算法不同。",
                });

            Add(CharacterDefinition.WayOfTheNinjaId, "Way of the Ninja", Alignment.TrueNeutral,
                shortSkill: "攻击拆成两段、伤害减半，每段各自计算攻防。",
                playGuide: "拆两段后每段各自加一次攻防，"
                         + "4 与 8 享受的增益是一样的，"
                         + "所以 4 不是死牌，只是 8 本身更值钱（排序 8 > 6 > 4）。",
                counter: "别给他高值（ 8 最该挡，6 次之）。"
                       + "一面盾只能挡下他其中一段，所以自己合盾的价值下降。",
                interactions: new[]
                {
                    "与风流剑客：盾与闪避是逐段独立结算（一面盾 / 一只 1 只挡一段）；"
                        + "太极也一样只对付一段 —— "
                        + "所以你的两段里总有一段能打到他。",
                    "与巨人道途：巨人道途的 5 也是两段，但每段伤害取决于另一只手。",
                });

            Add(CharacterDefinition.AssassinId, "Assassin", Alignment.ChaoticNeutral,
                shortSkill: "运算达成双7潜行，不能被攻击；出潜行攻击＋7，命中附流血2回合。",
                playGuide: "要的是双 7，一手做不出来 ——"
                         + "先保留一只 7，再用另一只去凑 7。"
                         + "⚠️ 开局直接摆出的双 7 不算潜行，必须经运算达成。",
                counter: "别给他 7（那是潜行的种子）。"
                       + "他潜行期间无法被选中，所以趁他没进潜行时压血线。",
                interactions: new[]
                {
                    "与太极宗：潜行中的攻击照样会被太极化解。",
                    "与咒法学派：潜行只挡\"被选中\"，取消攻击仍然生效。",
                });

            Add(CharacterDefinition.OathOfDevotionId, "Oath of Devotion", Alignment.LawfulGood,
                shortSkill: "4/5/6 都合盾，且合盾 / 碎盾时额外打出 2 点冲击。",
                playGuide: "选自己的手与加数后，4/5/6 直接合盾，只提示选择冲击目标。"
                         + "用完整盾再合盾，会分别触发碎盾和合盾两次冲击。",
                counter: "别让他合成 5，4/6 也要压 —— "
                       + "他每合一次盾就白赚一次冲击。",
                interactions: new[]
                {
                    "与咒法学派：盾不挡伤害，但合盾仍会触发冲击。",
                    "与破阵者 / 守望者道途 / 王冠之誓：他们也会合盾，场上盾多时双方技能都更活跃。",
                });

            Add(CharacterDefinition.TheArchfeyId, "The Archfey", Alignment.LawfulEvil,
                shortSkill: "妖精戏法：手上留着与对手攻击数字相同的牌即可取消该攻击。",
                playGuide: "核心是\"手上留着什么\"：能取消就取消。"
                         + "跳过回合能把一人血量改成与自己一致 —— "
                         + "比对方少 8 以上才值得。",
                counter: "别给他 1/2/7（能凑出最多的攻击数字）；"
                       + "或改用盾 / 回复推进局面。",
                interactions: new[]
                {
                    "与巨人道途：巨人道途的 5 是两段攻击、是高价值数字，"
                        + "所以至高妖精手上 5 的价值对巨人道途时更高（取消它收益更大）。",
                    "与剑士：追加攻击\"攻击数字视为 0\"，无法被取消。",
                    "与太极宗：太极反弹\"攻击数字视为 0\"，同样无法被取消。",
                });

            Add(CharacterDefinition.ArcaneArcherId, "Arcane Archer", Alignment.LawfulNeutral,
                shortSkill: "攻击距离 3；4 打两段、6 伤害 +2、8 无视护盾。",
                playGuide: "距离 3 让你能打到环上更远的人，先手优势明显。"
                         + "8 是穿透盾牌的答案，4/6 是稳定输出 —— 三者都好用。",
                counter: "与咒法学派同构但要弱很多：只有 8 能穿盾，"
                       + "6 会被完全挡下、4 能挡一半。所以你的 5 只小幅贬值。",
                interactions: new[]
                {
                    "与咒法学派：咒法学派无视全部护盾，魔射手只有 8 能穿。",
                    "与太极宗：你的 4 也是两段，而太极只能对付一段 —— "
                        + "所以另一段一定能打到他。",
                });

            Add(CharacterDefinition.GamblerId, "Gambler", Alignment.ChaoticNeutral,
                shortSkill: "放弃运算，掷 1d9 直接作为本次行动结果。",
                playGuide: "算不出高价值就赌（4/6/8 才算）。"
                         + "赌博不需要点他人的手，打谁等结果出来再选；"
                         + "正常运算过一回合后强制赌博。",
                counter: "基本不需要针对（与你的手值无关）；"
                       + "他赌博后的下一回合别让他凑出高价值。",
                interactions: new[]
                {
                    "与所有角色：赌博结果与双方手值完全无关。",
                    "与太极宗：赌出 5 时，太极宗进入太极形态而非合盾。",
                });

            Add(CharacterDefinition.OathOfTheCrownId, "Oath of the Crown", Alignment.LawfulGood,
                shortSkill: "王冠威仪：自己的盾破碎后，攻击距离与攻击加成永久一起 +1。",
                playGuide: "碎盾是成长，不是损失 —— 碎一次距离变 2、加成变 2。"
                         + "所以主动去打会被破盾的仗，越打越强。",
                counter: "别给他 5（合成 5 就是给他一面可碎的盾），"
                       + "1/2/3/4 这些凑 5 的原料也要压。",
                interactions: new[]
                {
                    "与破阵者：破阵者想碎盾换射程，正好帮王冠之誓成长。",
                    "与至高妖精：王冠之誓的攻击加成随距离成长，被取消时损失更大。",
                });

            Add(CharacterDefinition.WayOfTheTaiChiId, "Way of the Tai Chi", Alignment.NeutralGood,
                shortSkill: "5 不合盾而进入太极：被攻击时减半并反弹。",
                playGuide: "5 是高价值（太极攻守兼备，而且开局那个 5 就已经是太极）。"
                         + "被攻击时可以选择发动：把 5 变 0、受一半、反弹一半。"
                         + "自己的 0 被他人当加数触碰后会立刻变成 1。",
                counter: "别给他 5。一个太极只夹住一段，其余照常吃 ——"
                       + "多段攻击打他反而赚；咒法学派打他则完全失效。",
                interactions: new[]
                {
                    "与咒法学派：太极完全失效，而且那张 5 不会被消耗。",
                    "与盗贼：盗贼偷太极宗的 5 只会拿到普通盾 / 碎盾（不继承太极）；"
                        + "反之太极宗收到 5 会变成太极。盗贼偷太极宗的 0 只能偷到 1。",
                    "与至高妖精：反弹无法被取消。",
                    "与风流剑客：反弹可以被闪避。",
                });

            Add(CharacterDefinition.PathOfTheGiantId, "Path of the Giant", Alignment.ChaoticEvil,
                shortSkill: "4/6/8 完全无效果、5 不合盾；5 是两段攻击、9 是两段回复。",
                playGuide: "只有 5 与 9 是高价值。"
                         + "打 5 时每段伤害 = 另一只手的点数，"
                         + "所以配一个大数（7/8/9）收益最大；"
                         + "打 9 时同样按另一只手回复。生命上限 25。",
                counter: "别给他 5 与 9；4/6/8 对他完全无效果，"
                       + "所以那三个数字给他也无所谓（算安全牌）。",
                interactions: new[]
                {
                    "与至高妖精：巨人道途打 5 是两段，但攻击数字就是 5 ——"
                        + "至高妖精可以用 5 一次性取消两段。",
                    "与太极宗：你的两段只会被太极减免其中一段，"
                        + "另一段照常打到他身上 —— "
                        + "所以用多段攻击对付太极反而占便宜。",
                    "与忍宗：两者都是多段，但巨人道途每段伤害取决于另一只手。",
                });

            Add(CharacterDefinition.ThiefId, "Thief", Alignment.NeutralEvil,
                shortSkill: "放弃运算改为偷窃：双方点数互换，原先 ≥ 偷来的才生效。",
                playGuide: "点自己的手 → 点【偷窃】→ 点别人的手。"
                         + "判定看\"原先 ≥ 偷来的\"，所以握着大数字才偷得到好东西；"
                         + "无限距离、可以偷队友，但上一次行动偷过就不能再偷。",
                counter: "别给他 8 / 9，4 / 6 次之。"
                       + "递出完整 5 其实不亏（被偷会变成碎盾）。",
                interactions: new[]
                {
                    "与太极宗：偷他的 5 只得普通盾 / 碎盾（不继承太极）；"
                        + "太极宗收到 5 则变成太极；偷他的 0 只能得到 1。",
                    "与王冠之誓 / 破阵者 / 守望者道途：偷走他们的 5 会让他们的盾"
                        + "换成碎盾，等于顺手破坏了他们的技能前提。",
                    "与所有角色：被偷的人不触发效果、也不形成盾。",
                });

            // ---- 幽域追踪者（混乱善良）----
            Add(CharacterDefinition.GloomStalkerId, "Gloom Stalker", Alignment.ChaoticGood,
                shortSkill: "幽域伏击：运算后两手同为 1/2/3 时，可暗置那只手进入伏击。",
                playGuide: "点【伏击】把参与运算的那只手暗置：它不能再运算、"
                         + "对手也点不到，但数值可见。"
                         + "伏击期间另一只手照常行动，攻击 +1、距离 3；"
                         + "挨打时恢复那只手并挡下伤害。",
                counter: "数值可见，能预判他的伏击回合；"
                       + "伏击只有一次，逼他提早用掉即可。",
                interactions: new[]
                {
                    "与咒法学派：咒术攻击穿透伏击，且不会解除它。",
                    "与太极宗：双将同时有太极与伏击时，太极先生效。",
                    "与守望者道途：剩余那只手被锁住且另一只已暗置时，"
                        + "他会拿到跳过回合按钮。",
                });

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
                interactions: new[]
                {
                    "与咒法学派：他的攻击同样无视护盾与正向防御。",
                    "与风流剑客：他的攻击仍可被闪避。",
                    "与至高妖精：攻击数字是 2/4/6/8，要用相同数字的牌取消。",
                });

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
                interactions: new[]
                {
                    "与咒法学派：塑能学派的力场伤害与咒法学派同源 —— 都穿盾、无视正向防御、不可闪避。",
                    "与风流剑客：除力场外的 9 种伤害都能被闪避；一旦被闪避，这次放弃就白费了。",
                    "与至高妖精：攻击数字是 4/6/8，要用相同数字的牌取消；取消同样让这次放弃作废。",
                    "与太极宗：太极减免仍算命中 —— 状态照样挂上。",
                });

            // ---- 狂野魔法（混乱中立）----
            Add(CharacterDefinition.WildMagicId, "Wild Magic", Alignment.ChaoticNeutral,
                shortSkill: "每次攻击各 8% 变成 10 种元素之一（共 80%），"
                          + "改写后挂对应状态；余 20% 普攻。",
                playGuide: "能力自动发动，不用选。掷到力场穿盾、无视正向防御、不可闪避。"
                         + "普攻距离 1 内是钝击、1 外是穿刺；多段攻击每段各掷一次。",
                counter: "攻 0 防 −1 但射程无限，躲不掉只能硬扛。"
                       + "闪避或取消能让那一段连属性一起作废；盾与太极挡不住状态。",
                interactions: new[]
                {
                    "与风流剑客 / 至高妖精：被闪避或被取消的那一段不算命中，属性一起作废。",
                    "与太极宗：太极减免仍算命中；掷到力场时穿盾也照样穿透。",
                });

            // ---- 旧日支配者（中立邪恶）----
            Add(CharacterDefinition.TheGreatOldOneId, "The Great Old One", Alignment.NeutralEvil,
                shortSkill: "异界疏离：命中的攻击令你与目标之间的距离 +2，持续他两个回合；"
                          + "射程无限。",
                playGuide: "射程无限，站位不用管。打中就把他推开两格，"
                         + "他射程不够时这两个回合根本打不到你。"
                         + "类型按距离：1 内挥砍、1 外穿刺。",
                counter: "攻 0 防 −1，很软，抢血就行。"
                       + "闪避与取消能让这一击连带距离一起作废。"
                       + "被推开后先合盾或回复，别硬打。",
                interactions: new[]
                {
                    "与目盲：被推开到距离 3 时，目盲会让他一手都走不出来 —— "
                    + "按规则此时他可以跳过回合。",
                    "与风流剑客 / 至高妖精：被闪避或被取消不算命中，距离不会变。",
                });

            // ---- 勇气学院（守序善良）----
            Add(CharacterDefinition.CollegeOfValorId, "College of Valor", Alignment.LawfulGood,
                shortSkill: "勇气战歌：本回合受伤或实际回血后，可给攻击范围内 1~3 人"
                          + "加/减攻防 ±(4-人数)。",
                playGuide: "每次行动结算完，若本回合受过伤或回过血就会问你要不要发动。"
                         + "先选增幅/削弱 攻击/防御，再选人数（1 人 ±3、2 人 ±2、3 人 ±1），"
                         + "最后点角色。可以选自己，同一人点两只手只算一次。",
                counter: "持续到你下次发动或你死，所以要盯住他给谁加了什么。"
                       + "他攻 0 防 −1、射程 3，抢血就能逼他先给自己加防御。",
                interactions: new[]
                {
                    "与塑能学派体系：给目标削弱防御会与强酸 −2 叠加，等于双重破防。",
                    "与旧日支配者：被推开到距离 3 时，他的技能范围也跟着够不到远处的人。",
                });

            Add(CharacterDefinition.OathbreakerId, "Oathbreaker", Alignment.LawfulEvil,
                shortSkill: "5 改为 5 点钝击攻击；被攻击时可把目标转给持完整盾者。",
                playGuide: "5 既能进攻，也保留转移资格。被攻击时点持盾角色的转移按钮，或选不发动。",
                counter: "先破掉场上的完整盾；他没有5或场上没有合适持盾者时无法转移。",
                interactions: new[] { "与奉献之誓：转移后奉献之誓仍可代挡。" });

            Add(CharacterDefinition.PathOfTheBerserkerId, "Path of the Berserker", Alignment.NeutralEvil,
                shortSkill: "生命上限 +5；9 改为 9 点挥砍攻击；每少 5 血 +1/+1；造成伤害回一半。",
                playGuide: "血量越低越强，但别把自己送进斩杀线；9 现在是主要输出。",
                counter: "爆发要一次打死，别让他反复用伤害回血。",
                interactions: new[] { "与流血/燃烧：持续掉血也会触发他的失血加成。" });

            Add(CharacterDefinition.TacticianId, "Tactician", Alignment.ChaoticGood,
                shortSkill: "战局推演：按场上最高与最低体力差 ÷3 获得额外攻防；挥砍伤害。",
                playGuide: "场上血量差越大越强，适合在残局或有人被集火时发力。",
                counter: "尽量把双方血量拉平，削弱他的动态加成。",
                interactions: new[] { "与狂战士道途：狂战士道途血量越低，战术家的差值可能越大。" });

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
