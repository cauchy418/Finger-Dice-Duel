using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class OathOfConquestLogic:CharacterTemplate,ITurnEndHook,IAttackTargetedHook,IAttackFinishedHook,ISkillUseCounter
    {
        private readonly Dictionary<AttackContext,bool> _five=new Dictionary<AttackContext,bool>();
        public int AttackStacks {get;private set;}
        public int DefenseStacks {get;private set;}
        public int SkillUseCount {get;private set;}
        public override string DisplayName=>"圣武士·征服之誓";
        public override int AttackBonus=>base.AttackBonus+AttackStacks;
        public override int DefenseBonus=>base.DefenseBonus+DefenseStacks;
        public override AttackDamageType AttackTypeOf(AttackContext context)=>AttackDamageType.Bludgeoning;
        private static bool HasFive(PlayerState owner)=>owner.LeftHand.Value==5||owner.RightHand.Value==5;
        public override void OnMatchReset(PlayerState owner,GameManager game){AttackStacks=DefenseStacks=SkillUseCount=0;_five.Clear();}
        public override void OnResolveAttack(AttackContext attack)
        {if(!attack.IsStatusDamage)_five[attack]=HasFive(attack.Attacker);}
        public void OnTargetedByAttack(PlayerState owner,AttackContext attack,GameManager game)
        {if(!attack.IsStatusDamage)_five[attack]=HasFive(owner);}
        public void OnAttackFinished(PlayerState owner,AttackContext attack,bool initiated,GameManager game)
        {
            bool five=_five.TryGetValue(attack,out bool value)&&value;_five.Remove(attack);
            if(!owner.IsAlive||attack.IsStatusDamage||attack.WasCancelled)return;
            var target=initiated?attack.Defender:attack.Attacker;
            bool panic=initiated?attack.AttackBonusAtStart(owner)>attack.AttackBonusAtStart(target)
                :attack.DefenseBonusAtStart(owner)>attack.DefenseBonusAtStart(target);
            if(five){if(initiated)AttackStacks++;else DefenseStacks++;SkillUseCount++;
                game?.NoteSkill(owner,"征服之势",initiated?"攻击+1":"防御+1");}
            if(panic&&target?.IsAlive==true){target.ApplyStatus(StatusEffect.Panic,2,owner);
                if(target.HasStatus(StatusEffect.Panic))game?.NoteSkill(owner,"征服威慑",target.Name+" 恐慌2回合");}
        }
        public void OnTurnEnded(PlayerState owner,GameManager game)
        {
            if(!owner.BeganAttackThisTurn&&AttackStacks>0){AttackStacks--;game?.NoteSkill(owner,"征服之势","未发起攻击，攻击加成-1");}
            if(!HasFive(owner)&&DefenseStacks>0){DefenseStacks--;game?.NoteSkill(owner,"征服之势","未持有5，防御加成-1");}
        }
        public override int ScoreMoveBonus(MoveValueContext context)
            =>context.OtherHandValue==5||context.ActedValueAfter==5?30:0;
    }
    public sealed class OathOfVengeanceLogic:CharacterTemplate,ITurnStartHook,IAttackTargetedHook,IPlayerDeathListener,ITargetDamageBonus,IVowOfEnmity,IPreTurnChoiceProvider
    {
        private PlayerState _owner,_preparedAttacker;private int _preparedTurn=-1;private bool _initialized,_preparedRetarget;
        public PlayerState VowTarget {get;private set;}
        public override string DisplayName=>"圣武士·复仇之誓";
        public override AttackDamageType AttackTypeOf(AttackContext context)=>AttackDamageType.Bludgeoning;
        public override void OnMatchReset(PlayerState owner,GameManager game){_owner=owner;_initialized=false;VowTarget=null;_preparedAttacker=null;_preparedTurn=-1;_preparedRetarget=false;}
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {if(_initialized||!owner.IsAlive)return;_initialized=true;OfferTarget(owner,game,false);}
        private void Mark(PlayerState target,GameManager game)
        {if(target==null||!target.IsAlive||ReferenceEquals(target,_owner))return;VowTarget=target;game?.NoteSkill(_owner,"仇敌誓言","标记 "+target.Name);}
        private void OfferTarget(PlayerState owner,GameManager game,bool optional)
        {
            var targets=new List<PlayerState>();foreach(var p in game.Players)if(p.IsAlive&&!ReferenceEquals(p,owner))targets.Add(p);
            DivineChoices.ChooseTarget(DivineChoices.Vow,"仇敌誓言 · 选择一名其他角色"+(optional?"，或保留原标记":""),owner,game,targets,optional,target=>Mark(target,game));
        }
        private void Retarget(PlayerState attacker,GameManager game)
            =>ArcaneChoices.Option(DivineChoices.Retarget,"仇敌誓言 · 是否将标记转移至"+attacker.Name,_owner,attacker,game,
                NewSubclassChoices.YesNo("转移仇敌标记","Effects/vow_of_enmity","保留原标记"),v=>{if(v==1)Mark(attacker,game);});
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,IList<ChoiceRequest> requests)
        {
            if(!context.IsAttack||!CharacterBehaviors.ActorHas(context.Defender,this)||ReferenceEquals(context.Actor,VowTarget))return;
            var attacker=context.Actor;var owner=context.Defender;var game=context.Manager;
            requests.Add(new ChoiceRequest(HandEffect.VengeanceRetarget,"仇敌誓言 · 是否将标记转移至"+attacker.Name,Array.Empty<Hand>(),false,decider:owner,
                options:NewSubclassChoices.YesNo("转移仇敌标记","Effects/vow_of_enmity","保留原标记"),optionKey:DivineChoices.Retarget,relatedTarget:attacker,
                applyOption:v=>{_preparedAttacker=attacker;_preparedTurn=game.TurnCount;_preparedRetarget=v==1;}));
        }
        public void OnTargetedByAttack(PlayerState owner,AttackContext attack,GameManager game)
        {
            if(attack.IsStatusDamage||ReferenceEquals(attack.Attacker,VowTarget))return;
            if(_preparedTurn==game.TurnCount&&ReferenceEquals(_preparedAttacker,attack.Attacker))
            {
                bool transfer=_preparedRetarget;_preparedAttacker=null;_preparedRetarget=false;
                if(transfer)Mark(attack.Attacker,game);return;
            }
            Retarget(attack.Attacker,game);
        }
        public int BonusAgainst(PlayerState owner,PlayerState target)=>ReferenceEquals(target,VowTarget)?1:-1;
        public void OnPlayerDied(PlayerState dead,GameManager game)
        {if(_owner?.IsAlive==true&&ReferenceEquals(dead,VowTarget))OfferTarget(_owner,game,true);}
        public override void OnOtherShieldBroken(PlayerState owner,Hand shield,GameManager game)
        {
            if(owner.IsAlive&&VowTarget!=null&&ReferenceEquals(game.ShieldBreaker,VowTarget))
            {game.NoteSkill(owner,"誓言反击","仇敌破盾，可进行一次只改手值的额外运算");DivineChoices.OfferFreeOperation(owner,game);}
        }
    }
}
