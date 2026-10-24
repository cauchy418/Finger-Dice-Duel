using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>三战：连锁破盾、盾牌修复、主动碎盾流血。关卡演算见 level-03.md。</summary>
    public sealed class AdventureLevel3 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·三战";
        private AdventureLevel3(GameManager game, BoardController board) : base(game, board) { BuildBeats(); }
        public static AdventureLevel3 Attach(GameManager game, BoardController board) => new AdventureLevel3(game, board);
        public override int LevelNumber => 3;
        public override string DisplayTitle => Title;
        public override string OpeningText => "老兵递来一面盾。\n「破阵者既能守，也能用碎盾撕开敌阵。」\n本课学习连锁破盾、合盾修复与主动碎盾攻击。教官右手的完整盾，是训练前已经合成的。";
        public override string SummaryText => "本课要点\n自己的盾碎，可选择连带打碎场上一面完整盾。\n新合成5时，另一只碎盾5会被修复。\n主动用完整盾运算后攻击，命中附流血2回合。\n被动挨打碎盾、连锁破盾本身不附流血。\n流血：攻防−1，自己回合开始失去1点体力。\n下一课：牺牲另一面盾，换本次攻击距离∞。";
        protected override int OpeningMoveCount => 3;
        protected override ScriptedMoveSelector CreateOpeningScript() => new ScriptedMoveSelector().Add(0, 1).Add(1, 0).Add(0, 0);

        public static void Prepare(GameManager game)
        {
            game.PlayerA.LeftHand.SetValue(2); game.PlayerA.RightHand.SetValue(1);
            game.PlayerB.Rename(AdventureChapter.InstructorName); game.PlayerB.SetMaxHealth(18);
            game.PlayerB.LeftHand.SetValue(3); game.PlayerB.RightHand.SetValue(5);
            game.PlayerB.RightHand.SetShieldState(ShieldState.Formed);
        }

        private void BuildBeats()
        {
            _beats.Add(new Beat { Narration = "先准备盾：己左2＋教官左3＝5。\n点自己的左手，再点教官左手。\n教官右手是提前合好的完整盾。", ActedHand = _player.LeftHand, AddendHand = _soldier.LeftHand, Complete = l => l.Player.LeftHand.IsShield });
            var passive = new Beat { Narration = "教官攻击会被你的盾挡下。\n自己的盾碎时，发动【连锁破盾】，点教官的右盾。\n本次是被动破盾，不会附流血。", Complete = l => l.Player.LeftHand.State == ShieldState.Broken && l.Soldier.RightHand.State == ShieldState.Broken };
            passive.Choices[HandEffect.BreakerChainBreak] = _soldier.RightHand;
            _beats.Add(passive);
            _beats.Add(new Beat { Narration = "修复另一面盾：己右1＋教官左4＝5。\n右手新合成完整盾时，左手碎盾也会被修复。", ActedHand = _player.RightHand, AddendHand = _soldier.LeftHand, Complete = l => l.Player.LeftHand.IsShield && l.Player.RightHand.IsShield });
            _beats.Add(new Beat { Narration = "观察教官：右5＋己左5＝10→1，无效果。\n下一步用完整盾作运算源，主动出击。", Complete = l => l.Soldier.RightHand.Value == 1 });
            var active = new Beat { Narration = "己左完整盾5＋教官右1＝6。\n点击保留另一面盾，本次不连锁自己的盾。\n攻击教官造成7伤，并附流血2回合。\n用盾运算，会先主动碎盾。", ActedHand = _player.LeftHand, AddendHand = _soldier.RightHand, Complete = l => l.Player.LeftHand.Value == 6 && l.Soldier.HasStatus(StatusEffect.Bleed) };
            active.Choices[HandEffect.BreakerRangeBreak] = null;
            active.Choices[HandEffect.BreakerChainBreak] = null;
            _beats.Add(active);
            _beats.Add(new Beat { Narration = "教官回合开始，流血失去1点体力；攻防也各降低1。\n教官左4＋己左6＝10→1。\n流血来自刚才的主动碎盾攻击。", Complete = l => l.Soldier.LeftHand.Value == 1 && ReferenceEquals(l.Board.ActivePlayer, l.Player) });
            _beats.Add(new Beat { Narration = "自由练习合盾、修复或继续攻击。教官不再进攻，也不新增盾。", FreeClicks = true, Complete = l => l.IsComplete });
        }
    }
}
