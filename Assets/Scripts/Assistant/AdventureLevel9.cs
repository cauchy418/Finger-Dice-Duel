using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed class AdventureLevel9 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·九战";
        private AdventureLevel9(GameManager game, BoardController board) : base(game, board)
        {
            _beats.Add(new Beat { Narration = "选择加法，一加三得四，攻击教官。", ActedHand = _player.LeftHand, AddendHand = _soldier.LeftHand, Complete = l => l.Soldier.Health == 12 });
            _beats.Add(new Beat { Narration = "教官算出七，留意他的右手六。", Complete = l => l.Soldier.LeftHand.Value == 7 });
            _beats.Add(new Beat { Narration = "选择减法，四减六是负二，记作八。", ActedHand = _player.RightHand, AddendHand = _soldier.RightHand, Complete = l => l.Soldier.Health == 4 });
            _beats.Add(new Beat { Narration = "教官算出一，右手仍是六。", Complete = l => l.Soldier.LeftHand.Value == 1 });
            _beats.Add(new Beat { Narration = "选择加法，八加六记作四，攻击教官。", ActedHand = _player.RightHand, AddendHand = _soldier.RightHand, Complete = l => l.IsComplete });
        }
        public static AdventureLevel9 Attach(GameManager game, BoardController board) => new AdventureLevel9(game, board);
        public override int LevelNumber => 9;
        public override string DisplayTitle => Title;
        public override bool AllowsTutorialSkip => false;
        public override string Narration => CurrentBeat?.Narration;
        public override string OpeningText => "老兵摊开战术图。\n「借来同一个数字，可以相加，也可以相减。」";
        public override string SummaryText => "攻防为0／0。运算前可选择加法或减法。减法用自己的数字减去借来的数字，负数按十回绕，差为零记作一。";
        protected override int OpeningMoveCount => 2;
        protected override ScriptedMoveSelector CreateOpeningScript() => new ScriptedMoveSelector().Add(0, 0).Add(0, 0);
        public static void Prepare(GameManager game)
        {
            game.PlayerA.LeftHand.SetValue(1); game.PlayerA.RightHand.SetValue(4);
            game.PlayerB.Rename(AdventureChapter.InstructorName); game.PlayerB.LeftHand.SetValue(3); game.PlayerB.RightHand.SetValue(6);
            game.PlayerB.SetMaxHealth(16);
        }
    }
}
