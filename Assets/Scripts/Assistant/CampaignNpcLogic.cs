using System;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed class CampaignNpcLogic:CharacterTemplate,IStatusApplyingAttack,ICompletedDamageDealt,INpcCircuitProgress,ITurnStartHook
    {
        public readonly string Name;
        public int Attack,Defense,Range,FreeAttackDelta;
        public AttackDamageType Weapon;
        public bool FreeCombat,CanRaiseShield,CanAid,CanRally,CanMuster,Steady,Phantom,PeriodicForce;
        public int ShieldCooldown,AidCooldown,AidCharges=2;
        public int CircuitProgress{get;private set;}
        public bool RallySpent,MusterSpent;
        private int _attacks;
        public string AbilitySummary=>
            (CanRaiseShield?"架盾：低于半血时消耗正常行动形成5盾，冷却3自身回合。":"")+
            (CanAid?"军需急救：消耗正常行动，距离1内自己或队友回复4，2次，冷却3自身回合。":"")+
            (CanRally?"整军：不高于半血时消耗正常行动回复6，限一次。":"")+
            (CanMuster?"集结令：不高于半血时消耗正常行动，为自己和距离2内队友回复4，限一次。":"")+
            (Steady?"稳弦：普通6攻击基础伤害+1。":"")+
            (Phantom?"永久火焰、寒冷抗性及毒素免疫；每3次普通攻击实际伤人后回复3。":"")+
            (PeriodicForce?"每第三次普通攻击改为力场。":"");
        public CampaignNpcLogic(string name,int attack=0,int defense=0,int range=1,AttackDamageType weapon=AttackDamageType.Slashing)
        {Name=name;Attack=attack;Defense=defense;Range=range;Weapon=weapon;}
        public override string DisplayName=>Name;
        protected override int AdjustAttackBonus(int value)=>Attack+(FreeCombat?FreeAttackDelta:0);
        public override int DefaultDefenseBonus=>Defense;
        public override int BaseAttackRange=>Range;
        public override AttackDamageType AttackTypeOf(AttackContext context)
            =>FreeCombat&&PeriodicForce&&(_attacks+1)%3==0?AttackDamageType.Force:Weapon;
        public StatusEffect StatusToApplyOnHit(AttackContext context)=>ArcaneChoices.ElementStatus(context.AttackType);
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {if(!ReferenceEquals(game.CurrentPlayer,owner))return;if(ShieldCooldown>0)ShieldCooldown--;if(AidCooldown>0)AidCooldown--;}
        public override void OnMatchReset(PlayerState owner,GameManager game)
        {
            ShieldCooldown=AidCooldown=CircuitProgress=_attacks=0;AidCharges=2;RallySpent=MusterSpent=false;
            if(Phantom){owner.AddPermanentResistance(AttackDamageType.Fire);owner.AddPermanentResistance(AttackDamageType.Cold);owner.AddPermanentImmunity(AttackDamageType.Poison);}
        }
        public override void OnResolveAttack(AttackContext attack)
        {
            if(attack.IsStatusDamage||attack.IsReflected||attack.IsSpecialAttack)return;
            _attacks++;
            if(Steady&&attack.SourceHand?.Value==6){attack.FinalDamage++;attack.Manager?.NoteSkill(attack.Attacker,"稳弦","本次基础伤害+1",attack.Defender);}
        }
        public void OnCompletedDamage(AttackContext attack,GameManager game)
        {
            if(!Phantom||attack.IsStatusDamage||attack.IsReflected||attack.IsSpecialAttack||attack.IsNecromancyFreeAttack||attack.ActualHealthDamage<=0||!attack.Attacker.IsAlive)return;
            if(++CircuitProgress<3){game.RecordSkillVisual(attack.Attacker,"汲能回路",attack.Attacker,new SkillVisualData(value:CircuitProgress,variant:"charge"));return;}
            CircuitProgress=0;int before=attack.Attacker.Health;attack.Attacker.Heal(3);
            game.NoteSkill(attack.Attacker,"汲能回路","回复"+(attack.Attacker.Health-before)+"，进度归0",attack.Attacker,new SkillVisualData(value:0,previous:attack.Attacker.Health-before,variant:"recover"));
        }
        public bool TryAction(PlayerState owner,GameManager game)
        {
            if(!FreeCombat||!owner.IsAlive||owner.IsPolymorphed||!StatusRules.CanAct(owner))return false;
            if(CanMuster&&!MusterSpent&&owner.Health*2<=owner.MaximumHealth&&StatusRules.CanHeal(owner))
            {MusterSpent=true;foreach(var p in game.Players)if(p.IsAlive&&game.IsTeammate(owner,p)&&game.DistanceBetween(owner,p)<=2)Heal(game,owner,p,4,"集结令");return true;}
            if(CanRally&&!RallySpent&&owner.Health*2<=owner.MaximumHealth&&StatusRules.CanHeal(owner))
            {RallySpent=true;Heal(game,owner,owner,6,"整军");return true;}
            if(CanAid&&AidCharges>0&&AidCooldown==0)
            {
                PlayerState target=null;
                foreach(var p in game.Players)if(p.IsAlive&&(ReferenceEquals(p,owner)||game.IsTeammate(owner,p))&&game.DistanceBetween(owner,p)<=1&&p.Health<=p.MaximumHealth-4&&StatusRules.CanHeal(p)&&(target==null||p.Health<target.Health))target=p;
                if(target!=null){AidCharges--;AidCooldown=4;Heal(game,owner,target,4,"军需急救");return true;}
            }
            if(CanRaiseShield&&ShieldCooldown==0&&owner.Health*2<=owner.MaximumHealth&&!owner.LeftHand.IsShield&&!owner.RightHand.IsShield)
                return RaiseShield(owner,game);
            return false;
        }
        public bool RaiseShield(PlayerState owner,GameManager game)
        {
            if(ShieldCooldown>0||!StatusRules.CanAct(owner)||owner.IsPolymorphed)return false;
            Hand hand=owner.RightHand.CanBeOperated&&!owner.RightHand.IsShield?owner.RightHand:owner.LeftHand.CanBeOperated&&!owner.LeftHand.IsShield?owner.LeftHand:null;
            if(hand==null)return false;hand.SetValue(5);hand.SetShieldState(ShieldState.Formed);ShieldCooldown=4;
            owner.Character.OnShieldFormed(owner,hand,game);game.NoteSkill(owner,"架盾","形成完整5盾；冷却3自身回合",owner,new SkillVisualData(source:hand));return true;
        }
        public static void Heal(GameManager game,PlayerState source,PlayerState target,int amount,string skill)
        {int before=target.Health;target.Heal(amount);game.NoteSkill(source,skill,target.Name+" 回复"+(target.Health-before),target,new SkillVisualData(value:target.Health-before));game.RecordSkillVisual(source,"生命门徒",target,new SkillVisualData(value:target.Health-before));}
    }
}
