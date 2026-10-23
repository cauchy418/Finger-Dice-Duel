using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 牧师·战争领域 WarDomain（攻防 0 / -1，攻击距离无限）
    ///
    /// 技能（玩家口径，2026-10-23）：
    ///   1. **战争狂热**：受到攻击（**被选为目标即算** —— 被闪避 / 被盾挡 /
    ///      零伤害也算；被邪术师·至高妖精取消不算；被转移 / 代挡后
    ///      实际承受者同样收到一次通知）或**发起攻击**后，攻防各 +1，
    ///      **可叠加、无上限**；多段攻击一次只 +1。
    ///   2. **回合末清零**：自己回合结束时，若**本回合未发起过攻击**，
    ///      立即清空全部加成。清零之后（别人回合里挨打）新获得的层数
    ///      是新一轮累积，不会被回头抹掉 —— 清零时机在**回合结束**，
    ///      不在回合开始，正是为了让"挨打攒的层"活得过下一个自己的空回合。
    ///   3. 距离 1 内为钝击伤害、1 以外为穿刺伤害。
    ///
    /// 加成通过 <see cref="PlayerState.SetDynamicBonuses"/> 落地
    /// （与狂战士道途 / 战术家的动态攻防同一条链路）；
    /// 回合结束钩子由 <see cref="GameManager.ResolveStatusTurnEnd"/> 统一触发
    /// （界面与无头两条路径都经过它，不会漂移）。
    /// </summary>
    public sealed class WarDomainLogic : CharacterTemplate, IDynamicStats,
        ITurnStartHook, ITurnEndHook, IAttackTargetedHook, ISkillUseCounter
    {
        private int _stacks;
        private bool _initiatedAttackThisTurn;

        /// <summary>当前战争狂热层数（攻防各 + 层数）。观测口供 UI / 测试。</summary>
        public int WarStacks => _stacks;

        /// <summary>技能观测量：用当前层数做"技能发动"的观测口（重开清零）。</summary>
        public int SkillUseCount => _stacks;

        public override string DisplayName => "牧师·战争领域";
        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;

        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            // 双将部件拿不到容器的 LastAttackDistance，直接问对局（防护学派同做法）；
            // 无对局时退回基类记录的距离。
            int distance = context?.Manager != null
                ? context.Manager.DistanceBetween(context.Attacker, context.Defender)
                : LastAttackDistance;
            return distance <= 1 ? AttackDamageType.Bludgeoning : AttackDamageType.Piercing;
        }

        /// <summary>
        /// 发起攻击即 +1（不要求命中；多段 / 追加 / 反弹等"由自己发起"的攻击都算）。
        /// 反弹等已带类型（继承）的攻击不覆盖类型。
        /// </summary>
        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null || context.IsStatusDamage) return;
            if (context.AttackType == AttackDamageType.None)
                context.AttackType = AttackTypeOf(context);
            GainStack(context.Attacker, "发起攻击", context.Manager);
            _initiatedAttackThisTurn = true;
        }

        /// <summary>被选为攻击目标即 +1（被闪避 / 被盾挡 / 零伤害也算，玩家口径）。</summary>
        public void OnTargetedByAttack(PlayerState owner, AttackContext context, GameManager manager)
            => GainStack(owner, "受到攻击", manager);

        private void GainStack(PlayerState owner, string reason, GameManager manager)
        {
            if (owner == null || !owner.IsAlive) return;
            _stacks++;
            owner.SetDynamicBonuses(_stacks, _stacks);
            manager?.NoteSkill(owner, "战争狂热", $"攻防 +{_stacks}（{reason}）");
        }

        /// <summary>
        /// 自己回合开始：重置"本回合是否发起过攻击"。
        /// ⚠️ <see cref="GameManager.BeginTurn"/> 会给**所有**玩家发回合开始通知，
        ///    所以必须判"当前行动者是不是自己"。
        /// </summary>
        public void OnTurnStarted(PlayerState owner, GameManager manager)
        {
            if (owner == null || manager == null) return;
            if (!ReferenceEquals(manager.CurrentPlayer, owner)) return;
            _initiatedAttackThisTurn = false;
        }

        /// <summary>
        /// 自己回合结束：本回合未发起过攻击 -> 清空全部加成。
        /// </summary>
        public void OnTurnEnded(PlayerState owner, GameManager manager)
        {
            if (owner == null) return;
            if (_initiatedAttackThisTurn) return;
            if (_stacks == 0) return;

            _stacks = 0;
            owner.SetDynamicBonuses(0, 0);
            manager?.NoteSkill(owner, "战争狂热消退", "本回合未发起攻击，攻防加成清零");
        }

        /// <summary>动态攻防的统一刷新口（层在事件里实时写入，这里只是幂等同步）。</summary>
        public void RefreshDynamicStats(PlayerState owner, GameManager manager)
            => owner?.SetDynamicBonuses(_stacks, _stacks);

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _stacks = 0;
            _initiatedAttackThisTurn = false;
            owner?.SetDynamicBonuses(0,0);
        }
    }
}
