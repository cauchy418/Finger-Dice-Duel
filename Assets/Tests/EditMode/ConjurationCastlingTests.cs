using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class ConjurationCastlingTests
    {
        private static PlayerState Make(int i, CharacterDefinition d=null)
            => CharacterFactory.CreatePlayer("P"+i,d ?? CharacterDefinition.Blank);
        private static GameManager Match(int count,out List<PlayerState> p)
        {
            p=new List<PlayerState>();
            var providers=new List<IChoiceProvider>();
            for(int i=0;i<count;i++) {p.Add(Make(i,i==0?CharacterDefinition.SchoolOfConjuration:null));providers.Add(new SimpleChoiceProvider());}
            if(count==2) return new GameManager(p[0],p[1],providers[0],providers[1]);
            if(count==4) return GameManager.Create2v2(p,providers);
            return GameManager.CreateEight(p,providers,new[]{0,1,2,0,1,2,0,1});
        }
        private static BoardController Board(GameManager game)
        {
            var b=new BoardController(game,new SimpleChoiceProvider());
            b.BeginTurn();return b;
        }
        private static void Hands(PlayerState p,int l,int r){p.LeftHand.SetValue(l);p.RightHand.SetValue(r);}
        private static void IsTurn(GameManager g,PlayerState p,int round)
        {Assert.AreSame(p,g.CurrentPlayer);Assert.AreEqual(round,g.RoundCount);}

        [Test]
        public void Unordered_numbers_match_exactly_including_duplicates_and_zero()
        {
            var g=Match(2,out var p);var s=SeatSwapSkills.Of(p[0]);
            Hands(p[0],4,6);Hands(p[1],6,4);Assert.IsTrue(s.CanSwapSeats(p[0],p[1],g));
            Hands(p[1],3,7);Assert.IsFalse(s.CanSwapSeats(p[0],p[1],g),"Equal sum is insufficient.");
            Hands(p[0],4,4);Hands(p[1],4,6);Assert.IsFalse(s.CanSwapSeats(p[0],p[1],g));
            Hands(p[1],4,4);Assert.IsTrue(s.CanSwapSeats(p[0],p[1],g));
            Hands(p[0],0,6);Hands(p[1],6,0);Assert.IsTrue(s.CanSwapSeats(p[0],p[1],g));
        }

        [Test]
        public void Invalid_self_outsider_dead_owner_dead_target_and_wrong_turn_are_rejected()
        {
            var g=Match(4,out var p);var s=SeatSwapSkills.Of(p[0]);
            Assert.IsFalse(s.TrySwapSeats(p[0],p[0],g));
            Assert.IsFalse(s.TrySwapSeats(p[0],Make(9),g));
            Assert.IsFalse(s.TrySwapSeats(p[1],p[2],g));
            g.ForceActivePlayerForTest(p[1]);Assert.IsFalse(s.TrySwapSeats(p[0],p[2],g));
            g.ForceActivePlayerForTest(p[0]);p[2].TakeDamage(100);
            Assert.IsFalse(s.TrySwapSeats(p[0],p[2],g));
            p[0].TakeDamage(100);Assert.IsFalse(s.TrySwapSeats(p[0],p[1],g));
            Assert.AreEqual(0,g.SeatRevision);
        }

        [Test]
        public void Frozen_cannot_castle_but_can_still_skip_without_swapping()
        {
            var g=Match(2,out var p);p[0].ApplyStatus(StatusEffect.Frozen);
            var b=Board(g);Assert.IsFalse(b.CanCastle);Assert.IsTrue(b.CanSkipTurn);
            Assert.IsFalse(b.BeginCastling());Assert.IsTrue(b.SkipTurn());
            Assert.AreEqual(1,g.SeatOf(p[0]));Assert.AreEqual(1,g.TurnCount);
        }

        [Test]
        public void Current_round_keeps_all_old_actors_then_next_round_uses_new_seats()
        {
            foreach(int count in new[]{2,4,8})
            {
                var g=Match(count,out var p);var b=Board(g);
                Assert.IsTrue(b.BeginCastling());Assert.IsTrue(b.CompleteCastling(p[count-1]));
                Assert.AreEqual(count,g.SeatOf(p[0]));Assert.AreEqual(1,g.SeatOf(p[count-1]));
                for(int i=1;i<count;i++){IsTurn(g,p[i],0);g.GiveUpTurn();}
                IsTurn(g,p[count-1],1);g.GiveUpTurn();IsTurn(g,p[1==count-1?0:1],1);
                Assert.AreEqual(count+1,g.TurnCount);
            }
        }

        [Test]
        public void Mid_round_swap_with_already_acted_player_does_not_repeat_or_skip_anyone()
        {
            var g=Match(4,out var p);g.GiveUpTurn();
            Assert.IsTrue(g.TrySwapSeats(p[1],p[0]));
            g.GiveUpTurn();IsTurn(g,p[2],0);g.GiveUpTurn();IsTurn(g,p[3],0);
            g.GiveUpTurn();IsTurn(g,p[1],1);g.GiveUpTurn();IsTurn(g,p[0],1);
        }

        [Test]
        public void Last_actor_swap_takes_effect_at_immediately_following_round()
        {
            var g=Match(4,out var p);for(int i=0;i<3;i++)g.GiveUpTurn();
            g.TrySwapSeats(p[3],p[0]);g.GiveUpTurn();IsTurn(g,p[3],1);
        }

        [Test]
        public void Multiple_swaps_preserve_first_snapshot_and_latest_physical_seats()
        {
            var g=Match(4,out var p);
            g.TrySwapSeats(p[0],p[2]);g.GiveUpTurn();IsTurn(g,p[1],0);
            g.TrySwapSeats(p[1],p[3]);g.GiveUpTurn();IsTurn(g,p[2],0);
            g.GiveUpTurn();IsTurn(g,p[3],0);g.GiveUpTurn();IsTurn(g,p[2],1);
            g.GiveUpTurn();IsTurn(g,p[3],1);g.GiveUpTurn();IsTurn(g,p[0],1);
        }

        [Test]
        public void Dead_players_are_skipped_in_saved_and_new_orders()
        {
            var g=Match(4,out var p);g.TrySwapSeats(p[0],p[2]);
            p[1].TakeDamage(100);g.GiveUpTurn();IsTurn(g,p[2],0);
            p[0].TakeDamage(100);g.GiveUpTurn();IsTurn(g,p[3],0);
            g.GiveUpTurn();IsTurn(g,p[2],1);
        }

        [Test]
        public void Old_anchor_dying_at_turn_start_does_not_finish_round_early()
        {
            var g=Match(4,out var p);g.TrySwapSeats(p[0],p[2]);
            p[0].TakeDamage(19);p[0].ApplyStatus(StatusEffect.Poison);
            g.ResolveStatusTurnStart(p[0]);IsTurn(g,p[1],0);
            g.GiveUpTurn();IsTurn(g,p[2],0);g.GiveUpTurn();IsTurn(g,p[3],0);
            g.GiveUpTurn();IsTurn(g,p[2],1);
        }

        [Test]
        public void Distance_and_physical_neighbors_change_immediately_but_teams_do_not()
        {
            var g=Match(4,out var p);Assert.AreEqual(2,g.DistanceBetween(p[0],p[2]));
            Assert.IsTrue(g.IsTeammate(p[0],p[3]));
            g.TrySwapSeats(p[0],p[1]);
            Assert.AreEqual(1,g.DistanceBetween(p[0],p[2]));
            Assert.AreSame(p[2],g.NextSeatAfter(p[0]));Assert.AreSame(p[1],g.PreviousSeatBefore(p[0]));
            Assert.IsTrue(g.IsTeammate(p[0],p[3]));Assert.IsFalse(g.IsTeammate(p[0],p[1]));
            g.GiveUpTurn();IsTurn(g,p[1],0);
        }

        [Test]
        public void Target_ui_candidates_support_ally_enemy_and_out_of_range_without_double_turn_end()
        {
            var g=Match(8,out var p);var b=Board(g);
            Assert.IsTrue(b.BeginCastling());Assert.AreSame(p[0],b.Chooser);Assert.IsTrue(b.IsHumanChooser);
            Assert.AreEqual(14,b.ChoiceCandidates.Count);Assert.IsTrue(b.CanDeclinePendingChoice);
            Assert.AreEqual(HandEffect.SchoolOfConjurationCastling,b.PendingEffect);
            Assert.IsTrue(g.IsTeammate(p[0],p[3]));Assert.AreEqual(3,g.DistanceBetween(p[0],p[3]));
            Assert.IsTrue(b.CanAct(p[3].RightHand));Assert.IsFalse(b.CanAct(p[0].LeftHand));
            Assert.IsTrue(b.ClickHand(p[3].RightHand));Assert.AreEqual(4,g.SeatOf(p[0]));
            Assert.AreEqual(1,g.TurnCount);Assert.IsFalse(b.CompleteCastling(p[2]));
            Assert.IsTrue(string.Join("",b.Log).Contains("【王车易位】"));
        }

        [Test]
        public void Cancel_is_free_and_stale_target_does_not_end_turn_or_consume_status()
        {
            var g=Match(4,out var p);p[0].ApplyStatus(StatusEffect.Bleed);
            var b=Board(g);Assert.IsTrue(b.BeginCastling());
            Hands(p[1],2,3);Assert.IsFalse(b.CompleteCastling(p[1]));
            Assert.AreEqual(0,g.TurnCount);Assert.AreEqual(0,g.SeatRevision);
            Assert.IsTrue(b.DeclinePendingChoice());Assert.AreEqual(BoardPhase.ChoosingAttackerHand,b.Phase);
            Assert.AreEqual(2,p[0].StatusTurnsLeft(StatusEffect.Bleed));
            Assert.IsTrue(b.BeginCastling());Assert.IsTrue(b.CompleteCastling(p[2]));
            Assert.AreEqual(1,p[0].StatusTurnsLeft(StatusEffect.Bleed));Assert.AreEqual(1,g.TurnCount);
        }

        [Test]
        public void Dual_general_has_castling_in_either_primary_order()
        {
            foreach(bool primary in new[]{true,false})
            {
                var a=DualGeneralFactory.Create("dual",primary?CharacterDefinition.SchoolOfConjuration:CharacterDefinition.Gambler,
                    primary?CharacterDefinition.Gambler:CharacterDefinition.SchoolOfConjuration);
                var enemy=Make(1);var g=new GameManager(a,enemy,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                var b=Board(g);Assert.IsTrue(b.CanCastle);Assert.IsTrue(b.BeginCastling());
                Assert.IsTrue(b.CompleteCastling(enemy));Assert.AreEqual(2,g.SeatOf(a));
            }
        }

        [Test]
        public void Reset_restores_initial_seats_and_discards_pending_order_snapshot()
        {
            var g=Match(4,out var p);g.TrySwapSeats(p[0],p[2]);g.GiveUpTurn();g.ResetMatch();
            for(int i=0;i<4;i++)Assert.AreSame(p[i],g.Players[i]);
            for(int i=0;i<4;i++){IsTurn(g,p[i],0);g.GiveUpTurn();}
            IsTurn(g,p[0],1);Assert.AreEqual(2,g.SeatRevision);
        }

        [Test]
        public void Identity_and_lord_owner_remain_attached_to_person_after_swap()
        {
            Match(8,out var p);var providers=new List<IChoiceProvider>();
            for(int i=0;i<8;i++)providers.Add(new SimpleChoiceProvider());
            var roles=new[]{IdentityRole.Lord,IdentityRole.Loyalist,IdentityRole.Loyalist,IdentityRole.Rebel,
                IdentityRole.Rebel,IdentityRole.Rebel,IdentityRole.Rebel,IdentityRole.Traitor};
            var g=GameManager.CreateIdentityEight(p,providers,roles);int max=p[0].MaximumHealth;
            bool allied=g.IsTeammate(p[0],p[1]); int team=g.TeamOf(p[0]);
            Assert.IsTrue(g.TrySwapSeats(p[0],p[7]));
            Assert.AreEqual(IdentityRole.Lord,g.IdentityOf(p[0]));Assert.AreEqual(IdentityRole.Traitor,g.IdentityOf(p[7]));
            Assert.AreEqual(max,p[0].MaximumHealth);Assert.AreEqual(allied,g.IsTeammate(p[0],p[1]));
            Assert.AreEqual(team,g.TeamOf(p[0]));
        }

        [Test]
        public void Extra_action_castling_does_not_advance_normal_round_or_turn_count()
        {
            var g=Match(8,out var p);var b=Board(g);
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            typeof(BoardController).GetField("_extraActionActive",flags).SetValue(b,true);
            typeof(BoardController).GetField("_resumePlayer",flags).SetValue(b,p[1]);
            Assert.IsTrue(b.BeginCastling());Assert.IsTrue(b.CompleteCastling(p[7]));
            Assert.AreEqual(0,g.TurnCount);IsTurn(g,p[1],0);Assert.IsFalse(b.IsExtraAction);
            for(int i=1;i<8;i++){IsTurn(g,p[i],0);g.GiveUpTurn();}IsTurn(g,p[7],1);
        }

        [Test]
        public void Ai_uses_castling_to_reduce_adjacent_enemies_and_resolves_target_immediately()
        {
            var g=Match(8,out var p);
            var b=new BoardController(g,new SimpleChoiceProvider(),new GreedyChoiceProvider());
            b.BeginTurn();Assert.IsTrue(b.MakeAutomaticMove());
            Assert.AreEqual(1,g.SeatRevision);Assert.AreEqual(1,g.TurnCount);
            Assert.IsFalse(b.Phase==BoardPhase.ChoosingSeatSwapTarget);
            Assert.IsTrue(string.Join("",b.Log).Contains("【王车易位】"));
        }

        [Test]
        public void Ai_keeps_ordinary_play_when_castling_cannot_reduce_adjacent_enemies()
        {
            var g=Match(4,out var p);
            var b=new BoardController(g,new SimpleChoiceProvider(),new GreedyChoiceProvider());
            b.BeginTurn();Assert.IsTrue(b.MakeAutomaticMove());
            Assert.AreEqual(0,g.SeatRevision);
        }

        [Test]
        public void Non_attack_thief_turn_can_finish_the_saved_round()
        {
            var g=Match(4,out var p);g.TrySwapSeats(p[0],p[2]);
            for(int i=0;i<3;i++)g.GiveUpTurn();
            p[3].BindCharacter(new FingerDiceDuel.Core.ThiefLogic());
            // 只换值、无攻击的偷窃也必须收尾本轮。
            Hands(p[3],1,2);Hands(p[1],3,2);
            g.ExecuteSteal(p[3],p[3].LeftHand,p[1].LeftHand);
            IsTurn(g,p[2],1);
        }

        [Test]
        public void Round_limit_still_resolves_after_new_order_is_installed()
        {
            var g=Match(2,out var p);g.RoundLimit=1;g.TrySwapSeats(p[0],p[1]);
            g.GiveUpTurn();Assert.IsFalse(g.IsGameOver);g.GiveUpTurn();
            Assert.IsTrue(g.IsGameOver);Assert.AreEqual(1,g.RoundCount);
        }
    }
}
