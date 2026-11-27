using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class OperationStatusDisplayTests
    {
        private static BoardController Make(out GameManager game)
        {
            var a = new SimpleChoiceProvider(); var b = new SimpleChoiceProvider();
            game = CharacterFactory.CreateMatch(CharacterDefinition.Blank, CharacterDefinition.Blank, a, b);
            return new BoardController(game, a, b);
        }

        [Test] public void Punishment_happens_before_target_prompt_and_only_once()
        {
            var board = Make(out var game); var actor = game.PlayerA; var target = game.PlayerB;
            actor.LeftHand.SetValue(1); target.LeftHand.SetValue(3);
            actor.ApplyStatus(StatusEffect.DivinePunishment, 2); board.BeginTurn();
            board.ClickHand(actor.LeftHand); board.ClickHand(target.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);
            Assert.AreEqual(16, actor.Health); Assert.AreEqual(20, target.Health);
            Assert.AreEqual(4, board.PreviewHandValue(actor.LeftHand).Value);
            board.ConfirmTargetPlayer();
            Assert.AreEqual(16, actor.Health); Assert.AreEqual(16, target.Health);
            Assert.AreEqual(4, actor.LeftHand.Value);
        }

        [Test] public void Fatal_punishment_commits_digit_without_prompt_or_attack()
        {
            var board = Make(out var game); var actor = game.PlayerA; var target = game.PlayerB;
            actor.LeftHand.SetValue(1); target.LeftHand.SetValue(3); actor.SetHealthExact(3);
            actor.ApplyStatus(StatusEffect.DivinePunishment, 2); board.BeginTurn();
            board.ClickHand(actor.LeftHand); board.ClickHand(target.LeftHand);
            Assert.False(actor.IsAlive); Assert.AreEqual(BoardPhase.GameOver, board.Phase);
            Assert.AreEqual(4, actor.LeftHand.Value); Assert.AreEqual(20, target.Health);
            Assert.True(board.LastTurn != null); Assert.AreEqual(0, board.LastTurn.DamageDealt);
        }

        [Test] public void Decay_kills_before_nine_can_heal()
        {
            var board = Make(out var game); var actor = game.PlayerA; var target = game.PlayerB;
            actor.LeftHand.SetValue(1); target.LeftHand.SetValue(8); actor.SetHealthExact(8);
            actor.ApplyStatus(StatusEffect.Decay, 2); board.BeginTurn();
            board.ClickHand(actor.LeftHand); board.ClickHand(target.LeftHand);
            Assert.False(actor.IsAlive); Assert.AreEqual(BoardPhase.GameOver, board.Phase);
            Assert.AreEqual(9, actor.LeftHand.Value); Assert.AreEqual(0, board.LastTurn.HealAmount);
        }

        [Test] public void Normal_nonattack_digit_still_triggers_decay()
        {
            var board = Make(out var game); var actor = game.PlayerA; var target = game.PlayerB;
            actor.LeftHand.SetValue(1); target.LeftHand.SetValue(1);
            actor.ApplyStatus(StatusEffect.Decay, 2); board.BeginTurn();
            board.ClickHand(actor.LeftHand); board.ClickHand(target.LeftHand);
            Assert.AreEqual(18, actor.Health); Assert.AreEqual(2, actor.LeftHand.Value);
            Assert.AreEqual(1, game.TurnCount);
        }

        [Test] public void Source_shield_cannot_block_pre_effect_punishment()
        {
            var board = Make(out var game); var actor = game.PlayerA; var target = game.PlayerB;
            actor.LeftHand.SetValue(5); actor.LeftHand.SetShieldState(ShieldState.Formed);
            target.LeftHand.SetValue(1); actor.ApplyStatus(StatusEffect.DivinePunishment, 2); board.BeginTurn();
            board.ClickHand(actor.LeftHand); board.ClickHand(target.LeftHand);
            Assert.AreEqual(14, actor.Health); Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);
            board.ConfirmTargetPlayer();
            Assert.AreEqual(14, actor.Health); Assert.AreEqual(6, actor.LeftHand.Value);
            Assert.False(actor.LeftHand.IsShield); Assert.AreEqual(14, target.Health);
        }
        [Test] public void Fatal_punishment_preserves_source_break_shock_before_death_in_core_and_board()
        {
            foreach(bool ui in new[]{false,true})
            {
                var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
                var g=CharacterFactory.CreateMatch(CharacterDefinition.OathOfDevotion,CharacterDefinition.Blank,ca,cb);
                var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(5);a.LeftHand.SetShieldState(ShieldState.Formed);
                a.SetHealthExact(3);a.ApplyStatus(StatusEffect.DivinePunishment);b.LeftHand.SetValue(3);
                if(!ui)g.ExecuteTurn(a,a.LeftHand,b.LeftHand);
                else
                {
                    var board=new BoardController(g,ca,cb);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);
                    for(int step=0;step<12&&board.Phase!=BoardPhase.GameOver;step++)
                    {
                        if(board.PendingWizardChoice!=null){Assert.True(board.ChooseWizardHand(b.LeftHand));continue;}
                        if(board.PendingRequest.HasValue){Assert.True(board.ClickHand(b.LeftHand));continue;}
                        Assert.True(false,"unexpected phase "+board.Phase);
                    }
                    Assert.AreEqual(BoardPhase.GameOver,board.Phase);Assert.True(board.LastTurn.ShieldBrokenBeforeOp);
                }
                Assert.False(a.IsAlive);Assert.AreEqual(8,a.LeftHand.Value);Assert.AreEqual(19,b.Health,"only the source break shock resolves");
                Assert.AreEqual(0,((OathOfDevotionLogic)a.Character).ShieldFormCount);
            }
        }
        [Test] public void Source_break_knight_choice_finishes_before_fatal_status_damage()
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            var g=CharacterFactory.CreateMatch(CharacterDefinition.EldritchKnight,CharacterDefinition.Blank,ca,cb);
            var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(5);a.LeftHand.SetShieldState(ShieldState.Formed);
            a.SetHealthExact(3);a.ApplyStatus(StatusEffect.DivinePunishment);b.LeftHand.SetValue(1);
            var board=new BoardController(g,ca,cb);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);Assert.AreEqual(3,a.Health);
            Assert.True(board.PendingWizardChoice.Prompt.Contains("奥法武器"));
            Assert.True(board.ChooseSkillOption((int)AttackDamageType.Fire));
            Assert.AreEqual(BoardPhase.GameOver,board.Phase);Assert.False(a.IsAlive);Assert.AreEqual(6,a.LeftHand.Value);
            Assert.AreEqual(AttackDamageType.Fire,((FingerDiceDuel.Characters.SpecialLogics.EldritchKnightLogic)a.Character).Enchantment);
            Assert.True(board.LastTurn.ShieldBrokenBeforeOp);Assert.AreEqual(20,b.Health);
        }
        [Test] public void Source_break_shock_collects_human_dodge_before_lethal_punishment()
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            var g=CharacterFactory.CreateMatch(CharacterDefinition.OathOfDevotion,CharacterDefinition.Swashbuckler,ca,cb);
            var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(5);a.LeftHand.SetShieldState(ShieldState.Formed);
            a.SetHealthExact(3);a.ApplyStatus(StatusEffect.DivinePunishment);b.LeftHand.SetValue(3);b.RightHand.SetValue(1);
            var board=new BoardController(g,ca,cb);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);
            for(int step=0;step<12&&board.Phase!=BoardPhase.GameOver;step++)
            {
                Assert.AreEqual(3,a.Health,"punishment waits for the source break attack defense");
                if(board.PendingEffect==HandEffect.SwashbucklerDodge)Assert.True(board.ClickHand(b.RightHand));
                else if(board.PendingRequest.HasValue)Assert.True(board.ClickHand(b.LeftHand));
                else Assert.True(false,"unexpected phase "+board.Phase);
            }
            Assert.AreEqual(BoardPhase.GameOver,board.Phase);Assert.False(a.IsAlive);
            Assert.AreEqual(20,b.Health);Assert.AreEqual(2,b.RightHand.Value);
            Assert.True(board.LastTurn.ShieldBrokenBeforeOp);
        }
        [Test] public void Surviving_operation_source_break_shock_does_not_repeat_at_commit()
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            var g=CharacterFactory.CreateMatch(CharacterDefinition.OathOfDevotion,CharacterDefinition.Blank,ca,cb);
            var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(5);a.LeftHand.SetShieldState(ShieldState.Formed);
            a.ApplyStatus(StatusEffect.DivinePunishment);b.LeftHand.SetValue(3);
            var board=new BoardController(g,ca,cb);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);
            for(int step=0;step<16&&g.TurnCount==0;step++)
            {
                if(board.Phase==BoardPhase.ChoosingTargetPlayer){Assert.True(board.ConfirmTargetPlayer());continue;}
                if(board.PendingRequest.HasValue){Assert.True(board.ClickHand(b.LeftHand));continue;}
                Assert.True(false,"unexpected phase "+board.Phase);
            }
            Assert.AreEqual(1,g.TurnCount);Assert.AreEqual(12,a.Health);Assert.AreEqual(11,b.Health);
            Assert.True(board.LastTurn.ShieldBrokenBeforeOp);
        }
    }
}
