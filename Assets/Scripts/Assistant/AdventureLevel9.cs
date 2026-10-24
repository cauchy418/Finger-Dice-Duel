using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed class AdventureLevel9 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·九战";
        private AdventureLevel9(GameManager game, BoardController board) : base(game, board)
        {
            _beats.Add(new Beat { Narration = "双方20生命，体力差0，额外攻防0。\n己左1＋教官右3＝4，按攻+1造成5伤。", ActedHand = _player.LeftHand, AddendHand = _soldier.RightHand, Complete = l => l.Soldier.Health == 15 });
            _beats.Add(new Beat { Narration = "教官剩15，体力差5；⌊5/3⌋＝1。\n你的攻防变为+2／0。\n教官右3＋己左4＝7，无效果。", Complete = l => l.Soldier.RightHand.Value == 7 });
            _beats.Add(new Beat { Narration = "带着额外1攻防再次攻击：\n己左4＋教官左2＝6，6＋攻2＝8伤。\n攻击前的体力差决定本击加成。", ActedHand = _player.LeftHand, AddendHand = _soldier.LeftHand, Complete = l => l.Soldier.Health == 7 });
            _beats.Add(new Beat { Narration = "教官剩7，差13；⌊13/3⌋＝4。\n你的攻防变为+5／+3。\n教官左2＋己右1＝3，无效果。", Complete = l => l.Soldier.LeftHand.Value == 3 });
            _beats.Add(new Beat { Narration = "己右1＋教官右7＝8，8＋攻5＝13伤，完成击杀。\n统计所有存活角色的最高与最低当前体力，额外攻防会动态更新。", ActedHand = _player.RightHand, AddendHand = _soldier.RightHand, Complete = l => l.IsComplete });
        }
        public static AdventureLevel9 Attach(GameManager game, BoardController board) => new AdventureLevel9(game, board);
        public override int LevelNumber => 9;
        public override string DisplayTitle => Title;
        public override bool AllowsTutorialSkip => false;
        public override string Narration => CurrentBeat?.Narration;
        public override string OpeningText => "老兵摊开战术图。\n「战术家利用场上体力差，同时提高攻防。」\n双方20生命；依次观察差0、差5、差13时的攻击。\n额外攻防＝⌊存活者最高与最低体力差/3⌋。";
        public override string SummaryText => "基础攻+1／防−1。\n额外攻防＝⌊场上存活角色最高与最低体力差/3⌋。\n差0→额外0；差5→额外1；差13→额外4。\n比较当前体力，死亡者不参与；攻击与回复后动态更新。\n下一战自由选角，接受骑士的经典模式试炼。";
        protected override int OpeningMoveCount => 2;
        protected override ScriptedMoveSelector CreateOpeningScript() => new ScriptedMoveSelector().Add(1, 0).Add(0, 1);
        public static void Prepare(GameManager game)
        {
            game.PlayerA.LeftHand.SetValue(1); game.PlayerA.RightHand.SetValue(1);
            game.PlayerB.Rename(AdventureChapter.InstructorName); game.PlayerB.LeftHand.SetValue(2); game.PlayerB.RightHand.SetValue(3);
        }
    }
}
