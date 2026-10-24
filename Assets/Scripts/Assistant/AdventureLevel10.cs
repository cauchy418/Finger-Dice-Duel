using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed class AdventureLevel10 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·终战";
        private AdventureLevel10(GameManager game, BoardController board) : base(game, board)
        { _beats.Add(new Beat { Narration = "骑士试炼：自由运用所选角色的技能。\n对手20生命，攻+1／防+1，距离1。\n经典模式正常攻防，无安全保护；击败骑士即可通关。", FreeClicks = true, Complete = l => l.IsComplete }); }
        public static AdventureLevel10 Attach(GameManager game, BoardController board) => new AdventureLevel10(game, board);
        public override int LevelNumber => 10;
        public override string DisplayTitle => Title;
        public override bool UsesClassicSetup => true;
        public override bool AllowsTutorialSkip => false;
        public override string ResultHeading => "第一章通关";
        public override string Narration => CurrentBeat?.Narration;
        public override string OpeningText => "哨站骑士举盾致意。\n「选好你的道路，就用真正的战斗证明它。」\n"
            + "本场子职：" + (_player.Definition?.DisplayName ?? _player.Name) + "。\n"
            + "对手：哨站骑士，生命20。\n骑士攻击+1、防御+1、距离1，无额外技能。\n"
            + "投掷先攻后，按经典规则自由对战。\n获胜即第一章通关。";
        public override string SummaryText => "第一章：边境哨站 · 通关\n你已击败骑士，完成指尖骰术的入门试炼。\n可以更换角色再次挑战，或进入竞技模式实战。\n经典先攻、距离、盾牌、状态和角色技能均按普通规则结算。";
        public override string FailureText => "本场未击败骑士，未记为通关。\n可以同角色重新投骰；返回章节再进入终战可更换角色。\n留意骑士防+1，先保证生存，再用角色技能寻找突破。";
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
