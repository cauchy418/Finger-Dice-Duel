using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 战士·战斗大师 BattleMaster（攻防 0 / 0，攻击距离 2）
    ///
    /// 技能（玩家口径，2026-10-23）：
    ///   1. **伤害分型**：4 = 挥砍、6 = 钝击、8 = 穿刺。
    ///   2. **卓越战技**：攻击**实际造成伤害**（&gt; 0 —— 被盾挡、被闪避、
    ///      被取消、被减至 0 都不算）后，看**另一只手**的点数：
    ///        · 4 / 8 → 给目标附加 **流血** 或 **跛脚**（2 回合）；
    ///        · 6     → 给目标附加 **眩晕** 或 **倒伏**（2 回合）。
    ///      每次攻击结算**一次**（多段攻击不逐段附加）。
    ///
    /// 抉择走"运算前收集 + 结算回读"（项目铁律：ExecuteTurn 一次跑完）：
    ///   <see cref="CollectPreTurnChoices"/> 在预测到"攻击 + 另一手 4/6/8"
    ///   时提出【BattleMasterManeuver】请求；UI（卓越战技面板）与 AI 在运算前作答
    ///   （答案写入 <see cref="SelectManeuver"/> / <see cref="DeclineManeuver"/>），
    ///   <see cref="OnDamageLanded"/> 在真的造成伤害时按当前分支校验并落地。
    /// </summary>
    public sealed class BattleMasterLogic : CharacterTemplate,
        IPreTurnChoiceProvider, IOnDamageLanded, ITurnStartHook,
        IBattleMasterManeuverChoice, ISkillUseCounter
    {
        private int _maneuverCount;
        private StatusEffect _selectedManeuver = StatusEffect.None;
        private bool _maneuverDeclined;
        private int _pendingOtherHandValue = -1;

        /// <summary>本局成功附加的招式次数（观测口，重开清零）。</summary>
        public int SkillUseCount => _maneuverCount;

        public override string DisplayName => "战士·战斗大师";
        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => 0;
        public override int BaseAttackRange => 2;

        /// <summary>按攻击数字分型（快照缺失时退化为 RawDamage；其余兜底挥砍）。</summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            int value = context?.AttackValue ?? 0;
            switch (value)
            {
                case 6: return AttackDamageType.Bludgeoning;
                case 8: return AttackDamageType.Piercing;
                default: return AttackDamageType.Slashing;
            }
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null) return;
            // 反弹等已带类型（继承）的攻击不覆盖；正常攻击按数字分型。
            // 在 OnResolveAttack 里直接设类型是为了让**双将**也生效
            // （容器不转发 AttackTypeOf，只转发生命周期钩子）。
            if (context.AttackType == AttackDamageType.None)
                context.AttackType = AttackTypeOf(context);
        }

        // ============================================================
        // 运算前收集：提出"招式"抉择
        // ============================================================

        public override void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                   IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;
            if (!CharacterBehaviors.ActorHas(context.Actor, this)) return;   // 双将部件身份判定

            // 每次收集都先清上一回合的答案（残留会把旧偏好当成本回合的选择）
            ResetChoice();

            if (!context.IsAttack) return;
            Hand other = OtherHandOf(context.Actor, context.ActedHand);
            if (other == null) return;
            _pendingOtherHandValue = other.Value;

            StatusEffect[] options = ManeuverOptionsFor(other.Value);
            if (options == null) return;

            requests.Add(new ChoiceRequest(
                HandEffect.BattleMasterManeuver,
                "【卓越战技】本次攻击造成伤害后，给目标附加哪种状态（2 回合）？",
                new List<Hand>(1) { context.ActedHand },
                allowDecline: true,
                declineResult: null,
                decider: context.Actor,
                acceptLabel: "附加招式",
                resultValue: context.EffectiveResult));
        }

        /// <summary>行动方的另一只手（认不出归属时返回 null）。</summary>
        private static Hand OtherHandOf(PlayerState owner, Hand acted)
        {
            if (owner == null || acted == null || !owner.Owns(acted)) return null;
            return owner.GetOtherHand(acted);
        }

        /// <summary>
        /// 按另一只手给出可选招式（无招式时返回 null）：
        /// 4 / 8 → 流血/跛脚；6 → 眩晕/倒伏。
        /// </summary>
        public static StatusEffect[] ManeuverOptionsFor(int otherHandValue)
        {
            switch (otherHandValue)
            {
                case 4:
                case 8:
                    return new[] { StatusEffect.Bleed, StatusEffect.Limp };
                case 6:
                    return new[] { StatusEffect.Stunned, StatusEffect.KnockedDown };
                default:
                    return null;
            }
        }

        // ============================================================
        // IBattleMasterManeuverChoice：UI / 控制器写入答案
        // ============================================================

        /// <summary>当前（收集时记录的）分支可选招式；没有则为空数组。</summary>
        public StatusEffect[] ManeuverOptions
            => ManeuverOptionsFor(_pendingOtherHandValue) ?? new StatusEffect[0];

        public void SelectManeuver(StatusEffect status) => _selectedManeuver = status;

        /// <summary>玩家明确"不发动"——与"没问过"区分，结算时不再走 AI 默认。</summary>
        public void DeclineManeuver() => _maneuverDeclined = true;

        private void ResetChoice()
        {
            _selectedManeuver = StatusEffect.None;
            _maneuverDeclined = false;
            _pendingOtherHandValue = -1;
        }

        /// <summary>
        /// 自己回合开始也清一次：偷窃出的攻击等"没经过收集"的路径
        /// 不会把上一回合的旧答案带进结算。
        /// </summary>
        public void OnTurnStarted(PlayerState owner, GameManager manager)
        {
            if (owner == null || manager == null) return;
            if (!ReferenceEquals(manager.CurrentPlayer, owner)) return;
            ResetChoice();
        }

        // ============================================================
        // 结算：实际造成伤害后落地
        // ============================================================

        public void OnDamageLanded(AttackContext context, GameManager manager)
        {
            if (context == null || context.IsStatusDamage || context.AppliedDamage <= 0) return;
            PlayerState attacker = context.Attacker;
            if (attacker == null || !CharacterBehaviors.ActorHas(attacker, this)) return;
            PlayerState target = context.Defender;
            if (target == null || !target.IsAlive) return;

            // "另一只手"以攻击建立时为准（运算只改写运算源，另一只手不会变）
            Hand other = OtherHandOf(attacker, context.SourceHand);
            if (other == null) return;

            StatusEffect[] options = ManeuverOptionsFor(other.Value);
            if (options == null) return;

            StatusEffect chosen = ResolveManeuver(attacker, target, options, manager);
            if (chosen == StatusEffect.None || !IsValidOption(options, chosen)) return;

            context.ApplyAttachedStatus(target,chosen,PlayerState.StatusDurationTurns,attacker,manager,"卓越战技",requireDamage:true);
            _maneuverCount++;

        }

        private StatusEffect ResolveManeuver(PlayerState attacker, PlayerState target,
                                             StatusEffect[] options, GameManager manager)
        {
            if (_maneuverDeclined) return StatusEffect.None;      // 玩家明确"不发动"

            // 玩家在运算前选定的招式 —— 必须属于当前分支才算数
            if (_selectedManeuver != StatusEffect.None && IsValidOption(options, _selectedManeuver))
                return _selectedManeuver;

            // AI / 无头路径：角色侧启发式（AI 不能引用 Characters 层，由角色自报）
            return PickForAi(attacker, target, options, manager);
        }

        private static bool IsValidOption(StatusEffect[] options, StatusEffect status)
        {
            for (int i = 0; i < options.Length; i++)
                if (options[i] == status) return true;
            return false;
        }

        /// <summary>
        /// AI 的招式选择（启发式，与生命领域 BestHealTargetFor 同一模式）：
        ///   · 打队友不附加；
        ///   · 目标残血（≤6）→ 流血收尾（若在选项里）；
        ///   · 6 分支 → 眩晕压制（"不能攻击"比易伤更普适）；
        ///   · 4/8 分支默认跛脚（多人局射程减半更普适）。
        /// </summary>
        public static StatusEffect PickForAi(PlayerState attacker, PlayerState target,
                                             StatusEffect[] options, GameManager manager)
        {
            if (options == null || options.Length == 0 || target == null) return StatusEffect.None;
            if (attacker != null && manager != null
                && !ReferenceEquals(attacker, target)
                && manager.IsTeammate(attacker, target)) return StatusEffect.None;

            if (target.Health <= 6 && IsValidOption(options, StatusEffect.Bleed))
                return StatusEffect.Bleed;
            if (IsValidOption(options, StatusEffect.Stunned))
                return StatusEffect.Stunned;
            return options[options.Length - 1];      // 跛脚 / 倒伏
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _maneuverCount = 0;
            ResetChoice();
        }
    }
}
