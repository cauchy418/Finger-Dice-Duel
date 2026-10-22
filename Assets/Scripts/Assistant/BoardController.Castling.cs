using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class BoardController
    {
        public bool CanCastleWith(PlayerState target)
            => SeatSwapSkills.Of(ActivePlayer)?.CanSwapSeats(ActivePlayer,target,_game) == true;

        public bool CanCastle
        {
            get
            {
                if (_game.IsGameOver || CollegeOfValorChoicePending
                    || (Phase!=BoardPhase.ChoosingAttackerHand && Phase!=BoardPhase.ChoosingTargetHand
                        && Phase!=BoardPhase.ChoosingGamble)) return false;
                foreach (var player in _game.Players) if (CanCastleWith(player)) return true;
                return false;
            }
        }

        public bool BeginCastling()
        {
            if (!CanCastle) return false;
            SelectedHand=null; _selectedTarget=null; _chosenDefenseShield=null; _shieldChoices.Clear();
            _pendingChoiceCandidates.Clear();
            foreach (var player in _game.Players)
                if (CanCastleWith(player))
                {
                    _pendingChoiceCandidates.Add(player.LeftHand);
                    _pendingChoiceCandidates.Add(player.RightHand);
                }
            Phase=BoardPhase.ChoosingSeatSwapTarget;
            Hint="【王车易位】选择手值相同的角色换位并跳过回合；距离立即更新，下轮按新座位行动。";
            return true;
        }

        public bool CancelCastling()
        {
            if (Phase!=BoardPhase.ChoosingSeatSwapTarget) return false;
            _pendingChoiceCandidates.Clear();
            _preTargetChoiceActive=false; _returnToTargetAfterChoice=false;
            SetupFirstStep();
            return true;
        }

        public bool CompleteCastling(PlayerState target)
        {
            if (Phase!=BoardPhase.ChoosingSeatSwapTarget || !CanCastleWith(target)) return false;
            var actor=ActivePlayer;
            int from=_game.SeatOf(actor), to=_game.SeatOf(target);
            if (!SeatSwapSkills.Of(actor).TrySwapSeats(actor,target,_game)) return false;
            _log.Add(CombatText.Skill(actor,"王车易位",$"与{target.Name}换位 {from}↔{to}，跳过回合"));
            _statusTurnEndFor=actor;
            _game.SkipTurnAlreadyValidated(extraAction: _extraActionActive);
            SelectedHand=null; _selectedTarget=null; _chosenDefenseShield=null;
            _shieldChoices.Clear(); _pendingChoiceCandidates.Clear();
            _preTargetChoiceActive=false; _returnToTargetAfterChoice=false;
            if (_extraActionActive) BeginNextExtraActionOrResume();
            else BeginTurn();
            return true;
        }

        // 仅在换位能减少近身威胁时牺牲行动，不作无收益的来回换位。
        private bool TryAiCastling()
        {
            if (!CanCastle || _game.Players.Count<4) return false;
            PlayerState best=null;
            int bestGain=0;
            int n=_game.Players.Count;
            foreach (var target in _game.Players)
            {
                if (!CanCastleWith(target)) continue;
                int gain=0;
                foreach (var enemy in _game.Players)
                {
                    if (!enemy.IsAlive || ReferenceEquals(enemy,ActivePlayer) || _game.IsTeammate(ActivePlayer,enemy)) continue;
                    int oldDistance=_game.DistanceBetween(ActivePlayer,enemy);
                    // 以实际存活环估算；目标本人与施法者互换后，两者距离不变。
                    int after=0;
                    if (ReferenceEquals(enemy,target)) after=oldDistance;
                    else
                    {
                        int between=0, alive=0, x=-1, y=-1;
                        for(int i=0;i<n;i++)
                        {
                            var p=_game.Players[i];
                            if (!p.IsAlive) continue;
                            if(ReferenceEquals(p,target)) x=alive;
                            if(ReferenceEquals(p,enemy)) y=alive;
                            alive++;
                        }
                        between=System.Math.Abs(x-y);
                        after=System.Math.Min(between,alive-between);
                    }
                    if(oldDistance<=1 && after>1) gain++;
                    if(oldDistance>1 && after<=1) gain--;
                }
                if(gain>bestGain) {bestGain=gain;best=target;}
            }
            return best!=null && BeginCastling() && CompleteCastling(best);
        }
    }
}
