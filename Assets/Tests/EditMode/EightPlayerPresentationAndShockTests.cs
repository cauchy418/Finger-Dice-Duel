using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class EightPlayerPresentationAndShockTests
    {
        private static readonly IdentityRole[] Roles = { IdentityRole.Lord, IdentityRole.Loyalist, IdentityRole.Loyalist,
            IdentityRole.Rebel, IdentityRole.Rebel, IdentityRole.Rebel, IdentityRole.Rebel, IdentityRole.Traitor };
        private static GameManager Match(int paladinSeat, bool identity, out List<PlayerState> players, out BoardController board)
        {
            players = new List<PlayerState>(); var choices = new List<IChoiceProvider>(); var sinks = new List<SimpleChoiceProvider>();
            for (int i = 0; i < 8; i++)
            {
                players.Add(CharacterFactory.CreatePlayer("P" + (i + 1), i == paladinSeat ? CharacterDefinition.OathOfDevotion : CharacterDefinition.Blank));
                var sink = new SimpleChoiceProvider(); sinks.Add(sink); choices.Add(sink);
            }
            var game = identity ? GameManager.CreateIdentityEight(players, choices, Roles)
                : GameManager.CreateEight(players, choices, new[] { 0, 1, 2, 0, 1, 2, 0, 1 });
            // 明确复现原身份局的A/B构造sink与Game记录器不同，但本回合答案在控制器中。
            board = new BoardController(game, new SimpleChoiceProvider(), new SimpleChoiceProvider());
            for (int i = 0; i < 8; i++) board.BindPlayer(players[i], null, sinks[i]);
            return game;
        }
        private static PlayerState LaterNeighbor(List<PlayerState> p, int seat)
            => p[System.Math.Max((seat + 1) % 8, (seat + 7) % 8)];
        [Test] public void Human_paladin_in_each_eight_player_seat_shocks_exact_selected_neighbor_in_both_modes()
        {
            foreach (bool identity in new[] { false, true }) for (int seat = 0; seat < 8; seat++)
            {
                var g = Match(seat, identity, out var p, out var b); var owner = p[seat]; var far = p[(seat + 4) % 8];
                var selected = LaterNeighbor(p, seat); int before = selected.Health; var initial = new List<int>(); foreach(var player in p) initial.Add(player.Health);
                owner.LeftHand.SetValue(3); far.LeftHand.SetValue(1); b.SetFirstPlayer(owner); b.BeginTurn();
                Assert.IsTrue(b.ClickHand(owner.LeftHand)); Assert.IsTrue(b.ClickHand(far.LeftHand));
                Assert.AreEqual(HandEffect.OathOfDevotionShockTarget, b.PendingEffect, "seat=" + seat + " identity=" + identity);
                foreach (var hand in b.PendingRequest.Value.Candidates) Assert.IsTrue(g.CanReach(owner, g.OwnerOfHand(hand)), "候选不得含远处加数提供者");
                Assert.IsTrue(b.ClickHand(selected.RightHand)); Assert.AreEqual(before - 2, selected.Health, "必须命中明确选择的人");
                for(int i=0;i<8;i++) if(!ReferenceEquals(p[i],selected)) Assert.AreEqual(initial[i],p[i].Health,"不能改打兜底目标");
                Assert.IsTrue(string.Join(" | ", b.Log).Contains("冲击"));
            }
        }
        [Test] public void Active_break_and_reforming_shocks_keep_distinct_selected_targets()
        {
            var g = Match(0, false, out var p, out var b); var owner=p[0];
            owner.LeftHand.SetValue(5); owner.LeftHand.SetShieldState(ShieldState.Formed); p[4].LeftHand.SetValue(9);
            b.BeginTurn(); Assert.IsTrue(b.ClickHand(owner.LeftHand)); Assert.IsTrue(b.ClickHand(p[4].LeftHand));
            Assert.AreEqual(HandEffect.OathOfDevotionShockTarget,b.PendingEffect); Assert.AreEqual(1,b.PendingRequest.Value.Variant);
            Assert.IsTrue(b.ClickHand(p[7].LeftHand)); Assert.AreEqual(0,b.PendingRequest.Value.Variant);
            Assert.IsTrue(b.ClickHand(p[1].LeftHand)); Assert.AreEqual(18,p[7].Health); Assert.AreEqual(18,p[1].Health);
            Assert.AreEqual(20,p[4].Health);
        }
        [Test] public void Passive_shield_break_shock_hits_the_defending_humans_selected_target_once()
        {
            var g = Match(5, false, out var p, out var b); var owner=p[5];
            owner.RightHand.SetValue(5); owner.RightHand.SetShieldState(ShieldState.Formed);
            p[4].LeftHand.SetValue(3); owner.LeftHand.SetValue(1); b.SetFirstPlayer(p[4]);b.BeginTurn();
            b.ClickHand(p[4].LeftHand);b.ClickHand(owner.LeftHand);
            if(b.Phase==BoardPhase.ChoosingTargetPlayer) b.ClickHand(owner.LeftHand);
            Assert.AreEqual(HandEffect.OathOfDevotionShockTarget,b.PendingEffect);
            Assert.AreEqual(owner,b.Chooser);Assert.IsTrue(b.ClickHand(p[6].LeftHand));
            Assert.AreEqual(18,p[6].Health);Assert.AreEqual(20,owner.Health);
        }
        [Test] public void No_shock_target_is_offered_when_all_real_players_are_out_of_range()
        {
            var g=Match(0,false,out var p,out var b);g.ConfigureSeatObstacles(1,1,1,1,1,1,1,1);
            p[0].LeftHand.SetValue(3);b.BeginTurn();b.ClickHand(p[0].LeftHand);b.ClickHand(p[4].LeftHand);
            Assert.IsFalse(b.PendingEffect==HandEffect.OathOfDevotionShockTarget);
            foreach(var player in p) Assert.AreEqual(20,player.Health);
        }
        [Test] public void Identity_counts_follow_actual_sides_not_distinct_attack_teams()
        {
            var g=Match(-1,true,out var p,out var b);
            foreach(int i in new[]{0,1,2}){IdentityPresentation.Counts(g,p[i],out int mate,out int foe);Assert.AreEqual(2,mate);Assert.AreEqual(5,foe);}
            foreach(int i in new[]{3,4,5,6}){IdentityPresentation.Counts(g,p[i],out int mate,out int foe);Assert.AreEqual(3,mate);Assert.AreEqual(4,foe);}
            IdentityPresentation.Counts(g,p[7],out int tmate,out int tfoe);Assert.AreEqual(0,tmate);Assert.AreEqual(7,tfoe);
            Assert.IsFalse(g.IsTeammate(p[0],p[1]),"展示不能修改身份局允许互相攻击的内核分队");
        }
        [Test] public void Hidden_identity_relationships_stay_unlabelled_and_known_relationships_are_correct()
        {
            var g=Match(-1,true,out var p,out var b);
            Assert.AreEqual(RingRelation.Self,IdentityPresentation.Relation(g,p[0],p[0]));
            Assert.AreEqual(RingRelation.Unknown,IdentityPresentation.Relation(g,p[0],p[1]));
            Assert.AreEqual(RingRelation.Teammate,IdentityPresentation.Relation(g,p[1],p[0]));
            Assert.AreEqual(RingRelation.Opponent,IdentityPresentation.Relation(g,p[3],p[0]));
            Assert.AreEqual(RingRelation.Unknown,IdentityPresentation.Relation(g,p[3],p[4]));
            Assert.AreEqual(RingRelation.Opponent,IdentityPresentation.Relation(g,p[7],p[4]));
            p[4].SetHealthExact(0);
            Assert.AreEqual(RingRelation.Teammate,IdentityPresentation.Relation(g,p[3],p[4]));
            IdentityPresentation.Counts(g,p[3],out int mate,out int foe);Assert.AreEqual(2,mate);Assert.AreEqual(4,foe);
        }
    }
}
