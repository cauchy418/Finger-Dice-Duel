using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 刺客 Assassin（攻防 +2 / −1）
    ///
    /// 技能：
    ///   1. **潜行**：两只手都为 7 时进入潜行状态，此时**不能被攻击**
    ///      （对手的攻击完全无效，且不消耗护盾）。
    ///   2. **跳过回合**：潜行期间可以跳过一回合 —— 但**一次潜行只能跳过一回合**，
    ///      跳过一次之后下一回合必须出手，出手即破除潜行。
    ///   3. **出潜行斩杀**：从潜行状态发起的攻击伤害 **+7**。
    ///
    /// 潜行是"按当前手值持续判定"的状态：双手都是 7 就在潜行中。
    /// 攻击会改动运算源的手值，因此出手后自然脱离潜行（除非结果又让它变回 7）。
    /// </summary>
    public sealed class AssassinLogic : CharacterTemplate, ITurnSkipRule, IAttackTargetRule
    {
        public override string DisplayName => "刺客";

        public override int DefaultAttackBonus => 2;
        public override int DefaultDefenseBonus => -1;

        /// <summary>进入潜行所需的双手数值。</summary>
        public const int StealthValue = 7;

        /// <summary>出潜行那一击的额外伤害。</summary>
        public const int StealthBonusDamage = 7;

        /// <summary>本次潜行是否已经用掉了"跳过一回合"的机会。</summary>
        private bool _skipUsedThisStealth;


        /// <summary>该玩家当前是否处于潜行（双手都为 7）。</summary>
        public static bool IsStealthed(PlayerState owner)
            => owner != null
               && owner.LeftHand.Value == StealthValue
               && owner.RightHand.Value == StealthValue;

        public bool IsOwnerStealthed(PlayerState owner) => IsStealthed(owner);

        /// <summary>
        /// 潜行中不能被选为攻击目标（<see cref="IAttackTargetRule"/>）。
        ///
        /// 由 Assistant 层的 BoardController 查询 —— 它不认识刺客这个具体角色，
        /// 只问"你能不能被打"。
        /// </summary>
        public bool IsImmuneToAttack(PlayerState owner) => IsStealthed(owner);

        // ============================================================
        // 潜行：完全免伤
        // ============================================================

        /// <summary>
        /// 潜行中不能被攻击：直接取消这次伤害，**不消耗护盾**。
        ///
        /// 用 <see cref="AttackContext.Resolve"/> 里的闪避通道实现 ——
        /// 它与"闪避"语义一致（完全免伤、不碰护盾），所以复用同一条路径。
        /// </summary>
        public override void TryDodge(PreTurnChoiceContext context, IPreTurnChoiceSink sink, int hitIndex)
        {
            if (context == null) return;

            PlayerState self = context.Defender;
            if (self?.Character != this) return;
            if (!IsStealthed(self)) return;

            context.Dodged = true;
            // 战报要说清是"打不到"而不是"被闪避"（复用了同一条免伤通道）
            context.DodgeReason = "目标在潜行中，无法被攻击";
        }

        // ============================================================
        // 跳过回合（潜行专属）
        // ============================================================

        /// <summary>
        /// 潜行中可以跳过回合，但**一次潜行只能跳一次**：
        /// 跳过一次之后，下一回合必须出手（出手即破除潜行）。
        ///
        /// 这里直接以"当前是否潜行"为准（按手值判定），
        /// 而不是依赖某个标记是否被重置 —— 手值可以被外部直接改写
        /// （例如测试或复位），只看状态更不容易出错。
        /// </summary>
        public bool CanSkip(PlayerState owner, GameManager manager)
        {
            if (owner == null) return false;

            bool stealthed = IsStealthed(owner);

            // 只要观察到"不在潜行"，就认为这次潜行已经结束 ——
            // 无论这个观察发生在 sync 里还是查询里。
            // （若只在 sync 里复位，那么"外部直接改写手值再改回双 7"这种路径
            //   永远清不掉标记，重新进入潜行后就再也跳不了。）
            if (!stealthed) _skipUsedThisStealth = false;

            return stealthed && !_skipUsedThisStealth;
        }

        public void SyncSkipState(PlayerState owner)
        {
            if (owner != null && !IsStealthed(owner)) _skipUsedThisStealth = false;
        }

        public void OnTurnSkipped(PlayerState owner, GameManager manager)
            => _skipUsedThisStealth = true;

        // ============================================================
        // 出潜行斩杀
        // ============================================================

        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null) return;
            if (context.Attacker?.Character != this) return;

            // 出手时若仍在潜行，则这一击 +7（并随即破除潜行）
            if (!IsStealthed(context.Attacker)) return;

            context.FinalDamage += StealthBonusDamage;
        }

        /// <summary>
        /// 每次运算后重新判定潜行状态：
        ///   · 新进入双 7 -> 重置"跳过已用"标记；
        ///   · 脱离双 7 -> 结束潜行。
        /// </summary>
        public override void OnOperationResolved(PlayerState owner, Hand actedHand, int actedHandValueBefore,
                                                 int targetValue, HandEffectType effect, GameManager manager,
                                                 IPreTurnChoiceSink choiceSink)
        {
            if (owner == null) return;

            if (!IsStealthed(owner))
            {
                // 手值不再是双 7 -> 潜行结束，标记复位（下次进入潜行可再跳一次）
                _skipUsedThisStealth = false;
            }
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager)
            => _skipUsedThisStealth = false;
    }
}
