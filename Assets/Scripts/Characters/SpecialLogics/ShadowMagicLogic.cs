using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class ShadowMagicLogic:CharacterTemplate,ITurnEndHook,IGraveStrength,IShadowEyes,IAttackTypeProvider,ITargetDamageBonus,IStatusApplyingAttack
    {
        private GameManager _game;
        public bool GraveSpent{get;private set;}
        public override string DisplayName=>"术士·幽影魔法";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override void OnMatchReset(PlayerState owner,GameManager game){GraveSpent=false;_game=game;}
        public void SurviveDeath(PlayerState owner)
        {
            if(GraveSpent||owner.Health>0||owner.IsPolymorphed)return;
            var frame=_game?.CaptureVisualFrame();
            GraveSpent=true;owner.SetHealthExact(1);_game?.NoteSkill(owner,"坟冢之力","恢复至1生命",owner,
                new SkillVisualData(value:1),frame);
        }
        public override AttackDamageType AttackTypeOf(AttackContext ctx)=>ctx?.Defender?.HasStatus(StatusEffect.DarknessBlind)==true?AttackDamageType.Necrotic:ArcaneChoices.NearFar(ctx);
        public int BonusAgainst(PlayerState owner,PlayerState target)=>target?.HasStatus(StatusEffect.DarknessBlind)==true?2:0;
        public override void OnResolveAttack(AttackContext ctx)
        {if(!ctx.IsStatusDamage&&!ctx.IsReflected&&ctx.Defender.HasStatus(StatusEffect.DarknessBlind)){ctx.AttackType=AttackDamageType.Necrotic;ctx.Manager?.NoteSkill(ctx.Attacker,"幽暗之瞳","暗蚀，伤害+2",ctx.Defender,new SkillVisualData(source:ctx.SourceHand,value:2));}}
        public StatusEffect StatusToApplyOnHit(AttackContext ctx)=>ArcaneChoices.ElementStatus(ctx.AttackType);
        public void OnTurnEnded(PlayerState owner,GameManager game)
        {
            if(!owner.IsAlive||!StatusRules.CanAct(owner)||owner.LeftHand.Value+owner.RightHand.Value!=10)return;
            var options=new List<SkillChoiceOption>{new SkillChoiceOption(1,"黑暗术","Status/darkness_blind"),new SkillChoiceOption(0,"不发动","UI/status_counter")};
            ArcaneChoices.Option(ShadowRules.Darkness,"黑暗术 · 使距离1内的角色失明2回合？",owner,null,game,options,v=>{
                if(v!=1)return;foreach(var p in game.Players)if(p.IsAlive&&game.DistanceBetween(owner,p)<=1)
                {
                    var frame=game.CaptureVisualFrame();p.ApplyStatus(StatusEffect.DarknessBlind,2,owner);
                    if(p.HasStatus(StatusEffect.DarknessBlind))game.NoteSkill(owner,"黑暗术",p.Name+" 失明2回合",p,
                        new SkillVisualData(value:2,status:StatusEffect.DarknessBlind),frame);
                }
            });
        }
    }
}
