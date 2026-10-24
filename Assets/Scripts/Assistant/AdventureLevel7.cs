using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed class AdventureLevel7 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·七战";
        private AdventureLevel7(GameManager game, BoardController board) : base(game, board)
        {
            Add("己左1＋教官右3＝4，另一手4。\n在招式面板选【流血】：攻防−1，自己回合开始失去1体力。", _player.LeftHand, _soldier.RightHand, StatusEffect.Bleed, l => l.Soldier.HasStatus(StatusEffect.Bleed));
            _beats.Add(new Beat { Narration = "教官回合开始，流血扣1体力；攻防栏显示−1。\n教官右3＋己左4＝7，无效果。", Complete = l => l.Soldier.RightHand.Value == 7 });
            Add("己左4＋教官左2＝6，另一手4。\n选择【跛脚】：教官射程2减为1，够不到距离2的你。\n此击仍受流血防−1影响。", _player.LeftHand, _soldier.LeftHand, StatusEffect.Limp, l => l.Soldier.HasStatus(StatusEffect.Limp));
            _beats.Add(new Beat { Narration = "观察教官射程2→1，脚边的石头仍占位。\n跛脚不禁止运算：教官右7＋己右4＝11→1。", Complete = l => l.Soldier.RightHand.Value == 1 });
            Add("己右4＋教官左2＝6，此时另一手6。\n选择【震慑】：不能攻击，仍能运算。\n不要与不能行动的冻结混淆。", _player.RightHand, _soldier.LeftHand, StatusEffect.Stunned, l => l.Soldier.HasStatus(StatusEffect.Stunned));
            _beats.Add(new Beat { Narration = "教官左2＋己左6＝8，但震慑使其不能攻击。\n无合法攻击目标，放弃攻击；左手保留8。", Complete = l => l.Soldier.LeftHand.Value == 8 && ReferenceEquals(l.Board.ActivePlayer, l.Player) });
            Add("己左6＋教官左8＝14→4，另一手6。\n选择【倒伏】：受到伤害×1.5。\n招式在造成伤害后附加，不放大施加它的这一击。", _player.LeftHand, _soldier.LeftHand, StatusEffect.KnockedDown, l => l.Soldier.HasStatus(StatusEffect.KnockedDown));
            _beats.Add(new Beat { Narration = "教官休整，倒伏仍在。\n下一击验证易伤，不再附加其它招式。", Complete = l => ReferenceEquals(l.Board.ActivePlayer, l.Player) });
            Add("己右6＋教官左8＝14→4，另一手4。\n本次选择【不附加】，只观察倒伏：4×1.5＝6伤。\n完成四种状态演示即可结束训练。", _player.RightHand, _soldier.LeftHand, StatusEffect.None, l => l.IsComplete);
        }
        private void Add(string text, Hand acted, Hand addend, StatusEffect maneuver, System.Func<AdventureLesson, bool> complete)
            => _beats.Add(new Beat { Narration = text, ActedHand = acted, AddendHand = addend, Maneuver = maneuver, Complete = complete });
        public static AdventureLevel7 Attach(GameManager game, BoardController board) => new AdventureLevel7(game, board);
        public override int LevelNumber => 7;
        public override string DisplayTitle => Title;
        public override bool AllowsTutorialSkip => false;
        public override string ResultHeading => "训练完成";
        public override bool IsComplete => (_board.LastTurn?.Actor == _player && _game.TurnCount >= 9 && _board.LastTurn.DamageDealt == 6) || base.IsComplete;
        public override string Narration => CurrentBeat?.Narration;
        public override string OpeningText => "老兵换上训练护具。\n「战斗大师看另一只手决定招式。」\n教官40生命、攻防0/0、训练射程2；双方距离2。\n本课依次选择流血、跛脚、震慑、倒伏，并验证状态效果。";
        public override string SummaryText => "另一手4或8：可附流血／跛脚。\n另一手6：可附震慑／倒伏。\n流血：攻防−1，回合开始失去1体力。\n跛脚：射程减半，至少1。震慑：不能攻击。\n倒伏：受到伤害×1.5，向上取整。\n招式仅在实际造成伤害后附加，持续目标自己的2回合。";
        protected override int OpeningMoveCount => 3;
        protected override ScriptedMoveSelector CreateOpeningScript() => new ScriptedMoveSelector().Add(1, 0).Add(1, 1).Add(0, 0);
        public override void Bind(BoardController board)
        {
            base.Bind(board);
            board.ScriptedRestAllowed = p => ReferenceEquals(p, _soldier) && _soldier.LeftHand.Value == 8;
        }
        public static void Prepare(GameManager game)
        {
            game.PlayerA.LeftHand.SetValue(1); game.PlayerA.RightHand.SetValue(4);
            game.PlayerB.Rename(AdventureChapter.InstructorName); game.PlayerB.SetMaxHealth(40); game.PlayerB.SetHealthExact(40);
            game.PlayerB.BindCharacter(new Instructor());
            game.PlayerB.LeftHand.SetValue(2); game.PlayerB.RightHand.SetValue(3);
            game.ConfigureSeatObstacles(1, 1);
        }
        private sealed class Instructor : CharacterTemplate
        {
            public override string DisplayName => AdventureChapter.InstructorName;
            public override int BaseAttackRange => 2;
        }
    }
}
