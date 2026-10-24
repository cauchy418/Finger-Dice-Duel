using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>四战：石头固定占位；真实不可达攻击、放弃、合盾与换射程。</summary>
    public sealed class AdventureLevel4 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·四战";
        private AdventureLevel4(GameManager game, BoardController board) : base(game, board) { BuildBeats(); }
        public static AdventureLevel4 Attach(GameManager game, BoardController board) => new AdventureLevel4(game, board);
        public override int LevelNumber => 4;
        public override string DisplayTitle => Title;
        public override bool AllowsTutorialSkip => false;
        public override string ResultHeading => "训练完成";
        public override string OpeningText => "老兵在你左右各放下一块石头，教官站在对面。\n「石头不出招，却占着座位。借得到数字，不代表打得到人。」\n环上双方距离2，破阵者通常射程1。本课先体验超距，再用碎盾换射程命中教官。";
        public override string SummaryText => "本课要点\n距离按座位环最短步数计算，左右石头使双方距离为2。\n石头只占位，两手为−，不可选择或行动。\n可借远处角色的手值；攻击目标另行检查射程。\n没有合法目标时，放弃攻击，保留运算结果。\n碎掉另一只手的完整盾，使本次攻击距离∞，命中附流血2回合。\n基础射程仍为1；下一课用经典先攻自由实战。";
        public override bool IsComplete => (_board.LastTurn?.Actor == _player && _board.LastTurn.DamageDealt == 9
            && _player.LeftHand.Value == 8 && _player.RightHand.State == ShieldState.Broken
            && _soldier.HasStatus(StatusEffect.Bleed)) || base.IsComplete;
        protected override int OpeningMoveCount => 2;
        protected override ScriptedMoveSelector CreateOpeningScript() => new ScriptedMoveSelector().Add(0, 0).Add(0, 0);

        public static void Prepare(GameManager game)
        {
            game.PlayerA.LeftHand.SetValue(1); game.PlayerA.RightHand.SetValue(1);
            game.PlayerB.Rename(AdventureChapter.InstructorName); game.PlayerB.SetMaxHealth(18);
            game.PlayerB.LeftHand.SetValue(3); game.PlayerB.RightHand.SetValue(4);
            game.ConfigureSeatObstacles(1, 1);
        }

        private void BuildBeats()
        {
            _beats.Add(new Beat { Narration = "己左1＋教官左3＝4。\n双方距离2，通常射程1：能借数字，却不能攻击教官。\n尝试确认会被拒绝；点击【放弃攻击】继续，左手保留4。", ActedHand = _player.LeftHand, AddendHand = _soldier.LeftHand, Complete = l => l.Player.LeftHand.Value == 4 && ReferenceEquals(l.Board.ActivePlayer, l.Soldier) });
            _beats.Add(new Beat { Narration = "教官左3＋己左4＝7，无效果。\n石头不行动，只占据环上的位置。", Complete = l => l.Soldier.LeftHand.Value == 7 });
            _beats.Add(new Beat { Narration = "先准备射程资源：己右1＋教官右4＝5。\n这面完整盾既能防守，也能主动牺牲。", ActedHand = _player.RightHand, AddendHand = _soldier.RightHand, Complete = l => l.Player.RightHand.IsShield });
            _beats.Add(new Beat { Narration = "教官左7＋己左4＝11→1。\n现在你有左4、右盾5；距离仍为2。", Complete = l => l.Soldier.LeftHand.Value == 1 });
            var ranged = new Beat { Narration = "己左4＋教官右4＝8。\n点击【碎盾换射程】，牺牲右盾，使本次距离∞；再选教官确认。\n造成9伤，附流血2回合。通常射程仍为1。", ActedHand = _player.LeftHand, AddendHand = _soldier.RightHand, Complete = l => l.IsComplete };
            ranged.Choices[HandEffect.BreakerRangeBreak] = _player.RightHand;
            _beats.Add(ranged);
        }
    }
}
