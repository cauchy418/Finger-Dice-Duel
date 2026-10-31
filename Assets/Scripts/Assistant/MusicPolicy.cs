using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public enum MusicCue { Dusk, Battlefield, Dawn }

    /// <summary>Music state only: no writes to combat, RNG or turn progression.</summary>
    public sealed class MusicPolicy
    {
        private GameManager _match;
        public MusicCue Cue { get; private set; }
        public bool Percussion { get; private set; }
        public void Sync(GameManager game, bool inBattle, int adventureLevel)
        {
            if (!inBattle || game == null)
            {
                _match = null; Cue = MusicCue.Dusk; Percussion = false; return;
            }
            if (!ReferenceEquals(_match, game)) { _match = game; Percussion = false; }
            Cue = adventureLevel > 0 && adventureLevel % 10 == 0 ? MusicCue.Dawn : MusicCue.Battlefield;
            if (!Percussion)
                foreach (var player in game.Players)
                    if (!player.IsSummon && player.MaximumHealth > 0 && player.Health * 2 <= player.MaximumHealth)
                    { Percussion = true; break; }
        }
    }
}
