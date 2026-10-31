using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class SchoolOfNecromancyLogic : CharacterTemplate,
        IAttackTypeProvider, IAttackBlocker, IStatusApplyingAttack, ITurnStartHook, IPlayerDeathListener
    {
        private PlayerState _owner;
        private int _lastFreeAttackTurn=int.MinValue;
        public override string DisplayName => "法师·死灵学派";
        public override int DefaultAttackBonus => 0;
        public override int ScoreMoveBonus(MoveValueContext context) => (context.ActedValueAfter==2?35:context.ActedValueAfter==1?22:0)-(context.ActedHand.Value==2 && context.OtherHandValue!=2 && context.ActedValueAfter!=2?18:0);
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;
        public override void OnMatchReset(PlayerState owner, GameManager manager)
        { _owner=owner; _lastFreeAttackTurn=int.MinValue; }
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => (context?.Manager?.DistanceBetween(context.Attacker,context.Defender)??1)<=1
                ?AttackDamageType.Bludgeoning:AttackDamageType.Piercing;

        private static List<Hand> Hands(PlayerState p, int value)
        {
            var list=new List<Hand>();
            if(p!=null) foreach(var h in p.Hands) if(h.Value==value && !h.IsLocked) list.Add(h);
            return list;
        }
        public override void CollectPreTurnChoices(PreTurnChoiceContext context, IList<ChoiceRequest> requests)
        {
            if(context==null || !context.IsAttack || !CharacterBehaviors.ActorHas(context.Defender,this)) return;
            var preview=new AttackContext(context.Actor,context.Defender,context.ActedHand,context.EffectiveResult,context.Manager);
            var incoming=(context.Actor?.Character as IAttackTypeProvider)?.AttackTypeOf(preview)??AttackDamageType.None;
            var mage=CharacterBehaviors.FirstImplementer<IMageState>(context.Actor);
            if(incoming==AttackDamageType.Force || mage?.PendingDamageType==DamageType.Force)return;
            var hands=Hands(context.Defender,2);
            int count=Math.Min(hands.Count,CharacterFactory.MaxAttackHits(context.Actor)
                +(((context.Actor?.Character as IExtraAttackProvider)?.WillExtraAttack(context)==true)?1:0));
            for(int i=0;i<count;i++) requests.Add(new ChoiceRequest(HandEffect.SchoolOfNecromancyBlock,
                $"【不死适应】选择一只2变为1，抵挡第{i+1}段攻击；力场不可挡。",hands,
                allowDecline:true,decider:context.Defender,variant:i));
        }
        public bool TryBlockAttack(AttackContext context, GameManager manager, int hitIndex)
        {
            if(context?.Defender==null || context.AttackType==AttackDamageType.Force || context.IsForceElement) return false;
            var hands=Hands(context.Defender,2); if(hands.Count==0) return false;
            Hand hand;
            // 有界面读取器时必须尊重显式的“不发动”，不能回退到自动第一只。
            if(manager?.AnsweredLookupFor!=null)
                hand=manager.AnsweredLookupFor(context.Defender,HandEffect.SchoolOfNecromancyBlock,hitIndex);
            else hand=hands[0];
            if(hand==null || !hands.Contains(hand)) return false;
            hand.SetValue(1);
            manager?.NoteSkill(context.Defender,"不死适应",$"{CombatText.HandName(context.Defender,hand)}2→1，抵挡第{hitIndex+1}段");
            return true;
        }
        public StatusEffect StatusToApplyOnHit(AttackContext context)
            => context?.IsNecromancyFreeAttack==true?StatusEffect.Decay:StatusEffect.None;

        public void OnTurnStarted(PlayerState owner, GameManager manager)
        {
            if(owner==null || manager==null || !owner.IsAlive || !ReferenceEquals(manager.CurrentPlayer,owner)
                || !StatusRules.CanAct(owner) || _lastFreeAttackTurn==manager.TurnCount) return;
            var sources=Hands(owner,1); if(sources.Count==0) return;
            _lastFreeAttackTurn=manager.TurnCount;
            Action<Hand> chooseSource=source=>
            {
                if(source==null || !owner.IsAlive || source.Value!=1 || source.IsLocked) return;
                var targets=new List<Hand>();
                foreach(var p in manager.Players)
                    if(p.IsAlive && !ReferenceEquals(p,owner) && StatusRules.CanAttackTarget(owner,p,manager)
                        && !((p.Character as IAttackTargetRule)?.IsImmuneToAttack(p)??false)) targets.Add(p.LeftHand);
                if(targets.Count==0) return;
                var request=new WizardChoice(owner,"【惧怖收割】选择攻击目标；免费攻击不消耗正常行动。",targets,true,picked=>
                {
                    if(picked==null) return;
                    PlayerState target=null;
                    foreach(var p in manager.Players) if(p.Owns(picked)) target=p;
                    if(target==null || !target.IsAlive || !owner.IsAlive || source.Value!=1) return;
                    var attack=new AttackContext(owner,target,source,1) {
                        AttackType=AttackDamageType.Necrotic, IgnoreStatBonuses=true, IsNecromancyFreeAttack=true };
                    Action resolve=()=> {
                        attack.Resolve(manager.GetChoiceProvider(target),manager);
                        manager.NoteSkill(owner,"惧怖收割",$"对{target.Name}发起1点暗蚀"); manager.RefreshGameOver(); };
                    if(manager.DeferWizardAttack?.Invoke(attack,resolve)!=true) resolve();
                },targetsPlayer:true,declineLabel:"放弃攻击");
                if(manager.DeferWizardChoice?.Invoke(request)==true) return;
                Hand best=null; int hp=int.MaxValue;
                foreach(var h in targets) foreach(var p in manager.Players)
                    if(p.Owns(h) && !manager.IsTeammate(owner,p) && p.Health<hp) {best=h;hp=p.Health;}
                var policyTarget=manager.WizardPolicy(owner)?.NecroticTarget(owner,manager);
                if(manager.WizardPolicy(owner)!=null) best=policyTarget?.LeftHand;
                request.Choose(best);
            };
            // 有可用1即自动进入目标选择；不再要求点某只手发动。
            chooseSource(sources[0]);
        }

        public void OnPlayerDied(PlayerState dead, GameManager manager)
        {
            if(_owner==null || !_owner.IsAlive || dead==null || dead.IsAlive || ReferenceEquals(dead,_owner)
                || manager==null || manager.IsGameOver) return;
            QueueExchange(dead,manager,new HashSet<Hand>(),new HashSet<Hand>());
        }
        private int ValueOf(int value,GameManager game) => game.WizardPolicy(_owner)?.ExchangeValue(_owner,value)??value;
        private void QueueExchange(PlayerState dead, GameManager manager, HashSet<Hand> ownUsed, HashSet<Hand> deadUsed)
        {
            if(ownUsed.Count>=2 || !_owner.IsAlive || manager.IsGameOver) return;
            var own=new List<Hand>(); var other=new List<Hand>();
            foreach(var h in _owner.Hands) if(!ownUsed.Contains(h)) own.Add(h);
            foreach(var h in dead.Hands) if(!deadUsed.Contains(h)) other.Add(h);
            var sourceChoice=new WizardChoice(_owner,$"【不死奴仆】与{dead.Name}交换0～2只手值：选自己的手，或结束。",own,true,source=>
            {
                if(source==null) return;
                var destination=new WizardChoice(_owner,"【不死奴仆】选择死者的一只手与所选手交换数字。",other,true,target=>
                {
                    if(target==null || !_owner.IsAlive || manager.IsGameOver) return;
                    int a=source.Value,b=target.Value; source.SetValue(b);target.SetValue(a);
                    ownUsed.Add(source);deadUsed.Add(target);
                    manager.NoteSkill(_owner,"不死奴仆",$"与{dead.Name}交换{CombatText.HandName(_owner,source)} {a}↔{b}",dead,new SkillVisualData(source,target,a,b));
                    QueueExchange(dead,manager,ownUsed,deadUsed);
                });
                if(manager.DeferWizardChoice?.Invoke(destination)==true) return;
                Hand best=null; foreach(var h in other) if(ValueOf(h.Value,manager)>ValueOf(source.Value,manager)
                    && (best==null || ValueOf(h.Value,manager)>ValueOf(best.Value,manager))) best=h;
                destination.Choose(best);
            });
            if(manager.DeferWizardChoice?.Invoke(sourceChoice)==true) return;
            Hand lowest=null; foreach(var h in own) if(lowest==null || ValueOf(h.Value,manager)<ValueOf(lowest.Value,manager)) lowest=h;
            bool useful=false; foreach(var h in other) if(lowest!=null && ValueOf(h.Value,manager)>ValueOf(lowest.Value,manager)) useful=true;
            sourceChoice.Choose(useful?lowest:null);
        }
    }
}
