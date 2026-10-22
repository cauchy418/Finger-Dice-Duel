using System;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private Button _seatSwapButton;
        public event Action SeatSwapClicked;

        public void SetSeatSwapActionVisible(bool visible)
        {
            if (_seatSwapButton==null && visible)
            {
                _seatSwapButton=CreateButton(_canvasRoot,"SeatSwap",Vector2.zero,new Vector2(250,76),"王车易位 · 跳过回合");
                _seatSwapButton.onClick.AddListener(()=>SeatSwapClicked?.Invoke());
            }
            if (_seatSwapButton!=null) _seatSwapButton.gameObject.SetActive(visible);
            LayoutBattleActions();
        }
    }
}
