using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// **冒险模式 · 剧情模式 · 第一关「初战 · 边境哨站」**（用户已确认，2026-10-24）。
    ///
    /// 定位：一场 7 回合、**绝对不可失败**的剧本化新手教学。
    /// 玩家 = 战士·勇士（攻+1/防0/生命20）；对手 = 边境士兵（白板，攻0/防0，
    /// **生命 12** —— 剧情口径"身负旧伤的残兵"）。
    ///
    /// 教学序列（逐回合数值已演算核对，见 Docs/Adventure/level-01.md）：
    ///   1. 攻击（1+3=4 → 5 伤，士兵 12→7）；2. 士兵还手 4 伤（玩家 20→16）；
    ///   3. 合盾（1+4=5）；4. 士兵攻击（1+5=6）被盾挡下、盾碎；
    ///   5. 回血（5+4=9，16→20 上限截断）；
    ///   6. 满十归一（士兵 6+4=10 → 1，无效果）；
    ///   7. 半自由决胜（提示 4+4=8，放开全部点击；士兵进入收官保护）。
    ///
    /// 架构说明：
    ///   · 本类只是**导演**：挂在调用方建好的 <see cref="GameManager"/> 与
    ///     <see cref="BoardController"/> 上，不建对局、不碰 Core。
    ///   · **点击守门在界面层**（调用方把 <see cref="AllowClick"/> 接进
    ///     OnHandClicked / SetInteractable）。不能放进 BoardController：
    ///     AI 的 MakeAutomaticMove 复用 ClickHand 路径，控制器内守门
    ///     会把士兵的脚本出招也挡死。
    ///   · 士兵 = <see cref="ScriptedMoveSelector"/>（剧本步 + 收官保护），
    ///     由调用方经 <see cref="Bind"/> 绑到控制器。
    /// </summary>
    public sealed class AdventureLevel1
    {
        public const string Title = "初战 · 边境哨站";
        public const string GuideAvatarKey = "Adventure/guide_soldier";
        public const string GuideName = "老兵";

        /// <summary>士兵（白板）的生命上限：剧情口径"身负旧伤"。</summary>
        public const int SoldierMaxHealth = 12;

        /// <summary>开场剧情文案（点「跟随指引」后进入拍 1）。</summary>
        public const string IntroText =
            "边境哨站。一名身负旧伤的士兵拦住了去路。\n" +
            "「新兵，先学会指尖骰术，再谈闯荡。」\n" +
            "—— 两只手指相加，个位定乾坤：4/6/8 攻击、5 合盾、9 回血。";

        /// <summary>胜利要点卡文案。</summary>
        public const string VictoryText =
            "本课要点\n" +
            "4/6/8：攻击，基础伤害等于点数。\n" +
            "5：合盾，抵挡一段攻击后破碎。\n" +
            "9：回复9点生命，不超过上限。\n" +
            "1/2/3/7：无基础效果；满十归一，10变1。\n\n" +
            "下一课：勇士「十点重击」。\n" +
            "运算后双手和10，发起攻击的基础伤害×1.5。";

        private readonly GameManager _game;
        private readonly BoardController _board;
        private readonly PlayerState _player;
        private readonly PlayerState _soldier;
        private readonly List<Beat> _beats = new List<Beat>();

        private int _beatIndex = -1;

        private AdventureLevel1(GameManager game, BoardController board)
        {
            _game = game;
            _board = board;
            _player = game.PlayerA;
            _soldier = game.PlayerB;
            BuildBeats();
        }

        // ============================================================
        // 建关
        // ============================================================

        /// <summary>
        /// 把关卡导演挂到**已建好**的对局上（玩家 = PlayerA = 勇士，士兵 = PlayerB）。
        /// 调用方随后：①<see cref="PrepareSoldier"/>（或自行定制）；②<see cref="Bind"/> 绑士兵 AI；
        /// ③按正常 1v1 流程推上屏并 BeginTurn。
        /// </summary>
        public static AdventureLevel1 Attach(GameManager game, BoardController board)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            return new AdventureLevel1(game, board);
        }

        /// <summary>
        /// 教学版士兵定制：改名"边境士兵"、起手 3/1、生命 12（剧情"旧伤"）。
        /// 必须在 BeginTurn 之前调用（SetMaxHealth 会把当前生命夹到新上限）。
        /// </summary>
        public static void PrepareSoldier(PlayerState soldier)
        {
            if (soldier == null) return;
            soldier.Rename("边境士兵");
            soldier.LeftHand.SetValue(3);
            soldier.RightHand.SetValue(1);
            soldier.SetMaxHealth(SoldierMaxHealth);
        }

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

        // ============================================================
        // 剧本：教学拍
        // ============================================================

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
            public Func<AdventureLevel1, bool> Complete;
        }

        private void BuildBeats()
        {
            // —— 拍 1：攻击教学（1+3=4 → 4+1攻=5 伤；士兵 12→7）——
            _beats.Add(new Beat
            {
                Narration = "先点自己的左手1，再点士兵左手3。\n" +
                            "1+3=4，发动攻击。\n" +
                            "4/6/8是攻击数字，基础伤害等于点数。\n" +
                            "勇士攻击+1，本次造成5伤。\n" +
                            "最后点士兵，确认攻击目标。",
                ActedHand = _player.LeftHand,
                AddendHand = _soldier.LeftHand,
                Complete = l => l._soldier.Health == SoldierMaxHealth - 5
                                && l._player.LeftHand.Value == 4,
            });

            // —— 拍 2：士兵还手（士兵 3+1=4 → 4 伤；玩家 20→16）——
            _beats.Add(new Beat
            {
                Narration = "士兵用同样的方式还手了——注意你的生命条，被攻击就会掉血。" +
                            "别慌，老兵这就教你防御。",
                Complete = l => l._player.Health == 16,
            });

            // —— 拍 3：合盾教学（1+4=5 → 盾）——
            _beats.Add(new Beat
            {
                Narration = "结果是【5】就是合盾！点你的右手（1），再点士兵的左手（4）——" +
                            "1+4=5，那只手变成一面盾，可完全挡下一次攻击。" +
                            "注意：合盾作用于自己，没有第三步。",
                ActedHand = _player.RightHand,
                AddendHand = _soldier.LeftHand,
                Complete = l => l._player.RightHand.State == ShieldState.Formed,
            });

            // —— 拍 4：盾挡教学（士兵 1+5=6 攻击 → 被盾挡下、盾碎）——
            _beats.Add(new Beat
            {
                Narration = "观察护盾如何挡下士兵这次攻击。\n" +
                            "抵挡一段后，护盾变为破碎；手上的数字5仍可用于下一次运算。",
                Complete = l => l._player.RightHand.State == ShieldState.Broken
                                && l._soldier.RightHand.Value == 6,
            });

            // —— 拍 5：回血教学（5+4=9 → 回 9；16→20 上限截断；玩家的回合）——
            _beats.Add(new Beat
            {
                Narration = "该你出招了：结果是【9】就是回复！点你的碎盾手（5），" +
                            "再点士兵的左手（4）——5+4=9，回复 9 点生命。" +
                            "注意：回复不能超过上限，超出的部分会浪费。",
                ActedHand = _player.RightHand,
                AddendHand = _soldier.LeftHand,
                Complete = l => l._player.Health == l._player.MaximumHealth,
            });

            // —— 拍 6：满十归一教学（士兵 6+4=10 → 1，无效果；士兵的回合）——
            _beats.Add(new Beat
            {
                Narration = "士兵慌了，打出 6+4=10——满十归一！个位变成【1】。" +
                            "1/2/3/7 都没有效果，只改变手值。凑数时要小心十。",
                Complete = l => l._soldier.RightHand.Value == 1,
            });

            // —— 拍 7：半自由决胜（提示 4+4=8；放开全部点击）——
            _beats.Add(new Beat
            {
                Narration = "士兵只剩 7 血——自由发挥，终结它！提示：左手 4 + 士兵左手 4 = 8。" +
                            "这一步由你决定。士兵停止进攻，你可以放心尝试。",
                FreeClicks = true,
                Complete = l => l.IsComplete,
            });
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
                    return "自由练习：士兵已停止进攻，不会回血或合盾。尝试改变手值，凑出4/6/8攻击；没有安全运算时，士兵会休整。";
                string side = ReferenceEquals(move.ActedHand, _player.LeftHand) ? "左" : "右";
                string addend = ReferenceEquals(move.TargetHand, _soldier.LeftHand) ? "左" : "右";
                int result = Hand.ComputeSum(move.ActedHand.Value, move.TargetHand.Value);
                return $"自由练习：选择任意合法操作。建议你的{side}手{move.ActedHand.Value}＋士兵{addend}手{move.TargetHand.Value}＝{result}，再点士兵确认攻击。士兵不再进攻，可放心尝试。";
            }
        }

        public int StepNumber => CurrentBeat == null ? 0 : _beatIndex + 1;
        public int StepCount => _beats.Count;
        public bool IsFreeStage => CurrentBeat != null && CurrentBeat.FreeClicks;

        /// <summary>仅由已通关后的界面入口调用；新开对局不回放前三次士兵进攻。</summary>
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
                    return beat.ActedHand == null ? "请观察士兵的行动，下一步再由你操作。" : "先点指引的那只手——你的" +
                           (ReferenceEquals(beat.ActedHand, _player.LeftHand) ? "左手" : "右手") +
                           $"（{beat.ActedHand.Value}）。";
                if (_board.Phase == BoardPhase.ChoosingTargetHand)
                    return beat.AddendHand == null ? "请观察士兵的行动，下一步再由你操作。" : "再点士兵的" +
                           (ReferenceEquals(beat.AddendHand, _soldier.LeftHand) ? "左手" : "右手") +
                           $"（{beat.AddendHand.Value}）。";
                if (_board.Phase == BoardPhase.ChoosingTargetPlayer)
                    return "点士兵的手牌，确认攻击目标。";
                return "跟着指引走，先完成这一步。";
            }
        }

        // ============================================================
        // 士兵脚本与收官保护
        // ============================================================

        private sealed class SoldierMoves : IMoveSelector
        {
            private readonly AdventureLevel1 _level;
            private readonly ScriptedMoveSelector _opening = new ScriptedMoveSelector()
                .Add(0, 1).Add(1, 1).Add(1, 0);
            private int _openingMoves;
            public SoldierMoves(AdventureLevel1 level) { _level = level; }
            public string Describe => "边境士兵教学剧本";
            public LegalMove ChooseMove(GameManager game, PlayerState actor)
            {
                if (!_level.IsFreeStage && _openingMoves < 3)
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
