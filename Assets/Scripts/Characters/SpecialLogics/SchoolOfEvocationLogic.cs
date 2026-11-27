namespace FingerDiceDuel.Characters.SpecialLogics
{
    using System.Collections.Generic;
    using FingerDiceDuel.Core;

    /// <summary>攻击整次结算后为下一次攻击选择魔法类型；连续相同选择使下一次每段伤害−2。</summary>
    public sealed class SchoolOfEvocationLogic : CharacterTemplate,
        IPendingElementResistance, IStatusApplyingAttack, IDodgeBlocker, IMageState, IPreTurnChoiceProvider, IAttackFinishedHook
    {
        public const int MinAttackValue = 4;

        public override int BaseAttackRange=>InfiniteAttackRange;
        private bool _resistanceSuppressed;
        public bool ProvidesResistance(AttackDamageType type)=>!_resistanceSuppressed&&HasPendingType&&type==AttackDamageTypes.FromDamageType(PendingDamageType);
        public void SuppressResistance()=>_resistanceSuppressed=true;
        public override string DisplayName => "法师·塑能学派";
        // 伤害类型（跨回合状态）

        public DamageType PendingDamageType { get; private set; } = DamageType.None;
        public DamageType LastChosenDamageType { get; private set; } = DamageType.None;
        public int PendingDamagePenalty { get; private set; }
        public void OnAttackFinished(PlayerState owner, AttackContext attack, bool initiated, GameManager game)
        {
            if(initiated && !attack.WasCancelled && !attack.IsReflected && owner.IsAlive)
                game?.OfferEvocationPreparation(owner,attack,this);
        }

        public bool HasPendingType => PendingDamageType != DamageType.None;

        public void SetPendingDamageType(DamageType type)
        {
            if(type==DamageType.None) return;
            PendingDamagePenalty=LastChosenDamageType==type?-2:0;
            LastChosenDamageType=type;
            PendingDamageType = type;_resistanceSuppressed=false;
        }

        public void ClearPendingDamageType() { PendingDamageType=DamageType.None; PendingDamagePenalty=0; }

        public DamageType ConsumeDamageType()
        {
            DamageType t = PendingDamageType;
            PendingDamageType = DamageType.None;
            PendingDamagePenalty=0;
            return t;
        }
        // 保留旧接口兼容存档；攻击前不再收集塑形选择。

        public override void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                   IList<ChoiceRequest> requests)
        { }

        public bool WantsToAbandonThisTurn { get; set; }

        public void ReadFromSink(IDamageTypeChoiceSink sink)
        {
        }

        public static bool IsAttackResult(int value)
            => value == 4 || value == 6 || value == 8;
        // 攻击命中 -> 挂状态

        public StatusEffect StatusToApplyOnHit(AttackContext context)
            => StatusNames.StatusOf(PendingDamageType);
        // 力场：同法师·咒法学派

        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null || context.IsStatusDamage) return;
            if(!context.IsReflected && HasPendingType) context.AdditionalDamageBonus+=PendingDamagePenalty;
            if(HasPendingType&&!context.IsReflected)context.Manager?.RecordSkillVisual(context.Attacker,"法术塑形",context.Defender,new SkillVisualData(source:context.SourceHand,element:AttackDamageTypes.FromDamageType(PendingDamageType),variant:"consume"));
            if (!IsForceAttack) return;

            context.IgnoreShield = true;
            context.IgnorePositiveDefense = true;
        }

        public bool IsForceAttack => PendingDamageType == DamageType.Force;

        public bool PreventsDodge => IsForceAttack;
        // 开局复位

        public override void OnMatchReset(PlayerState owner, GameManager manager)
            { PendingDamageType=DamageType.None; LastChosenDamageType=DamageType.None; PendingDamagePenalty=0; WantsToAbandonThisTurn=false; _resistanceSuppressed=false; }

        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            if (HasPendingType)
                return AttackDamageTypes.FromDamageType(PendingDamageType);

            return ByDistance(AttackDamageType.Bludgeoning, AttackDamageType.Piercing);
        }

}
}
