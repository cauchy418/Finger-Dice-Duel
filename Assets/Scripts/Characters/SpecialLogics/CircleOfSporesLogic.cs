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
            foreach(var p in game.Players)if(p.IsAlive&&!ReferenceEquals(p,owner)&&game.DistanceBetween(owner,p)==1&&p.Health<owner.Health)
            {p.ApplyStatus(StatusEffect.Decay,2,owner);game.NoteSkill(owner,"孢子环绕",p.Name+(p.HasStatus(StatusEffect.Decay)?" 腐朽2回合":" 未附加腐朽"),p);}
            if(owner.LeftHand.Value!=owner.RightHand.Value||owner.SporeLife>0||owner.WildCharges<=0||!StatusRules.CanAct(owner))return;
            MonkRules.Offer(owner,game,"druid_symbiosis","一体共生 · 消耗1荒野充能获得10孢子生命？",MonkRules.YesNo("一体共生","Effects/spore_life"),value=>{
                if(value==1&&owner.IsAlive&&owner.SporeLife==0&&owner.SpendWildCharge())
                {owner.GainSporeLife();game.NoteSkill(owner,"一体共生","获得10孢子生命，攻击改为毒素",owner);}
            });
        }
        public void OnNearbyDeath(PlayerState owner,GameManager game)
        {int before=owner.WildCharges;owner.RestoreWildCharge();if(before!=owner.WildCharges)game.NoteSkill(owner,"真菌感染","荒野充能+1",owner);}
    }
}
