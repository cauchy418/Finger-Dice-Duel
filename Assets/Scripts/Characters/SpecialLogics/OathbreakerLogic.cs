using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>破誓者：5 改为攻击；持5可把整次来袭攻击转移给持完整盾的角色。</summary>
    public sealed class OathbreakerLogic : CharacterTemplate,
        IHandEffectRewriter, IHandEffectDisplay, IResultTargetProvider, IPreTurnChoiceProvider, IAttackTransfer
    {
        public const int AttackValue = 5;

        public override string DisplayName => "破誓者";
        public override int DefaultAttackBonus => 2;
        public override int DefaultDefenseBonus => 0;
        public override int BaseAttackRange => 1;

        public HandEffectType RewriteEffect(PlayerState actor, Hand actedHand,
                                            int targetValue, GameManager manager)
        {
            if (actedHand != null && actedHand.Value == AttackValue)
                return HandEffectType.Attack4;
            return Hand.GetEffectType(actedHand?.Value ?? 0);
        }

        public HandEffectType DisplayedEffectOf(int resultValue)
            => resultValue == AttackValue ? HandEffectType.Attack4 : Hand.GetEffectType(resultValue);

        public override bool ResultNeedsTarget(PreTurnChoiceContext context)
        {
            if (context != null && context.ResultValue == AttackValue) return true;
            return base.ResultNeedsTarget(context);
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null || context.AttackValue != AttackValue) return;
            context.HitCount = 1;
            context.FinalDamage = AttackValue;
            context.Manager?.NoteSkill(context.Attacker, "破誓", "5 转为攻击");
        }

        // 仅供本轮收集后续防御抉择，不用于跨回合结算。
        private PreTurnChoiceContext _transferContext;

        private static bool HasFive(PlayerState owner)
            => owner != null && (owner.LeftHand.Value == 5 || owner.RightHand.Value == 5);

        private static bool Eligible(PlayerState owner, PlayerState attacker, PlayerState candidate)
            => candidate != null && candidate.IsAlive && !ReferenceEquals(candidate, owner)
                && !ReferenceEquals(candidate, attacker)
                && (candidate.LeftHand.State == ShieldState.Formed || candidate.RightHand.State == ShieldState.Formed);

        private static ChoiceRequest TransferRequest(PlayerState owner, PlayerState attacker, GameManager manager)
        {
            var hands = new List<Hand>();
            if (manager != null && HasFive(owner))
                foreach (var p in manager.Players)
                    if (Eligible(owner, attacker, p))
                    {
                        // 两只手均可代表该角色，UI 按人去重，点击手牌也可确认。
                        hands.Add(p.LeftHand); hands.Add(p.RightHand);
                    }
            return new ChoiceRequest(HandEffect.OathbreakerTransfer,
                "破誓者【转移攻击】：选择一名持完整盾牌的角色承受整次攻击，或不发动。保留手中的5。",
                hands, allowDecline: true, declineResult: null, decider: owner);
        }

        public override void CollectPreTurnChoices(PreTurnChoiceContext context, IList<ChoiceRequest> requests)
        {
            if (context == null || !context.IsAttack || !CharacterBehaviors.ActorHas(context.Defender, this)) return;
            _transferContext = context;
            var request = TransferRequest(context.Defender, context.Actor, context.Manager);
            if (request.Candidates.Count > 0) requests.Add(request);
        }

        public override void CollectFollowUpChoices(ChoiceRequest answered, Hand answer, IList<ChoiceRequest> requests)
        {
            if (answered.Effect != HandEffect.OathbreakerTransfer || answer == null || _transferContext?.Manager == null) return;
            var context = _transferContext;
            var manager = context.Manager;
            var target = manager.OwnerOfHand(answer);
            if (!Eligible(context.Defender, context.Actor, target)) return;
            var defense = new PreTurnChoiceContext(context.Actor, target, context.ActedHand, context.TargetHand)
            {
                Manager = manager, AlivePlayerCount = context.AlivePlayerCount,
                ActedHandValueBefore = context.ActedHandValueBefore,
                TargetHandValueBefore = context.TargetHandValueBefore,
                ResultValue = context.ResultValue, AnsweredLookup = context.AnsweredLookup,
            };
            // 转移仅一次；为实际目标预收集闪避、太极和碎盾等防御问题。
            var defenses = new List<ChoiceRequest>();
            (target.Character as IPreTurnChoiceProvider)?.CollectPreTurnChoices(defense, defenses);
            foreach (var request in defenses)
                if (request.Effect != HandEffect.OathbreakerTransfer) requests.Add(request);
            foreach (var p in manager.Players)
                CharacterBehaviors.FirstImplementer<PaladinLogic>(p)
                    ?.CollectRedirectChoice(p, context.Actor, target, manager, requests);
        }

        public PlayerState TransferAttack(AttackContext context, GameManager manager)
        {
            var owner = context?.Defender;
            if (!CharacterBehaviors.ActorHas(owner, this) || !HasFive(owner) || manager == null) return null;
            var request = TransferRequest(owner, context.Attacker, manager);
            if (request.Candidates.Count == 0) return null;
            var lookup = context.AnsweredLookupFor ?? manager.AnsweredLookupFor;
            Hand answer = lookup != null ? lookup(owner, HandEffect.OathbreakerTransfer, 0)
                : PreTurnChoiceReader.Read(manager.GetChoiceProvider(owner) as IPreTurnChoiceSink,
                    manager.GetChoiceProvider(owner), request, owner);
            var target = manager.OwnerOfHand(answer);
            return Eligible(owner, context.Attacker, target) ? target : null;
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Bludgeoning;
    }
}
