using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBootstrap
    {
        private Text _bannersChapterStatus;
        private bool BannersUnlocked=>PlayerPrefs.GetInt(MageTowerChapter.CompletionKey,0)!=0
            ||PlayerPrefs.GetInt("FingerDiceDuel.Adventure.Level20.Completed",0)!=0;
        private void StartBannersLevel(int level,bool launchFinale=false,int sampleIndex=-1)
        {
            if(!BannersUnlocked){OpenStoryChapter(3);return;}
            CleanupMatch();_storyChapter=3;
            if(level==30&&!launchFinale){ConfigureFinaleScreen();ShowScreen(GameScreen.AdventureFinaleSelect);return;}
            if(sampleIndex<0)
            {
                sampleIndex=0;var samples=TatteredBannersChapter.Samples(level);
                if(PlayerPrefs.GetInt("FingerDiceDuel.Adventure.Level"+level+".Completed",0)==0)
                    for(int i=0;i<samples.Count;i++)if(PlayerPrefs.GetInt(TatteredBannersChapter.SampleKey(level,samples[i].Key),0)==0){sampleIndex=i;break;}
            }
            var role=level==30?SelectedFinaleRoster[Mathf.Clamp(_finaleIndex,0,SelectedFinaleRoster.Count-1)]:null;
            var lesson=TatteredBannersLesson.Create(level,sampleIndex,role);
            BindBannersMatch(lesson);
            StartPreparedMatch(beginTurn:!lesson.IsFinale);SetBannersPresentation(lesson);
            _board.HideAdventureNarration();
            _board.ShowAdventureIntro(lesson.DisplayTitle,lesson.OpeningText,lesson.GuideAvatarKey,lesson.GuideName,
                OnAdventureIntroConfirmed,ReturnToAdventure,null,lesson.IsFinale?"学习2v2先攻":"开始演练");
        }
        private void BindBannersMatch(TatteredBannersLesson lesson)
        {
            _adventure=lesson;_game=lesson.Game;_controller=lesson.Board;
            _pendingDefA=lesson.Player.Definition;_pendingDefB=lesson.Soldier.Definition;
            _choiceA=_game.ChoiceProviderA as SimpleChoiceProvider;_choiceB=_game.ChoiceProviderB as SimpleChoiceProvider;
            _choiceC=_game.ChoiceProviderC as SimpleChoiceProvider;_choiceD=_game.ChoiceProviderD as SimpleChoiceProvider;
            _sinkByPlayer.Clear();foreach(var p in lesson.Seats)_sinkByPlayer[p]=_game.GetChoiceProvider(p) as SimpleChoiceProvider;
            _controller.AiResolver=new AiChoiceResolver(ProviderFor);
            int me=Array.IndexOf(lesson.Seats,lesson.Player);
            _isTwoOnTwo=lesson.Seats.Length==4;_twoOnTwoSeated=_isTwoOnTwo?new List<PlayerState>(lesson.Seats):null;
            _twoOnTwoMeIndex=me;_twoOnTwoMySeat=me+1;
            _isEightPlayer=lesson.Seats.Length==8;_eightSeated=_isEightPlayer?new List<PlayerState>(lesson.Seats):null;
            _eightHumanPlayer=_isEightPlayer?lesson.Player:null;
            _isIdentity=false;_isDualGeneral=false;_adventureVictoryShown=false;_adventureEndReady=false;
        }
        private void SetBannersPresentation(TatteredBannersLesson lesson)
        {
            string relations="";
            if(lesson.Seats.Length==8)
            {
                int allies=0,enemies=0;foreach(var p in lesson.Seats)if(p.IsAlive&&!ReferenceEquals(p,lesson.Player))
                {if(lesson.Game.IsTeammate(lesson.Player,p))allies++;else enemies++;}
                relations="　队友"+allies+"人／对手"+enemies+"人";
            }
            _board.SetTitle(lesson.DisplayTitle+" · "+lesson.Sample.Title+relations);
            _board.SetBattleBackground(lesson.BattleBackgroundKey);
        }
        private void BeginBannersInitiative()
        {
            var lesson=_adventure as TatteredBannersLesson;if(lesson==null||!lesson.IsFinale)return;
            _twoOnTwoRoster=new List<PlayerState>(lesson.Seats);_twoOnTwoMeIndex=Array.IndexOf(lesson.Seats,lesson.Player);
            _twoOnTwoStart=TwoOnTwoStartController.RollAndBegin(_twoOnTwoRoster,new[]{0,1,1,0},new System.Random());
            _screenTwoOnTwoInitiative.SetHuman(lesson.Player);_board.SetVisible(false);
            ShowScreen(GameScreen.TwoOnTwoInitiative);_screenTwoOnTwoInitiative.Begin(_twoOnTwoStart);
            _screenTwoOnTwoInitiative.SetLesson(lesson.DisplayTitle+" · 经典先攻",ReturnToAdventure);
        }
        private void FinishBannersInitiative()
        {
            var human=((TatteredBannersLesson)_adventure).Player;
            var seats=new PlayerState[4];for(int i=0;i<4;i++)seats[i]=_twoOnTwoStart.PlayerAtSeat(i+1);
            // ApplyToPlayers由真实先攻界面执行；重新建局只清技能，保留这四人的骰点手值。
            var lesson=TatteredBannersLesson.AttachFinale(seats,human);
            BindBannersMatch(lesson);lesson.BeginTutorial();StartPreparedMatch();SetBannersPresentation(lesson);
        }
        private void SaveBannersResult(TatteredBannersLesson lesson)
        {
            if(lesson.SamplePassed)PlayerPrefs.SetInt(TatteredBannersChapter.SampleKey(lesson.LevelNumber,lesson.Sample.Key),1);
            bool clear=lesson.IsComplete;
            if(!lesson.IsFinale)foreach(var sample in TatteredBannersChapter.Samples(lesson.LevelNumber))
                clear&=PlayerPrefs.GetInt(TatteredBannersChapter.SampleKey(lesson.LevelNumber,sample.Key),0)!=0;
            if(clear)
            {
                PlayerPrefs.SetInt(lesson.CompletionKey,1);
                if(lesson.IsFinale)PlayerPrefs.SetInt(TatteredBannersChapter.CompletionKey,1);
            }
            PlayerPrefs.Save();
        }
        private void ShowBannersResult(TatteredBannersLesson lesson)
        {
            Action next=null;string label=null;
            if(lesson.IsComplete)
            {
                if(!lesson.LastSample){next=()=>StartBannersLevel(lesson.LevelNumber,true,lesson.SampleIndex+1);label="下一演练 · 重新布置";}
                else if(!lesson.IsFinale){next=()=>StartBannersLevel(lesson.LevelNumber+1);label="继续"+AdventureChapter.BattleName(lesson.LevelNumber-19);}
                else {next=()=>{CleanupMatch();ConfigureFinaleScreen();ShowScreen(GameScreen.AdventureFinaleSelect);};label="重新选择圣武士";}
            }
            else if(lesson.IsFinale){next=()=>{CleanupMatch();ConfigureFinaleScreen();ShowScreen(GameScreen.AdventureFinaleSelect);};label="重新选择圣武士";}
            _board.ShowAdventureVictory((lesson.IsComplete?lesson.ResultHeading:"试炼未完成")+" · "+lesson.Sample.Title,
                lesson.IsComplete?lesson.SummaryText:lesson.FailureText,lesson.GuideAvatarKey,
                ()=>StartBannersLevel(lesson.LevelNumber,true,lesson.SampleIndex),ReturnToAdventure,next,label,
                lesson.IsFinale?"四人重新投骰":"重试本演练",lesson.GuideName);
        }
        private void ShowBannersTransition(int page=0)
        {
            string[] words={"你已经学会辨认力量，也学会了改变它的方向。但战场不会总为你留下一对一的空地。",
                "去残旗仍立的地方。那里的人会告诉你：同袍的数字可以成为你的刀，而你的盾，也未必只属于自己。"};
            _board.ShowAdventureIntro("通往残旗战场",words[page],"Adventure/guide_arcane_codex","秘法典籍",
                ()=>{if(page==0)ShowBannersTransition(1);else{CleanupMatch();OpenStoryChapter(3);}},
                ()=>{CleanupMatch();ShowScreen(GameScreen.StoryChapterSelect);},null,page==0?"继续对话":"前往残旗战场");
        }
    }
}
