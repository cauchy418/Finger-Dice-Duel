using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed class AdventureLevel6 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·六战";
        private AdventureLevel6(GameManager game, BoardController board) : base(game, board)
        {
            _beats.Add(new Beat { Narration = "魔射三式，先看8：己右3＋教官右盾5＝8。\n【奥术射击·穿透箭】造成8伤；盾仍完整。\n穿盾不等于打碎盾。", ActedHand = _player.RightHand, AddendHand = _soldier.RightHand, Complete = l => l.Soldier.Health == 12 });
            _beats.Add(new Beat { Narration = "教官用右盾5＋己右8＝13→3。\n盾手主动运算，失去盾牌；现在可以直击体力。", Complete = l => l.Soldier.RightHand.Value == 3 });
            _beats.Add(new Beat { Narration = "再看6：己右8＋教官左8＝16→6。\n【奥术射击·强力射击】6＋2＝8伤；教官12→4。", ActedHand = _player.RightHand, AddendHand = _soldier.LeftHand, Complete = l => l.Soldier.Health == 4 });
            _beats.Add(new Beat { Narration = "教官右3＋己左2＝5，重新合成完整盾。\n留意最后一式如何穿过这一面盾。", Complete = l => l.Soldier.RightHand.IsShield });
            _beats.Add(new Beat { Narration = "最后看4：己右6＋教官左8＝14→4。\n【奥术射击·魔能齐射】两段各4：第一段破盾，第二段造成4伤完成击杀。", ActedHand = _player.RightHand, AddendHand = _soldier.LeftHand, Complete = l => l.IsComplete });
        }
        public static AdventureLevel6 Attach(GameManager game, BoardController board) => new AdventureLevel6(game, board);
        public override int LevelNumber => 6;
        public override string DisplayTitle => Title;
        public override bool AllowsTutorialSkip => false;
        public override string OpeningText => "老兵拉开靶场的弓。\n「魔射手的4、6、8，各有用法。」\n本课依次练习穿盾、增伤、两段破盾击杀。教官20生命，右手提前合成完整盾。";
        public override string SummaryText => "魔射三式\n8：无视盾牌造成伤害，盾牌保留。\n6：伤害额外＋2。\n4：两段各以4为基础伤害，分别判盾与闪避。\n面对单盾，第一段破盾，第二段仍能命中。";
        public override string Narration => CurrentBeat?.Narration;
        protected override int OpeningMoveCount => 2;
        protected override ScriptedMoveSelector CreateOpeningScript() => new ScriptedMoveSelector().Add(1, 1).Add(1, 0);
        public static void Prepare(GameManager game)
        {
            game.PlayerA.LeftHand.SetValue(2); game.PlayerA.RightHand.SetValue(3);
            game.PlayerB.Rename(AdventureChapter.InstructorName); game.PlayerB.LeftHand.SetValue(8); game.PlayerB.RightHand.SetValue(5);
            game.PlayerB.RightHand.SetShieldState(ShieldState.Formed);
        }
    }
}
