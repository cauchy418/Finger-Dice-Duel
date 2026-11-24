using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class PaladinGuardianAuraTests
    {
        private static GameManager Match(out List<SimpleChoiceProvider> sinks)
        {
            var defs=new[]{CharacterDefinition.ArcaneArcher,CharacterDefinition.OathOfTheCrown,CharacterDefinition.Champion,CharacterDefinition.OathOfDevotion};
            var players=new List<PlayerState>();var choices=new List<IChoiceProvider>();sinks=new List<SimpleChoiceProvider>();
            for(int i=0;i<4;i++){players.Add(CharacterFactory.CreatePlayer("seat"+i,defs[i]));var sink=new SimpleChoiceProvider();sinks.Add(sink);choices.Add(sink);}
            players[1].LeftHand.SetValue(5);players[3].LeftHand.SetValue(5);
            return GameManager.Create2v2(players,choices);
        }
        [Test] public void Clockwise_first_acceptance_stops_all_later_questions_even_for_multiple_hits()
        {
            var game=Match(out _);var asked=new List<PlayerState>();
            game.AnsweredLookupFor=(p,e,v)=>{if(e!=HandEffect.OathOfDevotionRedirect)return null;asked.Add(p);return p.LeftHand;};
            var hit=new AttackContext(game.Players[0],game.Players[2],game.Players[0].RightHand,4){HitCount=3};hit.Resolve(game.GetChoiceProvider(game.Players[2]),game);
            Assert.AreSame(game.Players[3],hit.Defender);Assert.AreEqual(2,hit.LandedHits);Assert.AreEqual(1,asked.Count);Assert.AreSame(game.Players[3],asked[0]);
        }
        [Test] public void Decline_continues_clockwise_to_other_paladin_with_unformed_five()
        {
            var game=Match(out _);var asked=new List<PlayerState>();
            game.AnsweredLookupFor=(p,e,v)=>{if(e!=HandEffect.OathOfDevotionRedirect)return null;asked.Add(p);return p==game.Players[1]?p.LeftHand:null;};
            var hit=new AttackContext(game.Players[0],game.Players[2],game.Players[0].RightHand,4);hit.Resolve(game.GetChoiceProvider(game.Players[2]),game);
            Assert.AreSame(game.Players[1],hit.Defender);Assert.AreEqual(2,asked.Count);Assert.AreSame(game.Players[3],asked[0]);Assert.AreSame(game.Players[1],asked[1]);
        }
        [Test] public void Devotion_six_is_shield_and_its_shock_is_one()
        {
            var owner=CharacterFactory.CreatePlayer("devotion",CharacterDefinition.OathOfDevotion);
            Assert.AreEqual(HandEffectType.Shield,CharacterBehaviors.DisplayedEffectOf(owner,6));
            Assert.AreEqual(1,OathOfDevotionLogic.ShockDamage);
        }
        [Test] public void Board_stops_guard_prompts_after_first_acceptance()
        {
            var game=Match(out var sinks);game.Players[0].LeftHand.SetValue(3);
            var board=new BoardController(game,sinks[0],sinks[1]);board.BindPlayer(game.Players[2],null,sinks[2]);board.BindPlayer(game.Players[3],null,sinks[3]);
            board.BeginTurn();Assert.True(board.ClickHand(game.Players[0].LeftHand));Assert.True(board.ClickHand(game.Players[2].RightHand));Assert.True(board.ClickHand(game.Players[2].RightHand));
            Assert.AreEqual(HandEffect.OathOfDevotionRedirect,board.PendingEffect);Assert.AreSame(game.Players[3],board.Chooser);
            board.ClickHand(game.Players[3].RightHand);
            Assert.False(board.PendingEffect==HandEffect.OathOfDevotionRedirect);
        }
        [Test] public void Devotion_four_and_six_can_guard_without_an_intact_shield()
        {
            foreach(int value in new[]{4,6})
            {
                var game=Match(out var sinks);var owner=game.Players[3];owner.LeftHand.SetValue(value);owner.LeftHand.SetShieldState(ShieldState.Broken);
                sinks[3].SetChoice(HandEffect.OathOfDevotionRedirect,owner.RightHand);
                var hit=new AttackContext(game.Players[0],game.Players[2],game.Players[0].RightHand,4);hit.Resolve(game.GetChoiceProvider(game.Players[2]),game);
                Assert.AreSame(owner,hit.Defender);
            }
        }
        [Test] public void Secondary_paladin_identity_is_active_until_transformed()
        {
            var primary=CharacterFactory.CreatePlayer("primary",CharacterDefinition.Champion);
            var secondary=CharacterFactory.CreatePlayer("secondary",CharacterDefinition.OathOfTheCrown);
            var owner=new PlayerState("dual",new DualGeneralBehavior(primary.Character,secondary.Character));owner.BindDefinition(CharacterDefinition.Champion);
            Assert.True(PaladinGuardianAura.IsPaladin(owner));owner.TryPolymorph();Assert.False(PaladinGuardianAura.IsPaladin(owner));
        }
        [Test] public void Shield_formation_shock_deals_exactly_one_to_an_unshielded_target()
        {
            var game=Match(out var sinks);var owner=game.Players[3];var target=game.Players[2];
            sinks[3].SetChoice(HandEffect.OathOfDevotionShockTarget,target.LeftHand,OathOfDevotionLogic.ShockVariantForm);
            ((OathOfDevotionLogic)owner.Character).OnShieldFormed(owner,owner.LeftHand,game);
            Assert.AreEqual(19,target.Health);
        }

        private sealed class GuardAi : AbstractChoiceProvider, IMoveSelector
        {
            public int Questions; public string Describe=>"guard";
            public LegalMove ChooseMove(GameManager game,PlayerState actor)=>default;
            public override Hand ChooseHandFor(ChoiceRequest request,PlayerState decider)
            {if(request.Effect==HandEffect.OathOfDevotionRedirect){Questions++;return decider.RightHand;}return base.ChooseHandFor(request,decider);}
        }
        [Test] public void Board_ai_first_acceptance_prevents_later_ai_questions_and_is_not_asked_again_during_resolution()
        {
            var game=Match(out var sinks);game.Players[0].LeftHand.SetValue(3);
            var first=new GuardAi();var later=new GuardAi();
            var board=new BoardController(game,sinks[0],sinks[1]);board.BindPlayer(game.Players[1],later,sinks[1]);board.BindPlayer(game.Players[2],null,sinks[2]);board.BindPlayer(game.Players[3],first,sinks[3]);
            board.BeginTurn();board.ClickHand(game.Players[0].LeftHand);board.ClickHand(game.Players[2].RightHand);board.ClickHand(game.Players[2].RightHand);
            Assert.AreEqual(1,first.Questions);Assert.AreEqual(0,later.Questions);Assert.AreEqual(20,game.Players[2].Health);Assert.True(game.Players[3].Health<20);
        }

        [Test] public void Human_guardian_can_redirect_a_nested_Devotion_shock_in_real_board_flow()
        {
            var players=new List<PlayerState>{CharacterFactory.CreatePlayer("devotion",CharacterDefinition.OathOfDevotion),CharacterFactory.CreatePlayer("victim",CharacterDefinition.Champion),CharacterFactory.CreatePlayer("guardian",CharacterDefinition.OathOfTheCrown),CharacterFactory.CreatePlayer("fourth",CharacterDefinition.Champion)};
            var sinks=new List<SimpleChoiceProvider>();var choices=new List<IChoiceProvider>();for(int i=0;i<4;i++){var sink=new SimpleChoiceProvider();sinks.Add(sink);choices.Add(sink);}
            var game=GameManager.Create2v2(players,choices);players[0].LeftHand.SetValue(3);players[2].LeftHand.SetValue(5);
            var board=new BoardController(game,sinks[0],sinks[1]);board.BindPlayer(players[2],null,sinks[2]);board.BindPlayer(players[3],null,sinks[3]);
            board.BeginTurn();board.ClickHand(players[0].LeftHand);board.ClickHand(players[1].RightHand);
            Assert.AreEqual(HandEffect.OathOfDevotionShockTarget,board.PendingEffect);board.ClickHand(players[1].LeftHand);
            Assert.AreEqual(HandEffect.OathOfDevotionRedirect,board.PendingEffect);Assert.AreSame(players[2],board.Chooser);board.ClickHand(players[2].RightHand);
            Assert.AreEqual(20,players[1].Health);Assert.AreEqual(19,players[2].Health);
        }

        [Test] public void Swordsman_extra_and_main_attacks_offer_independent_human_guardian_choices()
        {
            var game=Match(out var sinks);CharacterFactory.Configure(game.Players[0],CharacterDefinition.Swordsman);CharacterFactory.Configure(game.Players[1],CharacterDefinition.Champion);CharacterFactory.Configure(game.Players[2],CharacterDefinition.OathOfDevotion);CharacterFactory.Configure(game.Players[3],CharacterDefinition.OathOfTheCrown);((OathOfTheCrownLogic)game.Players[3].Character).OnOwnShieldBroken(game.Players[3],game.Players[3].LeftHand,game);game.Players[1].LeftHand.SetValue(1);game.Players[2].LeftHand.SetValue(5);game.Players[3].LeftHand.SetValue(5);game.Players[0].LeftHand.SetValue(3);game.Players[0].RightHand.SetValue(4);
            var board=new BoardController(game,sinks[0],sinks[1]);board.BindPlayer(game.Players[2],null,sinks[2]);board.BindPlayer(game.Players[3],null,sinks[3]);
            board.BeginTurn();board.ClickHand(game.Players[0].LeftHand);board.ClickHand(game.Players[1].RightHand);board.ClickHand(game.Players[1].RightHand);
            Assert.AreEqual(HandEffect.SwordsmanFollowUp,board.PendingEffect);board.AcceptPendingChoice();
            Assert.AreEqual(HandEffect.OathOfDevotionRedirect,board.PendingEffect);Assert.AreEqual(1,board.PendingRequest.Value.Variant);Assert.AreSame(game.Players[2],board.Chooser);board.ClickHand(game.Players[2].RightHand);
            Assert.AreEqual(HandEffect.OathOfDevotionRedirect,board.PendingEffect);Assert.AreEqual(0,board.PendingRequest.Value.Variant);Assert.AreSame(game.Players[2],board.Chooser);board.DeclinePendingChoice();
            Assert.AreSame(game.Players[3],board.Chooser);board.ClickHand(game.Players[3].RightHand);
            Assert.AreEqual(16,game.Players[2].Health);Assert.True(game.Players[3].Health<20);Assert.AreEqual(20,game.Players[1].Health);
        }

        [Test] public void Attached_knowledge_force_does_not_offer_a_second_guardian_for_the_same_attack()
        {
            var game=Match(out _);((OathOfTheCrownLogic)game.Players[1].Character).OnOwnShieldBroken(game.Players[1],game.Players[1].RightHand,game);
            game.Players[0].GainKnowledge(game.Players[0]);int questions=0;
            game.AnsweredLookupFor=(p,e,v)=>{if(e!=HandEffect.OathOfDevotionRedirect)return null;questions++;return p.RightHand;};
            var hit=new AttackContext(game.Players[0],game.Players[2],game.Players[0].RightHand,4);hit.Resolve(game.GetChoiceProvider(game.Players[2]),game);
            Assert.AreEqual(1,questions);Assert.AreSame(game.Players[3],hit.Defender);
        }

        [Test] public void Human_guardian_can_redirect_the_independent_Taichi_reflection()
        {
            var game=Match(out var sinks);CharacterFactory.Configure(game.Players[0],CharacterDefinition.Champion);CharacterFactory.Configure(game.Players[1],CharacterDefinition.WayOfTheTaiChi);CharacterFactory.Configure(game.Players[3],CharacterDefinition.OathOfTheCrown);
            game.Players[0].LeftHand.SetValue(3);game.Players[1].LeftHand.SetValue(5);game.Players[1].LeftHand.SetShieldState(ShieldState.Taichi);
            var board=new BoardController(game,sinks[0],sinks[1]);board.BindPlayer(game.Players[2],null,sinks[2]);board.BindPlayer(game.Players[3],null,sinks[3]);
            board.BeginTurn();board.ClickHand(game.Players[0].LeftHand);board.ClickHand(game.Players[1].RightHand);board.ClickHand(game.Players[1].RightHand);
            Assert.AreEqual(HandEffect.WayOfTheTaiChiTaichi,board.PendingEffect);board.ClickHand(game.Players[1].LeftHand);if(board.PendingEffect==HandEffect.TaichiKiBoost)board.ChooseSkillOption(0);
            Assert.AreEqual(HandEffect.OathOfDevotionRedirect,board.PendingEffect);Assert.AreSame(game.Players[3],board.Chooser);board.ClickHand(game.Players[3].RightHand);
            Assert.AreEqual(20,game.Players[0].Health);Assert.True(game.Players[3].Health<20);
        }

        [Test] public void Swordsman_extra_transfer_and_guardian_are_independent_from_main_transfer()
        {
            var game=Match(out var sinks);CharacterFactory.Configure(game.Players[0],CharacterDefinition.Swordsman);CharacterFactory.Configure(game.Players[1],CharacterDefinition.Oathbreaker);CharacterFactory.Configure(game.Players[3],CharacterDefinition.OathOfTheCrown);
            game.Players[0].LeftHand.SetValue(3);game.Players[0].RightHand.SetValue(4);game.Players[1].LeftHand.SetValue(1);game.Players[1].RightHand.SetValue(5);game.Players[2].LeftHand.SetValue(5);game.Players[2].LeftHand.SetShieldState(ShieldState.Formed);
            var board=new BoardController(game,sinks[0],sinks[1]);board.BindPlayer(game.Players[2],null,sinks[2]);board.BindPlayer(game.Players[3],null,sinks[3]);
            board.BeginTurn();board.ClickHand(game.Players[0].LeftHand);board.ClickHand(game.Players[1].LeftHand);board.ClickHand(game.Players[1].LeftHand);board.AcceptPendingChoice();
            Assert.AreEqual(HandEffect.OathbreakerTransfer,board.PendingEffect);Assert.AreEqual(1,board.PendingRequest.Value.Variant);board.ClickHand(game.Players[2].RightHand);
            Assert.AreEqual(HandEffect.OathOfDevotionRedirect,board.PendingEffect);Assert.AreEqual(1,board.PendingRequest.Value.Variant);Assert.AreSame(game.Players[3],board.Chooser);board.ClickHand(game.Players[3].RightHand);
            Assert.AreEqual(HandEffect.OathbreakerTransfer,board.PendingEffect);Assert.AreEqual(0,board.PendingRequest.Value.Variant);board.DeclinePendingChoice();
            Assert.AreEqual(16,game.Players[3].Health);Assert.AreEqual(14,game.Players[1].Health);Assert.AreEqual(ShieldState.Formed,game.Players[2].LeftHand.State);
        }

    }
}
