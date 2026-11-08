using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class CircleOfSporesLogic:CharacterTemplate,IDruidCaster,IDruidForm,ITurnStartHook,IStatusApplyingAttack,ISporeDruid
    {
        public string CircleKey=>"moon";public int FormValue=>0;public bool NeedsSecondAddend=>false;
        public override string DisplayName=>"德鲁伊·孢子结社";
        public override AttackDamageType AttackTypeOf(AttackContext ctx)=>ctx.Attacker.SporeLife>0?AttackDamageType.Poison:AttackDamageType.Bludgeoning;
        public StatusEffect StatusToApplyOnHit(AttackContext ctx)=>ArcaneChoices.ElementStatus(ctx.AttackType);
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {
            if(!ReferenceEquals(game.CurrentPlayer,owner)||!owner.IsAlive)return;
            foreach(var p in game.Players)if(owner.SporeLife>0&&p.IsAlive&&!ReferenceEquals(p,owner)&&game.DistanceBetween(owner,p)==1)
            {
                var frame=game.CaptureVisualFrame();p.ApplyStatus(StatusEffect.Decay,2,owner);
                bool applied=p.HasStatus(StatusEffect.Decay);
                game.NoteSkill(owner,"孢子环绕",p.Name+(applied?" 腐朽2回合":" 未附加腐朽"),p,
                    new SkillVisualData(variant:applied?"apply":"blocked",status:StatusEffect.Decay),frame);
            }
            if(owner.LeftHand.Value!=owner.RightHand.Value||owner.SporeLife>0||owner.WildCharges<=0||!StatusRules.CanAct(owner))return;
            MonkRules.Offer(owner,game,"druid_symbiosis","一体共生 · 消耗1荒野充能获得10孢子生命？",MonkRules.YesNo("一体共生","Effects/spore_life"),value=>{
                if(value!=1||!owner.IsAlive||owner.SporeLife!=0)return;
                var frame=game.CaptureVisualFrame();
                if(!owner.SpendWildCharge())return;
                owner.GainSporeLife();game.NoteSkill(owner,"一体共生","获得10孢子生命，攻击改为毒素",owner,
                    new SkillVisualData(value:owner.SporeLife,amount:1),frame);
            });
        }
        public void OnNearbyDeath(PlayerState owner,PlayerState dead,GameManager game)
        {
            int before=owner.WildCharges;
            if(before>=owner.InitialWildCharges)return;
            var frame=game.CaptureVisualFrame();owner.RestoreWildCharge();
            game.NoteSkill(owner,"真菌感染","荒野充能+1",owner,
                new SkillVisualData(value:owner.WildCharges,previous:before,destinationOwner:dead),frame);
        }
    }
}
