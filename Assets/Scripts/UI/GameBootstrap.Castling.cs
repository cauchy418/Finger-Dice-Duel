using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBootstrap
    {
        private GameManager _seatLayoutGame;
        private int _seatLayoutRevision=-1;

        private void OnSeatSwapClicked()
        {
            if (_controller==null || !_controller.IsHumanTurn
                || !ReferenceEquals(_controller.ActivePlayer,HumanPlayer())) return;
            if (_controller.BeginCastling()) RefreshView();
        }

        private void SyncSeatLayout()
        {
            if (ReferenceEquals(_seatLayoutGame,_game) && _seatLayoutRevision==_game.SeatRevision) return;
            _seatLayoutGame=_game; _seatLayoutRevision=_game.SeatRevision;
            if (!_isEightPlayer && !_isTwoOnTwo) return;
            // 必须在替换座位列表前记住人类玩家；队友关系始终向规则层查询。
            PlayerState me=HumanPlayer();
            var seated=new List<PlayerState>(_game.Players);
            if (me==null || !seated.Contains(me)) return;
            var relations=new List<RingRelation>();
            PlayerState mate=null;
            foreach(var p in seated)
            {
                bool self=ReferenceEquals(p,me), ally=!self && _game.IsTeammate(me,p);
                relations.Add(self?RingRelation.Self:ally?RingRelation.Teammate:RingRelation.Opponent);
                if(ally) mate=p;
            }
            int mySeat=seated.IndexOf(me)+1;
            if (_isEightPlayer) _eightSeated=seated;
            else
            {
                _twoOnTwoSeated=seated; _twoOnTwoMeIndex=mySeat-1; _twoOnTwoMySeat=mySeat;
            }
            _board.BindPlayersRing(me,seated,mySeat,relations);
            if (_isTwoOnTwo && !_isEightPlayer)
                _board.SetTitle($"2 v 2　我（{me.Definition?.DisplayName ?? me.Name}）　队友（{mate?.Definition?.DisplayName ?? mate?.Name}）");
        }
    }
}
