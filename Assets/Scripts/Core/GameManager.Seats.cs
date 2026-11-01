using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public partial class GameManager
    {
        private int[] _seatObstacles;
        public int StaticSeatObstacleCount { get; private set; }

        /// <summary>关卡开局配置各物理座位之后的固定占位物；不登记角色、不参与行动或胜负。</summary>
        public void ConfigureSeatObstacles(params int[] obstaclesAfterSeats)
        {
            if (TurnCount != 0) throw new InvalidOperationException("占位物须在开局前配置。");
            if (obstaclesAfterSeats == null || obstaclesAfterSeats.Length != _players.Count)
                throw new ArgumentException("每个角色座位需对应一项占位物数量。");
            int total = 0;
            foreach (int count in obstaclesAfterSeats)
            {
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(obstaclesAfterSeats));
                total = checked(total + count);
            }
            _seatObstacles = (int[])obstaclesAfterSeats.Clone();
            StaticSeatObstacleCount = total;
        }

        // 换位只改物理座位。本轮首次换位时锁定行动名单，下轮才采用新座位。
        private List<PlayerState> _roundActionOrder;
        private List<PlayerState> _originalSeatOrder;
        public int SeatRevision { get; private set; }
        /// <summary>经典先攻完成后初始化座位，保留角色、队伍及所有状态；仅能在开战前调用。</summary>
        public void InitializeSeatOrder(IReadOnlyList<PlayerState> seats)
        {
            if(TurnCount!=0||RoundCount!=0)throw new InvalidOperationException("仅开战前可初始化座位。");
            if(seats==null||seats.Count!=_players.Count)throw new ArgumentException("座位数不符。");
            var seen=new HashSet<PlayerState>();
            foreach(var p in seats)if(!_players.Contains(p)||!seen.Add(p))throw new ArgumentException("座位必须包含原参战角色且不重复。");
            _players.Clear();_players.AddRange(seats);_roundActionOrder=null;_originalSeatOrder=null;SeatRevision++;
        }
        /// <summary>关卡推障的真实距离变化，不重建角色或清除行动顺序。</summary>
        public bool MoveSeatObstacle(int afterSeat)
        {
            if(_seatObstacles==null||afterSeat<0||afterSeat>=_seatObstacles.Length||_seatObstacles[afterSeat]==0)return false;
            _seatObstacles[afterSeat]--;StaticSeatObstacleCount--;SeatRevision++;return true;
        }
        private List<PlayerState> ActionOrder => _roundActionOrder ?? _players;

        /// <summary>供已校验的座位技能使用；不改变队伍、身份、行动者或手牌。</summary>
        public bool TrySwapSeats(PlayerState a, PlayerState b)
        {
            if (IsGameOver || a == null || b == null || !a.IsAlive || !b.IsAlive || ReferenceEquals(a,b)) return false;
            int x=_players.IndexOf(a), y=_players.IndexOf(b);
            if (x<0 || y<0) return false;
            if (_originalSeatOrder==null) _originalSeatOrder=new List<PlayerState>(_players);
            if (_roundActionOrder==null) _roundActionOrder=new List<PlayerState>(_players);
            _players[x]=b; _players[y]=a;
            SeatRevision++;
            RecordSkillVisual(a,"王车易位",b);
            return true;
        }

        private void ResetSeats()
        {
            if (_originalSeatOrder!=null)
            {
                _players.Clear(); _players.AddRange(_originalSeatOrder);
                _originalSeatOrder=null; SeatRevision++;
            }
            _roundActionOrder=null;
        }
    }
}
