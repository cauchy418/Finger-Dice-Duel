using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using UnityEngine;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBootstrap
    {
        private void ShowCampaignSkillDialogue(CampaignLesson lesson)
        {
            if(lesson.Version3)
            {
                if(lesson.UsesClassicSetup)
                    _board.ShowAdventureIntro("交锋之前",string.Join("\n",CampaignGuideText.For(lesson.LevelNumber)),lesson.GuideAvatarKey,lesson.GuideName,
                        ContinueAdventureIntro,ReturnToAdventure,null,"开始投骰");
                else ContinueAdventureIntro();
                return;
            }
            var pages=new List<string>();var seen=new HashSet<string>();
            foreach(var p in lesson.Seats)
            {
                if(ReferenceEquals(p,lesson.Player))continue;
                string skill=p.Character is CampaignNpcLogic npc?npc.AbilitySummary:SkillHintText(p.Definition);
                if(string.IsNullOrEmpty(skill)||!seen.Add(skill))continue;
                pages.Add(p.Name+"：\n「"+skill+"」");
            }
            if(pages.Count==0)pages.Add(lesson.GuideName+"：看清对手的手值，用攻击、合盾和回复应对他的行动。");
            ShowCampaignSkillDialoguePage(lesson,pages,0);
        }
        private void ShowCampaignSkillDialoguePage(CampaignLesson lesson,List<string> pages,int index)
        {
            if(!ReferenceEquals(_adventure,lesson))return;
            _board.ShowAdventureIntro("交锋之前",pages[index],lesson.GuideAvatarKey,lesson.GuideName,
                ()=>{if(index+1<pages.Count)ShowCampaignSkillDialoguePage(lesson,pages,index+1);else ContinueAdventureIntro();},
                ReturnToAdventure,null,index+1<pages.Count?"继续对话":lesson.UsesClassicSetup?"开始投骰":"开始交锋");
        }
        private void StartCampaignLevel(int level,bool launchFinale)
        {
            if(level<1||level>40)throw new ArgumentOutOfRangeException(nameof(level));
            _storyChapter=(level-1)/10+1;
            if(!CampaignUnlocked(_storyChapter)){OpenStoryChapter(_storyChapter);return;}
            CleanupMatch();
            if(level%10==0&&!launchFinale){ConfigureFinaleScreen();ShowScreen(GameScreen.AdventureFinaleSelect);return;}
            var role=level%10==0?SelectedFinaleRoster[Mathf.Clamp(_finaleIndex,0,SelectedFinaleRoster.Count-1)]:null;
            var lesson=CampaignLesson.CreateV3(level,role);
            AttachCampaign(lesson);
            StartPreparedMatch(beginTurn:!lesson.UsesClassicSetup);SetCampaignPresentation(lesson);
            _board.HideAdventureNarration();
            _board.ShowAdventureIntro(lesson.DisplayTitle,lesson.OpeningText,lesson.GuideAvatarKey,lesson.GuideName,
                OnAdventureIntroConfirmed,ReturnToAdventure,
                lesson.AllowsTutorialSkip&&PlayerPrefs.GetInt(lesson.CompletionKey,0)!=0?(Action)OnAdventureSkipTutorial:null,
                lesson.UsesClassicSetup?"学习先攻":"跟随指引");
        }
        private void AttachCampaign(CampaignLesson lesson)
        {
            _campaignTrialPrompt=false;
            _adventure=lesson;_game=lesson.Game;_controller=lesson.Board;
            _pendingDefA=lesson.Player.Definition;_pendingDefB=lesson.Seats[1].Definition;
            _choiceA=_game.ChoiceProviderA as SimpleChoiceProvider;_choiceB=_game.ChoiceProviderB as SimpleChoiceProvider;
            _choiceC=_game.ChoiceProviderC as SimpleChoiceProvider;_choiceD=_game.ChoiceProviderD as SimpleChoiceProvider;
            _sinkByPlayer.Clear();foreach(var p in lesson.Seats)_sinkByPlayer[p]=_game.GetChoiceProvider(p) as SimpleChoiceProvider;
            _controller.AiResolver=new AiChoiceResolver(ProviderFor);
            _isTwoOnTwo=lesson.Seats.Length==4;_twoOnTwoSeated=_isTwoOnTwo?new List<PlayerState>(lesson.Seats):null;
            _twoOnTwoMeIndex=0;_twoOnTwoMySeat=1;
            _isEightPlayer=lesson.Seats.Length==8;_eightSeated=_isEightPlayer?new List<PlayerState>(lesson.Seats):null;
            _eightHumanPlayer=_isEightPlayer?lesson.Player:null;
            _isIdentity=false;_isDualGeneral=false;_adventureVictoryShown=false;_adventureEndReady=false;
        }
        private void SetCampaignPresentation(CampaignLesson lesson)
        {
            _board.SetTitle(lesson.DisplayTitle);_board.SetBattleBackground(lesson.BattleBackgroundKey);
            _board.ShowAdventureObstacles(lesson.LevelNumber==4||lesson.LevelNumber==7||lesson.LevelNumber==11,lesson.LevelNumber==7?2:lesson.LevelNumber==11?99:1);
            _board.SetCampaignObstacleMoved(lesson.ObstacleMoved);
            _board.ShowCampaignAction(lesson.Started?lesson.Board.CurrentCampaignAction:null,()=>{if(_controller.UseCampaignAction())RefreshView();});
        }
        private void BeginCampaignInitiative()
        {
            var lesson=(CampaignLesson)_adventure;
            _twoOnTwoRoster=new List<PlayerState>(lesson.Seats);_twoOnTwoMeIndex=0;
            _twoOnTwoStart=TwoOnTwoStartController.RollAndBegin(_twoOnTwoRoster,new[]{0,1,1,0},new System.Random());
            _screenTwoOnTwoInitiative.SetHuman(lesson.Player);_board.SetVisible(false);
            ShowScreen(GameScreen.TwoOnTwoInitiative);_screenTwoOnTwoInitiative.Begin(_twoOnTwoStart);
            _screenTwoOnTwoInitiative.SetLesson(lesson.DisplayTitle+" · 经典先攻",ReturnToAdventure);
        }
        private void FinishCampaignInitiative()
        {
            var lesson=(CampaignLesson)_adventure;var order=new List<PlayerState>();
            for(int i=1;i<=4;i++)order.Add(_twoOnTwoStart.PlayerAtSeat(i));
            _game.InitializeSeatOrder(order);_twoOnTwoSeated=order;_twoOnTwoMeIndex=order.IndexOf(lesson.Player);_twoOnTwoMySeat=_twoOnTwoMeIndex+1;
            _controller.SetFirstPlayer(order[0]);lesson.BeginTutorial();StartPreparedMatch();SetCampaignPresentation(lesson);
        }
        private void SaveCampaignResult(CampaignLesson lesson)
        {
            if(!lesson.IsComplete)return;
            PlayerPrefs.SetInt(lesson.CompletionKey,1);
            if(lesson.Version3)
            {
                if(lesson.LevelNumber%10==0)PlayerPrefs.SetInt(lesson.Chapter.CompletionKey,1);
                PlayerPrefs.Save();return;
            }
            if(lesson.LevelNumber==10)PlayerPrefs.SetInt(AdventureChapter.CompletionKey,1);
            if(lesson.LevelNumber==20)PlayerPrefs.SetInt(MageTowerChapter.CompletionKey,1);
            if(lesson.LevelNumber==30)PlayerPrefs.SetInt(TatteredBannersChapter.CompletionKey,1);
            PlayerPrefs.Save();
        }
        private void ShowCampaignResult(CampaignLesson lesson)
        {
            Action next=null;string label=null;
            if(lesson.Version3)
            {
                if(lesson.IsComplete)
                {
                    int chapter=lesson.Chapter.Number;
                    if(lesson.LevelNumber%10==0&&chapter<4){next=()=>ShowCampaignTransition(chapter);label="前往"+CampaignChapter.At(chapter+1).Name;}
                    else if(lesson.LevelNumber<40){next=()=>StartAdventureLevel(lesson.LevelNumber+1);label="继续"+AdventureChapter.BattleName(lesson.LevelNumber%10+1);}
                }
                else if(lesson.CanRetryTrial){next=()=>RetryCampaignTrial(lesson);label="再次试炼";}
                _board.ShowAdventureVictory((lesson.IsComplete?lesson.ResultHeading:_game.Winner==null?"试炼平局":"试炼失利")+" · "+lesson.DisplayTitle,
                    lesson.IsComplete?lesson.SummaryText:lesson.FailureText,lesson.GuideAvatarKey,
                    OnAdventureReplay,ReturnToAdventure,next,label,lesson.UsesClassicSetup?"重新投骰":"重新指引",lesson.GuideName);
                if(lesson.LevelNumber%10==0)_board.SetAdventureExtraAction(()=>{CleanupMatch();ConfigureFinaleScreen();ShowScreen(GameScreen.AdventureFinaleSelect);},"重新选择子职");
                return;
            }
            if(lesson.IsComplete)
            {
                if(lesson.LevelNumber==10){next=()=>ShowTowerTransition();label="前往法师高塔";}
                else if(lesson.LevelNumber==20){next=()=>ShowBannersTransition();label="前往残旗战场";}
                else if(lesson.LevelNumber<30){int n=lesson.LevelNumber+1;next=()=>StartAdventureLevel(n);label="继续"+AdventureChapter.BattleName((n-1)%10+1);}
            }
            _board.ShowAdventureVictory((lesson.IsComplete?lesson.ResultHeading:_game.Winner==null?"试炼平局":"试炼失利")+" · "+lesson.DisplayTitle,
                lesson.IsComplete?lesson.SummaryText:lesson.FailureText,lesson.GuideAvatarKey,
                OnAdventureReplay,ReturnToAdventure,next,label,lesson.UsesClassicSetup?"重新投骰":"重试整场",lesson.GuideName);
            if(lesson.LevelNumber%10==0)_board.SetAdventureExtraAction(()=>{CleanupMatch();ConfigureFinaleScreen();ShowScreen(GameScreen.AdventureFinaleSelect);},"重新选择子职");
        }
    }
}
