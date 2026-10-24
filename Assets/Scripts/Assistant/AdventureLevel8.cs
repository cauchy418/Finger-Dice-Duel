using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed class AdventureLevel8 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·八战";
        private AdventureLevel8(GameManager game, BoardController board) : base(game, board)
        {
            var beat = new Beat { Narration = "己左2＋教官左2＝4，双手成为双4。\n点击【发动追加】：先固定4伤并附流血，随后主攻击4＋攻2＋流血易伤1＝7伤。\n战报会分别显示两次结算，共11伤击杀。", ActedHand = _player.LeftHand, AddendHand = _soldier.LeftHand, Complete = l => l.IsComplete };
            beat.Choices[HandEffect.SwordsmanFollowUp] = _player.RightHand; _beats.Add(beat);
        }
        public static AdventureLevel8 Attach(GameManager game, BoardController board) => new AdventureLevel8(game, board);
        public override int LevelNumber => 8;
        public override string DisplayTitle => Title;
        public override bool AllowsTutorialSkip => false;
        public override string Narration => CurrentBeat?.Narration;
        public override string OpeningText => "老兵并起双剑。\n「运算后双手同为4、6、8，剑士就能先追加一击。」\n本课先打无盾教官，分别观察追加与主攻击；教官11生命。";
        public override string SummaryText => "运算后双4／双6／双8，可选择追加。\n追加在主攻击之前结算，固定4／6／8伤，无视双方攻防。\n追加命中附流血2回合，使随后主攻击也受防−1影响。\n两击独立判盾，单盾只能挡其中一次。";
        protected override int OpeningMoveCount => 0;
        protected override ScriptedMoveSelector CreateOpeningScript() => new ScriptedMoveSelector();
        public static void Prepare(GameManager game)
        {
            game.PlayerA.LeftHand.SetValue(2); game.PlayerA.RightHand.SetValue(4);
            game.PlayerB.Rename(AdventureChapter.InstructorName); game.PlayerB.SetMaxHealth(11);
            game.PlayerB.LeftHand.SetValue(2); game.PlayerB.RightHand.SetValue(3);
        }
    }
}
