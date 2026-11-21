using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        private int _bardEvaluationDepth;
        private int BardBestResult(LegalMove move, PlayerState actor, GameManager game, int original)
        {
            if (_bardEvaluationDepth > 0 || !game.CanAdjustBardResult(actor) || game.HasPreparedBard(actor, move.ActedHand, move.TargetHand)) return original;
            _bardEvaluationDepth++;
            try
            {
                int selected = original, best = ScoreMove(move, actor, game, original);
                foreach (int value in BardInspiration.Results(actor, original))
                {
                    int score = ScoreMove(move, actor, game, value);
                    if (score > best) { best = score; selected = value; }
                }
                return selected;
            }
            finally { _bardEvaluationDepth--; }
        }
        private int ScoredOperationResult(LegalMove move, PlayerState actor, GameManager game)
        {
            int original = DruidScoredResult(move, actor, game);
            return game == null ? original : BardBestResult(move, actor, game, original);
        }
        private double BardOptionScore(int delta, PlayerState owner, GameManager game)
        {
            if (game.CurrentBardSource == null || game.CurrentBardAddend == null) return delta == 0 ? 1 : 0;
            var move = new LegalMove(owner, game.CurrentBardSource, game.CurrentBardAddend);
            return ScoreMove(move, owner, game, BardInspiration.Adjust(game.CurrentBardResult, delta));
        }
    }
}
