using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class SchoolOfTransmutationLogic:CharacterTemplate,ITurnEndHook
    {
        public override string DisplayName=>"法师·变化学派";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override AttackDamageType AttackTypeOf(AttackContext ctx)=>ArcaneChoices.NearFar(ctx);
        public void OnTurnEnded(PlayerState owner,GameManager game)
        {
            var targets=new List<PlayerState>();foreach(var p in game.Players)
                if(p.IsAlive&&!p.HasStatus(StatusEffect.HolyLight)&&ArcaneChoices.MatchingHands(owner,p))targets.Add(p);
            ArcaneChoices.Target(ArcaneChoices.Transmute,"变形术 · 选择双手数字相同的角色变羊2回合",owner,game,targets,target=>{
                if(!ArcaneChoices.MatchingHands(owner,target)||!target.TryPolymorph())return;
                game.NoteSkill(owner,"变形术",$"{target.Name} 变羊2回合（10/10，−2/−2，距离1）",target);});
        }
        public override int ScoreMoveBonus(MoveValueContext context)=>context.ActedValueAfter==context.OtherHandValue?8:0;
    }

    public sealed class SchoolOfEnchantmentLogic:CharacterTemplate,ICompletedDamageDealt,ICompletedDamageTaken
    {
        public override string DisplayName=>"法师·惑控学派";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override AttackDamageType AttackTypeOf(AttackContext ctx)=>ArcaneChoices.NearFar(ctx);
        public void OnCompletedDamage(AttackContext attack,GameManager game){if(attack.ActualHealthDamage>0)Offer(attack.ActualDamageSource,attack.Defender,game,attack);}
        public void OnAttackDamageTaken(PlayerState owner,AttackContext attack,GameManager game){if(attack.ActualHealthDamage>0)Offer(owner,attack.ActualDamageSource,game,attack);}
        private static void Offer(PlayerState owner,PlayerState target,GameManager game,AttackContext attack)
        {
            if(owner==null||target==null||!owner.IsAlive||!target.IsAlive||target.HasStatus(StatusEffect.HolyLight))return;
            var options=new List<SkillChoiceOption>();foreach(var status in new[]{StatusEffect.Disabled,StatusEffect.Charm})
                options.Add(new SkillChoiceOption((int)status,StatusNames.Of(status),"Status/"+StatusNames.IconKeyOf(status),"基础2回合，格挡／对应抗性各−1；之后实际掉血解除。"));
            options.Add(new SkillChoiceOption(0,"不发动","Subclasses/wizard_school_of_enchantment"));
            ArcaneChoices.Option(ArcaneChoices.Enchantment,"催眠凝视 · 对"+target.Name+"施加失能或魅惑",owner,target,game,options,value=>{
                if(value==0||!target.IsAlive)return;attack.ApplyAttachedStatus(target,(StatusEffect)value,2,owner,game,"催眠凝视",enchantment:true,requireHealthDamage:true);});
        }
    }

    public sealed class EldritchKnightLogic:CharacterTemplate,ICompletedDamageTaken,IElementEnchantment,IStatusApplyingAttack,IDodgeBlocker
    {
        public override string DisplayName=>"战士·奥法骑士";
        public override int BaseAttackRange=>2;
        public AttackDamageType Enchantment {get;private set;}
        private readonly HashSet<Hand> _preparedOperationShields=new HashSet<Hand>();
        public bool PreventsDodge=>Enchantment==AttackDamageType.Force;
        public override void OnMatchReset(PlayerState owner,GameManager game){Enchantment=AttackDamageType.None;_preparedOperationShields.Clear();}
        // 同步运算不能中途等待人类作答；控制器在提交运算前收集，实际破盾时只消费一次。
        public bool PrepareOperationShieldBreak(PlayerState owner,Hand hand,GameManager game)
        {
            if(hand==null||!hand.IsShield||!_preparedOperationShields.Add(hand))return false;
            Broken(owner,game);return true;
        }
        public override void OnOwnShieldBroken(PlayerState owner,Hand hand,GameManager game)
        {
            if(game?.ResolvingPassiveShieldBreak==true)return;
            if(_preparedOperationShields.Remove(hand))return;
            Broken(owner,game);
        }
        public override void OnOtherShieldBroken(PlayerState owner,Hand hand,GameManager game)=>Broken(owner,game);
        private void Broken(PlayerState owner,GameManager game)
            =>ArcaneChoices.Option(ArcaneChoices.KnightElement,"奥法武器 · 选择下一次攻击的元素",owner,null,game,ArcaneChoices.Elements("保留当前附魔"),value=>Set((AttackDamageType)value,owner,game));
        private void Set(AttackDamageType type,PlayerState owner,GameManager game)
        {if(type==AttackDamageType.None)return;Enchantment=type;game.NoteSkill(owner,"奥法武器","下次攻击改为"+AttackDamageTypes.NameOf(type),owner,new SkillVisualData(element:type,variant:"prepare"));}
        public void OnAttackDamageTaken(PlayerState owner,AttackContext ctx,GameManager game)
        {
            if(ctx.ActualHealthDamage<=0||!AttackDamageTypes.IsMagic(ctx.AttackType))return;
            var options=NewSubclassChoices.YesNo("记忆 · "+AttackDamageTypes.NameOf(ctx.AttackType),"Damage/"+AttackDamageTypes.IconKeyOf(ctx.AttackType),"保留当前附魔");
            ArcaneChoices.Option(ArcaneChoices.KnightCopy,"法术回响 · 是否记录此次伤害类型",owner,ctx.Attacker,game,options,v=>{if(v==1){Set(ctx.AttackType,owner,game);game.RecordSkillVisual(owner,"法术回响",ctx.Attacker,new SkillVisualData(element:ctx.AttackType,variant:"echo"));}});
        }
        public override void OnResolveAttack(AttackContext ctx)
        {
            if(ctx.IsStatusDamage||ctx.IsReflected||Enchantment==AttackDamageType.None)return;
            ctx.AttackType=Enchantment;ctx.VisualWeapon="weapon_sword";Enchantment=AttackDamageType.None;
            ctx.Manager?.NoteSkill(ctx.Attacker,"奥法武器",AttackDamageTypes.NameOf(ctx.AttackType)+"攻击，消耗附魔",ctx.Defender,new SkillVisualData(source:ctx.SourceHand,element:ctx.AttackType,variant:"consume"));
        }
        public override AttackDamageType AttackTypeOf(AttackContext ctx)=>Enchantment!=AttackDamageType.None?Enchantment:AttackDamageType.Slashing;
        public StatusEffect StatusToApplyOnHit(AttackContext ctx)=>ArcaneChoices.ElementStatus(ctx.AttackType);
    }

    public sealed class BladesingingLogic:CharacterTemplate,ITurnEndHook,IBladesong,IStatusApplyingAttack
    {
        public override string DisplayName=>"法师·剑咏者";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>2;
        public bool Singing {get;private set;}
        public void EndSong(){Singing=false;SongElement=AttackDamageType.None;}
        public AttackDamageType SongElement {get;private set;}
        public override int AttackBonus=>base.AttackBonus+(Singing?1:0);
        public override int DefenseBonus=>base.DefenseBonus+(Singing?1:0);
        public override void OnMatchReset(PlayerState owner,GameManager game){Singing=false;SongElement=AttackDamageType.None;}
        public void OnTurnEnded(PlayerState owner,GameManager game)
        {
            if(!owner.IsAlive)return;
            if(Singing)
            {
                if(!owner.BeganAttackThisTurn&&!owner.FormedShieldThisTurn&&!owner.HealedThisTurn)
                {Singing=false;SongElement=AttackDamageType.None;game.NoteSkill(owner,"剑歌","未完成攻击／合盾／有效回复，剑歌结束",owner,new SkillVisualData(value:0));}
                return;
            }
            if(owner.HasStatus(StatusEffect.HolyLight)||owner.LeftHand.Value+owner.RightHand.Value!=owner.Health)return;
            ArcaneChoices.Option(ArcaneChoices.Bladesong,"剑歌 · 选择元素进入剑歌，或不发动",owner,null,game,ArcaneChoices.Elements(),value=>{
                if(value==0||owner.HasStatus(StatusEffect.HolyLight)||owner.LeftHand.Value+owner.RightHand.Value!=owner.Health)return;
                Singing=true;SongElement=(AttackDamageType)value;game.NoteSkill(owner,"剑歌","攻防+1，攻击改为"+AttackDamageTypes.NameOf(SongElement));});
        }
        public override void OnResolveAttack(AttackContext ctx){if(Singing&&!ctx.IsStatusDamage&&!ctx.IsReflected){ctx.AttackType=SongElement;ctx.VisualWeapon="weapon_sword";}}
        public override AttackDamageType AttackTypeOf(AttackContext ctx)=>Singing?SongElement:AttackDamageType.Slashing;
        public StatusEffect StatusToApplyOnHit(AttackContext ctx)=>Singing?ArcaneChoices.ElementStatus(ctx.AttackType):StatusEffect.None;
    }

    public sealed class HexbladeLogic:CharacterTemplate,ITurnStartHook,IHexbladeCurse,ITargetDamageBonus,IOnDamageDealt
    {
        public override string DisplayName=>"邪术师·咒剑";
        public override int DefaultAttackBonus=>1;
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>2;
        public PlayerState CurseTarget {get;private set;}
        public int CurseTurns {get;private set;}
        public override void OnMatchReset(PlayerState owner,GameManager game){CurseTarget=null;CurseTurns=0;}
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {
            if(!ReferenceEquals(owner,game.CurrentPlayer)||!owner.IsAlive||(owner.LeftHand.Value!=7&&owner.RightHand.Value!=7))return;
            var targets=new List<PlayerState>();foreach(var p in game.Players)if(p.IsAlive&&!ReferenceEquals(p,owner)&&!p.HasStatus(StatusEffect.HolyLight))targets.Add(p);
            ArcaneChoices.Target(ArcaneChoices.Hex,"咒剑诅咒 · 选择目标、移动诅咒，或不发动",owner,game,targets,target=>{
                CurseTarget=target;CurseTurns=2;game.NoteSkill(owner,"咒剑诅咒",$"{target.Name} 2回合，对其每段攻击+2",target);});
        }
        public void TickCurse(PlayerState ended)
        {if(CurseTarget==null)return;if(!CurseTarget.IsAlive||ReferenceEquals(ended,CurseTarget)&&--CurseTurns<=0){CurseTarget=null;CurseTurns=0;}}
        public int BonusAgainst(PlayerState owner,PlayerState target)=>CurseTurns>0&&ReferenceEquals(CurseTarget,target)?2:0;
        public void OnDamageDealt(AttackContext ctx,GameManager game)
        {
            if(ctx.IsStatusDamage||ctx.IsReflected||ctx.ActualHealthDamage<=0||ctx.Defender.IsAlive||ctx.Defender.IsSummon||!ReferenceEquals(ctx.Defender,CurseTarget))return;
            game.QueueHexSpecter(ctx.Attacker,ctx.Defender);CurseTarget=null;CurseTurns=0;
        }
        public override int ScoreMoveBonus(MoveValueContext context)=>context.ActedValueAfter==7||context.OtherHandValue==7?30:0;
    }
}
