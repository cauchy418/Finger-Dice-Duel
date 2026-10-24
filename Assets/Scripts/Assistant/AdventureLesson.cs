using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>共享教学导演：步骤白名单、提示、脚本对手与安全自由练习；不改变普通对局规则。</summary>
    public abstract class AdventureLesson
    {
        protected readonly GameManager _game;
        protected readonly BoardController _board;
        protected readonly PlayerState _player;
        protected readonly PlayerState _soldier;
        protected readonly List<Beat> _beats = new List<Beat>();
        private int _beatIndex = -1;

        protected AdventureLesson(GameManager game, BoardController board)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _player = game.PlayerA;
            _soldier = game.PlayerB;
        }

        public virtual string BattleBackgroundKey => "Backgrounds/border_outpost_chapter_01";
        public abstract int LevelNumber { get; }
        public abstract string DisplayTitle { get; }
        public abstract string OpeningText { get; }
        public abstract string SummaryText { get; }
        public string CompletionKey => "FingerDiceDuel.Adventure.Level" + LevelNumber + ".Completed";
        protected abstract ScriptedMoveSelector CreateOpeningScript();
        protected abstract int OpeningMoveCount { get; }

        /// <summary>士兵的剧本 + 收官保护出招器（供调用方绑给士兵当 AI）。</summary>
        public IMoveSelector SoldierSelector => new SoldierMoves(this);

        /// <summary>把士兵的脚本 AI 绑到控制器（BindPlayer 会覆盖既有绑定）。</summary>
        public void Bind(BoardController board)
        {
            if (board == null) return;
            board.BindPlayer(_soldier, SoldierSelector, new SimpleChoiceProvider());
            board.ScriptedRestAllowed = actor => ReferenceEquals(actor, _soldier)
                && IsFreeStage && ChooseGuardedMove(_game, _soldier).Actor == null;
        }

        public GameManager Game => _game;
        public BoardController Board => _board;
        public PlayerState Player => _player;
        public PlayerState Soldier => _soldier;

        /// <summary>一拍教学：旁白 + 引导点击范围 + 完成条件。</summary>
        public sealed class Beat
        {
            /// <summary>引导面板显示的旁白文案。</summary>
            public string Narration;

            /// <summary>引导期允许点的"自己的运算源"（选运算源子阶段只许点它）。</summary>
            public Hand ActedHand;

            /// <summary>引导期允许点的"加数手"（选加数子阶段只许点它）。</summary>
            public Hand AddendHand;

            /// <summary>true = 半自由阶段（提示但不限制点击）。</summary>
            public bool FreeClicks;

            /// <summary>完成条件：Tick 里轮询，满足即切下一拍。</summary>
            public Func<AdventureLesson, bool> Complete;
        }

        // ============================================================
        // 导演推进（界面的刷新循环或无头测试调用）
        // ============================================================

        /// <summary>关卡是否已按剧本完成（胜利且玩家是赢家）。</summary>
        public bool IsComplete => _game.IsGameOver
                                  && _game.Winner != null
                                  && ReferenceEquals(_game.Winner, _player);

        /// <summary>当前拍（null = 未开始或已通关）。</summary>
        public Beat CurrentBeat
            => _beatIndex >= 0 && _beatIndex < _beats.Count ? _beats[_beatIndex] : null;

        /// <summary>当前引导文案（无拍时为 null）。</summary>
        public string Narration
        {
            get
            {
                if (!IsFreeStage) return CurrentBeat?.Narration;
                LegalMove move = RecommendedAttack();
                if (move.Actor == null)
                    return $"自由练习：{_soldier.Name}已停止进攻，不会回血或合盾。尝试改变手值，凑出4/6/8攻击；没有安全运算时，对手会休整。";
                string side = ReferenceEquals(move.ActedHand, _player.LeftHand) ? "左" : "右";
                string addend = ReferenceEquals(move.TargetHand, _soldier.LeftHand) ? "左" : "右";
                int result = Hand.ComputeSum(move.ActedHand.Value, move.TargetHand.Value);
                return $"自由练习：选择任意合法操作。建议你的{side}手{move.ActedHand.Value}＋{_soldier.Name}{addend}手{move.TargetHand.Value}＝{result}，再点对手确认攻击。对手不再进攻，可放心尝试。";
            }
        }

        public int StepNumber => CurrentBeat == null ? 0 : _beatIndex + 1;
        public int StepCount => _beats.Count;
        public bool IsFreeStage => CurrentBeat != null && CurrentBeat.FreeClicks;

        /// <summary>仅由已通关后的界面入口调用；新开对局不回放对手的引导动作。</summary>
        public void SkipTutorial() => _beatIndex = _beats.Count - 1;

        private LegalMove RecommendedAttack()
        {
            LegalMove best = default;
            int largest = -1;
            foreach (var move in _game.GetLegalMoves(_player))
            {
                int value = Hand.ComputeSum(move.ActedHand.Value, move.TargetHand.Value);
                if ((value == 4 || value == 6 || value == 8) && value > largest)
                { best = move; largest = value; }
            }
            return best;
        }

        /// <summary>开始教学（开场剧情确认后调用；重复调用无副作用）。</summary>
        public void BeginTutorial()
        {
            if (_beatIndex >= 0) return;
            _beatIndex = 0;
        }

        /// <summary>教学是否已开始（否则仍在开场剧情）。</summary>
        public bool Started => _beatIndex >= 0;

        /// <summary>
        /// 推进导演：当前拍的完成条件满足则切下一拍。幂等，可每帧调用。
        /// </summary>
        public void Tick()
        {
            var beat = CurrentBeat;
            if (beat?.Complete == null || !beat.Complete(this)) return;
            _beatIndex++;
        }

        // ============================================================
        // 点击守门（只约束人类点击；士兵走 AI 泵不经此处）
        // ============================================================

        /// <summary>教程白名单：这张手当前允许被【玩家】点击吗？</summary>
        public bool AllowClick(Hand hand)
        {
            var beat = CurrentBeat;
            if (beat == null || beat.FreeClicks) return true;

            switch (_board.Phase)
            {
                case BoardPhase.ChoosingAttackerHand:
                    return ReferenceEquals(hand, beat.ActedHand);

                case BoardPhase.ChoosingTargetHand:
                    return ReferenceEquals(hand, beat.AddendHand);

                case BoardPhase.ChoosingTargetPlayer:
                    return _soldier.Owns(hand);     // 确认攻击目标：点士兵任意一只手

                default:
                    return true;                    // 防守等非引导阶段不限制（本关不会出现）
            }
        }

        /// <summary>当前引导该点的手（供界面高亮/箭头；无引导或自由拍为 null）。</summary>
        public Hand SuggestedHand
        {
            get
            {
                var beat = CurrentBeat;
                if (beat == null || !_board.IsHumanTurn) return null;
                if (beat.FreeClicks)
                {
                    var move = RecommendedAttack();
                    if (move.Actor == null) return null;
                    if (_board.Phase == BoardPhase.ChoosingAttackerHand) return move.ActedHand;
                    if (_board.Phase == BoardPhase.ChoosingTargetPlayer) return _soldier.LeftHand;
                    return null;
                }
                if (_board.Phase == BoardPhase.ChoosingAttackerHand) return beat.ActedHand;
                if (_board.Phase == BoardPhase.ChoosingTargetHand) return beat.AddendHand;
                if (_board.Phase == BoardPhase.ChoosingTargetPlayer) return _soldier.LeftHand;
                return null;
            }
        }

        /// <summary>拒绝点击时的提示文案（界面弹气泡用）。</summary>
        public string RejectionHint
        {
            get
            {
                var beat = CurrentBeat;
                if (beat == null || beat.FreeClicks) return null;
                if (_board.Phase == BoardPhase.ChoosingAttackerHand)
                    return beat.ActedHand == null ? "请观察对手的行动，下一步再由你操作。" : "先点指引的那只手——你的" +
                           (ReferenceEquals(beat.ActedHand, _player.LeftHand) ? "左手" : "右手") +
                           $"（{beat.ActedHand.Value}）。";
                if (_board.Phase == BoardPhase.ChoosingTargetHand)
                    return beat.AddendHand == null ? "请观察对手的行动，下一步再由你操作。" : "再点对手的" +
                           (ReferenceEquals(beat.AddendHand, _soldier.LeftHand) ? "左手" : "右手") +
                           $"（{beat.AddendHand.Value}）。";
                if (_board.Phase == BoardPhase.ChoosingTargetPlayer)
                    return "点对手的手牌，确认攻击目标。";
                return "跟着指引走，先完成这一步。";
            }
        }

        // ============================================================
        // 士兵脚本与收官保护
        // ============================================================

        private sealed class SoldierMoves : IMoveSelector
        {
            private readonly AdventureLesson _level;
            private readonly ScriptedMoveSelector _opening;
            private int _openingMoves;
            public SoldierMoves(AdventureLesson level) { _level = level; _opening = level.CreateOpeningScript(); }
            public string Describe => "边境士兵教学剧本";
            public LegalMove ChooseMove(GameManager game, PlayerState actor)
            {
                if (!_level.IsFreeStage && _openingMoves < _level.OpeningMoveCount)
                { _openingMoves++; return _opening.ChooseMove(game, actor); }
                return _level.ChooseGuardedMove(game, actor);
            }
        }

        /// <summary>
        /// 收官保护：士兵只进行无效果运算，尽量留出玩家可攻击的加数；
        /// 没有安全运算时由控制器休整，不攻击、不回血、不合盾。
        /// </summary>
        private LegalMove ChooseGuardedMove(GameManager game, PlayerState soldier)
        {
            IReadOnlyList<LegalMove> moves = game.GetLegalMoves(soldier);
            if (moves.Count == 0) return default;

            LegalMove best = default;
            int bestRank = int.MinValue;

            for (int i = 0; i < moves.Count; i++)
            {
                LegalMove move = moves[i];
                int result = Hand.ComputeSum(move.ActedHand.Value, move.TargetHand.Value);

                // 教学关的士兵不碰回血与合盾
                if (result != 1 && result != 2 && result != 3 && result != 7) continue;

                int rank = RankGuardedMove(soldier, move, result);
                if (rank > bestRank) { bestRank = rank; best = move; }
            }

            return best;
        }

        /// <summary>
        /// 收官步打分：玩家"下一回合有攻击可达"优先于一切（+1000）；
        /// 所有候选已限定为无效果步（1/2/3/7）；不能承诺每个局面都能凑攻击。
        /// </summary>
        private int RankGuardedMove(PlayerState soldier, LegalMove move, int result)
        {
            // 演算士兵这只手变成 result 之后，自己两只手的取值
            bool actsLeft = ReferenceEquals(move.ActedHand, soldier.LeftHand);
            int soldierLeft = actsLeft ? result : soldier.LeftHand.Value;
            int soldierRight = actsLeft ? soldier.RightHand.Value : result;

            int rank = 0;
            if (result == 1 || result == 2 || result == 3 || result == 7) rank += 100;
            else if (result == 4) rank += 40;
            else if (result == 6) rank += 20;

            if (PlayerHasAttack(_player, soldierLeft, soldierRight)) rank += 1000;
            else rank -= 500;

            return rank;
        }

        /// <summary>给定士兵双手值，玩家能否用手牌 × 士兵手牌凑出 4/6/8。</summary>
        private static bool PlayerHasAttack(PlayerState player, int soldierLeft, int soldierRight)
        {
            int[] hands = { player.LeftHand.Value, player.RightHand.Value };
            int[] addends = { soldierLeft, soldierRight };
            for (int i = 0; i < hands.Length; i++)
                for (int j = 0; j < addends.Length; j++)
                {
                    int result = Hand.ComputeSum(hands[i], addends[j]);
                    if (result == 4 || result == 6 || result == 8) return true;
                }
            return false;
        }
    }
}
