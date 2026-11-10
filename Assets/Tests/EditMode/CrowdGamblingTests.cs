using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using System.Linq;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class CrowdGamblingTests
    {
        private sealed class Dice : System.Random
        {
            public int Calls;
            private readonly int _face;
            public Dice(int face) { _face=face; }
            public override int Next(int min,int max) { Calls++; return _face; }
        }
        private static BoardController Board(out GameManager game)
        {
            var a=new SimpleChoiceProvider();var b=new SimpleChoiceProvider();
            game=CharacterFactory.CreateMatch(CharacterDefinition.Gambler,CharacterDefinition.Blank,a,b);
            game.PlayerA.LeftHand.SetValue(2);game.PlayerB.LeftHand.SetValue(9);
            return new BoardController(game,a,b);
        }
        private static void RollOne(BoardController board,GameManager game)
        {
            board.GambleRng=new Dice(1);board.BeginTurn();
            Assert.True(board.ClickHand(game.PlayerA.LeftHand));Assert.True(board.Gamble());
            Assert.True(board.ChooseSkillOption(0));
        }
        [Test]
        public void One_offers_optional_living_player_choice_and_self()
        {
            var board=Board(out var game);RollOne(board,game);
            Assert.True(board.PendingWizardChoice!=null,"A final 1 must offer 聚众赌博");
            Assert.True(board.PendingWizardChoice.Prompt.Contains("聚众赌博"));
            Assert.True(board.PendingWizardChoice.TargetsPlayer);Assert.True(board.PendingWizardChoice.AllowDecline);
            Assert.True(board.PendingWizardChoice.Candidates.Contains(game.PlayerA.LeftHand));
            Assert.True(board.PendingWizardChoice.Candidates.Contains(game.PlayerB.LeftHand));
            Assert.True(board.ChooseWizardHand(null));
            Assert.AreEqual(1,game.TurnCount);
        }
        private static BoardController MarkOther(out GameManager game)
        {
            var board=Board(out game);RollOne(board,game);
            Assert.True(board.ChooseWizardHand(game.PlayerB.LeftHand));
            Assert.True(game.HasCrowdGambling(game.PlayerB));return board;
        }
        [Test]
        public void Non_gambler_uses_real_gambling_ui_once_without_evil_luck_or_cycle()
        {
            var board=MarkOther(out var game);var dice=new Dice(5);board.GambleRng=dice;
            board.BeginTurn();Assert.True(board.ClickHand(game.PlayerB.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingGamble,board.Phase);
            Assert.False(board.ClickHand(game.PlayerA.LeftHand));Assert.True(board.CanGamble);
            Assert.True(board.Gamble());Assert.AreEqual(1,dice.Calls);
            Assert.True(board.PendingWizardChoice==null);Assert.True(game.PlayerB.LeftHand.IsShield);
            Assert.True(board.LastTurn.WasGamble);Assert.False(game.PlayerB.MustGamble);
            Assert.False(game.HasCrowdGambling(game.PlayerB));
            game.ForceActivePlayerForTest(game.PlayerB);board.BeginTurn();
            Assert.True(board.ClickHand(game.PlayerB.RightHand));
            Assert.AreEqual(BoardPhase.ChoosingTargetHand,board.Phase);Assert.False(board.CanGamble);
        }
        [Test]
        public void Core_rejects_regular_main_operation_but_extra_operation_does_not_consume_mark()
        {
            MarkOther(out var game);var b=game.PlayerB;
            bool rejected=false;try{game.ExecuteTurn(b,b.LeftHand,game.PlayerA.LeftHand);}catch(System.InvalidOperationException){rejected=true;}
            Assert.True(rejected);Assert.AreEqual(1,game.TurnCount);
            game.ExecuteTurn(b,b.RightHand,game.PlayerA.LeftHand,extraAction:true);
            Assert.True(game.RequiresCrowdGambling(b));Assert.AreEqual(1,game.TurnCount);
            game.ExecuteGambleWithRoll(b,b.LeftHand,3);
            Assert.False(game.HasCrowdGambling(b));Assert.False(b.MustGamble);
        }
        [Test]
        public void Self_target_starts_next_turn_and_does_not_change_legacy_cycle()
        {
            var board=Board(out var game);RollOne(board,game);
            Assert.True(board.ChooseWizardHand(game.PlayerA.LeftHand));
            Assert.True(game.HasCrowdGambling(game.PlayerA));Assert.False(game.PlayerA.MustGamble);
            game.ExecuteTurn(game.PlayerB,game.PlayerB.RightHand,game.PlayerA.LeftHand);
            board.BeginTurn();Assert.True(board.ClickHand(game.PlayerA.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingGamble,board.Phase);
            board.GambleRng=new Dice(3);Assert.True(board.Gamble());Assert.True(board.ChooseSkillOption(0));
            Assert.False(game.HasCrowdGambling(game.PlayerA));Assert.False(game.PlayerA.MustGamble);
        }
        [Test]
        public void Cannot_act_turn_expires_mark_and_reset_clears_all_roles()
        {
            var board=MarkOther(out var game);game.PlayerB.ApplyStatus(StatusEffect.Frozen,1);
            board.BeginTurn();Assert.True(board.CanSkipTurn);Assert.True(board.SkipTurn());
            Assert.False(game.HasCrowdGambling(game.PlayerB));
            game.PlayerA.LeftHand.SetValue(1);WizardChoice choice=null;game.DeferWizardChoice=q=>{choice=q;return true;};
            game.OfferCrowdGambling(game.PlayerA,game.PlayerA.LeftHand);choice.Choose(game.PlayerB.LeftHand);
            Assert.True(game.HasCrowdGambling(game.PlayerB));game.ResetMatch();Assert.False(game.HasCrowdGambling(game.PlayerB));
        }
        [Test]
        public void Non_gambler_direct_gamble_does_not_offer_or_pay_reroll_and_keeps_own_role_effect()
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            var game=CharacterFactory.CreateMatch(CharacterDefinition.Gambler,CharacterDefinition.OathOfDevotion,ca,cb);
            game.PlayerA.LeftHand.SetValue(1);WizardChoice choice=null;game.DeferWizardChoice=q=>{choice=q;return true;};
            game.OfferCrowdGambling(game.PlayerA,game.PlayerA.LeftHand);choice.Choose(game.PlayerB.LeftHand);
            var die=new Dice(4);var result=game.ExecuteGamble(game.PlayerB,game.PlayerB.LeftHand,die);
            Assert.AreEqual(1,die.Calls);Assert.True(result.WasGamble);Assert.True(game.PlayerB.LeftHand.IsShield);
            Assert.False(game.HasCrowdGambling(game.PlayerB));
        }
        [Test]
        public void Normal_final_one_also_triggers_and_decline_has_no_side_effects()
        {
            var board=Board(out var game);board.BeginTurn();
            Assert.True(board.ClickHand(game.PlayerA.LeftHand));Assert.True(board.ClickHand(game.PlayerB.LeftHand));
            Assert.True(board.PendingWizardChoice?.Prompt.Contains("聚众赌博")==true);
            Assert.True(board.ChooseWizardHand(null));Assert.False(game.HasCrowdGambling(game.PlayerA));
            Assert.False(game.HasCrowdGambling(game.PlayerB));Assert.True(game.PlayerA.MustGamble);
        }
        [Test]
        public void Ai_uses_same_board_path_for_target_choice_and_forced_non_gambler_gamble()
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            var game=CharacterFactory.CreateMatch(CharacterDefinition.Gambler,CharacterDefinition.Blank,ca,cb);
            var board=new BoardController(game,ca,cb,new GreedyChoiceProvider(),new GreedyChoiceProvider());
            game.PlayerA.LeftHand.SetValue(2);game.PlayerA.RightHand.SetValue(2);
            game.PlayerB.LeftHand.SetValue(4);game.PlayerB.RightHand.SetValue(3);
            board.GambleRng=new Dice(1);board.BeginTurn();Assert.True(board.ClickHand(game.PlayerA.LeftHand));
            Assert.True(board.Gamble());Assert.True(board.PendingWizardChoice==null);
            Assert.True(game.HasCrowdGambling(game.PlayerB));
            board.GambleRng=new Dice(3);board.BeginTurn();Assert.True(board.MakeAutomaticMove());
            Assert.True(board.LastTurn.WasGamble);Assert.False(game.PlayerB.MustGamble);Assert.False(game.HasCrowdGambling(game.PlayerB));
        }
        [Test]
        public void Reapply_is_single_marker_with_targeted_visual_and_skip_expiry()
        {
            var board=MarkOther(out var game);game.RecordCombatVisuals=true;
            game.PlayerA.LeftHand.SetValue(1);WizardChoice choice=null;game.DeferWizardChoice=q=>{choice=q;return true;};
            game.OfferCrowdGambling(game.PlayerA,game.PlayerA.LeftHand);choice.Choose(game.PlayerB.LeftHand);
            var icons=CombatStatusIcons.For(game.PlayerB,game).Where(i=>i.Description.Contains("聚众赌博")).ToList();
            Assert.AreEqual(1,icons.Count);Assert.AreEqual("1",icons[0].Counter);
            var visual=game.DrainCombatVisuals().Single(e=>e.RedirectKind=="聚众赌博");
            Assert.AreEqual(game.PlayerB,visual.Target);
            game.GiveUpTurn();Assert.False(game.HasCrowdGambling(game.PlayerB));
        }
        [Test]
        public void Legacy_match_runner_honors_forced_gambling()
        {
            MarkOther(out var game);game.DeferWizardChoice=null;
            var result=new MatchRunner{MaxTurns=1,GambleRng=new Dice(3)}.Run(game,new GreedyChoiceProvider(),new GreedyChoiceProvider());
            Assert.AreEqual(1,result.History.Count);Assert.True(result.History[0].WasGamble);
            Assert.False(game.HasCrowdGambling(game.PlayerB));
        }
        [Test]
        public void Illegal_extra_gamble_is_rejected_before_rng_is_used()
        {
            MarkOther(out var game);var dice=new Dice(3);bool rejected=false;
            try{game.ExecuteGamble(game.PlayerB,game.PlayerB.LeftHand,dice,extraAction:true);}
            catch(System.InvalidOperationException){rejected=true;}
            Assert.True(rejected);Assert.AreEqual(0,dice.Calls);Assert.True(game.HasCrowdGambling(game.PlayerB));
        }
        [Test]
        public void Loyalist_ai_does_not_disrupt_known_lords_strong_operation()
        {
            var players=new System.Collections.Generic.List<PlayerState>();
            var policies=new System.Collections.Generic.List<IChoiceProvider>();
            for(int i=0;i<8;i++){players.Add(CharacterFactory.CreatePlayer("P"+i,i==1?CharacterDefinition.Gambler:CharacterDefinition.Blank));policies.Add(new SimpleChoiceProvider());}
            var roles=new[]{IdentityRole.Lord,IdentityRole.Loyalist,IdentityRole.Rebel,IdentityRole.Traitor,IdentityRole.Rebel,IdentityRole.Loyalist,IdentityRole.Rebel,IdentityRole.Rebel};
            var game=GameManager.CreateIdentityEight(players,policies,roles);
            players[1].LeftHand.SetValue(1);players[1].RightHand.SetValue(2);players[0].LeftHand.SetValue(4);
            Assert.True(game.IsKnownAlly(players[1],players[0]));
            Assert.Null(CrowdGamblingPolicy.Choose(players[1],new[]{players[0].LeftHand},game));
        }
        private static void ResumeForcedAiGamble(bool automaticEntry)
        {
            var board=MarkOther(out var game);
            board.BindPlayer(game.PlayerB,new GreedyChoiceProvider(),(SimpleChoiceProvider)game.GetChoiceProvider(game.PlayerB));
            board.GambleRng=new Dice(3);board.BeginTurn();Assert.True(board.ClickHand(game.PlayerB.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingGamble,board.Phase);Assert.False(board.IsHumanChooser);
            Assert.True(automaticEntry?board.MakeAutomaticMove():board.ResolvePendingAiChoice());
            Assert.True(board.LastTurn.WasGamble);Assert.False(game.HasCrowdGambling(game.PlayerB));
        }
        [Test] public void Pending_ai_choice_resumes_forced_gambling_phase(){ResumeForcedAiGamble(false);}
        [Test] public void Automatic_ai_move_resumes_forced_gambling_phase(){ResumeForcedAiGamble(true);}
    }
}
