using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class BreakerAttackOrderTests
    {
        private static BoardController Build(out GameManager game,bool range=false)
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            game=CharacterFactory.CreateMatch(CharacterDefinition.Breaker,CharacterDefinition.Blank,ca,cb);
            game.PlayerA.LeftHand.SetValue(range?3:5);game.PlayerA.RightHand.SetValue(range?5:1);
            game.PlayerA.GetHand(range?1:0).SetShieldState(ShieldState.Formed);
            game.PlayerB.LeftHand.SetValue(range?3:1);game.PlayerB.RightHand.SetValue(5);game.PlayerB.RightHand.SetShieldState(ShieldState.Formed);
            var board=new BoardController(game,ca,cb);board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
            return board;
        }
        private static void ConfirmIfNeeded(BoardController board)
        {if(board.Phase==BoardPhase.ChoosingTargetPlayer)Assert.True(board.ConfirmTargetPlayer());}
        [Test] public void Operating_a_complete_shield_breaks_enemy_shield_before_same_attack()
        {
            var board=Build(out var game);ConfirmIfNeeded(board);Assert.AreEqual(HandEffect.BreakerChainBreak,board.PendingEffect);
            Assert.AreEqual(20,game.PlayerB.Health);Assert.True(board.ClickHand(game.PlayerB.RightHand));
            Assert.AreEqual(1,game.TurnCount);Assert.AreEqual(7,board.LastTurn.DamageDealt);Assert.AreEqual(12,game.PlayerB.Health);Assert.False(board.LastTurn.WasBlocked);Assert.True(game.PlayerB.HasStatus(StatusEffect.Bleed));
        }
        [Test] public void Declining_source_shield_chain_allows_enemy_shield_to_block()
        {
            var board=Build(out var game);ConfirmIfNeeded(board);Assert.True(board.DeclinePendingChoice());
            Assert.AreEqual(1,game.TurnCount);Assert.AreEqual(0,board.LastTurn.DamageDealt);Assert.AreEqual(19,game.PlayerB.Health);Assert.True(board.LastTurn.WasBlocked);
        }
        [Test] public void Range_break_collects_chain_and_breaks_shield_before_same_attack()
        {
            var board=Build(out var game,true);Assert.AreEqual(HandEffect.BreakerRangeBreak,board.PendingEffect);Assert.True(board.AcceptPendingChoice());
            Assert.AreEqual(HandEffect.BreakerRangeChainBreak,board.PendingEffect);Assert.AreEqual(0,game.TurnCount);Assert.True(game.PlayerA.RightHand.IsShield);
            Assert.True(board.ClickHand(game.PlayerB.RightHand));ConfirmIfNeeded(board);
            Assert.AreEqual(1,game.TurnCount);Assert.AreEqual(7,board.LastTurn.DamageDealt);Assert.AreEqual(12,game.PlayerB.Health);Assert.False(board.LastTurn.WasBlocked);Assert.True(game.PlayerB.HasStatus(StatusEffect.Bleed));
            var breaker=(BreakerLogic)game.PlayerA.Character;Assert.AreEqual(1,breaker.RangeBreakCount);Assert.AreEqual(1,breaker.ChainBreakCount);
        }
        [Test] public void Range_break_can_decline_chain_and_still_gain_range()
        {
            var board=Build(out var game,true);board.AcceptPendingChoice();Assert.AreEqual(HandEffect.BreakerRangeChainBreak,board.PendingEffect);
            Assert.True(board.DeclinePendingChoice());ConfirmIfNeeded(board);Assert.AreEqual(1,game.TurnCount);Assert.AreEqual(0,board.LastTurn.DamageDealt);Assert.AreEqual(19,game.PlayerB.Health);Assert.True(board.LastTurn.WasBlocked);
            Assert.AreEqual(1,((BreakerLogic)game.PlayerA.Character).RangeBreakCount);Assert.AreEqual(0,((BreakerLogic)game.PlayerA.Character).ChainBreakCount);
        }
        [Test] public void Declining_range_break_does_not_ask_for_its_chain()
        {
            var board=Build(out var game,true);Assert.True(board.DeclinePendingChoice());ConfirmIfNeeded(board);
            Assert.AreEqual(1,game.TurnCount);Assert.True(game.PlayerA.RightHand.IsShield);Assert.AreEqual(0,((BreakerLogic)game.PlayerA.Character).RangeBreakCount);
        }
        [Test] public void Range_chain_ai_chooses_enemy_shield_and_never_allied_shield()
        {
            var players=new PlayerState[4];var providers=new IChoiceProvider[4];for(int i=0;i<4;i++){players[i]=CharacterFactory.CreatePlayer("P"+i,i==0?CharacterDefinition.Breaker:CharacterDefinition.Blank);providers[i]=new SimpleChoiceProvider();}
            var game=GameManager.Create2v2(players,providers);players[3].LeftHand.SetShieldState(ShieldState.Formed);players[1].RightHand.SetShieldState(ShieldState.Formed);
            var ai=new GreedyChoiceProvider{UseChainBreak=true,Game=game};var request=new ChoiceRequest(HandEffect.BreakerRangeChainBreak,"",new[]{players[3].LeftHand,players[1].RightHand},true,decider:players[0]);
            Assert.AreEqual(players[1].RightHand,ai.ChooseHandFor(request,players[0]));
        }
        [Test] public void Range_chain_hits_a_distant_shielded_enemy_in_four_and_eight_player_boards()
        {
            foreach(int count in new[]{4,8})
            {
                var ps=new PlayerState[count];var sinks=new SimpleChoiceProvider[count];var teams=new int[count];
                for(int i=0;i<count;i++){ps[i]=CharacterFactory.CreatePlayer("P"+i,i==0?CharacterDefinition.Breaker:CharacterDefinition.Blank);sinks[i]=new SimpleChoiceProvider();teams[i]=i%3;}
                var game=count==4?GameManager.Create2v2(ps,sinks):GameManager.CreateEight(ps,sinks,teams);var board=new BoardController(game,sinks[0],sinks[1]);
                for(int i=2;i<count;i++)board.BindPlayer(ps[i],null,sinks[i]);
                ps[0].LeftHand.SetValue(3);ps[0].RightHand.SetValue(5);ps[0].RightHand.SetShieldState(ShieldState.Formed);ps[1].LeftHand.SetValue(3);ps[2].RightHand.SetValue(5);ps[2].RightHand.SetShieldState(ShieldState.Formed);
                Assert.AreEqual(2,game.DistanceBetween(ps[0],ps[2]));board.BeginTurn();board.ClickHand(ps[0].LeftHand);board.ClickHand(ps[1].LeftHand);
                Assert.AreEqual(HandEffect.BreakerRangeBreak,board.PendingEffect);board.AcceptPendingChoice();Assert.AreEqual(HandEffect.BreakerRangeChainBreak,board.PendingEffect);board.ClickHand(ps[2].RightHand);
                Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);Assert.True(board.CanBeAttacked(ps[2]),"distant target legal "+count+" "+board.DebugPhaseInfo());Assert.True(board.ClickHand(ps[2].LeftHand));ConfirmIfNeeded(board);
                Assert.AreEqual(1,game.TurnCount);Assert.AreEqual(ps[2],board.LastTurn.Defender);Assert.AreEqual(7,board.LastTurn.DamageDealt);Assert.False(board.LastTurn.WasBlocked);
            }
        }
        [Test] public void Breaker_knight_dual_range_shield_can_enchant_the_same_attack()
        {
            var a=DualGeneralFactory.Create("A",CharacterDefinition.Breaker,CharacterDefinition.EldritchKnight);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            var game=new GameManager(a,b,ca,cb);var board=new BoardController(game,ca,cb);a.LeftHand.SetValue(3);a.RightHand.SetValue(5);a.RightHand.SetShieldState(ShieldState.Formed);b.LeftHand.SetValue(3);b.RightHand.SetValue(5);b.RightHand.SetShieldState(ShieldState.Formed);
            board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);Assert.AreEqual(HandEffect.BreakerRangeBreak,board.PendingEffect);board.AcceptPendingChoice();Assert.AreEqual(HandEffect.BreakerRangeChainBreak,board.PendingEffect);board.ClickHand(b.RightHand);ConfirmIfNeeded(board);
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);Assert.True(board.ChooseSkillOption((int)AttackDamageType.Fire));Assert.AreEqual(1,game.TurnCount);Assert.AreEqual(AttackDamageType.Fire,board.LastTurn.AttackType);Assert.False(board.LastTurn.WasBlocked);
        }
    }
}
