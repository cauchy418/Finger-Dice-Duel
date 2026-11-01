using System;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private Button _campaignAction;
        public void ShowCampaignAction(string label,Action action=null)
        {
            if(_campaignAction==null&&label!=null)_campaignAction=FrontEndArt.Button(_canvasRoot,"CampaignAction",label,new Vector2(795,280),new Vector2(236,48),fontSize:22);
            if(_campaignAction==null)return;
            _campaignAction.gameObject.SetActive(label!=null);_campaignAction.onClick.RemoveAllListeners();
            if(label==null)return;
            bool ring=!_paintedBattle&&_ringCount>2;
            Position((RectTransform)_campaignAction.transform,new Vector2(ring?820:795,280),new Vector2(ring?230:236,48));
            _campaignAction.GetComponentInChildren<Text>().text=label;
            _campaignAction.onClick.AddListener(()=>action?.Invoke());TutorialButton(_campaignAction,true);_campaignAction.transform.SetAsLastSibling();
        }
        public void SetCampaignObstacleMoved(bool moved)
        {
            if(_adventureObstaclesRoot==null||!_adventureObstaclesRoot.gameObject.activeSelf)return;
            var stone=(RectTransform)_adventureObstaclesRoot.Find("LeftStone");
            stone.anchoredPosition=new Vector2(moved?-690:-440,moved?225:245);
            stone.Find("Title").GetComponent<Text>().text=moved?"石头 · 已推至场边":"左侧石头 · 占位";
            if(moved)_adventureObstaclesRoot.Find("DistanceLesson").GetComponent<Text>().text="一侧路障已移开 · 双方最短距离1";
        }
    }
}
