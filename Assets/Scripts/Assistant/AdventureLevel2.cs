using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>次战：普通攻击对照、精通重击、半自由练习。逐步演算见 Docs/Adventure/level-02.md。</summary>
    public sealed class AdventureLevel2 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·次战";
        public const int InstructorMaxHealth = 18;
        public const string IntroText =
            "初战过后，老兵请来哨站教官。\n" +
            "「用点数为1的手发起攻击，精通重击会自动发动。」\n" +
            "基础伤害先×1.5，再计算攻防，命中附物理易伤。";
        public const string VictoryText =
            "本课要点\n" +
            "精通重击：用运算前为1的手发起攻击。\n" +
            "先将基础伤害×1.5，再加攻击、减防御。\n" +
            "本课普通6：6＋1＝7伤；重击4：4×1.5＋1＝7伤。\n" +
            "技能自动发动，战报与提示显示「精通重击」。\n" +
            "物理易伤通常持续两个自身回合，期间受到的物理伤害×1.5。";

        private AdventureLevel2(GameManager game, BoardController board) : base(game, board)
        {
            BuildBeats();
        }

        public static AdventureLevel2 Attach(GameManager game, BoardController board)
            => new AdventureLevel2(game, board);
        public override int LevelNumber => 2;
        public override string DisplayTitle => Title;
        public override string OpeningText => IntroText;
        public override string SummaryText => VictoryText;
        protected override int OpeningMoveCount => 2;
        protected override ScriptedMoveSelector CreateOpeningScript()
            => new ScriptedMoveSelector().Add(0, 1).Add(1, 1);

        /// <summary>设置本关起手与教官数据，须在 BeginTurn 前调用。</summary>
        public static void Prepare(GameManager game)
        {
            game.PlayerA.LeftHand.SetValue(3);
            game.PlayerA.RightHand.SetValue(1);
            game.PlayerB.Rename(AdventureChapter.InstructorName);
            game.PlayerB.LeftHand.SetValue(3);
            game.PlayerB.RightHand.SetValue(3);
            game.PlayerB.SetMaxHealth(InstructorMaxHealth);
        }

        private void BuildBeats()
        {
            _beats.Add(new Beat
            {
                Narration = "用左手3借教官左手3，得到6。普通攻击造成7点伤害。",
                ActedHand = _player.LeftHand,
                AddendHand = _soldier.LeftHand,
                Complete = l => l.Soldier.Health == 11 && l.Player.LeftHand.Value == 6,
            });
            _beats.Add(new Beat
            {
                Narration = "教官用左手3借你的右手1算四，造成4点伤害。留意他右手的3。",
                Complete = l => l.Soldier.LeftHand.Value == 4,
            });
            _beats.Add(new Beat
            {
                Narration = "用右手1借教官右手3算四。精通重击造成4×1.5＋1＝7点伤害，附物理易伤。",
                ActedHand = _player.RightHand,
                AddendHand = _soldier.RightHand,
                Complete = l => l.Soldier.Health == 4 && l.Player.RightHand.Value == 4,
            });
            _beats.Add(new Beat
            {
                Narration = "教官用右手3借你的右手4算七。观察他的物理易伤还有一回合。",
                Complete = l => l.Soldier.RightHand.Value == 7,
            });
            _beats.Add(new Beat
            {
                Narration = "用右手4借教官左手4算八。物理易伤会提高这次挥砍的伤害。",
                FreeClicks = true,
                Complete = l => l.IsComplete,
            });
        }
    }
}
