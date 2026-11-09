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
            Add(CharacterDefinition.WayOfTheDrunkenMasterId,"Way of the Drunken Master",Alignment.ChaoticNeutral,
                shortSkill:"9后可入醉；全数字攻击，基础1d10，受击1d5。",
                playGuide:"9先正常回复，再选择入醉。每个自身回合消耗1气维持；行动后双手相同回1气。持续伤害不掷骰。",
                counter:"消耗其气，等醉拳解除；持续伤害与固定伤害保持原规则。",
                interactions:new[]{"与其它武僧双将共用2/6气；圣火清除并阻止醉拳。"});
            Add(CharacterDefinition.ChampionId, "Champion", Alignment.NeutralGood,
                shortSkill: "精通重击：用1发起攻击，基础伤害×1.5，命中附物理易伤2回合。",
                playGuide: "保留一只1，借3、5或7打出重击。趁目标的物理易伤仍在，用挥砍、钝击或穿刺追击。",
                counter: "留意他手上的1。闪避或取消可阻止易伤，格挡或对应抗性会缩短时长。",
                interactions: new[] { "与忍宗双将：精通重击先乘基础伤害，再分段计算攻防。" });

            Add(CharacterDefinition.BreakerId, "Breaker", Alignment.LawfulEvil,
                shortSkill: "盾碎可破阵强袭；主动碎盾攻击附流血2回合，碎另一盾可换∞距离。",
                playGuide: "核心是主动合盾。"
                         + "攻击时若另一只手是完整盾，可碎它换本次攻击距离∞ ——"
                         + "够不到远处对手时这是解法；"
                         + "自己的盾本来就要碎时，发动破阵强袭打碎对手的盾。",
                counter: "别让他合成 5（那是他合盾的唯一来源）。"
                       + "给他的数字里 1/2/3/4/5 都要压低，4/6/8 次之。",
                interactions: new string[0]);

            Add(CharacterDefinition.SchoolOfConjurationId, "School of Conjuration", Alignment.LawfulNeutral,
                shortSkill: "次级咒术穿盾、正防与闪避；王车易位：同手值换座并跳过。",
                playGuide: "优先合8/6/4。王车易位：点击技能后选双手值相同的存活角色（4/6同6/4），换座并跳过；不限距离，取消不耗回合，冻结不能用。",
                counter: "盾、闪避、太极与伏击挡不住次级咒术，可用潜行或妖精取消。避免同手值被换座，注意换位后的射程变化。",
                interactions: new[] { "与太极宗：次级咒术不减半、不反弹，也不消耗太极手。", "与幽域追踪者：次级咒术穿透伏击，但不解除暗置。" });

            Add(CharacterDefinition.PathOfTheWardenId, "Path of the Warden", Alignment.ChaoticGood,
                shortSkill: "合盾可锁手；体力≤15可将来袭攻击改为力场。",
                playGuide: "合盾后选择锁手。低体力受击可用守望誓约清除附加状态，但力场无法盾挡或闪避。",
                counter: "先破盾解除锁手。低体力时仍要防其动态攻防加成，避免只靠状态消耗。",
                interactions: new[] { "守望誓约保留独立追加伤害；选择改为力场后，自己的盾不会挡下这次攻击。" });

            Add(CharacterDefinition.LifeDomainId, "Life Domain", Alignment.LawfulGood,
                shortSkill: "4/6/8可转治疗，双9两次治疗。净化，溢出攻防+1。",
                playGuide: "缺血时治疗，溢出时可为满血队友加攻防。",
                counter: "限制9与双9。净化能清除腐朽，攻击可消耗其治疗机会。",
                interactions: new[] { "神佑医者与战争祝福可共存。圣火清除已有角色状态。" });

            Add(CharacterDefinition.SwashbucklerId, "Swashbuckler", Alignment.ChaoticGood,
                shortSkill: "1→2闪避；2→3强化基础值×1.5、距离∞，命中附流血2回合。",
                playGuide: "1 是最值钱的手牌（能闪避，还能变成 2 再强化）。"
                         + "被攻击时用掉 1 换掉整次攻击永远划算。"
                         + "潇洒无畏同时把距离变成∞ —— 够不到远处对手时这是解法。",
                counter: "别给他 1 与 2 —— 那是他闪避与强化的原料。"
                       + "面对咒法学派时他的 1 完全无效（攻击不可闪避）。",
                interactions: new[] { "与太极宗：反弹可以被闪避；多段攻击每段分别消耗一只1。" });

            Add(CharacterDefinition.PathOfTheBeastId, "Path of the Beast", Alignment.ChaoticEvil,
                shortSkill: "双手和7积累兽魂；回合末达标后回复已损体力一半并成长。",
                playGuide: "运算凑双手和7积累兽魂。回合末达到实际攻防较小值才成长，负伤加成会提高门槛。",
                counter: "限制凑7的加数，留意兽魂积累；高攻防时需要更多兽魂才能成长。",
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
                shortSkill: "影分身：消耗1气把攻击拆为两段。",
                playGuide: "攻击前选影分身或不发动。对方持盾、自己攻防差占优时考虑两段；行动后同手值回1气。",
                counter: "压低其气与攻击数字；气不足或不发动时仍是普通一段攻击。",
                interactions: new string[0]);

            Add(CharacterDefinition.AssassinId, "Assassin", Alignment.ChaoticEvil,
                shortSkill: "运算达成双7潜行，不能被攻击；出潜行攻击＋7，命中附流血2回合。",
                playGuide: "要的是双 7，一手做不出来 ——"
                         + "先保留一只 7，再用另一只去凑 7。"
                         + "⚠️ 开局直接摆出的双 7 不算潜行，必须经运算达成。",
                counter: "别给他 7（那是潜行的种子）。"
                       + "他潜行期间无法被选中，所以趁他没进潜行时压血线。",
                interactions: new[] { "与咒法学派：潜行不能被选为攻击目标；次级咒术不能绕过目标选择。" });

            Add(CharacterDefinition.OathOfDevotionId, "Oath of Devotion", Alignment.LawfulGood,
                shortSkill: "4/5合盾，6正常攻击。合盾或碎盾触发2点冲击。",
                playGuide: "用完整盾再合盾可触发两次冲击。持完整盾可替人代挡。",
                counter: "限制4/5合盾，留意6和8的普通攻击。",
                interactions: new string[0]);

            Add(CharacterDefinition.TheArchfeyId, "The Archfey", Alignment.ChaoticEvil,
                shortSkill: "妖精步伐：被攻击时，可用同值手取消攻击并魅惑攻击者。",
                playGuide: "核心是\"手上留着什么\"：能取消就取消。"
                         + "跳过回合能把一人血量改成与自己一致 —— "
                         + "比对方少 8 以上才值得。",
                counter: "攻击至高妖精时避开他当前持有的数字；多人战可改选其他攻击目标。",
                interactions: new[] { "与剑士、太极宗：追加与反弹没有攻击数字，不能取消。" });

            Add(CharacterDefinition.ArcaneArcherId, "Arcane Archer", Alignment.LawfulNeutral,
                shortSkill: "攻击距离 3；4 打两段、6 伤害 +2、8 无视护盾。",
                playGuide: "距离 3 让你能打到环上更远的人，先手优势明显。"
                         + "8 是穿透盾牌的答案，4/6 是稳定输出 —— 三者都好用。",
                counter: "与咒法学派同构但要弱很多：只有 8 能穿盾，"
                       + "6 会被完全挡下、4 能挡一半。所以你的 5 只小幅贬值。",
                interactions: new string[0]);

            Add(CharacterDefinition.GamblerId, "Gambler", Alignment.ChaoticNeutral,
                shortSkill: "掷1d9代替运算，见点后可失去1d5生命重掷。",
                playGuide: "先选自己的手再开赌。首掷出来后决定是否重掷。",
                counter: "正常运算后下一次必须赌博。低血重掷可能阵亡。",
                interactions: new string[0]);

            Add(CharacterDefinition.OathOfTheCrownId, "Oath of the Crown", Alignment.LawfulGood,
                shortSkill: "神圣誓忠：自己的盾破碎后，攻击距离与攻击加成永久一起 +1。",
                playGuide: "碎盾是成长，不是损失 —— 碎一次距离变 2、加成变 2。"
                         + "所以主动去打会被破盾的仗，越打越强。",
                counter: "别给他 5（合成 5 就是给他一面可碎的盾），"
                       + "1/2/3/4 这些凑 5 的原料也要压。",
                interactions: new string[0]);

            Add(CharacterDefinition.WayOfTheTaiChiId, "Way of the Tai Chi", Alignment.NeutralGood,
                shortSkill: "太极5可化解并反弹；可耗1气免伤或全反。",
                playGuide: "先决定是否用太极5，再选保留气、1气免伤或1气全反。每只太极只化解一段。",
                counter: "多段攻击可消耗太极与气；力场穿过化解，取消仍能阻止其出手。",
                interactions: new[] { "与咒法学派：次级咒术不触发太极，不消耗5。", "与风流剑客：反弹可以被闪避。" });

            Add(CharacterDefinition.PathOfTheGiantId, "Path of the Giant", Alignment.ChaoticEvil,
                shortSkill: "体力≤15时5攻击、9回复各两段；可失去5体力附魔。",
                playGuide: "高体力按普通数字行动；体力≤15时留大数配5或9。元素战刃先失去5体力，发起下次攻击时消耗。",
                counter: "体力≤15时防两段攻击与回复；高体力时4/6/8仍可攻击。元素战刃会改变下次攻击类型。",
                interactions: new[] { "与狂野魔法：元素战刃的指定属性优先，本次不随机改写类型。" });

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
                shortSkill: "恐惧伏击：运算后两手同为 1/2/3 时，可暗置那只手进入伏击。",
                playGuide: "点【伏击】把参与运算的那只手暗置：它不能再运算、"
                         + "对手也点不到，但数值可见。"
                         + "伏击期间另一只手照常行动，额外攻击 +2、距离 3；"
                         + "挨打时恢复那只手并挡下伤害。",
                counter: "数值可见，能预判他的伏击回合；"
                       + "伏击只有一次，逼他提早用掉即可。",
                interactions: new[] { "与咒法学派：次级咒术穿透伏击，且不解除暗置。", "双将有太极与伏击时：先太极，再伏击。" });

            // ---- 魔术师（混乱中立）----
            Add(CharacterDefinition.MagicianId, "Magician", Alignment.ChaoticGood,
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
                       + "闪避或取消能让那一段连属性一起作废；格挡使攻击附加的负面时长−1。",
                interactions: new string[0]);

            // ---- 旧日支配者（中立邪恶）----
            Add(CharacterDefinition.TheGreatOldOneId, "The Great Old One", Alignment.NeutralEvil,
                shortSkill: "思维之盾：命中的攻击令你与目标之间的距离 +2，持续他两个回合；"
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
                shortSkill: "战斗激励：本回合受伤或实际回血后，可给攻击范围内 1~3 人"
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
                shortSkill: "体力≤15时5/9改攻击，实际伤害回复一半体力。",
                playGuide: "低体力用5/9攻击并吸血；回复超过15后恢复普通数字效果，吸血也暂停。",
                counter: "低体力时限制5/9与连续吸血。高体力时仍按普通数字行动。",
                interactions: new string[0]);

            Add(CharacterDefinition.TacticianId, "Tactician", Alignment.TrueNeutral,
                shortSkill: "战术眼光：运算前选择加法或减法。",
                playGuide: "同一组手值可算出不同结果。用4借6可相加得到1，也可相减得到8。",
                counter: "评估他可借用的数字时，要同时考虑和与差。",
                interactions: new string[0]);

            Add(CharacterDefinition.SchoolOfDivinationId,"School of Divination",Alignment.TrueNeutral,
                shortSkill:"7记录另一手，每三个预兆攻防+1；持7可消耗预兆替换运算结果。",
                playGuide:"打出7积累不同数字。持有7时，运算完成后选择替换值或不发动；新数字决定攻击、回复或合盾。",
                counter:"留意其持有的7与已存预兆，逼其消耗记录；没有7时无法改写。",
                interactions:new[]{"替换发生在数字变化后、效果结算前；纯运算不发动预知未来，也不记录。"});

            Add(CharacterDefinition.SchoolOfNecromancyId, "School of Necromancy", Alignment.NeutralEvil,
                shortSkill: "2→1 挡一段；回合初有1免费暗蚀附腐朽；有人死亡可交换0～2只手值。",
                playGuide: "保留2防守。回合开始有可用1自动选攻击目标，也可放弃，不用选手。有人死亡后，依次选自己的手和死者的手交换数字，可提前结束。",
                counter: "力场攻击无视 2 抵挡；用多段攻击消耗多个 2。",
                interactions: new[] { "与防护学派：先选择2抵挡，剩余伤害再由屏障吸收。" });

            Add(CharacterDefinition.SchoolOfAbjurationId, "School of Abjuration", Alignment.LawfulGood,
                shortSkill: "2 赋物理整组或五种元素抗性；攻击数字可存屏障，吸收后清空。",
                playGuide: "算出2先选角色，再选物理整组、雷鸣、强酸、火焰、寒冷或闪电。攻击时可改存屏障；抗性持续至施法者死亡或再次施法，屏障吸收后清空。",
                counter: "换用其未防护的类型，或逼其重新施法撤销旧抗性。",
                interactions: new string[0]);

            // ---- 净化领域（中立善良，2026-10-23）----
            Add(CharacterDefinition.KnowledgeDomainId,"Knowledge Domain",Alignment.LawfulNeutral,
                shortSkill:"进位与回复积累知识祝福，攻击消耗追加力场与困惑。",
                playGuide:"运算进位自己得1层，回复使目标得1层；层数保留到攻击，整次只消耗一次。",
                counter:"闪避或取消避免追加伤害；困惑期间加法进位结果为1。",
                interactions:new[]{"普通盾挡仍可追加力场；圣火清除知识祝福。"});
            Add(CharacterDefinition.LightDomainId, "Purification Domain", Alignment.NeutralGood,
                shortSkill: "固定5火焰，穿透抗性免疫；命中附圣火与灼伤。治疗附圣火。",
                playGuide: "三个攻击数字均为固定5伤。圣火保留燃烧、灼伤、目盲，清除并阻止其他状态；防护暂停生效但继续计时。",
                counter: "闪避或取消避免附加；灼伤按实际回复量的一半造成火焰伤害。",
                interactions: new[]{"格挡与对应抗性各使灼伤时长−1，圣火为角色状态、不减时长。免疫仍阻止本击附加。"});

            Add(CharacterDefinition.WarDomainId,"War Domain",Alignment.LawfulEvil,
                shortSkill:"回合开始按攻击手数，最多赐予两名不同角色战争祝福2回合。",
                playGuide:"可以选自己或队友，少选或不发动。战争祝福攻防+1，9可改攻击并计入施恩次数；回复也附祝福2回合。",
                counter:"圣火清除并阻止战争祝福；减少其回合开始时的攻击数字，压低施恩次数。",
                interactions:new[]{"战争祝福不叠加，重复施加刷新；本次最多两名不同目标。"});

            Add(CharacterDefinition.BattleMasterId, "Battle Master", Alignment.LawfulNeutral,
                shortSkill: "4=挥砍、6=钝击、8=穿刺；造成伤害后按另一手附加流血/跛脚或震慑/倒伏。",
                playGuide: "攻击命中后看另一只手：4/8→流血或跛脚、6→震慑或倒伏（各 2 回合，可放弃）。"
                         + "先把手凑成「攻击数字 + 4/6/8」再出手；6 的震慑/倒伏最强。",
                counter: "别让他的另一只手停在 4/6/8；"
                       + "闪避与护盾挡住攻击就完全不给附加机会。",
                interactions: new[] { "与太极宗：减免后仍造成伤害才附招式，完全盾挡不附加。", "多段整次结算一次招式；圣火期间不能附加招式状态。" });

            Add(CharacterDefinition.DraconicBloodlineId, "Draconic Bloodline", Alignment.ChaoticNeutral,
                shortSkill: "选一种永久抗性，攻击80%改为所选类型，伤害穿透同型抗性和免疫。",
                playGuide: "按敌方攻击类型和状态弱点选元素，持续命中可延长同型状态。",
                counter: "用其他元素攻击。对应免疫仍阻止状态与反应，抗性可缩短状态。",
                interactions: new[] { "对应状态首次正常附加。抗性和格挡同时生效时无法延长。" });

            Add(CharacterDefinition.RangerHunterId, "Hunter", Alignment.NeutralGood,
                shortSkill: "无效果运算标记。攻击可追猎标记目标，格挡后仍可能受真实伤害。",
                playGuide: "标记距离3内便于追击的敌人。满血或重伤时，巨像屠夫的加伤更高。",
                counter: "被标记后避开射程；闪避或取消可避开伤害，格挡仍会受到附伤。",
                interactions: new[] { "正攻击加成越高，标记攻击被格挡时的真实伤害越高。" });

            Add(CharacterDefinition.WayOfTheOpenHandId, "Way of the Open Hand", Alignment.TrueNeutral,
                shortSkill: "1气倒伏／震慑；3气暗劲；回合初混元体。",
                playGuide: "1气控制或3气暗劲二选一；暗劲只由自己后续命中引爆。混元体不消耗正常行动。",
                counter: "逼其花气；被暗劲标记后，闪避或取消施加者攻击可避免引爆。",
                interactions: new[] { "暗劲不叠加；盾挡算命中，闪避／取消不引爆。" });

            Add(CharacterDefinition.WayOfTheFourElementsId, "Way of the Four Elements", Alignment.TrueNeutral,
                shortSkill: "2气改元素攻击，1气取得对应元素抗性。",
                playGuide: "保留防守所需的气。行动后双手同值回1气。",
                counter: "1气可防守，2气才可改元素。物理攻击可绕开四象法门。",
                interactions: new string[0]);

            Add(CharacterDefinition.CircleOfSporesId,"Circle of Spores",Alignment.TrueNeutral,
                shortSkill:"孢子生命护身并转毒；相邻死亡回复荒野充能。",playGuide:"双手相同时可用充能获得孢子生命；自己回合开始仍有孢子生命时，对距离1的其他角色附腐朽2回合。",counter:"避开相邻腐朽，先消耗孢子生命；别让附近伤员轻易死亡。",interactions:new[]{"形态解除不算死亡；孢子生命先于虚假生命吸收。"});
            Add(CharacterDefinition.CircleOfTheMoonId,"Circle of the Moon",Alignment.ChaoticGood,
                shortSkill:"3点充能；荒野独立体力，枭熊与黑豹耗2点。",playGuide:"回合开始双手和10可变形并跳过；熊4合盾，回合末可解除。",counter:"留意充能，破形态后的溢伤会扣原体力。",interactions:new[]{"荒野暂停原技能；治疗作用于当前形态，解除恢复原体力。"});

            Add(CharacterDefinition.CircleOfTheStarsId,"Circle of the Stars",Alignment.LawfulNeutral,
                shortSkill:"荒野叠星耀：射手光耀、圣杯治疗、巨龙纯运算。",playGuide:"先选荒野再选星耀；攻防相加、距离取高，一起解除。",counter:"打破独立血池；巨龙未在开局纯运算，回合末必须进行。",interactions:new[]{"荒野暂停原技能；治疗作用于当前形态，解除恢复原体力。"});

            Add(CharacterDefinition.CollegeOfLoreId,"College of Lore",Alignment.LawfulNeutral,
                shortSkill:"记录1～9；集齐可成长，消耗对应记录可减伤。",
                playGuide:"打出新数自动记录；集齐后选成长或保留。受击时选择清除对应记录减半，或保留。",
                counter:"留意其已有记录；换数字攻击，逼其消耗成长进度。",
                interactions:new[]{"风暴导向只改手值，不触发记录。"});
            Add(CharacterDefinition.ShadowMagicId,"Shadow Magic",Alignment.NeutralEvil,
                shortSkill:"首次归0恢复至1；双手和10可施黑暗术，免疫失明，对失明目标暗蚀伤害+2。",
                playGuide:"回合末双手和10时选择发动黑暗术或不发动；靠近敌人再施放，留意附近同袍也会失明。",
                counter:"拉开距离避免失明；首次致命伤只能迫使其恢复至1，继续追击。",
                interactions:new[]{"暗蚀攻击命中按普通规则附加腐朽。"});
            Add(CharacterDefinition.StormSorceryId,"Storm Sorcery",Alignment.ChaoticNeutral,
                shortSkill: "闪电、雷鸣各40%，伤害穿透对应抗性和免疫。行动后可纯运算。",
                playGuide: "开局选永久抗性。纯运算可调整下一回合的手值。",
                counter: "格挡仍能减伤，抗性仍缩短状态，免疫仍阻止元素效果。",
                interactions: new[] { "对免疫目标能造成闪电或雷鸣伤害，但不触发对应状态与反应。" });
            Add(CharacterDefinition.SchoolOfIllusionId,"School of Illusion",Alignment.ChaoticEvil,
                shortSkill:"持2可选择改用另一只合法手作加数；差2可变小手为2减伤。",
                playGuide:"持2时，先看对方的运算手和原加数，再选是否改用另一只合法手；受击差2可将小手变2减半。",
                counter:"借其手前比较两种运算结果；对方可主动改算。拆开其持2或差2条件，削弱防守。",
                interactions:new[]{"替换手也须是合法加数；隐藏或被封锁的另一手不会被强行借用。"});

            Add(CharacterDefinition.CircleOfTheLandId,"Circle of the Land",Alignment.NeutralGood,
                shortSkill:"3可束缚；回合末无3可封锁自己的加数手。",playGuide:"双手和10可荒野；原形打3可束缚，双手无3时可保护自己的加数。",counter:"封锁不妨碍主动运算；荒野期间暂停大地技能。",interactions:new[]{"荒野暂停原技能；治疗作用于当前形态，解除恢复原体力。"});

            Add(CharacterDefinition.OathOfTheAncientsId, "Oath of the Ancients", Alignment.NeutralGood,
                shortSkill: "盾碎可束缚射程内角色；受伤后按血量分支回复2。",
                playGuide: "主动或被动盾碎后选角色或不发动；受伤后≥10选射程内他人回复2，低于10自动自愈2。",
                counter: "利用射程隔开治疗对象；碎盾会给他控制机会。",
                interactions: new[] { "虚假生命：只扣虚假生命不触发庇护；致死不自愈。" });

            Add(CharacterDefinition.TheFiendId, "The Fiend", Alignment.LawfulEvil,
                shortSkill: "溢出治疗存10点虚假生命；可耗尽令目标失能/恐慌。",
                playGuide: "攻击时选失能、恐慌或保留；虚假生命先吸伤。造成伤害后目标半血以下（含击杀）回复3。",
                counter: "少量伤害先消耗其防护；用腐朽阻止回复与溢出转化。",
                interactions: new[] { "满血治疗仍可转化溢出；虚假生命不被净化或圣火移除。" });

            Add(CharacterDefinition.SchoolOfTransmutationId,"School of Transmutation",Alignment.ChaoticNeutral,
                shortSkill:"结束可将同数角色变羊；羊有独立8生命。形态进出时可额外运算。",playGuide:"回合结束可选自己或双手无序匹配的角色。变羊或解除后，可用变化莫测再做一次有效运算。",counter:"避免双手匹配。打破羊形态时留意施术者可能再出手，溢伤仍会扣原体力。",interactions:new[]{"羊形态保留原状态计时、暂停原技能；解除恢复原生命。"});
            Add(CharacterDefinition.SchoolOfEnchantmentId,"School of Enchantment",Alignment.NeutralEvil,
                shortSkill:"实际伤人可附魅惑；实际受伤可对伤害来源附失能。",playGuide:"实际伤到对方后可附魅惑，实际受伤后可对来源附失能。可放弃，附加后目标再次失去体力会解除。",counter:"后续实际掉血能解除惑控；虚假生命吸收不算实际掉血。",interactions:new[]{"只清除惑控来源效果；其他来源的同名状态仍保留。"});
            Add(CharacterDefinition.EldritchKnightId,"Eldritch Knight",Alignment.LawfulGood,
                shortSkill:"碎盾选五元素，受魔法伤可记录该类型，附魔下次攻击。",playGuide:"主动用完整盾运算先选元素，本次算出攻击立即生效；受非物理伤害可记录。附魔覆盖，攻击发起即消耗。",counter:"用盾挡或闪避消耗附魔；留意其记录的伤害类型。",interactions:new[]{"只被虚假生命吸收不触发记录；免疫或零伤不触发。"});
            Add(CharacterDefinition.BladesingingId,"Bladesinging",Alignment.ChaoticGood,
                shortSkill:"双手和等于体力可入剑歌，元素攻击且攻防+1。",playGuide:"回合结束达成条件后选元素或不发动；之后每回合至少攻击、合盾或实际回复一次维持剑歌。",counter:"破坏双手和与体力的等式；冻结或限制有效行动可令剑歌结束；圣火清除并阻止剑歌。",interactions:new[]{"闪避、盾挡或零伤仍可维持，攻击被取消则不算。"});
            Add(CharacterDefinition.OathOfConquestId,"Oath of Conquest",Alignment.LawfulEvil,
                shortSkill:"持5攻／受击后涨攻／防，以攻击开始的攻防优势施加恐慌。",playGuide:"保留数字5来累积加成，盾是否完整不影响持5判定。新层从下一击生效；回合末未攻击减攻层，无5减防层，最低0。",counter:"逼其改掉5或空过回合，逐渐消除技能层数；圣火可阻止恐慌。",interactions:new[]{"多段整次只成长一次，闪避／盾挡／零伤仍算，取消与持续伤害不算。"});
            Add(CharacterDefinition.TrickeryDomainId,"Trickery Domain",Alignment.ChaoticNeutral,
                "诡诈换位：借手逢2，可换数字并魅惑。",
                "对手借你的手时先选是否交换；交换成功后再选是否魅惑。对方按换入的数字结算。",
                "避开2相关运算；留意交换后可能把攻击变成合盾或回复。",
                new[]{"幻术先改加数，再判诡诈换位；换入的数字不重复受运算倍率影响。"});
            Add(CharacterDefinition.TempestDomainId,"Tempest Domain",Alignment.ChaoticNeutral,
                "风暴之怒：持4可先反击，再纯运算。",
                "开回合可额外运算；受攻击点一只4，再选闪电或雷鸣，反击后同手必须运算。",
                "避免在低血量时攻击持4的目标；护盾和对应抗性可抵挡反击。",
                new[]{"一整次来袭只反击一次，反击不连锁；击杀来袭者终止其未结算攻击。"});
            Add(CharacterDefinition.OathOfVengeanceId,"Oath of Vengeance",Alignment.LawfulNeutral,
                shortSkill:"开局标记仇敌，对其伤害+1、其余−1；仇敌破盾可额外纯运算。",playGuide:"受攻击前可转移仇敌标记，仇敌死亡可重选。额外运算先点自己的手，再点加数，可放弃，只改变数字。",counter:"留意标记归属；仇敌破盾会给予其调整手值的机会。",interactions:new[]{"每段攻击计算标记增减伤；每碎一面盾各获得一次纯运算，包含破阵强袭。"});
            Add(CharacterDefinition.TheHexbladeId,"The Hexblade",Alignment.NeutralEvil,
                shortSkill: "无效果运算可诅咒，时长等于结果。亲自击杀召唤幽灵。",
                playGuide: "优先诅咒能接近并击杀的敌人。",
                counter: "减少给他留下能合出长诅咒的加数。击杀召唤者会驱散幽灵。",
                interactions: new[] { "幽灵占位计距，从下一轮行动，不计入原玩家胜负人数。" });
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
