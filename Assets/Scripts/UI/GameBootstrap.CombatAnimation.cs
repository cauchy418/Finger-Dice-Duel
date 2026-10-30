using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using UnityEngine;
namespace FingerDiceDuel.UI
{
    public sealed partial class GameBootstrap
    {
        private bool TryPlayCombatQueue()
        {
            if(_board.CombatAnimationBusy)return true;
            _game.RecordCombatVisuals=true;
            var events=_game.DrainCombatVisuals();if(events.Count==0)return false;
            var game=_game;var final=game.CaptureVisualFrame();
            _animatedTurn=_controller.LastTurn;
            _board.PlayCombatEvents(events,final,()=>{
                if(!ReferenceEquals(game,_game))return;
                _pauseUntil=Time.unscaledTime+.08f;RefreshView();
            });return true;
        }
        private void UpdateCombatWindup()
        {
            if(_controller.Phase==BoardPhase.ChoosingTargetPlayer&&_controller.SelectedHand!=null)
            {
                var actor=_controller.ActivePlayer;var other=_controller.TargetPlayer??_controller.Defender;
                var ctx=new AttackContext(actor,other,_controller.SelectedHand,4,_game);
                ctx.AttackType=(actor.Character as IAttackTypeProvider)?.AttackTypeOf(ctx)??AttackDamageType.Slashing;
                if(AttackDamageTypes.IsMagic(ctx.AttackType))
                {
                    if(CharacterBehaviors.FirstImplementer<IElementEnchantment>(actor)!=null||CharacterBehaviors.FirstImplementer<IBladesong>(actor)!=null)ctx.VisualWeapon="weapon_sword";
                    else if(actor.Definition?.Profession?.Id=="monk")ctx.VisualWeapon="weapon_fist";
                }
                _board.ShowCombatWindup(actor,_controller.SelectedHand,CombatVisualRouting.Weapon(ctx,_game),ctx.AttackType);
            }
            else if(_controller.Phase==BoardPhase.ChoosingAttackerHand||_controller.Phase==BoardPhase.GameOver)_board.HideCombatWindup();
        }
    }
}
