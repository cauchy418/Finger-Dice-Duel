using System;
using System.Collections.Generic;
using FingerDiceDuel.Characters.SpecialLogics;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 单回合结算结果，供 UI 展示与测试断言使用。
    ///
    /// 注意：<see cref="ActedHand"/> / <see cref="TargetHand"/> 是**活动引用**。
    /// 同一只手会在后续回合继续变化，因此事后回看历史记录时，这两个引用反映的是
    /// "最终状态"而不是"当时状态"。需要当时数值时，一律使用
    /// <see cref="ActedHandValueBefore"/> / <see cref="ActedHandValueAfter"/> /
    /// <see cref="TargetHandValue"/> 这三个**快照**。
    /// </summary>
    public struct ExtraAttackInfo
    {
        /// <summary>发起者名字（战报用）。</summary>
        public string ActorName { get; }

        /// <summary>固定伤害值（无视攻防）。</summary>
        public int Damage { get; }

        /// <summary>实际造成的伤害（被护盾挡下时为 0）。</summary>
        public int Dealt { get; }

        /// <summary>是否被护盾挡下。</summary>
        public bool WasBlocked { get; }

        public ExtraAttackInfo(string actorName, int damage, int dealt, bool wasBlocked)
        {
            ActorName = actorName ?? "";
            Damage = damage;
            Dealt = dealt;
            WasBlocked = wasBlocked;
        }
    }

    /// <summary>
    /// 一次运算的结算结果。所有字段都是**快照**，不持有 Hand 的实时引用
    /// （这样 UI 显示历史战报时不会因为手值后续变化而前后不一致）。
    /// </summary>
    public class TurnResult
    {
        public PlayerState Actor;
        public Hand ActedHand;
        public Hand TargetHand;

        /// <summary>运算前行动方那只手的数值（快照）。</summary>
        public int ActedHandValueBefore;

        /// <summary>运算后行动方那只手的数值（快照，等于 <see cref="ResultValue"/>）。</summary>
        public int ActedHandValueAfter;

        /// <summary>运算时对方那只手的数值（快照）。</summary>
        public int TargetHandValue;

        public int ResultValue;
        public HandEffectType Effect;
        public bool ShieldBrokenBeforeOp;
        public int DamageDealt;
        public bool WasBlocked;

        /// <summary>本次攻击是否被闪避（游侠 / 刺客潜行）。</summary>
        public bool WasDodged;

        /// <summary>免伤原因（例如"目标在潜行中，无法被攻击"）。供战报显示。</summary>
        public string DodgeReason;

        public int Healed;

        /// <summary>本次回复效果实际作用到谁（牧师可以回给对手）。非回复效果时为 null。</summary>
        public PlayerState HealTarget;

        /// <summary>本次运算**之前**发生的追加攻击（剑客）。没有则为 null，供战报单独显示一行。</summary>
        public ExtraAttackInfo? ExtraAttack;

        /// <summary>本次攻击的段数（忍者 = 2，其余 = 1）。战报用它说明"两段攻击"。</summary>
        public int HitCount = 1;

        /// <summary>造成伤害的段数。</summary>
        public int LandedHits;

        /// <summary>被护盾挡下的段数。</summary>
        public int BlockedHits;

        /// <summary>被闪避掉的段数。</summary>
        public int DodgedHits;

        public bool GameOver;
        public PlayerState Winner;

        /// <summary>便于日志：例如 "A: 3 + 6 = 9 (Heal)"。</summary>
        public override string ToString()
            => $"{Actor?.Name}: {ActedHandValueBefore} + {TargetHandValue} = {ResultValue} ({Effect})";
    }

    /// <summary>
    /// 流程控制器：负责回合驱动、结算触发、胜负判定、连锁破盾防递归、整场复位。
    ///
    /// 回合契约（详见 RULES.md 第 5 节）：
    ///   行动方选自己的 1 只手 + 对方的 1 只手；
    ///   **只有行动方那只手的数值会改变**，对方的手值不变；
    ///   效果类型由行动方手的新值决定。
    /// </summary>
    public class GameManager
    {
        public PlayerState PlayerA { get; }
        public PlayerState PlayerB { get; }

        public IChoiceProvider ChoiceProviderA { get; set; }
        public IChoiceProvider ChoiceProviderB { get; set; }

        public bool IsGameOver { get; private set; }
        public PlayerState Winner { get; private set; }
        public PlayerState CurrentPlayer { get; private set; }
        public int TurnCount { get; private set; }

        /// <summary>本回合已破碎护盾集合，防止连锁破盾递归死循环。</summary>
        private readonly HashSet<Hand> _brokenThisTurn = new HashSet<Hand>();

        /// <summary>
        /// 由角色在结算过程中写入的"本次追加攻击"记录（剑客用）。
        /// <see cref="ResolveTurn"/> 结束时收进 <see cref="TurnResult.ExtraAttack"/> 并清空。
        ///
        /// 之所以要中转一下：追加攻击发生在 <c>OnBeforeEffectResolved</c> 里，
        /// 那时还没有 <see cref="TurnResult"/> 可以写。
        /// </summary>
        public ExtraAttackInfo? PendingExtraAttack;

        /// <summary>
        /// true 表示当前正在结算"追加攻击"（剑客）。
        ///
        /// 这类攻击发生在主攻击之前，玩家还没做过闪避选择，
        /// 所以它的闪避要**自动结算**（有几只"1"就闪几段），不弹提示。
        /// </summary>
        public bool ResolvingExtraAttack;

        /// <summary>
        /// 回合数上限。达到上限时按**平局**结束（见 RULES.md §7）。
        /// 这保证了对局一定会在有限步内结束，不会出现"双方都只顾回复"的死循环。
        /// </summary>
        public const int TurnLimit = 50;

        private readonly List<LegalMove> _legalMoves = new List<LegalMove>(4);

        public GameManager(PlayerState a, PlayerState b,
                           IChoiceProvider providerA, IChoiceProvider providerB)
        {
            PlayerA = a ?? throw new ArgumentNullException(nameof(a));
            PlayerB = b ?? throw new ArgumentNullException(nameof(b));
            ChoiceProviderA = providerA;
            ChoiceProviderB = providerB;
            CurrentPlayer = a;
            TurnCount = 0;

            // 开局即通知角色复位，保证内部状态与首局状态一致。
            NotifyMatchReset();
        }

        public PlayerState GetOpponent(PlayerState p) => p == PlayerA ? PlayerB : PlayerA;

        /// <summary>
        /// 查询"AI 已经替某一方做出的抉择"（由上层 BoardController 注入）。
        ///
        /// 为什么需要：`PreTurnChoiceReader.Read` 会回读 sink，而 sink 跨回合保留，
        /// 旧答案会被误当成"本回合的决定"，导致 AI 的技能不发动。
        /// 这份记录由上层在回合边界清空，所以只反映本回合。
        /// </summary>
        public System.Func<HandEffect, int, Hand> AiDecisionLookup { get; set; }

        /// <summary>
        /// 查询"本回合**先前已经问过**的抉择的答案"（由上层 BoardController 注入）。
        ///
        /// 与 <see cref="AiDecisionLookup"/> 的区别：这个是"玩家已经答过的"。
        /// 用于分阶段抉择 —— 牧师的 4/6/8 先问"攻击还是回复"（选目标手之前），
        /// 之后再据此决定"要不要回复"。
        ///
        /// ⚠️ 不能只靠 sink 回读：两段抉择之间会重建队列、sink 可能被清空，
        /// 于是"选了回复"会读成 null 而被当成"选攻击"（真实 bug）。
        /// </summary>
        public System.Func<HandEffect, int, Hand> AnsweredLookup { get; set; }

        /// <summary>结算过程中询问玩家的回调（由上层注入）。</summary>
        public IChoiceGatherer ChoiceGatherer { get; set; }

        /// <summary>
        /// 测试辅助：直接把行动方指定为某人。
        ///
        /// 为什么需要：测试若靠"反复推回合"来等轮到自己，每回合的运算都会改手值，
        /// 测试设定的现场很快被推走 —— 断言看到的不是自己设的东西（踩过两次）。
        /// 直接指定行动方后，每一步的手值都完全可控。
        /// </summary>
        public void ForceActivePlayerForTest(PlayerState player)
        {
            if (player == null) return;
            CurrentPlayer = player;
        }

        public IChoiceProvider GetChoiceProvider(PlayerState p)
            => p == PlayerA ? ChoiceProviderA : ChoiceProviderB;

        // ============================================================
        // 整场复位
        // ============================================================

        /// <summary>
        /// 复位整场比赛：生命、双手数值、护盾状态、锁定状态、回合计数、胜负标记，
        /// 并通知每个角色清空自身跨回合状态（<see cref="ICharacterBehavior.OnMatchReset"/>）。
        ///
        /// 重开一局必须走这里，而不是直接调用 PlayerState.Reset()，
        /// 否则野蛮人的锁手会残留到下一局。
        /// </summary>
        public void ResetMatch()
        {
            _brokenThisTurn.Clear();
            PlayerA.Reset();
            PlayerB.Reset();

            IsGameOver = false;
            Winner = null;
            TurnCount = 0;
            CurrentPlayer = PlayerA;

            NotifyMatchReset();
        }

        private void NotifyMatchReset()
        {
            PlayerA.Character?.OnMatchReset(PlayerA, this);
            PlayerB.Character?.OnMatchReset(PlayerB, this);
        }

        // ============================================================
        // 合法运算枚举与校验
        // ============================================================

        /// <summary>
        /// 枚举 <paramref name="actor"/> 当前所有合法运算（每人最多 2×2 = 4 种）。
        /// 被锁定的手不能作为运算源，但仍可作为运算目标。
        /// </summary>
        public IReadOnlyList<LegalMove> GetLegalMoves(PlayerState actor)
        {
            _legalMoves.Clear();
            if (actor == null || IsGameOver) return _legalMoves;

            PlayerState opponent = GetOpponent(actor);
            foreach (Hand mine in actor.Hands)
            {
                if (mine.IsLocked) continue;
                foreach (Hand theirs in opponent.Hands)
                    _legalMoves.Add(new LegalMove(actor, mine, theirs));
            }
            return _legalMoves;
        }

        /// <summary>
        /// 校验一次运算是否合法。不合法时通过 <paramref name="reason"/> 返回原因。
        /// </summary>
        public bool IsLegalMove(PlayerState actor, Hand actedHand, Hand targetHand, out string reason)
        {
            reason = null;

            if (IsGameOver) { reason = "比赛已结束。"; return false; }
            if (actor == null) { reason = "行动方为空。"; return false; }
            if (actedHand == null) { reason = "未选择运算源。"; return false; }
            if (targetHand == null) { reason = "未选择运算目标。"; return false; }

            if (!actor.Owns(actedHand)) { reason = "运算源必须是行动方自己的手。"; return false; }

            PlayerState opponent = GetOpponent(actor);
            if (!opponent.Owns(targetHand))
            {
                reason = "运算目标必须是对方的手（不能对自己的手运算）。";
                return false;
            }

            if (actedHand.IsLocked) { reason = "被锁定的手不能作为运算源。"; return false; }

            return true;
        }

        // ============================================================
        // 回合执行
        // ============================================================

        /// <summary>
        /// 执行一次回合操作（推荐入口，参数不可混淆）。
        /// </summary>
        /// <exception cref="InvalidOperationException">运算不合法时抛出（不静默失败）。</exception>
        public TurnResult ExecuteTurn(PlayerState actor, Hand actedHand, Hand targetHand)
        {
            if (!IsLegalMove(actor, actedHand, targetHand, out string reason))
                throw new InvalidOperationException($"非法运算：{reason}");

            return ResolveTurn(actor, actedHand, targetHand.Value, targetHand);
        }

        /// <summary>
        /// 执行一次回合操作（索引 + 数值形式）。
        /// otherHandValue 是**对方**参与运算的那只手的数值。
        /// </summary>
        /// <exception cref="InvalidOperationException">运算不合法时抛出（不静默失败）。</exception>
        public TurnResult ExecuteTurn(PlayerState actor, int actedHandIndex, int otherHandValue)
        {
            if (IsGameOver)
                throw new InvalidOperationException("非法运算：比赛已结束。");
            if (actor == null)
                throw new InvalidOperationException("非法运算：行动方为空。");

            Hand actedHand = actor.GetHand(actedHandIndex);
            if (actedHand.IsLocked)
                throw new InvalidOperationException("非法运算：被锁定的手不能作为运算源。");

            return ResolveTurn(actor, actedHand, otherHandValue, null);
        }

        /// <summary>已通过校验的实际结算流程。</summary>
        private TurnResult ResolveTurn(PlayerState actor, Hand actedHand, int targetValue, Hand targetHand)
        {
            var result = new TurnResult { Actor = actor, TargetHand = targetHand };
            _brokenThisTurn.Clear();

            // 0. 说明：本方法**不**清理 ShieldState.Broken。
            //
            //    Broken 表示"这面盾碎了、但它的手值仍然是 5，所以还能被修复"。
            //    只要这只手还是 5，碎盾状态就一直保留，**无论过多少回合**；
            //    一旦它不再是 5（被运算改写），它就成了普通手牌，状态自然回到 None。
            //    这两条由第 3 步的值判断保证，因此不需要按回合清理。
            PlayerState opponent = GetOpponent(actor);
            IChoiceProvider oppChoice = GetChoiceProvider(opponent);

            result.ActedHand = actedHand;
            result.ActedHandValueBefore = actedHand.Value;
            result.TargetHandValue = targetValue;

            // 1. 运算前破盾（自己用一面已成型的盾发起运算时，该盾先碎）
            result.ShieldBrokenBeforeOp = actedHand.BreakShieldBeforeOperation();
            if (result.ShieldBrokenBeforeOp)
                NotifyOwnShieldBroken(actor, actedHand);

            // 2. 执行运算：只有行动方的手会改变
            HandEffectType effect = actedHand.PerformOperation(targetValue);

            // 3. 运算前碎掉的盾：只有**结果仍然是 5** 时才保留 Broken 标记。
            //
            //    Broken 的语义是"这面盾碎了，但它仍然是一面（值为 5 的）盾，可以被修复"。
            //    如果运算把这只手变成了别的数字（例如 5+3=8），它就已经是普通手牌，
            //    不该再挂"碎盾"标签 —— 否则界面会出现"值为 8 的手显示碎盾展开"。
            //
            //    反过来，只要手值还是 5，Broken 就一直保留（可跨回合、可无限期），
            //    战士随时都能用另一只手合出新盾把它修好。
            if (result.ShieldBrokenBeforeOp
                && actedHand.State == ShieldState.None
                && actedHand.Value == 5)
            {
                actedHand.SetShieldState(ShieldState.Broken);
            }

            result.ResultValue = actedHand.Value;
            result.ActedHandValueAfter = actedHand.Value;
            result.Effect = effect;

            // 3. 效果分派前的"**双 9 抢跑**"阶段。
            //
            //    牧师的规则要求：运算出双 9 时，**先自动回自己 9 点**，
            //    然后再结算"运算出 9"那次回复。所以这一步必须排在效果分派之前。
            //
            //    判定用的是"运算后的双手值"：运算源已经是结果值，
            //    另一只手在运算中不变，所以两值之和为 18 即双 9。
            actor.Character?.OnBeforeEffectResolved(
                actor, actedHand, targetHand.Value, effect, this,
                GetChoiceProvider(actor) as IPreTurnChoiceSink);

            // 3b. 分派效果
            int healthBefore = actor.Health;

            // 牧师可以把 4/6/8 **当回复用**（回复量就是该数字，不受攻防影响）。
            // 这一步在分派**之前**完成，因为"选了回复"就完全不走攻击分支。
            //
            // 返回 null 表示"玩家选的是攻击" -> 走正常攻击路径；
            // 返回某位玩家表示"选的是回复，目标是这位"。
            //
            // 「攻击还是回复」与「回复给谁」是**同一次抉择**（点哪只手 = 回给谁），
            // 所以一次就能解析出目标：选了攻击 -> null；选了回复 -> 那只手的主人。
            PlayerState priestHealTarget = null;
            if (actor.Character is PriestLogic && isAttackEffect(effect))
            {
                var sink = GetChoiceProvider(actor) as IPreTurnChoiceSink;
                priestHealTarget = PriestLogic.ResolveHealChoiceTarget(actor, this, sink);
            }

            switch (effect)
            {
                case HandEffectType.Attack4:
                case HandEffectType.Attack6:
                case HandEffectType.Attack8:
                    {
                        // ① 牧师选择"当回复用" -> 不造成伤害，改为回复
                        if (priestHealTarget != null)
                        {
                            int amount = PriestLogic.HealAmountFor(effect);
                            PlayerState target = priestHealTarget;

                            int before = target?.Health ?? 0;
                            target?.Heal(amount);

                            result.Healed = ReferenceEquals(target, actor)
                                ? actor.Health - before
                                : 0;   // 回给了别人，行动方自己没变
                            result.HealTarget = target;
                            break;
                        }

                        // ② "放弃攻击"：运算已经生效（手值已改），但**不造成伤害**。
                        //
                        //    为什么不能整回合跳过：跳过会让手值回退到运算前 ——
                        //    玩家实测报的 bug（1 + 3 = 4 放弃攻击后手值退回 1）。
                        //    运算本身是有效的，只是这一击不落实。
                        if (SuppressAttackDamage)
                        {
                            result.WasBlocked = false;
                            result.WasDodged = false;
                            result.DamageDealt = 0;
                            break;
                        }

                        // ③ 正常攻击
                        var ctx = new AttackContext(actor, opponent, actedHand, (int)effect);
                        ctx.SourceHandValueBefore = result.ActedHandValueBefore;
                        ctx.TargetHandValueBefore = result.TargetHandValue;
                        ctx.AttackerChoiceSink = GetChoiceProvider(actor) as IPreTurnChoiceSink;
                        ctx.AttackerProvider = GetChoiceProvider(actor);

                        // 透传"AI 已做的抉择"查询与结算中询问回调：
                        // 让角色优先采用 AI 的决定，而不是回读可能过期的 sink
                        ctx.AiDecisionLookup = AiDecisionLookup;
                        ctx.ChoiceGatherer = ChoiceGatherer;

                        ctx.Resolve(oppChoice, this);
                        result.WasBlocked = ctx.WasBlocked;
                        result.WasDodged = ctx.WasDodged;
                        result.DodgeReason = ctx.DodgeReason;
                        result.HitCount = ctx.HitCount;
                        result.LandedHits = ctx.LandedHits;
                        result.BlockedHits = ctx.BlockedHits;
                        result.DodgedHits = ctx.DodgedHits;
                        result.DamageDealt = ctx.AppliedDamage;
                        break;
                    }

                case HandEffectType.Shield:
                    actor.Character?.OnShieldFormed(actor, actedHand, this);
                    break;

                case HandEffectType.Heal:
                    {
                        // 默认：回复行动方自己。
                        // 但角色可以"接管"这一步（例如牧师的 9 能回给对手），
                        // 接管时不再执行默认治疗，避免一次 9 回两次。
                        if (actor.Character is IHealOverride healer)
                        {
                            var sink = GetChoiceProvider(actor) as IPreTurnChoiceSink;
                            PlayerState target = healer.ApplyHeal(actor, this, sink);

                            // 回复量取角色自己声明的（牧师：9 回 9；将来若有别的量也走这里）
                            int amount = PriestLogic.HealAmountFor(effect);
                            int before = target?.Health ?? 0;
                            target?.Heal(amount);

                            result.Healed = ReferenceEquals(target, actor)
                                ? actor.Health - before
                                : 0;   // 回给了别人，行动方自己没变
                            result.HealTarget = target;
                        }
                        else
                        {
                            int before = actor.Health;
                            actor.Heal(Hand.HealAmount);
                            result.Healed = actor.Health - before;
                            result.HealTarget = actor;
                        }
                        break;
                    }
            }

            // 3c. "运算后"钩子（狼人的双手和为 7 等）。
            //     放在效果分派之后、"胜负判定"之前，这样钩子造成的回血/成长能立刻算进判定。
            //     只允许读取玩家**在运算前**已经做好的选择，结算中不能再向 UI 索取输入。
            actor.Character?.OnOperationResolved(
                actor, actedHand, result.ActedHandValueBefore, targetHand.Value, effect, this,
                GetChoiceProvider(actor) as IPreTurnChoiceSink);

            // 3d. 收下角色在结算中登记的"追加攻击"（剑客），供战报单独显示一行。
            result.ExtraAttack = PendingExtraAttack;
            PendingExtraAttack = null;

            // 4. 胜负判定
            CheckGameOver();
            result.GameOver = IsGameOver;
            result.Winner = Winner;

            // 5. 切换回合
            if (!IsGameOver)
            {
                TurnCount++;
                CurrentPlayer = opponent;

                // 5b. 回合数上限：打满 <see cref="TurnLimit"/> 回合仍未分出胜负 -> 平局。
                //     必须在切换回合之后判断（TurnCount 记的是"已完成的回合数"）。
                CheckTurnLimit();
                result.GameOver = IsGameOver;
                result.Winner = Winner;
            }

            return result;
        }

        /// <summary>打满回合上限仍未分胜负时，按平局结束对局。</summary>
        private void CheckTurnLimit()
        {
            if (IsGameOver) return;
            if (TurnCount < TurnLimit) return;

            IsGameOver = true;
            Winner = null;   // 平局
        }

        // ============================================================
        // 破盾（统一入口）
        // ============================================================

        /// <summary>
        /// 跳过当前回合：不执行任何运算，直接把回合交给对方。
        ///
        /// 规则：**只有当前行动方两只手都处于成型护盾状态时才允许跳过**，
        /// 由调用方（<see cref="Assistant.BoardController"/>）先用
        /// <see cref="CanSkipTurn"/> 判断，UI 再决定是否显示"跳过回合"按钮。
        /// 本方法自身也做校验，不满足条件时抛异常（不静默失败）。
        /// </summary>
        /// <exception cref="InvalidOperationException">当前不满足跳过条件时抛出。</exception>
        public void SkipTurn()
        {
            if (IsGameOver)
                throw new InvalidOperationException("非法跳过：比赛已结束。");

            if (!CanSkipTurn(CurrentPlayer))
                throw new InvalidOperationException(
                    "非法跳过：当前不满足跳过回合的条件。");

            PlayerState opponent = GetOpponent(CurrentPlayer);
            _brokenThisTurn.Clear();

            // 通知角色"跳过已发生"（刺客用它消耗掉潜行的那次跳过机会）
            (CurrentPlayer.Character as ITurnSkipRule)?.OnTurnSkipped(CurrentPlayer, this);

            TurnCount++;
            CurrentPlayer = opponent;

            CheckTurnLimit();   // 跳过同样计入回合数上限
        }

        /// <summary>
        /// **放弃本回合**（不计为"跳过回合"技能）：回合数 +1，交给对方，手值不变。
        ///
        /// 与 <see cref="SkipTurn"/> 的区别：后者是角色的技能（双盾 / 潜行），
        /// 有严格的前置条件；而"放弃攻击"是玩家在任何时候都可以做的选择
        /// （例如目标不可被攻击时）—— 所以这里**不校验**跳过条件。
        ///
        /// 规则确认：放弃攻击 = **算用掉本回合**。
        /// </summary>
        public void GiveUpTurn()
        {
            if (IsGameOver) return;

            PlayerState opponent = GetOpponent(CurrentPlayer);
            _brokenThisTurn.Clear();

            TurnCount++;
            CurrentPlayer = opponent;

            CheckTurnLimit();
        }

        /// <summary>
        /// 该玩家当前是否可以跳过回合。
        ///
        /// 默认规则：两只手都是成型护盾（见 RULES.md §14）。
        /// 但角色可以自己接管（<see cref="ITurnSkipRule"/>），例如刺客的潜行。
        /// </summary>
        public static bool CanSkipTurn(PlayerState player)
        {
            if (player == null) return false;

            // 角色可以自己接管跳过规则（例如刺客的潜行）
            if (player.Character is ITurnSkipRule rule)
            {
                rule.SyncSkipState(player);
                return rule.CanSkip(player, null);
            }

            return player.HasDoubleShield;
        }

        /// <summary>
        /// 破除某玩家一面护盾，并通知其角色钩子（供战士连锁破盾等使用）。
        /// 使用 _brokenThisTurn 防止递归。
        /// </summary>
        public void BreakShieldAndNotify(PlayerState owner, Hand shield)
        {
            if (owner == null || shield == null) return;
            if (_brokenThisTurn.Contains(shield)) return;

            _brokenThisTurn.Add(shield);
            shield.ForceBreakShield();
            NotifyOwnShieldBroken(owner, shield);
        }

        /// <summary>
        /// 通知角色"自己的盾碎了"。
        /// 注意：<see cref="BreakShieldAndNotify"/> 走的是"防递归"路径，
        /// 而运算前主动破盾走的是本方法（那面盾并未登记进 _brokenThisTurn，
        /// 因为它是由行动方自己消耗的，仍需触发角色钩子）。
        /// </summary>
        private void NotifyOwnShieldBroken(PlayerState owner, Hand shield)
        {
            owner.Character?.OnOwnShieldBroken(owner, shield, this);
        }

        // ============================================================
        // 胜负
        // ============================================================

        /// <summary>
        /// 本次结算是否**抑制攻击伤害**（"放弃攻击"用）。
        ///
        /// 与"跳过回合"完全不同：运算照常发生、手值照常改变，
        /// 只是这一击不落实到目标身上。调用方必须在结算后复位。
        /// </summary>
        public bool SuppressAttackDamage { get; set; }

        /// <summary>这个效果类型是不是"攻击"。</summary>
        private static bool isAttackEffect(HandEffectType effect)
            => effect == HandEffectType.Attack4
               || effect == HandEffectType.Attack6
               || effect == HandEffectType.Attack8;

        private void CheckGameOver()
        {
            if (!PlayerA.IsDead && !PlayerB.IsDead) return;

            IsGameOver = true;
            if (PlayerA.IsDead && PlayerB.IsDead)
                Winner = null; // 同归于尽
            else
                Winner = PlayerA.IsDead ? PlayerB : PlayerA;
        }
    }
}
