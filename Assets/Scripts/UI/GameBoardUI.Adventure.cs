using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private RectTransform _adventureObstaclesRoot;

        /// <summary>关卡石头只有图像与说明，没有手牌模型或按钮；真实距离来自内核静态占位。</summary>
        public void ShowAdventureObstacles(bool visible, int playerRange = 1)
        {
            if (!visible && _adventureObstaclesRoot == null) return;
            if (_adventureObstaclesRoot == null)
            {
                var go = new GameObject("AdventureSeatObstacles", typeof(RectTransform));
                go.transform.SetParent(_canvasRoot, false);
                _adventureObstaclesRoot = go.GetComponent<RectTransform>();
                Position(_adventureObstaclesRoot, Vector2.zero, new Vector2(1920, 1080));
                for (int i = 0; i < 2; i++)
                {
                    var frame = ArtImage(_adventureObstaclesRoot, i == 0 ? "LeftStone" : "RightStone", "UI/panel_tooltip",
                        new Vector2(i == 0 ? -440 : 330, 245), new Vector2(220, 210), true);
                    var title = CreateText(frame.rectTransform, "Title", new Vector2(0, 78), new Vector2(200, 28),
                        20, TextAnchor.MiddleCenter, BattleArt.Gold);
                    title.text = i == 0 ? "左侧石头 · 占位" : "右侧石头 · 占位";
                    ArtImage(frame.rectTransform, "Stone", "Adventure/stone_obstacle", new Vector2(0, 5), new Vector2(118, 118)).preserveAspect = true;
                    var hands = CreateText(frame.rectTransform, "Hands", new Vector2(0, -68), new Vector2(200, 28),
                        20, TextAnchor.MiddleCenter, BattleArt.Ink);
                    hands.text = "左手 −    右手 −";
                    title.raycastTarget = hands.raycastTarget = false;
                }
                var distance = CreateText(_adventureObstaclesRoot, "DistanceLesson", new Vector2(-80, 82), new Vector2(900, 28),
                    21, TextAnchor.MiddleCenter, BattleArt.Gold);
                distance.text = "环形座位：你 → 石头 → 教官 → 石头    双方距离2 · 通常射程1";
                distance.raycastTarget = false;
            }
            _adventureObstaclesRoot.Find("DistanceLesson").GetComponent<Text>().text =
                "环形座位：你 → 石头 → 教官 → 石头    双方距离2 · 你的通常射程" + playerRange;
            _adventureObstaclesRoot.gameObject.SetActive(visible);
        }
    }
}
