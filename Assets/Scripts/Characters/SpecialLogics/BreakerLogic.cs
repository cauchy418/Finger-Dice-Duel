using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 战士·破阵者（Breaker）特殊逻辑：
    ///
    /// 【技能一 · 破阵强袭】
    ///   当战士·破阵者自身的一面盾牌破碎时（无论是被攻击破碎，还是运算前主动破碎已有盾牌），
    ///   战士·破阵者可以选择场上任意一面【完整（Formed）】的盾牌，使其立即破碎。
    ///
    ///   ⚠️ **必须在运算前收集这个抉择。**
    ///   `GameManager.ExecuteTurn` 一旦开始就一次跑完，没有"暂停 / 恢复"机制；
    ///   在结算钩子里直接问 provider 的话，**人类玩家永远看不到弹窗**
    ///   （AI 能当场作答，所以表现为"有时生效"）。这与野蛮人·守望者道途锁手是同一模式。
    ///
    ///   判定"自己的盾这次会不会碎"只需两个事实（都在运算前可知）：
    ///     · 自己当行动方，且运算源是 `Formed` 的盾 → 运算前会主动破碎；
    ///     · 对手当行动方，且这次会打出攻击、又选中了自己的 `Formed` 盾 → 会被打碎。
    ///
    /// 【技能二 · 盾牌修复】
    ///   当战士·破阵者新运算出 5、在某只手上合成一面盾牌时，
    ///   若其【另一只手】为破碎（Broken）状态的盾牌，则将其修复为 Formed。
    ///
    /// 依赖：
    ///   - `IPreTurnChoiceProvider`：运算前声明"要不要发动破阵强袭、打碎哪面盾"。
    ///   - `GameManager.BreakShieldAndNotify(...)`：统一破盾入口，保证连锁链正确触发。
    /// </summary>
    public class BreakerLogic : CharacterTemplate, IPreTurnChoiceProvider, ISkillUseCounter, IShieldBreakChoiceProvider,
                                IAiMoveAdvisor
    {
        public override string DisplayName => "战士·破阵者";
        private GameManager _choiceGame;
        private Hand _choiceActedHand;
        private bool _resolvingRangeBreak;

        /// <summary>
        /// 让 AI **主动去合盾**（玩家指出："武僧·太极宗和战士·破阵者一起做，因为两人都是 5 高价值"）。
        ///
        /// 依据：战士·破阵者的两个技能**都要求先有盾** ——
        ///   · 破阵强袭：自己的盾碎时，连带打碎场上一面完好的盾
        ///   · 碎盾换射程：另一只手是完整盾牌时，可碎它换无限距离
        /// 而盾只能由"运算出 5"产生。没有盾，两个技能都无从发动。
        ///
        /// ⚠️ 只靠价值表不够：它的 5 已经是 55（基线），
        ///    而"打出 5 只能合盾、没有伤害"在贪心评分里打不过一次攻击。
        ///    所以必须给"合盾"一条**直接奖励**。
        /// </summary>
        public override int ScoreMoveBonus(MoveValueContext context)
        {
            // 合盾（结果 5）
            if (context.ActedValueAfter == 5) return 28;

            // 已经是双盾时别再合盾（没有新收益）
            return 0;
        }

        /// <summary>
        /// 本局**破阵强袭**发动过几次（"自己的盾碎了，连带打碎场上一面完好的盾"）。
        ///
        /// ⚠️ 为什么需要它：这个技能发动后**场上没有任何可观测的痕迹**
        ///    （破盾本身也会被之后的操作覆盖），于是"AI 从不发动它"
        ///    这种问题根本测不出来 —— 我就是被这个卡住的。
        ///    加个计数器，技能发动率才可验证。
        /// </summary>
        public int ChainBreakCount { get; private set; }

        /// <summary>本局**碎盾换射程**发动过几次（牺牲自己的完整盾换无限距离）。</summary>
        public int RangeBreakCount { get; private set; }

        /// <summary>本局发动招牌技能几次 —— 见 <see cref="ISkillUseCounter"/>。</summary>
        public int SkillUseCount => ChainBreakCount + RangeBreakCount + ShieldFormCount;

        /// <summary>
        /// 本局**合出盾牌**几次。
        ///
        /// ⚠️ 为什么单独算它：玩家明确说过
        ///    "对手不主动触发战士·破阵者破盾无所谓，**战士·破阵者只要倾向于合盾即可**"。
        ///    两个技能都**以盾为前提**（自己的盾碎了才能连锁、
        ///    另一只手是盾才能换射程），所以"有没有去合盾"才是
        ///    AI 该被衡量的东西 —— 而"技能有没有打出去"取决于对手。
        /// </summary>
        public int ShieldFormCount { get; private set; }

        /// <summary>
        /// 运算前收集"要不要发动破阵强袭、打碎哪面盾"。
        ///
        /// 只在**自己的盾这次会碎**时才提问（见类注释里的两条判定）。
        /// 场上没有其他完好盾时**静默跳过**，不弹选择。
        /// </summary>
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                   IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;
            if (!CharacterBehaviors.ActorHas(context.Actor, this)) return;   // 只有自己行动时才会"自己的盾碎"
            _choiceGame=context.Manager;_choiceActedHand=context.ActedHand;

            // ① 新增技能：碎掉另一只手上的**完整盾牌**，换本次攻击距离无限。
            //
            // 只在"这次是攻击、且另一只手确实是完整盾"时才问 ——
            // 否则问了也没意义（非攻击不需要攻击距离）。
            AddRangeBreakRequest(context, requests);

            Hand breaking = OwnShieldThatWillBreak(context);
            if (breaking == null) return;

            var candidates = CollectFormedShields(context.Actor, context.Defender, breaking, context.Manager);
            if (candidates.Count == 0) return;              // 没有其他完好的盾 -> 不问

            requests.Add(new ChoiceRequest(
                HandEffect.BreakerChainBreak,
                $"战士·破阵者的盾即将破碎：要连带打碎场上一面完好的盾吗？" +
                $"（同意后点那面盾牌；被打碎的可能是自己的，也可能是对手的）",
                ToHands(candidates),
                allowDecline: true,
                declineResult: null,                        // 放弃 = 不连锁
                decider: context.Actor,
                acceptLabel: "破阵强袭"));
        }

        public void CollectShieldBreakChoices(PlayerState owner, Hand breakingShield, GameManager game, IList<ChoiceRequest> requests)
        {
            if (owner == null || breakingShield == null || !owner.Owns(breakingShield) || !breakingShield.IsShield) return;
            var candidates = CollectFormedShields(game.PlayerA, game.PlayerB, breakingShield, game);
            if (candidates.Count == 0) return;
            requests.Add(new ChoiceRequest(HandEffect.BreakerChainBreak,
                "自己的盾将挡下攻击并破碎：可连带打碎场上一面完整盾，或放弃。",
                ToHands(candidates), allowDecline: true, declineResult: null, decider: owner, acceptLabel: "破阵强袭"));
        }

        /// <summary>
        /// 新增技能结算：若玩家选择了"无畏冲锋"，就把另一只手上的完整盾牌打碎。
        ///
        /// 由 <see cref="GameManager"/> 在**攻击距离检查之前**调用
        /// （见 <see cref="IRangeBreakEffect"/>）——否则来不及生效。
        ///
        /// 与破阵强袭同一原则：**只回读已记录的答案**，不在结算里向玩家索取输入。
        /// </summary>
        public override void ApplyRangeBreak(PlayerState actor, Hand actedHand, GameManager manager)
        {
            if (actor == null || actedHand == null || manager == null) return;
            if (!actor.Owns(actedHand)) return;

            Hand other = actor.GetOtherHand(actedHand);
            if (other == null || other.State != ShieldState.Formed) return;

            // 没答过这个抉择 -> Read 会返回 null（视为放弃，保留盾）
            Hand chosen = PreTurnChoiceReader.Read(
                manager.GetChoiceProvider(actor) as IPreTurnChoiceSink,
                manager.GetChoiceProvider(actor),
                BuildRangeBreakRequest(actor, other), actor);

            if (chosen == null) return;
            if (!ReferenceEquals(chosen, other)) return;

            _resolvingRangeBreak=true;
            try { manager.BreakActiveShieldAndNotify(actor,other); }
            finally { _resolvingRangeBreak=false; }
            RangeBreakCount++;
            manager.NoteSkill(actor, "无畏冲锋", $"{CombatText.HandName(actor, other)}盾破碎，本次距离∞");
        }

        /// <summary>构造"无畏冲锋"的抉择（收集与结算共用同一份，避免两处漂移）。</summary>
        private static ChoiceRequest BuildRangeBreakRequest(PlayerState actor, Hand shield)
            => new ChoiceRequest(
                HandEffect.BreakerRangeBreak,
                $"战士·破阵者：另一只手是完整的盾牌。可以令它破碎，" +
                $"使本次攻击距离∞，命中附流血2回合。要发动吗？",
                new List<Hand>(1) { shield },
                allowDecline: true,
                declineResult: null,             // 放弃 = 保留盾
                decider: actor,
                acceptLabel: "无畏冲锋");

        /// <summary>
        /// 新增技能：**碎盾换无限距离**。
        ///
        /// 规则（玩家新增）：发起攻击时，若**另一只手是完整的盾牌**，
        /// 可以让它破碎，使本次攻击距离无限。
        ///
        /// 只在"**这次是攻击**"时才问 —— 非攻击不需要攻击距离，
        /// 问了只是打扰（与"无效果不追问目标"同一原则）。
        /// </summary>
        private static void AddRangeBreakRequest(PreTurnChoiceContext context,
                                                 IList<ChoiceRequest> requests)
        {
            Hand other = ShieldForRangeBreak(context);
            if (other == null) return;

            requests.Add(BuildRangeBreakRequest(context.Actor, other));
        }

        /// <summary>
        /// "无畏冲锋"可用的那面盾；没有则返回 null。
        ///
        /// 收集与结算共用同一判定，避免一处认得、另一处认不得（踩过的坑）。
        /// </summary>
        private static Hand ShieldForRangeBreak(PreTurnChoiceContext context)
        {
            if (context == null || !context.IsAttack) return null;

            // ⚠️ 必须先确认**运算源是自己的手**。
            // 新规则允许"被加数"是任意人的手，而某些调用点会把那只手也放进
            // ActedHand（历史命名），于是 GetOtherHand 会抛
            // "传入的 Hand 不属于该玩家"（真实 bug，AI 对局测试抓到）。
            if (context.Actor == null) return null;
            if (context.ActedHand == null || !context.Actor.Owns(context.ActedHand)) return null;

            Hand other = context.Actor.GetOtherHand(context.ActedHand);
            if (other == null || other.IsLocked) return null;
            if (other.State != ShieldState.Formed) return null;

            return other;
        }

        /// <summary>
        /// 判断"行动方这次运算会让**自己的哪一面盾**破碎"；没有则返回 null。
        ///
        /// 两种情况（都在运算前可知）：
        ///   ① 运算源本身是 `Formed` 的盾 —— 运算前会主动破碎；
        ///   ② 对手打出攻击、且攻击目标选中了自己的 `Formed` 盾 —— 会被打碎。
        /// </summary>
        private static Hand OwnShieldThatWillBreak(PreTurnChoiceContext context)
        {
            PlayerState me = context.Actor;
            PlayerState foe = context.Defender;

            // ① 主动破盾：运算源是成型盾
            Hand acted = context.ActedHand;
            if (acted != null && acted.State == ShieldState.Formed && me.Owns(acted))
                return acted;

            // ② 被打碎：运算结果是攻击，且目标手是自己那面成型盾
            if (context.IsAttack)
            {
                Hand target = context.TargetHand;
                if (target != null && target.State == ShieldState.Formed && me.Owns(target))
                    return target;
            }

            return null;
        }

        /// <summary>把候选（含所有者）摊平成手牌列表 —— 抉择的 Candidates 只接受 Hand。</summary>
        private static List<Hand> ToHands(List<ChainBreakTarget> targets)
        {
            var hands = new List<Hand>(targets.Count);
            for (int i = 0; i < targets.Count; i++) hands.Add(targets[i].Shield);
            return hands;
        }

        /// <summary>
        /// 结算：自己的盾破碎时，把"运算前收集到的答案"施加出去。
        ///
        /// ⚠️ 这里**不能**再向 provider 索要输入 —— 结算中无法暂停问人类玩家。
        /// 只回读 sink / 抉择器里**已经记录**的答案。
        /// </summary>
        public override void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager)
        {
            if (manager == null || owner == null || brokenHand == null) return;

            var candidates = CollectFormedShields(manager.PlayerA, manager.PlayerB, brokenHand, manager);
            if (candidates.Count == 0) return;              // 没有其他完好的盾 -> 无事可做

            var provider = manager.GetChoiceProvider(owner);
            if (provider == null) return;

            Hand picked = PreTurnChoiceReader.Read(
                provider as IPreTurnChoiceSink, provider,
                BuildReadRequest(owner, candidates,_resolvingRangeBreak?HandEffect.BreakerRangeChainBreak:HandEffect.BreakerChainBreak), owner);

            if (picked == null) return;                     // 放弃

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!ReferenceEquals(candidates[i].Shield, picked)) continue;
                if (candidates[i].Shield.State != ShieldState.Formed) return;   // 防御性
                manager.NoteSkill(owner, "破阵强袭", $"{candidates[i].Owner.Name} {CombatText.HandName(candidates[i].Owner, candidates[i].Shield)}盾破碎",candidates[i].Owner,new SkillVisualData(brokenHand,candidates[i].Shield));
                manager.BreakShieldAndNotify(candidates[i].Owner, candidates[i].Shield, owner);
                ChainBreakCount++;
                return;
            }
            // 选了一个不在候选里 / 已经碎掉的盾 -> 视为放弃
        }

        /// <summary>回读答案用的请求（候选与收集阶段保持一致）。</summary>
        private static ChoiceRequest BuildReadRequest(PlayerState owner, List<ChainBreakTarget> candidates,HandEffect effect=HandEffect.BreakerChainBreak)
            => new ChoiceRequest(
                effect,
                "战士·破阵者：要连带打碎哪一面完好的盾？",
                ToHands(candidates),
                allowDecline: true,
                declineResult: null,
                decider: owner);

        public override void CollectFollowUpChoices(ChoiceRequest answered,Hand answer,IList<ChoiceRequest> requests)
        {
            if(answered.Effect!=HandEffect.BreakerRangeBreak||answer==null||_choiceGame==null)return;
            var owner=answered.Decider??_choiceGame.OwnerOfHand(answer);
            if(owner==null||!owner.Owns(answer)||!answer.IsShield)return;
            var candidates=CollectFormedShields(null,null,answer,_choiceGame);
            // 另一面完整盾若本身也是运算源，会先主动破碎，不能再选作连锁目标。
            candidates.RemoveAll(c=>ReferenceEquals(c.Shield,_choiceActedHand));
            if(candidates.Count==0)return;
            requests.Add(new ChoiceRequest(HandEffect.BreakerRangeChainBreak,
                "碎盾换射程：先连带打碎一面完整盾，再结算本次攻击；也可不连锁。",
                ToHands(candidates),allowDecline:true,declineResult:null,decider:owner,acceptLabel:"破阵强袭"));
        }

        /// <summary>收集场上所有 `Formed` 的盾（排除 <paramref name="exclude"/> 那一面）。</summary>
        private static List<ChainBreakTarget> CollectFormedShields(PlayerState a, PlayerState b, Hand exclude, GameManager manager = null)
        {
            var list = new List<ChainBreakTarget>(4);
            if (manager != null)
            {
                foreach (var player in manager.Players)
                {
                    if (player == null || !player.IsAlive) continue;
                    AddIfFormed(list, player, player.LeftHand, exclude);
                    AddIfFormed(list, player, player.RightHand, exclude);
                }
                return list;
            }
            if (a != null)
            {
                AddIfFormed(list, a, a.LeftHand, exclude);
                AddIfFormed(list, a, a.RightHand, exclude);
            }
            if (b != null)
            {
                AddIfFormed(list, b, b.LeftHand, exclude);
                AddIfFormed(list, b, b.RightHand, exclude);
            }
            return list;
        }



        /// <summary>
        /// 合成新盾时触发：如果另一只手是“破碎”盾，则修复为完整盾。
        /// 注意：这里的修复是被动、自动的（规则表述并未强调“可选”），
        /// 如需改为“可选”，把下面 if 内改为通过 IChoiceProvider 询问即可。
        /// </summary>
        public override void OnShieldFormed(PlayerState owner, Hand shieldHand, GameManager manager)
        {
            if (owner == null || shieldHand == null) return;

            ShieldFormCount++;

            var otherHand = owner.GetOtherHand(shieldHand);
            if (otherHand != null && otherHand.State == ShieldState.Broken)
            {
                otherHand.SetShieldState(ShieldState.Formed);
                manager?.NoteSkill(owner, "盾阵重整", $"{CombatText.HandName(owner, otherHand)}碎盾修复");
            }
        }

        /// <summary>
        /// 整场比赛开始时清空状态。战士·破阵者的技能不持有跨回合状态，
        /// 但显式声明"已复位"便于后续扩展（例如记录本局连锁次数）。
        /// </summary>
        public override void OnMatchReset(PlayerState owner, GameManager manager) { }

        /// <summary>默认攻击 +1（可被 CharacterDefinition.AttackBonus 覆盖）。</summary>
        public override int DefaultAttackBonus => 1;
        public override int DefaultDefenseBonus => 0;


        private static void AddIfFormed(List<ChainBreakTarget> list, PlayerState owner, Hand h, Hand exclude)
        {
            if (h == exclude) return;
            if (h.State == ShieldState.Formed) list.Add(new ChainBreakTarget(owner, h));
        }
    }
}
