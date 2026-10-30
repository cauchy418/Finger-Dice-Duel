using FingerDiceDuel.Assistant;
using UnityEngine;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBootstrap
    {
        private bool _adventureEndReady;
        private void PrepareAdventureResult()
        {
            if(_adventure==null||!_adventure.IsFinished||_adventureEndReady)return;
            _adventureEndReady=true;
            if(_adventure is MageTowerLesson tower)SaveTowerResult(tower);
            else if(_adventure is TatteredBannersLesson banners)SaveBannersResult(banners);
            else if(_adventure.IsComplete)
            {
                PlayerPrefs.SetInt(_adventure.CompletionKey,1);
                if(_adventure.LevelNumber==10)PlayerPrefs.SetInt(AdventureChapter.CompletionKey,1);
                PlayerPrefs.Save();
            }
            var completed=_adventure;
            _board.ShowTrialEndButton(true,()=>{
                if(!ReferenceEquals(completed,_adventure)||_adventureVictoryShown)return;
                _adventureVictoryShown=true;_board.ShowTrialEndButton(false);_board.SetHistoryOpen(false);_board.HideAdventureNarration();
                PresentAdventureResult();
            });
        }
        private void PresentAdventureResult()
        {
            if(_adventure is MageTowerLesson tower){ShowTowerResult(tower);return;}
            if(_adventure is TatteredBannersLesson banners){ShowBannersResult(banners);return;}
            string heading=_adventure.IsComplete?_adventure.ResultHeading:_game.Winner==null?"训练平局":"训练失利";
            int next=_adventure.LevelNumber+1;
            _board.ShowAdventureVictory(heading+" · "+_adventure.DisplayTitle,
                _adventure.IsComplete?_adventure.SummaryText:_adventure.FailureText,_adventure.GuideAvatarKey,
                OnAdventureReplay,ReturnToAdventure,
                _adventure.IsComplete&&next==11?(System.Action)(()=>ShowTowerTransition()):_adventure.IsComplete&&next<=10?(System.Action)(()=>StartAdventureLevel(next)):null,
                next==11?"前往法师高塔":next<=10?"继续"+AdventureBattleName(next):null,
                _adventure.UsesClassicSetup?"重新投骰":"重打本关");
        }
    }
}
