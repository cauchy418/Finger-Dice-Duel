using System;
using System.Collections.Generic;
using FingerDiceDuel.Characters.SpecialLogics;
namespace FingerDiceDuel.Core
{
    public partial class GameManager
    {
        internal bool? TacticianSubtraction { get; set; }

        public bool CanChooseTacticianOperation(PlayerState actor)
            => CharacterBehaviors.FirstImplementer<TacticianLogic>(actor) != null
                && !WillSuppressOperationEffects(actor);

        private bool ShouldSubtract(PlayerState actor)
            => TacticianSubtraction == true && CharacterBehaviors.FirstImplementer<TacticianLogic>(actor) != null;

        internal bool ChooseTacticianSubtraction(PlayerState actor, Hand source, Hand operand)
            => (GetSkillPolicy(actor) as ITacticianOperationPolicy ?? GetChoiceProvider(actor) as ITacticianOperationPolicy)
                ?.ChooseSubtraction(this, actor, source, operand) == true;

        internal int PreviewTacticianResult(PlayerState actor, Hand source, Hand operand, bool subtract)
            => ModifiedOperationValue(actor, OperationSum(actor, operand, source.Value, operand.Value, subtract: subtract));

        private bool IsTouchedTaiChiZero(PlayerState actor, Hand addend, int value)
        {
            if (value != 0) return false;
            var owner = OwnerOfHand(addend);
            return owner != null && !ReferenceEquals(owner, actor)
                && owner.Character is WayOfTheTaiChiLogic;
        }

        private int OperationSum(PlayerState actor, Hand addend, int sourceValue, int addendValue,
                                 bool useSecondAddend = true, bool touchTaiChi = true, bool subtract = false)
        {
            if (touchTaiChi && IsTouchedTaiChiZero(actor, addend, addendValue)) addendValue = 1;
            int value = subtract ? Hand.ComputeDifference(sourceValue, addendValue) : Hand.ComputeSum(sourceValue, addendValue);
            if (useSecondAddend && DruidForms.IsDragon(actor) && DruidSecondAddend != null)
                value = Hand.ComputeSum(value, ReferenceEquals(addend, DruidSecondAddend)
                    ? addendValue : DruidSecondAddend.Value);
            return value;
        }

        internal static int ModifiedOperationValue(PlayerState actor, int value)
            => Math.Max(1, StatusRules.ApplyResultModifiers(actor, value));

        public int PreviewOperationResult(PlayerState actor, Hand source, Hand addend)
            => PreviewOperationResult(actor, source, addend, source?.Value ?? 0, addend?.Value ?? 0);

        internal int PreviewOperationResult(PlayerState actor, Hand source, Hand addend, int sourceValue,
                                            int addendValue, List<KeyValuePair<Hand, int>> shown = null)
        {
            bool suppressed = WillSuppressOperationEffects(actor);
            int value = ModifiedOperationValue(actor,
                OperationSum(actor, addend, sourceValue, addendValue, !suppressed, !suppressed, !suppressed && ShouldSubtract(actor)));
            if (suppressed) return value;

            if (shown != null && IsTouchedTaiChiZero(actor, addend, addendValue))
                shown.Add(new KeyValuePair<Hand, int>(addend, 1));

            value = TrickeryPreview(actor, source, addend, value, shown);
            return PortentPreview(actor, source, addend, value);
        }

        internal bool OperationEffectsSuppressed(PlayerState actor, Hand source, Hand addend)
            => WillSuppressOperationEffects(actor) || (actor?.HasStatus(StatusEffect.Dizzy) == true
                && !HasPreparedPortent(actor, source, addend) && !HasPreparedTrickerySwap(actor, source, addend));

        /// <summary>Pure display projection; never invokes effect rewriters, RNG or skill hooks.</summary>
        public IReadOnlyList<KeyValuePair<Hand,int>> PreviewOperationHands(PlayerState actor,Hand source,Hand addend)
        {
            var shown=new List<KeyValuePair<Hand,int>>();
            if(actor==null||source==null||addend==null)return shown;
            int value = PreviewOperationResult(actor, source, addend, source.Value, addend.Value, shown);
            shown.Add(new KeyValuePair<Hand,int>(source,value));return shown;
        }
    }
}
