using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 战争狂热：基础0/0。整次攻击结束，发起者攻击+1；目标防御+1。
    /// 闪避、盾挡、零伤仍算，取消／持续伤害不算；多段只一次，反弹独立。
    /// 自身回合结束未发起攻击，清除技能带来的两类加成；重开也清除。
    /// </summary>
    public sealed class WarDomainLogic : CharacterTemplate, IDynamicStats,
        ITurnStartHook, ITurnEndHook, IAttackFinishedHook, ISkillUseCounter
    {
        private int _attackStacks, _defenseStacks;
        private bool _initiatedAttackThisTurn;

        /// <summary>战争狂热两类加成的合计层数。观测口供 UI / 测试。</summary>
        public int WarStacks => _attackStacks+_defenseStacks;
        public int AttackStacks=>_attackStacks;
        public int DefenseStacks=>_defenseStacks;

        /// <summary>技能观测量：用当前层数做"技能发动"的观测口（重开清零）。</summary>
        public int SkillUseCount => WarStacks;

        public override string DisplayName => "牧师·战争领域";
        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => 0;
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
        /// 发起攻击后攻击 +1（不要求命中；多段 / 追加 / 反弹等"由自己发起"的攻击都算）。
        /// 反弹等已带类型（继承）的攻击不覆盖类型。
        /// </summary>
        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null || context.IsStatusDamage) return;
            if (context.AttackType == AttackDamageType.None)
                context.AttackType = AttackTypeOf(context);
            _initiatedAttackThisTurn = true;
        }

        public void OnAttackFinished(PlayerState owner,AttackContext attack,bool initiated,GameManager manager)
        {
            if(owner==null||!owner.IsAlive||attack==null||attack.IsStatusDamage||attack.WasCancelled)return;
            if(initiated)_attackStacks++;else _defenseStacks++;
            RefreshDynamicStats(owner,manager);
            manager?.NoteSkill(owner,"战争狂热",(initiated?"攻击+1":"防御+1")+$"，累计攻+{_attackStacks}／防+{_defenseStacks}");
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
            if (WarStacks == 0) return;

            _attackStacks = _defenseStacks = 0;
            owner.SetDynamicBonuses(0, 0);
            manager?.NoteSkill(owner, "战争狂热消退", "本回合未发起攻击，攻防加成清零");
        }

        /// <summary>动态攻防的统一刷新口（层在事件里实时写入，这里只是幂等同步）。</summary>
        public void RefreshDynamicStats(PlayerState owner, GameManager manager)
            => owner?.SetDynamicBonuses(_attackStacks, _defenseStacks);

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _attackStacks = _defenseStacks = 0;
            _initiatedAttackThisTurn = false;
            owner?.SetDynamicBonuses(0,0);
        }
    }
}
