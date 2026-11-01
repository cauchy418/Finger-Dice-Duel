using System;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class BoardController
    {
        public Action<PlayerState> BeforeAutomaticAction {get;set;}
        public Func<PlayerState,bool> TryAutomaticCampaignAction {get;set;}
        public Func<PlayerState,string> CampaignActionLabel {get;set;}
        public Func<PlayerState,bool> PerformCampaignAction {get;set;}
        public event Action<PlayerState> CampaignActionSettled;
        private PlayerState _settlingCampaignAction;
        public string CurrentCampaignAction => Phase==BoardPhase.ChoosingAttackerHand && !CollegeOfValorChoicePending
            && PendingWizardChoice==null && StatusRules.CanAct(ActivePlayer) && !_game.IsGameOver
            ?CampaignActionLabel?.Invoke(ActivePlayer):null;
        public bool UseCampaignAction()
        {
            var label=CurrentCampaignAction;
            if(string.IsNullOrEmpty(label)||PerformCampaignAction?.Invoke(ActivePlayer)!=true)return false;
            return CompleteCampaignAction(label);
        }
        private bool CompleteCampaignAction(string label)
        {
            _log.Add(CombatText.Skill(ActivePlayer,label,"消耗本次正常行动"));
            _statusTurnEndFor=ActivePlayer;_settlingCampaignAction=ActivePlayer;
            _game.SkipTurnAlreadyValidated(extraAction:_extraActionActive);CompletedActions++;
            SelectedHand=null;_selectedTarget=null;_chosenDefenseShield=null;_shieldChoices.Clear();
            if(_extraActionActive)BeginNextExtraActionOrResume();else BeginTurn();return true;
        }
        private void SettleCampaignAction()
        {var actor=_settlingCampaignAction;_settlingCampaignAction=null;if(actor!=null)CampaignActionSettled?.Invoke(actor);}
    }
}
