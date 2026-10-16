using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 需要玩家做抉择的时机。新增角色时在这里登记一个值即可。
    /// </summary>
    public enum HandEffect
    {
        None = 0,

        /// <summary>防守方有双盾时，选择用哪一面抵挡本次攻击。</summary>
        ShieldBlock,

        // ---------- 已有角色 ----------
        /// <summary>野蛮人：合出护盾后，选择锁住对手哪一只手。</summary>
        BarbarianLockTarget,

        /// <summary>
        /// 战士：自己的盾破碎后，选择把场上哪一面**完好**的盾一起打碎。
        ///
        /// 规则：盾碎时**先看场上还有没有完好的盾** —— 没有就静默跳过、不弹选择；
        /// 有则弹「是否发动」，同意后**点那面完好的盾**作为目标。
        ///
        /// ⚠️ 与野蛮人锁手同一模式：**必须在运算前收集**。
        /// 结算一旦开始就一次跑完，没法中途问人类玩家
        /// （这正是本技能曾经对人类玩家"不生效"的原因）。
        /// </summary>
        WarriorChainBreak,

        /// <summary>
        /// 战士：发起攻击时，若**另一只手是完整的盾牌**，可以让它破碎，
        /// 使**本次攻击距离无限**（玩家新增的技能）。
        ///
        /// 为什么需要它：攻击距离只影响 2v2 / 8 人局 ——
        /// 距离 1 的战士够不到"环上对角"的对手（2v2 里相隔 2），
        /// 用一面盾换这一次的无限距离就是他的解法。
        ///
        /// ⚠️ 这是**可选**的（"可以让它破碎"）——允许放弃（放弃 = 保留盾）。
        /// 与其它抉择一样必须**在运算前收集**。
        /// </summary>
        WarriorRangeBreak,

        // ---------- 圣武士 ----------
        /// <summary>
        /// 圣武士：冲击的目标（攻击范围内一名角色，含队友）。
        ///
        /// **强制发动**（`allowDecline = false`）—— 玩家只选目标，不能放弃。
        /// 在运算前收集（合盾 / 碎盾都可能在结算里发生）。
        /// </summary>
        PaladinShockTarget,

        /// <summary>
        /// 圣武士：范围内的角色被攻击时，是否把目标改成自己。
        ///
        /// 允许放弃（放弃 = 不改目标）。
        /// </summary>
        PaladinRedirect,

        /// <summary>
        /// 武僧：被攻击时是否用**太极**化解（把 5 变 0、受一半、反弹一半）。
        ///
        /// **可选发动**（玩家要求"也要给按钮选择是否发动"）：
        /// 点候选（那只太极的手）即发动，点「不发动」就正常吃满伤害。
        /// </summary>
        MonkTaichi,

        // ---------- 邪术师 ----------        /// <summary>
        /// 邪术师：是否用某只手（+1，永久）取消这次攻击。
        ///
        /// **候选就是"与攻击数字相同的那只手"** —— 玩家点它即发动，
        /// 点「不发动」即放行。所以界面天然满足"点对应数字触发"。
        ///
        /// ⚠️ 只在邪术师是**人类**时创建；AI 走自动取消（见 AttackContext）。
        /// </summary>
        WarlockCancel,

        // ---------- 牧师 ----------
        /// <summary>牧师：9 的回复要回给谁。</summary>
        PriestHealTarget,

        /// <summary>
        /// 牧师：运算出 4 / 6 / 8 时，选择"当攻击打"还是"当回复用"。
        ///
        /// ⚠️ 这一步发生在**选目标手之前**（玩家要求的顺序）：
        /// 先问攻击还是回复，选了之后再点目标手。
        ///
        /// 选回复时回复量就是该数字本身（4 回 4、6 回 6、8 回 8），
        /// **不受攻击加成影响**；目标可以是自己或对手（后者为多人玩法预留）。
        /// </summary>
        PriestAttackOrHeal,

        /// <summary>
        /// 牧师：双 9 触发的那次**额外**回复给谁。
        ///
        /// 单独一个值（而不是复用 <see cref="PriestHealTarget"/>）是为了
        /// 让两次回复的选择互不覆盖 —— 否则双 9 时合计 17 点的两次回复会串味。
        /// </summary>
        PriestExtraHealTarget,

        // ---------- 游侠 ----------
        /// <summary>游侠：被攻击时是否用一只"1"闪避（以及用哪只手）。</summary>
        RangerDodge,

        /// <summary>游侠：攻击时是否把一只"2"变成 3 来让基础伤害 ×1.5。</summary>
        RangerPowerShot,

        // ---------- 剑客 ----------
        /// <summary>剑客：双 4/6/8 时是否发动"无视攻防加成"的追加攻击。</summary>
        SwordsmanFollowUp,

        /// <summary>
        /// 猎人：运算后若两只手同时是 1/2/3 且数字相同，
        /// 是否把参与运算的那只手暗置（进入伏击）。
        ///
        /// 玩家确认这是**主动选择**（"旁边给按钮"），
        /// 所以走与剑客追加攻击**同一条**拉择通道。
        /// </summary>
        HunterAmbush,

        // ---------- 狼人 ----------
        /// <summary>狼人：是否发动"双手和为 7"的回复与成长（预留）。</summary>
        WerewolfSurge,

        // ---------- 法师 ----------
        /// <summary>
        /// 法师：运算出攻击数字（4/6/8）时，是否**放弃本次攻击**，
        /// 改为给**下一次**攻击指定一种伤害类型。
        ///
        /// 玩家确认的顺序：
        ///   ① 先问"是否放弃攻击"
        ///   ② 选"放弃" -> 弹 10 种伤害类型
        ///   ③ 选完类型 -> 再选攻击目标
        ///
        /// 允许放弃（放弃 = 照常攻击）。
        ///
        /// ⚠️ 每次算出攻击数字**都要问**（玩家明确要求）。
        /// </summary>
        MageAbandonAttack,

        /// <summary>
        /// 法师：选择哪一种**伤害类型**（10 选 1）。
        ///
        /// 玩家确认：连打多次时**只保留最新的选择**（后选的覆盖先选的）。
        /// </summary>
        MageDamageType,
    }

    /// <summary>
    /// 供**角色钩子**回读"玩家在运算前选了什么"的接口。
    ///
    /// 放在 Core 而不是 Assistant：<see cref="AttackContext.Resolve"/> 与角色钩子都在 Core 层，
    /// 不能引用 Assistant。由 Assistant 的抉择器（SimpleChoiceProvider）实现本接口。
    ///
    /// 钩子拿到的是**行动方自己**的 sink，因此不需要猜"这次该读谁的记录"。
    /// </summary>
    public interface IPreTurnChoiceSink
    {
        /// <summary>
        /// 回读某个抉择的结果；没记录过时返回 <paramref name="fallback"/>。
        /// <paramref name="variant"/> 用于区分同类抉择的第几个（多段攻击）。
        /// </summary>
        Hand GetChoice(HandEffect effect, Hand fallback = null, int variant = 0);

        /// <summary>
        /// 只返回**玩家显式记录**过的答案；没记录过时返回 null
        /// （**绝不套用任何回退值**）。
        ///
        /// 用于**分阶段抉择**的判断，例如"牧师这次选了攻击还是回复"：
        ///   · 选了攻击   -> 记录为 null
        ///   · 选了回复   -> 记录为一只手（哪怕目标就是自己）
        ///
        /// 若用 <see cref="GetChoice"/>，第二种情况会拿到非 null 的回退值，
        /// 于是"选了攻击"被误判成"选了回复" —— 真实 bug：
        /// 牧师选了攻击仍被追问回复目标。
        /// </summary>
        Hand GetExplicitChoice(HandEffect effect, int variant = 0);
    }

    /// <summary>
    /// **法师的伤害类型选择**结果的存放处。
    ///
    /// 为什么不复用 <see cref="IPreTurnChoiceSink"/>：
    /// 那个接口的返回值是 <see cref="Hand"/>（"选哪只手"），
    /// 而这里选的是 <see cref="DamageType"/>（10 选 1 的枚举）——
    /// 硬塞进 Hand 会变成"用值当前缀的假手牌"，语义脏且容易出错。
    ///
    /// 由 Assistant 层的抉择器实现（那个对象同时是 AI / 脚本 / 界面玩家的答案来源），
    /// <see cref="Characters.SpecialLogics.MageLogic"/> 在结算时回读。
    /// </summary>
    public interface IDamageTypeChoiceSink
    {
        /// <summary>玩家 / AI 选定的伤害类型（没选过时是 <see cref="DamageType.None"/>）。</summary>
        DamageType ChosenDamageType { get; set; }

        /// <summary>是否愿意**放弃这次攻击**来设定伤害类型。</summary>
        bool WantsToAbandonAttack { get; set; }

        /// <summary>
        /// 由 Core 注入**当前对局**。
        ///
        /// ⚠️ 为什么需要它：抉择器（`GreedyChoiceProvider`）要按局面挑伤害类型，
        ///    而它的 `Game` 属性原本**只在 `BoardController` 里被赋值** ——
        ///    无头路径（`MatchRunner` / 测试 harness）从不设置，
        ///    于是 AI 的分支第一句 `if (Game == null) return null;` 直接放弃，
        ///    表现成"AI 法师从不在无头对局里铺状态"。
        ///
        ///    用接口而不是反射：反射在 IL2CPP 下有裁剪风险，
        ///    而这里已经有现成的实现类型可以声明。
        /// </summary>
        void SetGameForChoices(GameManager game);
    }

    /// <summary>
    /// **法师的跨回合状态**（待用的伤害类型）。
    ///
    /// 为什么要这个接口：AI（Assistant 层）需要知道
    /// "法师是不是已经铺好一个类型了" —— 铺好了就不该再放弃攻击。
    /// 而 **Assistant 不能引用 Characters 层**（架构铁律），
    /// 所以状态必须通过 Core 接口暴露。
    /// </summary>
    public interface IMageState
    {
        /// <summary>是否已经选定了一个待用的伤害类型。</summary>
        bool HasPendingType { get; }

        /// <summary>待用的伤害类型。</summary>
        DamageType PendingDamageType { get; }

        /// <summary>设定待用类型（覆盖旧的 —— 玩家确认只保留最新选择）。</summary>
        void SetPendingDamageType(DamageType type);

        /// <summary>
        /// 从抉择器**回读**这一次的选择并落到自己身上
        /// （选了"放弃攻击"才记类型；没放弃就什么都不做）。
        /// </summary>
        void ReadFromSink(IDamageTypeChoiceSink sink);

        /// <summary>
        /// 本回合是否决定**放弃攻击**（由 <see cref="ReadFromSink"/> 回读写入）。
        /// </summary>
        bool WantsToAbandonThisTurn { get; set; }
    }

    /// <summary>
    /// "结算过程中还需要问玩家"的回调 —— 由上层（BoardController）实现。
    ///
    /// 为什么需要它：多段攻击（忍者两段、剑客追加）的每一段都要单独问一次闪避，
    /// 而"要闪几段"只有执行时才知道。所以结算过程必须能在中途把问题抛出来，
    /// 由上层暂停、问玩家、再继续。
    ///
    /// 没有装这个回调时（例如直接 new AttackContext 的单元测试），
    /// 角色会退化成"自动选第一个可用的"，不会卡住。
    /// </summary>
    public interface IChoiceGatherer
    {
        /// <summary>
        /// 结算中请求一次抉择。返回玩家选中的手（null = 放弃）。
        /// 实现方负责暂停结算、等待玩家输入、再返回结果。
        /// </summary>
        Hand GatherChoice(ChoiceRequest request, PlayerState decider);
    }

    /// <summary>
    /// 角色钩子回读抉择的统一入口。
    ///
    /// 为什么需要它（这里的顺序很关键）：
    ///   · 经 <c>BoardController</c> 的正常流程会在运算前把玩家的选择写进 sink，
    ///     所以**先回读 sink**；
    ///   · 直接调 <c>GameManager.ExecuteTurn</c> 的场景（测试、脚本化对局）没有收集步骤，
    ///     sink 里没有记录，此时必须**当场询问抉择器**，否则角色的抉择会静默失效 ——
    ///     这个坑踩过一次（野蛮人锁手全部失灵）。
    /// </summary>
    public static class PreTurnChoiceReader
    {
        public static Hand Read(IPreTurnChoiceSink sink, IChoiceProvider provider,
                                ChoiceRequest request, PlayerState decider)
        {
            // ① 先问抉择器 —— 只有它知道"这一次"该选谁。
            //
            //    必须**优先**于回读 sink：sink 里存的是上一次的选择，
            //    而一次攻击可能有多段（忍者的两段、剑客的追加），
            //    每段都要各自的答案。曾经先回读 sink，导致：
            //      · 游侠第二段拿到的是第一段用过的那只手（已变成 2）→ 第二段闪不掉；
            //      · 强化时也拿到过期的手 → 无法发动强化。
            if (provider != null)
            {
                Hand asked = provider.ChooseHandFor(request, decider);

                // 结果必须在候选里，否则视为无效（交由下一步回退）
                if (asked != null && ContainsCandidate(request.Candidates, asked)) return asked;
            }

            // ② 再回读 sink（UI 在运算前已经让玩家点过手牌的场景）
            Hand recorded = sink?.GetChoice(request.Effect);
            if (recorded != null) return recorded;

            // ③ 都没给出有效结果 -> 用放弃值（可能是 null，也可能是"回给自己"这类默认）
            return request.DeclineResult;
        }

        private static bool ContainsCandidate(System.Collections.Generic.IReadOnlyList<Hand> list, Hand hand)
        {
            if (list == null || hand == null) return false;

            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], hand)) return true;

            return false;
        }
    }

    /// <summary>
    /// 角色在"做运算之前"必须向玩家问清楚的一个抉择。
    ///
    /// 为什么要在运算前问：<see cref="GameManager.ExecuteTurn"/> 一旦开始就会**一次跑完**，
    /// 没有"暂停 / 恢复"机制。所以所有会影响这次结算的抉择都必须在开跑前收集齐。
    /// </summary>
    public readonly struct ChoiceRequest
    {
        /// <summary>这是哪个抉择（用于回读玩家选了什么）。</summary>
        public HandEffect Effect { get; }

        /// <summary>展示给玩家的提示文字。</summary>
        public string Prompt { get; }

        /// <summary>可选项。空表示没有可选目标。</summary>
        public IReadOnlyList<Hand> Candidates { get; }

        /// <summary>是否允许放弃。允许时 UI 应提供"取消 / 不发动"。</summary>
        public bool AllowDecline { get; }

        /// <summary>
        /// 放弃时对应的回读结果（<see cref="HandEffect.BarbarianLockTarget"/> 用 null 表示不锁）。
        /// </summary>
        public Hand DeclineResult { get; }

        /// <summary>
        /// 该由谁回答这个抉择。null 表示"由行动方回答"。
        ///
        /// 大部分抉择是行动方自己的事（锁手、强化射击），但有些属于防守方
        /// （游侠的闪避）—— 所以不能一律用行动方。由角色在收集时显式指定。
        /// </summary>
        public PlayerState Decider { get; }

        /// <summary>
        /// "发动"按钮的文案。非空时 UI 会在"不发动"左侧显示一个发动按钮。
        ///
        /// 有些抉择只有唯一一个候选（例如剑客的追加攻击、游侠的强化射击），
        /// 这时让玩家去点手牌是很别扭的 —— 给一个明确的按钮更直观。
        /// </summary>
        public string AcceptLabel { get; }

        /// <summary>
        /// true 表示这是"一次攻击中的第 N 段"，不需要再问玩家、直接自动选择。
        ///
        /// 用于多段攻击：忍者一次攻击有两段，剑客的追加攻击也算独立一次。
        /// 让玩家为每一段单独点一次手牌是不现实的（点完第一段就没了），
        /// 所以这类"顺手再闪一下"由系统自动完成，并把结果写进战报。
        /// </summary>
        public bool AutoResolve { get; }

        /// <summary>
        /// 同类抉择的序号（第几段攻击就取第几个答案）。
        ///
        /// 一次性攻击只有一个闪避抉择；忍者两段就需要两个（0、1）。
        /// sink 用 <c>effect#variant</c> 作键分别存放，避免互相覆盖。
        /// </summary>
        public int Variant { get; }

        /// <summary>
        /// 可选：判定"这一次是否根本不用问"。
        ///
        /// 用途：**分阶段的抉择**。例如牧师的 4/6/8 要先问"攻击还是回复"，
        /// 选了攻击就**不该再问**"回复给谁"。
        /// 上层在推进时会调用它，返回 true 就直接按 <see cref="DeclineResult"/> 落地、
        /// 不打扰玩家。
        ///
        /// 这样"后一步的问题"可以提前收集（`ExecuteTurn` 要求运算前收集齐），
        /// 又不会真的问出来 —— 避免"选了攻击却被追问回复目标"这类 bug。
        /// </summary>
        public System.Func<bool> SkipWhen { get; }

        public ChoiceRequest(HandEffect effect, string prompt,
                             IReadOnlyList<Hand> candidates,
                             bool allowDecline = false,
                             Hand declineResult = null,
                             PlayerState decider = null,
                             string acceptLabel = null,
                             bool autoResolve = false,
                             int variant = 0,
                             System.Func<bool> skipWhen = null)
        {
            Effect = effect;
            Prompt = prompt ?? "";
            Candidates = candidates ?? Array.Empty<Hand>();
            AllowDecline = allowDecline;
            DeclineResult = declineResult;
            Decider = decider;
            AcceptLabel = acceptLabel;
            AutoResolve = autoResolve;
            Variant = variant;
            SkipWhen = skipWhen;
        }

        /// <summary>这一次是否应被自动跳过（不问玩家，直接按放弃值落地）。</summary>
        public bool ShouldSkip() => SkipWhen != null && SkipWhen();
    }

    /// <summary>
    /// 角色在"做运算之前"需要玩家做抉择的能力。由角色自己实现。
    ///
    /// 这样 <see cref="Assistant.BoardController"/> 不需要认识任何具体角色类型：
    /// 它只遍历 <see cref="ICharacterBehavior"/>，谁实现了本接口就问谁。
    /// （在引入本接口之前，控制器里写的是 <c>is BarbarianLogic</c> 这种硬编码判断，
    /// 每加一个角色就要改控制器，不可扩展。）
    /// </summary>
    public interface IPreTurnChoiceProvider
    {
        /// <summary>
        /// 追加本次运算需要玩家先决定的抉择（可以追加多个）。
        /// 调用时机是"行动方与目标手都已选定、运算尚未执行"。
        /// </summary>
        void CollectPreTurnChoices(PreTurnChoiceContext context, IList<ChoiceRequest> requests);

        /// <summary>
        /// 玩家答完一个抉择后，按**刚收到的答案**追加后续问题（默认什么都不做）。
        ///
        /// 为什么需要：`GameManager.ExecuteTurn` 一次跑完整个回合，
        /// 抉择必须在运算前收集齐；但有些问题**依赖前一个答案**
        /// （例如牧师先选"攻击还是回复"，选了回复才需要问"回复给谁"）。
        /// 控制器不参与判断 —— 它只把"刚答完的请求 + 答案"交给角色。
        /// </summary>
        void CollectFollowUpChoices(ChoiceRequest answered, Hand answer,
                                    IList<ChoiceRequest> requests);
    }

    /// <summary>
    /// 角色声明"需要在**选定目标手之前**先问的问题"。
    ///
    /// 目前只有牧师：运算出 4/6/8 时先问"当攻击打还是当回复用"，
    /// 选了之后再点目标手（玩家要求的顺序）。
    ///
    /// 与 <see cref="IPreTurnChoiceProvider"/> 的区别只是**时机**：
    /// 后者在"目标手已选定"之后问。
    /// </summary>
    public interface ITargetHandChoiceProvider
    {
        /// <summary>
        /// 在选定目标手**之前**需要先问的抉择（可返回多个）。
        /// 只有在"已选运算源、尚未选目标手"时才调用。
        /// </summary>
        void CollectPreTargetChoices(PreTurnChoiceContext context, IList<ChoiceRequest> requests);
    }

    /// <summary>
    /// 角色声明"本次运算**结果**是否需要指定目标"。
    ///
    /// 用于决定交互：只有会产生**指向某人的效果**（攻击 / 回复 / 追加攻击……）
    /// 时，才让玩家"再点一次手牌确认打谁"。
    /// 否则 1 + 1 = 2 这种**无效果**的运算也会被追问目标，纯属打扰（玩家实测反馈）。
    ///
    /// 角色可以重写（例如牧师：他的 4/6/8 可以改成回复，同样需要选人）。
    /// </summary>
    public interface IResultTargetProvider
    {
        /// <summary>
        /// 本次运算结果是否会产生一个需要指定目标的效果。
        /// </summary>
        bool ResultNeedsTarget(PreTurnChoiceContext context);
    }

    /// <summary>
    /// 收集抉择时的局面信息。
    /// </summary>
    public sealed class PreTurnChoiceContext
    {
        /// <summary>本次运算的行动方。</summary>
        public PlayerState Actor { get; }

        /// <summary>防守方。</summary>
        public PlayerState Defender { get; }

        /// <summary>行动方这次用来运算的手。</summary>
        public Hand ActedHand { get; }

        /// <summary>对方被选中的那只手。</summary>
        public Hand TargetHand { get; }

        /// <summary>
        /// 这次运算的结果（已按十进制归一化）。
        ///
        /// ⚠️ 可写：构造时按"两手的和"算出来，但**发起运算的那只手在结算中
        ///    会先被改写成结果值**，所以"结算中途"问的抉择要重新填一次
        ///    （法师的"结果是不是 4/6/8"就依赖它 —— 填错会永远问不出东西）。
        /// </summary>
        public int ResultValue { get; set; }

        /// <summary>
        /// 这次运算会产生什么效果。
        ///
        /// 计算时优先使用"运算前的手值快照"（<see cref="ActedHandValueBefore"/> /
        /// <see cref="TargetHandValueBefore"/>）—— 运算源在结算钩子里已经是结果值，
        /// 直接用当前手值会把效果算错（曾经算成 None，导致游侠闪避被整个跳过）。
        /// </summary>
        public HandEffectType Effect => Hand.GetEffectType(EffectiveResult);

        /// <summary>运算结果的权威值（优先用快照重算）。</summary>
        public int EffectiveResult
        {
            get
            {
                int a = ActedHandValueBefore ?? (ActedHand?.Value ?? 0);
                int b = TargetHandValueBefore ?? (TargetHand?.Value ?? 0);
                return Hand.ComputeSum(a, b);
            }
        }

        /// <summary>这次效果是否是一次攻击。</summary>
        public bool IsAttack =>
            Effect == HandEffectType.Attack4
            || Effect == HandEffectType.Attack6
            || Effect == HandEffectType.Attack8;

        /// <summary>
        /// 该由谁回答抉择时的抉择器（通常是防守方的）。
        ///
        /// 用于"直接调 GameManager.ExecuteTurn、没有运算前收集步骤"的场景：
        /// 此时 sink 里没有记录，角色钩子需要当场询问抉择器。
        /// </summary>
        public IChoiceProvider Provider { get; set; }

        /// <summary>
        /// true 表示这次抉择由系统**自动**决定（不弹给玩家）。
        /// 没有上层回调时（单元测试直连）走这条路径。
        /// </summary>
        public bool AutoResolveChoice { get; set; }

        /// <summary>
        /// 结算过程中询问玩家的回调（由上层注入）。
        /// 非 null 时角色应当**用它**把问题抛给玩家，而不是自己选。
        /// </summary>
        public IChoiceGatherer Gatherer { get; set; }

        /// <summary>
        /// 查询"AI 已经替某一方做出的抉择"（由上层注入）。
        ///
        /// **必须优先于回读 sink**：sink 是跨回合保留的，
        /// 里面的旧答案会被当成"本回合的决定"，导致技能不发动。
        /// 这份记录由上层在回合边界清空，因此只反映本回合。
        /// 返回 null 表示"本回合 AI 没有就此项做过决定"。
        /// </summary>
        public System.Func<HandEffect, int, Hand> AiDecisionLookup { get; set; }

        /// <summary>
        /// **场上还活着几名玩家**（由 <see cref="GameManager"/> 在构造上下文时填）。
        ///
        /// 用途：有些规则**只在场上仅剩两人时**生效 ——
        /// 例如野蛮人的"同时只能控制对手一只手"。
        ///
        /// 取 <see cref="UnknownAliveCount"/> 表示"没填"（单元测试直连时），
        /// 这时按**保守**方式处理（当作 1v1，限制生效），
        /// 以免多人局的放行规则被误用到对局里。
        /// </summary>
        public int AlivePlayerCount { get; set; } = UnknownAliveCount;

        /// <summary>"存活人数未知"的哨兵值。</summary>
        public const int UnknownAliveCount = -1;

        /// <summary>
        /// 当前对局（由 <see cref="Assistant.BoardController"/> 在收集前填入）。
        ///
        /// 用途：有些抉择的**候选必须按距离过滤**（圣武士的冲击目标
        /// 只能在攻击范围内）—— 没有 GameManager 就算不出距离。
        ///
        /// 直连单元测试时可能为 null，此时各角色应退化为"不按距离过滤"。
        /// </summary>
        public GameManager Manager { get; set; }

        /// <summary>场上是否**只剩两名玩家**（本来就 1v1，或多人局打到只剩两人）。</summary>
        public bool OnlyTwoPlayersLeft =>
            AlivePlayerCount == UnknownAliveCount || AlivePlayerCount <= 2;

        /// <summary>查询 AI 抉择（便捷封装，未注入时返回 null）。</summary>
        public Hand GetAiDecision(HandEffect effect, int variant = 0)
            => AiDecisionLookup?.Invoke(effect, variant);

        /// <summary>
        /// 查询"本回合**先前已经问过**的抉择的答案"（由上层注入）。
        ///
        /// 用途：**分阶段的抉择**。牧师的 4/6/8 先问"攻击还是回复"
        /// （在选目标手之前），之后再决定"要不要问回复目标" ——
        /// 后者必须能读到前者的答案。
        ///
        /// ⚠️ 实现必须只返回**玩家显式给出的答案**，不能套用
        /// <see cref="ChoiceRequest.DeclineResult"/> 之类的回退 ——
        /// 否则"玩家选了攻击"会因为回退值非 null 而被误判成"选了回复"。
        /// </summary>
        public System.Func<HandEffect, int, Hand> AnsweredLookup { get; set; }

        /// <summary>查询先前的答案（未注入时返回 null）。</summary>
        public Hand GetAnswered(HandEffect effect, int variant = 0)
            => AnsweredLookup?.Invoke(effect, variant);

        /// <summary>牧师本次 4/6/8 是否被选择"当回复用"。</summary>
        public bool ChoseToHeal()
            => GetAnswered(HandEffect.PriestAttackOrHeal) != null;

        /// <summary>
        /// 运算源的**运算前**手值。默认取 <see cref="ActedHand"/> 的当前值，
        /// 由发起方在"运算源已被改写"的场景显式覆盖。
        /// </summary>
        public int? ActedHandValueBefore { get; set; }

        /// <summary>
        /// 运算目标的**运算前**手值。同上。
        /// </summary>
        public int? TargetHandValueBefore { get; set; }

        /// <summary>本次运算的结果（优先用保存的快照重算，保证运算后被改写也不出错）。</summary>
        public int EffectiveSum
        {
            get
            {
                int a = ActedHandValueBefore ?? (ActedHand?.Value ?? 0);
                int b = TargetHandValueBefore ?? (TargetHand?.Value ?? 0);
                return Hand.ComputeSum(a, b);
            }
        }

        /// <summary>本次运算产生的效果（基于 <see cref="EffectiveSum"/>）。</summary>
        public HandEffectType EffectiveEffect => Hand.GetEffectType(EffectiveSum);

        /// <summary>结果值对应的基础伤害（仅攻击时有效）。</summary>
        public int BaseDamage => IsAttack ? (int)Effect : 0;

        /// <summary>
        /// 闪避/免伤的原因，供战报显示。
        /// 例如刺客潜行时是"无法被攻击"，而不是"被游侠闪避"。
        /// </summary>
        public string DodgeReason { get; set; }

        /// <summary>
        /// 闪避是否已发生。由 <see cref="CharacterTemplate.TryDodge"/> 的实现写入，
        /// <see cref="AttackContext.Resolve"/> 读取后跳过护盾与扣血。
        /// </summary>
        public bool Dodged { get; set; }

        /// <summary>闪避发生时记录消耗掉的那只手。</summary>
        public Hand ConsumedHand { get; set; }

        public PreTurnChoiceContext(PlayerState actor, PlayerState defender,
                                    Hand actedHand, Hand targetHand)
        {
            Actor = actor;
            Defender = defender;
            ActedHand = actedHand;
            TargetHand = targetHand;
            ResultValue = Hand.ComputeSum(actedHand?.Value ?? 0, targetHand?.Value ?? 0);
        }

        /// <summary>
        /// 连带填好"场上存活人数"的构造。
        ///
        /// 由 <see cref="GameManager"/> 一侧调用 —— 只有它知道还剩几个人，
        /// 而"仅剩两人才生效"的规则（野蛮人锁手）需要这个信息。
        /// </summary>
        public PreTurnChoiceContext(PlayerState actor, PlayerState defender,
                                    Hand actedHand, Hand targetHand,
                                    int alivePlayerCount)
            : this(actor, defender, actedHand, targetHand)
        {
            AlivePlayerCount = alivePlayerCount;
        }

        /// <summary>
        /// 该角色自己的两只手（只返回属于 <paramref name="owner"/> 的）。
        /// </summary>
        public static List<Hand> HandsOf(PlayerState owner)
        {
            var list = new List<Hand>(2);
            if (owner == null) return list;

            list.Add(owner.LeftHand);
            list.Add(owner.RightHand);
            return list;
        }
    }
}
