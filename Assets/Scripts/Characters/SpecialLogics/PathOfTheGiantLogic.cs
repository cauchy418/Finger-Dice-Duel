using FingerDiceDuel.Characters.SpecialLogics;
namespace FingerDiceDuel.Core
{
    public sealed class PathOfTheGiantLogic:BarbarianTemplate,IHandEffectRewriter,ISelectiveHandEffectRewriter,IMultiHitAttack,IHealOverride,IHealAmountProvider,IHandEffectDisplay,ILethalThresholdAdvisor,ITurnStartHook,IElementEnchantment,IStatusApplyingAttack
    {
        private int _other;
        public const int HealthBonus=5,HitCount=2,HealCount=2;
        public int MaxHits=>Active?2:1;
        public int OtherHandValueThisOperation=>_other;
        private bool Active=>BarbarianOwner?.Health<=15;
        public AttackDamageType Enchantment {get;private set;}
        public override string DisplayName=>"野蛮人·巨人道途";
        public bool HandlesResult(int value)=>Active&&(value==4||value==5||value==6||value==8||value==9);
        public override void OnMatchReset(PlayerState owner,GameManager game){BindBarbarianOwner(owner);_other=0;Enchantment=AttackDamageType.None;ResetStats();}
        public HandEffectType RewriteEffect(PlayerState owner,Hand hand,int add,GameManager game)
        {_other=owner.Owns(hand)?owner.GetOtherHand(hand).Value:0;return owner.Health<=15?LowEffect(hand.Value):Hand.GetEffectType(hand.Value);}
        private static HandEffectType LowEffect(int v)=>v==5?HandEffectType.Attack6:v==4||v==6||v==8?HandEffectType.None:Hand.GetEffectType(v);
        public HandEffectType DisplayedEffectOf(int value)=>Active?LowEffect(value):Hand.GetEffectType(value);
        public override bool ResultNeedsTarget(PreTurnChoiceContext c)=>c.Actor.Health<=15?c.EffectiveResult==5:base.ResultNeedsTarget(c);
        public override void OnResolveAttack(AttackContext c)
        {
            if(c.IsStatusDamage||c.IsSpecialAttack||c.IsReflected||c.Attacker.Health>15||c.AttackValue!=5)return;
            c.FinalDamage=_other*2;c.HitCount=2;
        }
        public PlayerState ApplyHeal(PlayerState owner,GameManager game,IPreTurnChoiceSink sink)=>owner;
        public int HealAmountOf(PlayerState owner,GameManager game)=>owner.Health<=15?_other*2:9;
        public int EstimateLethalDamage(LethalContext c,int value,int attack,int defense)
        {
            if(!Active)return AttackContext.ApplyStatBonuses(value,attack,defense,false);
            return value==5?AttackContext.ApplyStatBonuses(c.OtherHandValue,attack,defense,false)*2:0;
        }
        public override int ScoreMoveBonus(MoveValueContext context)
        {
            if(!Active)return 0;
            if(context.ActedValueAfter==5)return 35+context.OtherHandValue*6;
            if(context.ActedValueAfter==9)return StatusRules.CanHeal(BarbarianOwner)?System.Math.Min(BarbarianOwner.MaximumHealth-BarbarianOwner.Health,context.OtherHandValue*2)*8:0;
            return context.ActedValueAfter==4||context.ActedValueAfter==6||context.ActedValueAfter==8?-70:0;
        }
        public override AttackDamageType AttackTypeOf(AttackContext c)=>Enchantment!=AttackDamageType.None?Enchantment:AttackDamageType.Slashing;
        public StatusEffect StatusToApplyOnHit(AttackContext c)=>ArcaneChoices.ElementStatus(c.AttackType);
        public AttackDamageType ConsumeBlade(){var type=Enchantment;Enchantment=AttackDamageType.None;return type;}
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {
            if(!ReferenceEquals(game.CurrentPlayer,owner)||owner.Health<=5)return;
            ArcaneChoices.Option("giant_element_blade","元素战刃 · 失去5点体力，为下次攻击选择元素",owner,null,game,ArcaneChoices.Elements(),v=>{
                if(v==0||owner.Health<=5)return;
                owner.SetHealthExact(owner.Health-5);Enchantment=(AttackDamageType)v;
                game.NoteSkill(owner,"元素战刃","失去5体力，下次攻击为"+AttackDamageTypes.NameOf(Enchantment),owner,new SkillVisualData(element:Enchantment,variant:"prepare"));
            });
        }
    }
}
