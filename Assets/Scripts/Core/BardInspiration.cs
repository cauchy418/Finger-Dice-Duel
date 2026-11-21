using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public interface IBardInspiration { }

    public static class BardInspiration
    {
        public const string Key = "bard_result_adjust";
        public static bool Available(PlayerState actor)
            => actor?.IsAlive == true && !actor.HasStatus(StatusEffect.Dizzy)
                && CharacterBehaviors.FirstImplementer<IBardInspiration>(actor) != null;
        // Use ordinary arithmetic: decimal wrap, with zero normalized to one.
        public static int Adjust(int result, int delta)
            => delta < 0 ? Hand.ComputeDifference(result, 1) : delta > 0 ? Hand.ComputeSum(result, 1) : result;
        public static IEnumerable<int> Results(PlayerState actor, int result)
        {
            yield return result;
            if (!Available(actor)) yield break;
            int lower = Adjust(result, -1), upper = Adjust(result, 1);
            if (lower != result) yield return lower;
            if (upper != result && upper != lower) yield return upper;
        }
    }

    public partial class GameManager
    {
        private PlayerState _bardActor;
        private Hand _bardSource, _bardAddend;
        private int _bardTurn = -1, _bardEpoch, _bardDelta;
        private bool _bardReady;
        public int CurrentBardResult { get; private set; }
        public Hand CurrentBardSource => _bardSource;
        public Hand CurrentBardAddend => _bardAddend;
        public bool CanAdjustBardResult(PlayerState actor) => BardInspiration.Available(actor) && !WillSuppressOperationEffects(actor);
        private bool SameBard(PlayerState actor, Hand source, Hand addend)
            => _bardTurn == TurnCount && ReferenceEquals(actor, _bardActor) && ReferenceEquals(source, _bardSource) && ReferenceEquals(addend, _bardAddend);
        public bool HasPreparedBard(PlayerState actor, Hand source, Hand addend) => _bardReady && SameBard(actor, source, addend);
        public void ClearPreparedBard()
        { _bardEpoch++; _bardActor = null; _bardSource = _bardAddend = null; _bardTurn = -1; _bardReady = false; _bardDelta = 0; }
        public int BardPreview(PlayerState actor, Hand source, Hand addend, int fallback)
            => HasPreparedBard(actor, source, addend) && CanAdjustBardResult(actor) ? BardInspiration.Adjust(fallback, _bardDelta) : fallback;
        public bool RequestBardOperation(PlayerState actor, Hand source, Hand addend, Action completed)
        {
            if (HasPreparedBard(actor, source, addend) || !CanAdjustBardResult(actor) || source == null || addend == null)
            { completed(); return false; }
            ClearPreparedBard(); _bardActor = actor; _bardSource = source; _bardAddend = addend; _bardTurn = TurnCount;
            int epoch = _bardEpoch;
            CurrentBardResult = PreviewOperationResult(actor, source, addend);
            int original = CurrentBardResult;
            var options = new[] {
                new SkillChoiceOption(0, "保留 " + original, "Professions/bard"),
                new SkillChoiceOption(-1, "−1 → " + BardInspiration.Adjust(original, -1), "Professions/bard"),
                new SkillChoiceOption(1, "+1 → " + BardInspiration.Adjust(original, 1), "Professions/bard") };
            var request = new WizardChoice(actor, "吟游激励 · 运算结果 " + original + "，可保留或调整1点", options, delta => {
                if (epoch != _bardEpoch || _bardTurn != TurnCount || _bardReady) return;
                _bardDelta = CanAdjustBardResult(actor) && source.CanBeOperated ? delta : 0;
                _bardReady = true;
                completed();
            });
            if (DeferWizardChoice?.Invoke(request) == true) return true;
            request.ChooseOption(SkillChoicePolicy.Choose(BardInspiration.Key, options, actor, null, this));
            return false;
        }
        private int ApplyBardResult(PlayerState actor, Hand source, Hand addend, int value)
        {
            int result = BardPreview(actor, source, addend, value);
            if (result != value) NoteSkill(actor, "吟游激励", $"运算结果{value}→{result}", actor,
                new SkillVisualData(source: source, value: result, previous: value));
            return result;
        }
    }
}
