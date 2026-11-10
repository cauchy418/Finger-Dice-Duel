using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class ProductionAiConfigurationTests
    {
        [Test] public void Free_adventure_opponents_apply_the_validated_role_profile()
        {
            foreach (int level in new[] { 5, 10 })
            {
                var a = new SimpleChoiceProvider(); var b = new SimpleChoiceProvider();
                var game = CharacterFactory.CreateMatch(CharacterDefinition.Blank, CharacterDefinition.Champion, a, b);
                var board = new BoardController(game, a, b);
                AdventureLesson lesson = level == 5 ? (AdventureLesson)AdventureLevel5.Attach(game, board) : AdventureLevel10.Attach(game, board);
                lesson.Bind(board);
                var ai = (GreedyChoiceProvider)game.SkillPolicyFor(game.PlayerB);
                ai.ChooseMove(game, game.PlayerB);
                Assert.AreEqual(100, ai.DenyRatioPercent, "Free combat must apply the validated Champion profile, level " + level);
            }
        }
        [Test] public void Campaign_free_combat_applies_the_same_validated_role_profile()
        {
            foreach (bool banners in new[] { false, true })
            {
                var game = CharacterFactory.CreateMatch(CharacterDefinition.Champion, CharacterDefinition.Blank);
                var ai = banners ? TatteredBannersLesson.NewAi(game) : CampaignLesson.NewAi(game);
                ai.ChooseMove(game, game.PlayerA);
                Assert.AreEqual(100, ai.DenyRatioPercent);
            }
        }
        [Test] public void Production_chain_break_uses_bound_team_context_before_selecting_a_move()
        {
            var p = new PlayerState[4];
            var providers = new IChoiceProvider[4];
            for (int i = 0; i < 4; i++)
            {
                p[i] = CharacterFactory.CreatePlayer("P" + i, i == 0 ? CharacterDefinition.Breaker : CharacterDefinition.Blank);
                providers[i] = new SimpleChoiceProvider();
            }
            var game = GameManager.Create2v2(p, providers);
            var ai = ProductionAi.Create(game);
            p[3].LeftHand.SetShieldState(ShieldState.Formed);
            p[1].RightHand.SetShieldState(ShieldState.Formed);
            var request = new ChoiceRequest(HandEffect.BreakerChainBreak, "", new[] { p[3].LeftHand, p[1].RightHand }, true, decider: p[0]);
            Assert.AreEqual(p[1].RightHand, ai.ChooseHandFor(request, p[0]));
        }
        [Test] public void Normal_eight_seat_focus_and_identity_focus_use_their_intended_mode_policy()
        {
            var p = new PlayerState[8]; var providers = new IChoiceProvider[8];
            for (int i = 0; i < 8; i++)
            {
                p[i] = CharacterFactory.CreatePlayer("P" + i, CharacterDefinition.Champion);
                providers[i] = new SimpleChoiceProvider();
            }
            var game = GameManager.CreateEight(p, providers, new[] { 0, 1, 1, 2, 2, 0, 1, 0 });
            Assert.AreEqual(p[1], ProductionAi.Create(game).ResolveFocusTarget(game, p[0]));
            Assert.Null(ProductionAi.Create(game, learnedFocus: false).ResolveFocusTarget(game, p[0]));
        }
    }
}
