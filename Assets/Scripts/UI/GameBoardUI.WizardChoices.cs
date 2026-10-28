using System;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private RectTransform _wizardModal;
        private RectTransform _wizardPanel;
        private Text _wizardTitle;
        private readonly Button[] _wizardButtons=new Button[16];
        private readonly Image[] _wizardIcons=new Image[16];
        private Button _wizardDecline;

        public void SetWizardChoices(BoardController controller, Action changed, Func<StatusEffect, bool> allowManeuver = null)
        {
            bool resistance=controller.IsHumanChooser && controller.PendingEffect==HandEffect.SchoolOfAbjurationResistanceType;
            bool maneuver=controller.IsHumanChooser && controller.PendingEffect==HandEffect.BattleMasterManeuver;
            var maneuvers=maneuver?controller.BattleMasterManeuverOptions:null;
            if(maneuver&&(maneuvers==null||maneuvers.Length==0)) maneuver=false;
            bool guidedManeuver=false;
            if(maneuver&&allowManeuver!=null)
            {
                guidedManeuver=!allowManeuver(StatusEffect.None);
                foreach(var status in maneuvers)if(!allowManeuver(status))guidedManeuver=true;
            }
            var request=controller.IsHumanChooser?controller.PendingWizardChoice:null;
            var options=controller.PendingSkillOptions;
            bool skill=controller.IsHumanChooser && options.Count>0;
            // 回合边界的选手／选人请求也走战场交互：手牌直接点，角色由技能栏列出。
            // 不能让全屏遮罩挡住风暴额外运算、不死奴仆、大地行者等候选手。
            if(request!=null && !skill)
            {
                if(_wizardModal!=null) _wizardModal.gameObject.SetActive(false);
                SetAcceptButtonVisible(false);
                SetDeclineButtonVisible(request.AllowDecline);
                if(request.AllowDecline) SetDeclineButtonLabel(request.DeclineLabel);
                return;
            }
            bool visible=resistance || maneuver || request!=null || skill;
            if(!visible) {if(_wizardModal!=null) _wizardModal.gameObject.SetActive(false);return;}
            if(_wizardModal==null) BuildWizardModal();
            _wizardModal.gameObject.SetActive(true);_wizardModal.SetAsLastSibling();
            _wizardTitle.text=resistance?"法术抗性 · 选择类型":maneuver?"卓越战技 · 选择状态":skill?controller.Hint:request.Prompt;
            int count=resistance?ResistanceNames.Choices.Length:maneuver?maneuvers.Length:skill?options.Count:request.Candidates.Count;
            bool painted=resistance||maneuver||skill;
            int columns=(resistance||skill&&count>4)?3:Mathf.Min(2,count);
            float panelHeight=skill?Mathf.Ceil(count/(float)columns)*156+180:resistance?480:maneuver?380:Mathf.Max(280,Mathf.Ceil(count/2f)*102+168);
            _wizardPanel.sizeDelta=new Vector2(skill?columns*230+100:resistance?820:maneuver?620:count==1?620:990,panelHeight);
            _wizardTitle.rectTransform.sizeDelta=new Vector2(_wizardPanel.sizeDelta.x-78,66);
            _wizardTitle.rectTransform.anchoredPosition=new Vector2(0,panelHeight/2-47);
            _wizardDecline.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-panelHeight/2+44);
            for(int i=0;i<_wizardButtons.Length;i++)
            {
                var button=_wizardButtons[i];button.gameObject.SetActive(i<count);
                if(i>=count) continue;
                int index=i;
                var rect=(RectTransform)button.transform;
                rect.sizeDelta=new Vector2(painted?218:428,painted?144:88);
                rect.anchoredPosition=new Vector2((i%columns-(columns-1)*0.5f)*(painted?230:450),
                    (skill?panelHeight/2-145:resistance?75:maneuver?10:panelHeight/2-130)-(i/columns)*(painted?156:102));
                button.onClick.RemoveAllListeners();
                button.interactable=true;
                var label=button.GetComponentInChildren<Text>();
                label.rectTransform.anchoredPosition=new Vector2(0,painted?-45:0);
                label.rectTransform.sizeDelta=new Vector2(rect.sizeDelta.x-22,painted?34:74);
                _wizardIcons[i].gameObject.SetActive(painted);
                if(skill)
                {
                    var option=options[index];_wizardIcons[i].sprite=BattleArt.Get(option.IconKey);
                    label.text=option.Label;
                    button.onClick.AddListener(()=> {controller.ChooseSkillOption(option.Value);changed();});
                    BattleTooltip.Set(button.GetComponent<Image>(),option.Description);
                }
                else if(resistance)
                {
                    var type=ResistanceNames.Choices[index];
                    _wizardIcons[i].sprite=BattleArt.Get(ResistanceNames.Key(type));
                    label.text=index==0?"物理整组（3类）":ResistanceNames.Name(type);
                    button.onClick.AddListener(()=> {controller.ChooseResistanceType(type);changed();});
                    BattleTooltip.Set(button.GetComponent<Image>(),index==0?"同时获得挥砍、钝击、穿刺抗性。":"获得"+ResistanceNames.Name(type));
                }
                else if(maneuver)
                {
                    // 战士·战斗大师：两个招式状态按钮（图标与状态图标栏同源）。
                    var status=maneuvers[index];
                    button.interactable=allowManeuver==null || allowManeuver(status);
                    if(guidedManeuver)TutorialButton(button,button.interactable);else ClearTutorialOutline(button);
                    _wizardIcons[i].sprite=BattleArt.Get("Status/"+StatusNames.IconKeyOf(status));
                    label.text=StatusNames.Of(status);
                    button.onClick.AddListener(()=> {if(allowManeuver==null || allowManeuver(status)) controller.ChooseBattleMasterManeuver(status);changed();});
                    BattleTooltip.Set(button.GetComponent<Image>(),StatusNames.Describe(status));
                }
                else
                {
                    var hand=request.Candidates[index];label.text=controller.WizardHandLabel(hand);
                    BattleTooltip.Set(button.GetComponent<Image>(),label.text);
                    button.onClick.AddListener(()=> {controller.ChooseWizardHand(hand);changed();});
                }
            }
            _wizardDecline.gameObject.SetActive(!skill && (maneuver||(!resistance && request!=null && request.AllowDecline)));
            if(!resistance && !skill) _wizardDecline.GetComponentInChildren<Text>().text=maneuver?"不附加":request.DeclineLabel;
            _wizardDecline.onClick.RemoveAllListeners();
            _wizardDecline.interactable=!maneuver || allowManeuver==null || allowManeuver(StatusEffect.None);
            if(maneuver){if(guidedManeuver)TutorialButton(_wizardDecline,_wizardDecline.interactable);else ClearTutorialOutline(_wizardDecline);}
            _wizardDecline.onClick.AddListener(()=> {
                if(maneuver) {if(allowManeuver==null || allowManeuver(StatusEffect.None)) controller.DeclineBattleMasterManeuver();}
                else controller.ChooseWizardHand(null);
                changed();});
            // 模态窗口接管全部本次抉择，避免底层留有重复入口。
            SetAcceptButtonVisible(false);SetDeclineButtonVisible(false);HideSkillChoices();
        }
        private void BuildWizardModal()
        {
            var backdrop=new GameObject("WizardChoiceModal",typeof(Image));backdrop.transform.SetParent(_canvasRoot,false);
            _wizardModal=backdrop.GetComponent<RectTransform>();
            _wizardModal.anchorMin=Vector2.zero;_wizardModal.anchorMax=Vector2.one;_wizardModal.sizeDelta=Vector2.zero;
            backdrop.GetComponent<Image>().color=new Color(0,0,0,0.68f);
            var frame=new GameObject("Panel",typeof(Image));frame.transform.SetParent(_wizardModal,false);
            _wizardPanel=frame.GetComponent<RectTransform>();_wizardPanel.sizeDelta=new Vector2(990,680);
            BattleArt.Apply(frame.GetComponent<Image>(),"UI/panel_tooltip",true);
            _wizardTitle=WizardText(_wizardPanel,"Title",new Vector2(0,287),new Vector2(904,66),24);
            _wizardTitle.font=UiFont.Title;
            for(int i=0;i<_wizardButtons.Length;i++)
            {
                var go=new GameObject("WizardOption"+i,typeof(Image),typeof(Button));go.transform.SetParent(_wizardPanel,false);
                _wizardButtons[i]=go.GetComponent<Button>();
                WizardText(go.GetComponent<RectTransform>(),"Label",Vector2.zero,new Vector2(200,40),20);
                BattleArt.Button(_wizardButtons[i]);
                var icon=new GameObject("Icon",typeof(Image));icon.transform.SetParent(go.transform,false);
                _wizardIcons[i]=icon.GetComponent<Image>();_wizardIcons[i].raycastTarget=false;_wizardIcons[i].preserveAspect=true;
                _wizardIcons[i].rectTransform.sizeDelta=new Vector2(94,94);_wizardIcons[i].rectTransform.anchoredPosition=new Vector2(0,19);
            }
            var decline=new GameObject("WizardDecline",typeof(Image),typeof(Button));decline.transform.SetParent(_wizardPanel,false);
            var dr=decline.GetComponent<RectTransform>();dr.sizeDelta=new Vector2(260,56);dr.anchoredPosition=new Vector2(0,-288);
            WizardText(dr,"Label",Vector2.zero,new Vector2(240,46),21).text="结束选择 / 不发动";
            _wizardDecline=decline.GetComponent<Button>();BattleArt.Button(_wizardDecline);
        }
        private static Text WizardText(RectTransform parent,string name,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject(name,typeof(Text));go.transform.SetParent(parent,false);
            var text=go.GetComponent<Text>();text.font=UiFont.Regular;text.fontSize=fontSize;text.color=BattleArt.Gold;
            text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=15;text.resizeTextMaxSize=fontSize;
            text.rectTransform.anchoredPosition=position;text.rectTransform.sizeDelta=size;return text;
        }
    }
}
