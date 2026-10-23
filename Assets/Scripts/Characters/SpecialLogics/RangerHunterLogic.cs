using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>游侠·猎人：7 标记、标记目标无视正防御并追加已损体力/5。</summary>
    public sealed class RangerHunterLogic : CharacterTemplate, IAttackTypeProvider, IHunterMarkSource
    {
        private readonly HashSet<PlayerState> _marked = new HashSet<PlayerState>();
        private PlayerState _owner;

        public override string DisplayName => "游侠·猎人";
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => 3;

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _owner = owner;
            _marked.Clear();
        }

        public bool IsMarked(PlayerState target) => target != null && _marked.Contains(target);

        public void Mark(PlayerState target)
        {
            if (target == null || !target.IsAlive) return;
            _marked.Add(target);
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
                                                  int actedHandValueBefore, int targetValue,
                                                  HandEffectType effect, GameManager manager,
                                                  IPreTurnChoiceSink choiceSink)
        {
            if (owner == null || actedHand == null || actedHand.Value != 7) return;
            var targets=new List<PlayerState>();var hands=new List<Hand>();
            foreach(var p in manager.Players)
                if(p.IsAlive && !ReferenceEquals(p,owner) && manager.DistanceBetween(owner,p)<=3)
                {targets.Add(p);hands.Add(p.LeftHand);}
            if(targets.Count==0) return;
            var request=new WizardChoice(owner,"猎人标记 · 选择射程3内的角色",hands,true,hand=> {
                var target=manager.OwnerOfHand(hand);
                if(target==null || !target.IsAlive || ReferenceEquals(target,owner) || manager.DistanceBetween(owner,target)>3) return;
                Mark(target);manager.NoteSkill(owner,"猎人标记",$"{target.Name} 被标记");
            },targetsPlayer:true,declineLabel:"不标记");
            if(manager.DeferWizardChoice?.Invoke(request)==true) return;
            var policy=manager.GetSkillPolicy(owner);
            var pick=policy!=null?policy.HunterMarkTarget(owner,targets,manager):targets[0];
            request.Choose(pick?.LeftHand);
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context?.Defender == null || !_marked.Contains(context.Defender)) return;
            context.IgnorePositiveDefense = true;
            int lost = context.Defender.MaximumHealth - context.Defender.Health;
            if (lost > 0) context.FinalDamage += lost / 5;
            _marked.Remove(context.Defender);
            context.Manager?.NoteSkill(context.Attacker, "猎人印记", $"消耗 {context.Defender.Name} 的标记，已损体力/5 加伤");
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Piercing;
    }
}
