using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>次战：普通攻击对照、十点重击、半自由练习。逐步演算见 Docs/Adventure/level-02.md。</summary>
    public sealed class AdventureLevel2 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·次战";
        public const int InstructorMaxHealth = 18;
        public const string IntroText =
            "初战过后，老兵请来哨站教官。\n" +
            "「勇士不只会凑攻击数字，还要让两只手相互配合。」\n" +
            "本课学习十点重击：运算后，自身双手和为10时，攻击自动发动重击。\n" +
            "基础伤害先×1.5，再计算攻防。";
        public const string VictoryText =
            "本课要点\n" +
            "十点重击：发起攻击时，运算后的自身双手和为10。\n" +
            "先将基础伤害×1.5，再加攻击、减防御。\n" +
            "本课普通6：6＋1＝7伤；重击4：4×1.5＋1＝7伤。\n" +
            "技能自动发动，战报与提示显示「十点重击」。\n" +
            "双手和10但未攻击，不触发；重击仍遵守护盾与闪避规则。";

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
            => new ScriptedMoveSelector().Add(0, 0).Add(1, 0);

        /// <summary>仅设置本关起手与教官数据，起手5遵循既有碎盾口径，不生成完整盾；须在 BeginTurn 前调用。</summary>
        public static void Prepare(GameManager game)
        {
            game.PlayerA.LeftHand.SetValue(1);
            game.PlayerA.RightHand.SetValue(6);
            game.PlayerB.Rename(AdventureChapter.InstructorName);
            game.PlayerB.LeftHand.SetValue(5);
            game.PlayerB.RightHand.SetValue(8);
            game.PlayerB.SetMaxHealth(InstructorMaxHealth);
        }

        private void BuildBeats()
        {
            _beats.Add(new Beat
            {
                Narration = "先作对照：点你的左手1，再点教官左手5，得到6。\n" +
                            "你的双手变成6/6，和为12，不触发重击。\n" +
                            "普通攻击：6＋攻击1＝7伤。最后点教官确认目标。\n" +
                            "教官起手的5是普通手，没有盾。",
                ActedHand = _player.LeftHand,
                AddendHand = _soldier.LeftHand,
                Complete = l => l.Soldier.Health == 11 && l.Player.LeftHand.Value == 6,
            });
            _beats.Add(new Beat
            {
                Narration = "教官左手5＋你的左手6＝11，取个位变成1，无效果。\n" +
                            "留意教官右手8：下一步用它配出十点重击。",
                Complete = l => l.Soldier.LeftHand.Value == 1,
            });
            _beats.Add(new Beat
            {
                Narration = "点你的左手6，再点教官右手8：14取个位，得到4。\n" +
                            "你的双手变成4/6，和为10！\n" +
                            "点教官确认攻击，自动发动十点重击：4×1.5＋攻击1＝7伤。\n" +
                            "判定看自己的两只手，不是运算的两个加数。",
                ActedHand = _player.LeftHand,
                AddendHand = _soldier.RightHand,
                Complete = l => l.Soldier.Health == 4 && l.Player.LeftHand.Value == 4,
            });
            _beats.Add(new Beat
            {
                Narration = "刚才普通6与重击4都打出7伤，区别在于重击先乘基础伤害。\n" +
                            "教官右手8＋你的左手4＝12，变成2。接下来由你自由练习。",
                Complete = l => l.Soldier.RightHand.Value == 2,
            });
            _beats.Add(new Beat
            {
                Narration = "自由练习：教官停止进攻，尝试改变手值或凑10重击。\n" +
                            "也可直接用右手6＋教官右手2＝8结束本课。",
                FreeClicks = true,
                Complete = l => l.IsComplete,
            });
        }
    }
}
