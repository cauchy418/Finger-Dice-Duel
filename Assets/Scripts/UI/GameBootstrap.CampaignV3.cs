using System;
using FingerDiceDuel.Assistant;
using UnityEngine;
using UnityEngine.UI;
namespace FingerDiceDuel.UI
{
    public sealed partial class GameBootstrap
    {
        private Text _galleryChapterStatus;
        private bool _campaignTrialPrompt;
        private const string CampaignMigrationKey="FingerDiceDuel.Adventure.V3.Migrated";
        private void MigrateCampaignProgress()
        {
            if(PlayerPrefs.GetInt(CampaignMigrationKey,0)!=0)return;
            var old=new bool[31];for(int n=1;n<=30;n++)old[n]=PlayerPrefs.GetInt("FingerDiceDuel.Adventure.Level"+n+".Completed",0)!=0;
            bool outpost=PlayerPrefs.GetInt(AdventureChapter.CompletionKey,0)!=0||old[10];
            bool tower=PlayerPrefs.GetInt(MageTowerChapter.CompletionKey,0)!=0||old[20];
            bool banners=PlayerPrefs.GetInt(TatteredBannersChapter.CompletionKey,0)!=0||old[30];
            for(int level=1;level<=40;level++)
            {int legacy=CampaignChapter.LegacyLevel(level);if(legacy>0&&old[legacy])PlayerPrefs.SetInt(CampaignChapter.LevelKey(level),1);}
            if(old[12])PlayerPrefs.SetInt("FingerDiceDuel.Adventure.V3.Archive.tower.evocation.Completed",1);
            if(outpost)PlayerPrefs.SetInt(CampaignChapter.Outpost.CompletionKey,1);
            if(tower)PlayerPrefs.SetInt(CampaignChapter.Tower.CompletionKey,1);
            if(banners)PlayerPrefs.SetInt(CampaignChapter.Banners.CompletionKey,1);
            // Legacy access is retained independently from newly inserted lessons.
            if(outpost)PlayerPrefs.SetInt("FingerDiceDuel.Adventure.V3.Access.tower",1);
            if(tower)PlayerPrefs.SetInt("FingerDiceDuel.Adventure.V3.Access.banners",1);
            PlayerPrefs.SetInt(CampaignMigrationKey,1);PlayerPrefs.Save();
        }
        private bool CampaignUnlocked(int chapter)
        {
            if(chapter==1)return true;
            if(PlayerPrefs.GetInt(CampaignChapter.At(chapter-1).CompletionKey,0)!=0)return true;
            return PlayerPrefs.GetInt("FingerDiceDuel.Adventure.V3.Access."+CampaignChapter.At(chapter).Id,0)!=0;
        }
        private void ShowCampaignTrial(CampaignLesson lesson)
        {
            if(_campaignTrialPrompt||!lesson.TrialConfirmationPending)return;
            _campaignTrialPrompt=true;
            _board.ShowAdventureIntro("实战试炼","接下来进入实战试炼，对手会恢复至最佳状态。",lesson.GuideAvatarKey,lesson.GuideName,
                ()=>{if(!ReferenceEquals(_adventure,lesson))return;_campaignTrialPrompt=false;lesson.ConfirmTrial();RefreshView();},ReturnToAdventure,null,"进入实战");
        }
        private void RetryCampaignTrial(CampaignLesson lesson)
        {
            if(!lesson.CanRetryTrial)return;
            var restored=lesson.RetryTrial();CleanupMatch();AttachCampaign(restored);
            _animatedTurn=restored.Board.LastTurn;
            StartPreparedMatch(beginTurn:false);SetCampaignPresentation(restored);RefreshView();
        }
        private void ShowCampaignTransition(int chapter,int page=0)
        {
            string[] words=chapter==1?new[]{"哨站骑士：你的战技已得到认可，但刀剑并不能解释所有战场上的事。","老兵：向北去吧，高塔里的典籍会回答你；不过在踏进那道门之前，先穿过万象回廊。","哨站骑士：那里留着元素的回声，认得它们，你才不会在塔中被第一缕光骗过。","老兵：带上这枚通行印，愿你看清每一道伤痕的来处。"}:chapter==2?new[]{"回廊铭文：你已经能听懂万象的回声。","回廊铭文：高塔的大门就在前方，去学会塑造、守护和改变这份力量。"}:new[]{"秘法典籍：你已认识力量，也学会了改变它的方向；可战场并不总为你留下一对一的空地。","残旗军士长：带上你学会的本领，来看看怎样护住身旁的同袍。"};
            var next=CampaignChapter.At(chapter+1);var guide=CampaignChapter.At(chapter);
            _board.ShowAdventureIntro("通往"+next.Name,words[page],guide.Avatar,guide.Guide,
                ()=>{if(page+1<words.Length)ShowCampaignTransition(chapter,page+1);else{CleanupMatch();OpenStoryChapter(chapter+1);}},
                ()=>{CleanupMatch();ShowScreen(GameScreen.StoryChapterSelect);},null,page+1<words.Length?"继续对话":"前往"+next.Name);
        }
    }
}
