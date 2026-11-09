using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class PathOfTheBerserkerLogic:BarbarianTemplate,IHandEffectRewriter,ISelectiveHandEffectRewriter,IHandEffectDisplay,IResultTargetProvider,ICompletedDamageDealt
    {
        public const int AttackValue=9;
        public override string DisplayName=>"野蛮人·狂战士道途";
        private bool Active=>BarbarianOwner?.Health<=15;
        public bool HandlesResult(int value)=>Active&&(value==5||value==9);
        public override void OnMatchReset(PlayerState owner,GameManager game){BindBarbarianOwner(owner);ResetStats();}
        public HandEffectType RewriteEffect(PlayerState actor,Hand hand,int add,GameManager game)=>actor.Health<=15&&(hand.Value==5||hand.Value==9)?HandEffectType.Attack4:Hand.GetEffectType(hand.Value);
        public HandEffectType DisplayedEffectOf(int value)=>HandlesResult(value)?HandEffectType.Attack4:Hand.GetEffectType(value);
        public override bool ResultNeedsTarget(PreTurnChoiceContext c)=>c.Actor.Health<=15&&(c.EffectiveResult==5||c.EffectiveResult==9)||base.ResultNeedsTarget(c);
        public override void OnResolveAttack(AttackContext c)
        {
            if(c.IsStatusDamage||c.IsSpecialAttack||c.IsReflected||c.Attacker.Health>15)return;
            if(c.AttackValue==5||c.AttackValue==9){c.FinalDamage=c.AttackValue;c.HitCount=1;c.Manager?.RecordSkillVisual(c.Attacker,"狂怒",c.Defender,new SkillVisualData(source:c.SourceHand,value:c.AttackValue));}
        }
        public void OnCompletedDamage(AttackContext c,GameManager game)=>OnHitLanded(c,game);
        public void OnHitLanded(AttackContext c,GameManager game)
        {
            if(c?.Attacker==null||c.IsStatusDamage||!c.Attacker.IsAlive||c.Attacker.Health>15||c.ActualHealthDamage<=0)return;
            int heal=c.ActualHealthDamage/2;c.Attacker.Heal(heal,game);
            game?.NoteSkill(c.Attacker,"嗜血","回复"+c.Attacker.LastActualHealing,c.Attacker,new SkillVisualData(value:c.Attacker.LastActualHealing));
        }
        public void RefreshDynamicStats(PlayerState owner,GameManager game){}
    }
}
