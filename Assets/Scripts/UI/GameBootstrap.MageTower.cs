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
        private int _storyChapter=1;
        private Text _storyHeading;
        private readonly Button[] _storyBattleButtons=new Button[10];
        private Text _towerChapterStatus;
        private bool TowerUnlocked => PlayerPrefs.GetInt(AdventureChapter.CompletionKey,0)!=0
            || PlayerPrefs.GetInt("FingerDiceDuel.Adventure.Level10.Completed",0)!=0;
        private IReadOnlyList<CharacterDefinition> SelectedFinaleRoster => _storyChapter==2?MageTowerChapter.FinaleRoster:AdventureChapter.FinaleRoster;
        private void OpenStoryChapter(int chapter)
        {
            if(chapter==2&&!TowerUnlocked){ShowModalWithScrollReset("法师高塔尚未开放","请先完成边境哨站·终战，取得高塔通行印。");return;}
            _storyChapter=chapter;ShowScreen(GameScreen.StoryBattleSelect);
        }
        private void StartMageTowerLevel(int level,bool launchFinale=false,int sampleIndex=-1)
        {
            if(!TowerUnlocked){OpenStoryChapter(2);return;}
            CleanupMatch();_storyChapter=2;
            if(level==20&&!launchFinale){ConfigureFinaleScreen();ShowScreen(GameScreen.AdventureFinaleSelect);return;}
            if(sampleIndex<0)
            {
                sampleIndex=0;
                var samples=MageTowerChapter.Samples(level);
                if(PlayerPrefs.GetInt("FingerDiceDuel.Adventure.Level"+level+".Completed",0)==0)
                    for(int i=0;i<samples.Count;i++)if(PlayerPrefs.GetInt(MageTowerChapter.SampleKey(level,samples[i].Key),0)==0){sampleIndex=i;break;}
            }
            CharacterDefinition role=level==20?SelectedFinaleRoster[Mathf.Clamp(_finaleIndex,0,SelectedFinaleRoster.Count-1)]:null;
            var lesson=MageTowerLesson.Create(level,sampleIndex,role);
            _adventure=lesson;_game=lesson.Game;_controller=lesson.Board;
            _pendingDefA=lesson.Player.Definition;_pendingDefB=lesson.Soldier.Definition;
            _choiceA=_game.ChoiceProviderA as SimpleChoiceProvider;_choiceB=_game.ChoiceProviderB as SimpleChoiceProvider;
            _controller.AiResolver=new AiChoiceResolver(ProviderFor);
            foreach(var p in lesson.Seats)_sinkByPlayer[p]=_game.GetChoiceProvider(p) as SimpleChoiceProvider;
            _isTwoOnTwo=lesson.Seats.Length==4;
            _twoOnTwoSeated=_isTwoOnTwo?new List<PlayerState>(lesson.Seats):null;
            _twoOnTwoMeIndex=0;_twoOnTwoMySeat=1;
            _isEightPlayer=false;_eightSeated=null;_isDualGeneral=false;
            _adventureVictoryShown=false;
            StartPreparedMatch(beginTurn:!lesson.UsesClassicSetup);
            _board.SetTitle(lesson.DisplayTitle+" · "+lesson.Sample.Title);
            _board.SetBattleBackground(lesson.BattleBackgroundKey);
            _board.HideAdventureNarration();
            _board.ShowAdventureIntro(lesson.DisplayTitle,lesson.OpeningText,lesson.GuideAvatarKey,lesson.GuideName,
                OnAdventureIntroConfirmed,ReturnToAdventure,null,lesson.UsesClassicSetup?"学习先攻":"开始试验");
        }

        private void SaveTowerResult(MageTowerLesson lesson)
        {
            if(lesson.SamplePassed)
                PlayerPrefs.SetInt(MageTowerChapter.SampleKey(lesson.LevelNumber,lesson.Sample.Key),1);
            bool clear=lesson.IsComplete&&(lesson.LevelNumber==20||lesson.LastSample);
            if(clear)
            {
                PlayerPrefs.SetInt(lesson.CompletionKey,1);
                if(lesson.LevelNumber==20)PlayerPrefs.SetInt(MageTowerChapter.CompletionKey,1);
            }
            PlayerPrefs.Save();
        }

        private void ShowTowerResult(MageTowerLesson lesson)
        {
            Action next=null;string nextLabel=null;
            if(lesson.IsComplete)
            {
                if(!lesson.LastSample)
                {next=()=>StartMageTowerLevel(lesson.LevelNumber,true,lesson.SampleIndex+1);nextLabel="下一试验 · 重置场景";}
                else if(lesson.LevelNumber<20)
                {next=()=>StartMageTowerLevel(lesson.LevelNumber+1);nextLabel="继续"+AdventureChapter.BattleName(lesson.LevelNumber-9);}
                else {next=()=>{CleanupMatch();ConfigureFinaleScreen();ShowScreen(GameScreen.AdventureFinaleSelect);};nextLabel="重新选择法师";}
            }
            string text=lesson.IsComplete?lesson.SummaryText:lesson.FailureText;
            if(lesson.LevelNumber<20&&!lesson.LastSample&&lesson.IsComplete)
                text+="\n下一试验会重置双方手值、体力与状态。已完成样本保留记录。";
            _board.ShowAdventureVictory((lesson.IsComplete?lesson.ResultHeading:"试验未完成")+" · "+lesson.Sample.Title,
                text,lesson.GuideAvatarKey,()=>StartMageTowerLevel(lesson.LevelNumber,true,lesson.SampleIndex),ReturnToAdventure,
                next,nextLabel,lesson.UsesClassicSetup?"重新投骰":"重试本试验",lesson.GuideName);
        }

        private void ShowTowerTransition(int page=0)
        {
            string[] words={"哨站骑士：你的战技已经得到认可。但战场上，还有一些事，不能只靠刀剑解释。",
                "老兵：记得那些会发光的伤口吗？有人隔着盾牌伤人，有人让一场攻击在发生前便失去意义。向北去，法师高塔会给你答案。",
                "哨站骑士：带上这枚通行印。到了塔里，先翻开那本等候访客的典籍。它比守门人更有耐心。"};
            _board.ShowAdventureIntro("通往法师高塔",words[page],page==1?AdventureLevel1.GuideAvatarKey:AdventureChapter.KnightEmblem,
                page==1?"老兵":"哨站骑士",()=>{if(page<2)ShowTowerTransition(page+1);else{CleanupMatch();OpenStoryChapter(2);}},
                ()=>{CleanupMatch();ShowScreen(GameScreen.StoryChapterSelect);},null,page==2?"前往法师高塔":"继续对话");
        }

        private void ConfigureFinaleScreen()
        {
            _finaleIndex=0;_finaleRoster.SetRoster(SelectedFinaleRoster,0);
            RefreshDetail(_finaleDetail,SelectedFinaleRoster[0]);
            bool tower=_storyChapter==2;
            _selectRoot.Find("FinaleTitle").GetComponent<Text>().text=tower?"法师高塔·终战 · 选择角色":"边境哨站·终战 · 选择角色";
            _selectRoot.Find("FinaleHint").GetComponent<Text>().text=tower?"仅限法师子职 · 经典规则挑战法师幻影":"仅限战士子职 · 按经典规则击败骑士即可通关";
            var foe=_selectRoot.Find("FinaleKnight");
            BattleArt.Apply(foe.Find("Emblem").GetComponent<Image>(),tower?"Adventure/npc_mage_phantom":AdventureChapter.KnightEmblem);
            foe.Find("Name").GetComponent<Text>().text=tower?"法师幻影":"哨站骑士";
            var stats=foe.Find("Stats").GetComponent<Text>();
            stats.fontSize=tower?22:25;
            stats.text=tower?"生命32  攻0 / 防0  距∞\n强酸攻击\n本场永久：火焰、寒冷抗性\n毒素免疫\n\n汲能回路：每3次普通攻击\n实际伤人后回复3生命":"生命20\n攻击+1  防御+1\n距离1\n\n经典先攻 · 正常AI\n胜利即第一章通关";
        }
    }
}
