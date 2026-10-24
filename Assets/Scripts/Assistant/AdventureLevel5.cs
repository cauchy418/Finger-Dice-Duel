using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>五战：真实经典先攻；自由对战与正常胜负，不绑定安全剧本。</summary>
    public sealed class AdventureLevel5 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·五战";
        public const string InitiativeHint = "小点先分配 · 大点先行动 · 同点同时分配（玩家先行动）";
        private AdventureLevel5(GameManager game, BoardController board) : base(game, board)
        {
            game.PlayerB.Rename(AdventureChapter.InstructorName);
            _beats.Add(new Beat { Narration = "经典实战：先攻由骰点决定，5起手是碎盾，9不会立即回复。\n教官正常进攻；留意血量、盾牌和流血。自由选择合法操作，击败教官即可通关。", FreeClicks = true, Complete = l => l.IsComplete });
        }
        public static AdventureLevel5 Attach(GameManager game, BoardController board) => new AdventureLevel5(game, board);
        public override int LevelNumber => 5;
        public override string DisplayTitle => Title;
        public override bool UsesClassicSetup => true;
        public override bool AllowsTutorialSkip => false;
        public override string OpeningText => "老兵收起指引，开始正式校验。\n双方各投1d20：小点先分配手值，大点先行动；同点同时分配，玩家先行动。\n2～18按骰点总和拆分；1由对手指定，19至少一手9，20自由分配。\n本关自由对战，教官正常进攻，你也可能失败。";
        public override string SummaryText => "本课要点\n分配先后和行动先后不同：小点先分配，大点先行动。\n1由对手指定；19至少一手9；20自由分配。\n起手只设置数字：5不是完整盾，9不立即回复。\n经典实战正常结算攻防、技能与状态。\n接下来学习魔射手、战斗大师、剑士与战术家，最后自由选角挑战骑士。";
        public override string FailureText => "经典训练按真实胜负结算，教官会正常进攻。\n留意先攻顺序，先合盾稳住血量，再寻找主动碎盾的机会。\n点击“重新投骰”开始一场全新对局；本次未记为通关。";
        public override string Narration => CurrentBeat?.Narration;
        protected override int OpeningMoveCount => 0;
        protected override ScriptedMoveSelector CreateOpeningScript() => new ScriptedMoveSelector();
        public override void Bind(BoardController board)
        {
            board.BindPlayer(_soldier, new GreedyChoiceProvider { UseLearnedFocus = true },
                _game.ChoiceProviderB as IPlayerChoiceSink ?? new SimpleChoiceProvider());
            board.ScriptedRestAllowed = null;
        }
    }
}
