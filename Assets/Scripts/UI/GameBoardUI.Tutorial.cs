using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private Button _trialEndButton;
        private bool _adventureGuideCompact;
        public void ShowTrialEndButton(bool visible,System.Action clicked=null)
        {
            if(_trialEndButton==null&&visible)
                _trialEndButton=FrontEndArt.Button(_canvasRoot,"TrialEnd","试炼结束",new Vector2(795,350),new Vector2(236,48),fontSize:24);
            if(_trialEndButton==null)return;
            _trialEndButton.gameObject.SetActive(visible);_trialEndButton.onClick.RemoveAllListeners();
            if(visible){_trialEndButton.onClick.AddListener(()=>clicked?.Invoke());TutorialButton(_trialEndButton,true);_trialEndButton.transform.SetAsLastSibling();}
        }
        /// <summary>普通战斗传null恢复可用状态；教程的规则在控制器再次校验。</summary>
        public void SetTutorialChoices(MageTowerLesson lesson)
        {
            var input=lesson?.Board.TutorialInput;
            TutorialButton(_acceptButton,input==null?(bool?)null:input.AllowsAccept);
            TutorialButton(_declineButton,input==null?(bool?)null:input.AllowsDecline,lesson?.Board.Phase!=BoardPhase.ChoosingSeatSwapTarget);
            TutorialButton(_seatSwapButton,input==null?(bool?)null:input.AllowsCastling);
            if(input!=null)TutorialButton(_skipButton,input.AllowsSkip);
            else ClearTutorialOutline(_skipButton);
            for(int i=0;i<_skillChoiceButtons.Length;i++)
                TutorialButton(_skillChoiceButtons[i],input==null?(bool?)null:input.AllowsHand(_skillHands[i]));
            var controller=lesson?.Board;
            for(int i=0;i<_wizardButtons.Length;i++)
            {
                bool? allowed=null;
                if(input!=null&&Visible(_wizardButtons[i]))
                {
                    if(controller.PendingSkillOptions.Count>i)allowed=input.AllowsOption(controller.PendingSkillOptions[i].Value);
                    else if(controller.PendingEffect==HandEffect.SchoolOfAbjurationResistanceType&&i<ResistanceNames.Choices.Length)
                        allowed=input.AllowsResistance(ResistanceNames.Choices[i]);
                }
                if(input!=null)TutorialButton(_wizardButtons[i],allowed);else ClearTutorialOutline(_wizardButtons[i]);
            }
            if(input!=null)TutorialButton(_wizardDecline,input.AllowsDecline);else ClearTutorialOutline(_wizardDecline);
            _damageTypePanel?.SetTutorialFilter(input==null?(System.Func<DamageType,bool>)null:input.AllowsDamageType);
        }
        public static void TutorialButton(Button button,bool? allowed,bool highlight=true)
        {
            if(button==null)return;
            button.interactable=allowed??true;
            var outline=button.GetComponent<Outline>();
            if(allowed==true&&highlight)
            {
                if(outline==null)outline=button.gameObject.AddComponent<Outline>();
                outline.effectColor=new Color(1f,.83f,.34f,1);outline.effectDistance=new Vector2(3,3);outline.useGraphicAlpha=false;outline.enabled=true;
            }
            else if(outline!=null)outline.enabled=false;
        }
        private static void ClearTutorialOutline(Button button)
        {if(button!=null&&button.GetComponent<Outline>() is Outline outline)outline.enabled=false;}

        public void ConfigureAdventureGuide(bool compact)
        {
            _adventureGuideCompact=compact;
            _adventurePanel?.SetCompact(compact);
            if(compact)
            {
                Position(_battleHint,new Vector2(0,-82),new Vector2(1160,44));
                _hintText.rectTransform.sizeDelta=new Vector2(1100,38);_hintText.fontSize=17;
            }
        }
    }
}
