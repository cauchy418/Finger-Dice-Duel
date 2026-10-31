using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class MusicPolicyTests
    {
        private static GameManager Match() => CharacterFactory.CreateMatch(CharacterDefinition.Blank, CharacterDefinition.Blank, new SimpleChoiceProvider(), new SimpleChoiceProvider());
        [Test] public void Menus_and_selection_use_dusk_even_with_a_previous_match()
        { var policy = new MusicPolicy(); var game = Match(); game.PlayerA.SetHealthExact(1); policy.Sync(game, false, 10); Assert.AreEqual(MusicCue.Dusk, policy.Cue); Assert.IsFalse(policy.Percussion); }
        [Test] public void Every_chapter_finale_uses_dawn_and_other_battles_use_battlefield()
        { var policy = new MusicPolicy(); var game = Match(); foreach (int level in new[] { 10, 20, 30 }) { policy.Sync(game, true, level); Assert.AreEqual(MusicCue.Dawn, policy.Cue); } foreach (int level in new[] { 0, 1, 9, 11, 19, 21, 29 }) { policy.Sync(game, true, level); Assert.AreEqual(MusicCue.Battlefield, policy.Cue); } }
        [Test] public void Half_health_is_inclusive_and_checks_both_sides()
        { var policy = new MusicPolicy(); var game = Match(); game.PlayerB.SetHealthExact(11); policy.Sync(game, true, 0); Assert.IsFalse(policy.Percussion); game.PlayerB.SetHealthExact(10); policy.Sync(game, true, 0); Assert.IsTrue(policy.Percussion); }
        [Test] public void Healing_does_not_restart_the_intensity_transition()
        { var policy = new MusicPolicy(); var game = Match(); game.PlayerA.SetHealthExact(9); policy.Sync(game, true, 0); game.PlayerA.SetHealthExact(20); policy.Sync(game, true, 0); Assert.IsTrue(policy.Percussion); }
        [Test] public void A_new_match_and_return_to_menu_reset_intensity()
        { var policy = new MusicPolicy(); var game = Match(); game.PlayerA.SetHealthExact(1); policy.Sync(game, true, 10); policy.Sync(Match(), true, 10); Assert.IsFalse(policy.Percussion); policy.Sync(null, false, 0); Assert.AreEqual(MusicCue.Dusk, policy.Cue); Assert.IsFalse(policy.Percussion); }
        [Test] public void Odd_maximum_health_uses_the_actual_half_threshold()
        { var policy = new MusicPolicy(); var game = Match(); game.PlayerA.SetMaxHealth(21); game.PlayerA.SetHealthExact(11); policy.Sync(game, true, 0); Assert.IsFalse(policy.Percussion); game.PlayerA.SetHealthExact(10); policy.Sync(game, true, 0); Assert.IsTrue(policy.Percussion); }
        [Test] public void Music_reads_do_not_change_the_game_or_consume_actions()
        { var policy = new MusicPolicy(); var game = Match(); int hp = game.PlayerA.Health; int hand = game.PlayerA.LeftHand.Value; for (int i = 0; i < 20; i++) policy.Sync(game, true, 30); Assert.AreEqual(hp, game.PlayerA.Health); Assert.AreEqual(hand, game.PlayerA.LeftHand.Value); Assert.IsFalse(game.IsGameOver); }
    }
}
