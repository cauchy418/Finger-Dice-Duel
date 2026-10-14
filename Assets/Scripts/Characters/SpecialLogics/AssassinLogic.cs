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
    public sealed class AssassinLogic : CharacterTemplate, ITurnSkipRule, IAttackTargetRule,
                                       ISkillUseCounter
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

        /// <summary>
        /// **本局是否真的"进入过"潜行**（由运算达成双 7 时才置位）。
        ///
        /// ⚠️ 为什么不能只看手值：**初始手值由开局掷骰决定**，
        /// 一个刺客完全可能开局就是 7 / 7 —— 但那**不该**算潜行。
        /// 玩家明确要求："刺客初始出双 7 也不会进潜行"。
        ///
        /// 所以潜行 = `双方都是 7` **且** `本局已通过运算达成过`。
        /// </summary>
        private bool _stealthEntered;

        /// <summary>双手是否都是 7（**只看数值**，不代表已进入潜行）。</summary>
        public static bool HasDoubleSeven(PlayerState owner)
            => owner != null
               && owner.LeftHand.Value == StealthValue
               && owner.RightHand.Value == StealthValue;

        /// <summary>
        /// 该玩家当前是否**处于潜行**。
        ///
        /// ⚠️ 与 <see cref="HasDoubleSeven"/> 的区别：本判定还要求
        /// "本局已通过运算进入过潜行"。开局掷骰直接摆出的双 7 **不算**。
        /// </summary>
        public bool IsStealthActive(PlayerState owner)
            => _stealthEntered && HasDoubleSeven(owner);

        public bool IsOwnerStealthed(PlayerState owner) => IsStealthActive(owner);

        /// <summary>
        /// 潜行中不能被选为攻击目标（<see cref="IAttackTargetRule"/>）。
        ///
        /// 由 Assistant 层的 BoardController 查询 —— 它不认识刺客这个具体角色，
        /// 只问"你能不能被打"。
        /// </summary>
        public bool IsImmuneToAttack(PlayerState owner) => IsStealthActive(owner);

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
            if (!IsStealthActive(self)) return;

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

            bool stealthed = IsStealthActive(owner);

            // 只要观察到"不在潜行"，就认为这次潜行已经结束 ——
            // 无论这个观察发生在 sync 里还是查询里。
            // （若只在 sync 里复位，那么"外部直接改写手值再改回双 7"这种路径
            //   永远清不掉标记，重新进入潜行后就再也跳不了。）
            if (!stealthed) _skipUsedThisStealth = false;

            return stealthed && !_skipUsedThisStealth;
        }

        public void SyncSkipState(PlayerState owner)
        {
            if (owner != null && !IsStealthActive(owner)) _skipUsedThisStealth = false;
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
            if (!IsStealthActive(context.Attacker)) return;

            context.FinalDamage += StealthBonusDamage;
        }

        /// <summary>
        /// **仅供测试 / 回放**：直接把角色标成"已进入潜行"。
        ///
        /// 正常对局里潜行由 <see cref="OnOperationResolved"/> 在运算达成双 7 时置位；
        /// 而测试往往想直接摆好局面，不该被迫去凑一次恰好等于 7 的加法
        /// （那会把测试绑死在算术上）。所以提供一个语义明确的入口。
        ///
        /// ⚠️ 生产代码不要调用它。
        /// </summary>
        public void EnterStealthForTest() => _stealthEntered = true;

        /// <summary>
        /// 每次运算后重新判定潜行状态：
        ///   · 新达成双 7 -> **进入潜行**（置位 `_stealthEntered`）并重置"跳过已用"；
        ///   · 脱离双 7 -> 结束潜行。
        ///
        /// ⚠️ 只有**经由运算**达成双 7 才算进入潜行 ——
        /// 开局掷骰直接摆出的双 7 走不到这里，所以不算（玩家明确要求）。
        /// </summary>
        public override void OnOperationResolved(PlayerState owner, Hand actedHand, int actedHandValueBefore,
                                                 int targetValue, HandEffectType effect, GameManager manager,
                                                 IPreTurnChoiceSink choiceSink)
        {
            if (owner == null) return;

            if (!HasDoubleSeven(owner))
            {
                // 手值不再是双 7 -> 潜行结束
                _stealthEntered = false;
                _skipUsedThisStealth = false;   // 下次进入潜行可再跳一次
                return;
            }

            // 运算达成了双 7 -> 正式进入潜行
            if (!_stealthEntered)
            {
                _stealthEntered = true;
                _skipUsedThisStealth = false;
                StealthEnterCount++;
            }
        }

        /// <summary>
        /// 本局**进入过潜行**几次（"经运算达成双 7"）。
        ///
        /// ⚠️ 为什么要计数器：潜行会被后续运算**解除**（手值不再是双 7），
        ///    所以只看终局状态测不出"AI 到底有没有去凑双 7"——
        ///    我只能靠它把"AI 从没凑到"与"凑到过但后来解除了"区分开。
        /// </summary>
        public int StealthEnterCount { get; private set; }

        /// <summary>本局发动招牌技能（进入潜行）几次 —— 见 <see cref="ISkillUseCounter"/>。</summary>
        public int SkillUseCount => StealthEnterCount;

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _skipUsedThisStealth = false;
            _stealthEntered = false;    // 重开一局必须清空，否则带着上一局的潜行
            StealthEnterCount = 0;
        }
    }
}
