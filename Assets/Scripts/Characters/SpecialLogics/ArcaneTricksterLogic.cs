using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>The mage hand stores a digit outside the two targetable body hands.</summary>
    public sealed class ArcaneTricksterLogic : CharacterTemplate, ITurnStartHook, ITurnEndHook
    {
        public const string ExchangeChoice = "mage_hand_exchange";
        public int MageHandValue { get; private set; } = 1;
        /// <summary>Timing of the currently offered exchange, including deferred UI choices.</summary>
        public bool ExchangeAtTurnStart { get; private set; } = true;
        public override string DisplayName => "游荡者·诡术师";
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => (context?.Manager?.DistanceBetween(context.Attacker, context.Defender) ?? 1) > 1
                ? AttackDamageType.Piercing : AttackDamageType.Slashing;
        public override void OnResolveAttack(AttackContext context)
        {
            // The dual-general container does not forward IAttackTypeProvider.
            if (context.Attacker.Character is ICharacterBehaviorContainer && context.AttackType == AttackDamageType.None)
                context.AttackType = AttackTypeOf(context);
        }
        public static ArcaneTricksterLogic For(PlayerState owner)
            => CharacterBehaviors.FirstImplementer<ArcaneTricksterLogic>(owner);
        public static ArcaneTricksterLogic StoredFor(PlayerState owner)
        {
            var original = owner?.PersistentCharacter;
            if (original is ArcaneTricksterLogic direct) return direct;
            if (original is ICharacterBehaviorContainer container)
                foreach (var part in container.Parts)
                    if (part is ArcaneTricksterLogic trickster) return trickster;
            return null;
        }
        public override void OnMatchReset(PlayerState owner, GameManager manager)
        { MageHandValue = 1; ExchangeAtTurnStart = true; }
        public bool CanExchange(PlayerState owner, Hand hand, GameManager game)
            => owner?.IsAlive == true && game != null && !game.IsGameOver
                && CharacterBehaviors.ActorHas(owner, this) && StatusRules.CanAct(owner)
                && hand != null && owner.Owns(hand) && hand.CanBeOperated;
        public void OnTurnStarted(PlayerState owner, GameManager game)
        {
            // BeginTurn broadcasts hooks to every player, but only the acting rogue may swap.
            if (ReferenceEquals(owner, game?.CurrentPlayer)) OfferExchange(owner, game, true);
        }
        public void OnTurnEnded(PlayerState owner, GameManager game) => OfferExchange(owner, game, false);
        private void OfferExchange(PlayerState owner, GameManager game, bool atStart)
        {
            var candidates = new List<Hand>();
            foreach (var hand in owner.Hands) if (CanExchange(owner, hand, game)) candidates.Add(hand);
            if (candidates.Count == 0) return;
            ExchangeAtTurnStart = atStart;
            string timing = atStart ? "回合开始" : "回合结束";
            bool resolved = false;
            var request = new WizardChoice(owner,
                $"法师之手诈术 · {timing} · 魔法手 {MageHandValue}，点击一只本体手交换点数", candidates, true,
                hand => {
                    if (resolved) return;
                    resolved = true;
                    if (hand != null) Exchange(owner, hand, game);
                }, declineLabel: "不交换");
            if (game.DeferWizardChoice?.Invoke(request) == true) return;
            var options = new List<SkillChoiceOption> { new SkillChoiceOption(0, "不交换", "UI/status_counter") };
            foreach (var hand in candidates)
                options.Add(new SkillChoiceOption(ReferenceEquals(hand, owner.LeftHand) ? 1 : 2,
                    ReferenceEquals(hand, owner.LeftHand) ? "交换左手" : "交换右手", "Effects/mage_hand"));
            int choice = SkillChoicePolicy.Choose(ExchangeChoice, options, owner, null, game);
            request.Choose(choice == 1 ? owner.LeftHand : choice == 2 ? owner.RightHand : null);
        }
        /// <summary>Only digits move. Body-hand restrictions remain attached to the same hand.</summary>
        public bool Exchange(PlayerState owner, Hand hand, GameManager game)
        {
            if (!CanExchange(owner, hand, game)) return false;
            int previous = hand.Value, stored = MageHandValue;
            var frame = game.CaptureVisualFrame();
            var state = hand.State;
            if (hand.IsShield && stored != 5)
            {
                game.BreakActiveShieldAndNotify(owner, hand);
                // A normal shield-break hook may kill or transform the owner.
                if (!CanExchange(owner, hand, game)) return false;
                state = hand.State;
            }
            hand.SetValue(stored);
            if (previous == stored) hand.SetShieldState(state);
            else if (stored == 5) hand.SetShieldState(ShieldState.Broken);
            MageHandValue = previous;
            game.NoteSkill(owner, "法师之手诈术", $"{CombatText.HandName(owner, hand)} {previous}↔{stored}，魔法手储存 {MageHandValue}",
                owner, new SkillVisualData(source: hand, value: stored, previous: previous, variant: "exchange"), frame);
            return true;
        }
    }
}
