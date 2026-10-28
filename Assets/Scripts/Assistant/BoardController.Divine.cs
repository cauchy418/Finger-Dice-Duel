using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class BoardController
    {
        private bool TryOfferWarBlessingNine()
        {
            if(SelectedHand==null||_selectedTarget==null)return false;
            return TryOfferWarBlessingNine(_game.PreviewOperationResult(ActivePlayer,SelectedHand,_selectedTarget),ContinueSelectedOperation);
        }
        private bool TryOfferWarBlessingNine(int value,System.Action resume)
        {
            if(_game.WarBlessingNineAttack.HasValue||_game.WillSuppressOperationEffects(ActivePlayer)
                ||!ActivePlayer.HasStatus(StatusEffect.WarBlessing)||ActivePlayer.IsPolymorphed
                ||value!=9||CharacterBehaviors.DisplayedEffectOf(ActivePlayer,value)!=HandEffectType.Heal)return false;
            var actor=ActivePlayer;
            var request=new WizardChoice(actor,"战恩 · 9选择照常回复，或放弃回复改为攻击",DivineChoices.NineOptions(),v=>_game.WarBlessingNineAttack=v==1);
            if(!QueueWizardChoice(request))
            {request.ChooseOption(SkillChoicePolicy.Choose(DivineChoices.Nine,request.Options,actor,DefaultAttackTarget(),_game));return false;}
            TryStartWizardBoundary(resume);return true;
        }
        private void ContinueGambleAfterRoll()
        {
            var effect=_game.OperationEffect(ActivePlayer,_pendingGambleRoll);
            bool attack=DivineChoices.IsAttack(effect);
            _pendingIsAttack=attack;
            if(!attack||_game.WillSuppressOperationEffects(ActivePlayer))
            {_pendingIsAttack=false;_selectedTarget=SelectedHand;ExecuteResolvedTurn();return;}
            Phase=BoardPhase.ChoosingTargetPlayer;
            if(!BeginTargetPlayerChoice()){NoteGambleGaveUp();GiveUpAttack();}
            else Hint=$"掷出{_pendingGambleRoll}，请点击攻击目标的手牌。";
        }
        private void ContinueSelectedOperation()
        {
            PrepareOperationDisplay();
            _pendingIsAttack=DruidChoiceContext(ActivePlayer,TargetPlayer??Defender,SelectedHand,_selectedTarget).IsAttack;
            if(BeginPreTargetChoicesIfAny())return;
            if(TryEnterHealTargetChoice())return;
            if(!NeedsTargetAfterOperation()){CommitMove(_selectedTarget);return;}
            Phase=BoardPhase.ChoosingTargetPlayer;
            bool found=BeginTargetPlayerChoice();
            Hint=found?$"运算结果为 {_game.PreviewOperationResult(ActivePlayer,SelectedHand,_selectedTarget)}。请点击攻击目标的手牌，确认本次攻击。":"没有可攻击的目标。请点右侧【放弃攻击】。";
        }
    }
}
