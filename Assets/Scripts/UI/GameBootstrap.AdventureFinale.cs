using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBootstrap
    {
        private ScreenGroup _screenAdventureFinale;
        private CharacterRosterView _finaleRoster;
        private int _finaleIndex;
        private Text _finaleDetail;

        private void BuildAdventureFinaleScreen()
        {
            _screenAdventureFinale = new ScreenGroup();
            _screenAdventureFinale.Add(FrontEndArt.Text(_selectRoot, "FinaleTitle", "边境哨站·终战 · 选择角色",
                new Vector2(0, 440), new Vector2(1500, 65), 42, title: true).gameObject);
            _screenAdventureFinale.Add(FrontEndArt.Text(_selectRoot, "FinaleHint", "仅限战士子职 · 按经典规则击败骑士即可通关",
                new Vector2(0, 380), new Vector2(1500, 40), 23).gameObject);
            _finaleRoster = new CharacterRosterView();
            _finaleRoster.Build(_selectRoot, "FinaleRoster", new Vector2(-200, 35), AdventureChapter.FinaleRoster, UiFont.Regular, 0, SkillHintText);
            _finaleRoster.ConfigureGrid(new Vector2(-200, 35), new Vector2(1200, 600), 3, 260);
            _screenAdventureFinale.Add(_finaleRoster.Root.gameObject);
            _finaleDetail = MakeDetailPanel("FinaleDetail", new Vector2(-200, -330));
            _finaleDetail.rectTransform.sizeDelta = new Vector2(1140, 84);
            ((RectTransform)_finaleDetail.transform.parent).sizeDelta = new Vector2(1200, 104);
            _screenAdventureFinale.Add(_finaleDetail.transform.parent.gameObject);
            _finaleRoster.SelectionChanged += index => { _finaleIndex = index; RefreshDetail(_finaleDetail, AdventureChapter.FinaleRoster[index]); };
            RefreshDetail(_finaleDetail, AdventureChapter.FinaleRoster[0]);
            var foe = FrontEndArt.Image(_selectRoot, "FinaleKnight", "UI/panel_content", new Vector2(650, 10), new Vector2(390, 650));
            var emblem = FrontEndArt.Rect(foe.rectTransform, "Emblem", new Vector2(0, 160), new Vector2(270, 270)).gameObject.AddComponent<Image>();
            BattleArt.Apply(emblem, AdventureChapter.KnightEmblem); emblem.preserveAspect = true; emblem.raycastTarget = false;
            FrontEndArt.Text(foe.rectTransform, "Name", "哨站骑士", new Vector2(0, -5), new Vector2(350, 50), 34, title: true);
            FrontEndArt.Text(foe.rectTransform, "Stats", "生命20\n攻击+1  防御+1\n距离1\n\n经典先攻 · 正常AI\n胜利即第一章通关", new Vector2(0, -155), new Vector2(345, 240), 25);
            _screenAdventureFinale.Add(foe.gameObject);
            var start = FrontEndArt.Button(_selectRoot, "FinaleStart", "开始挑战", new Vector2(180, -445), new Vector2(310, 64));
            start.onClick.AddListener(() => StartAdventureLevel(10, true)); _screenAdventureFinale.Add(start.gameObject);
            var back = FrontEndArt.Button(_selectRoot, "FinaleBack", "返回章节", new Vector2(-220, -445), new Vector2(270, 64));
            back.onClick.AddListener(() => ShowScreen(GameScreen.StoryBattleSelect)); _screenAdventureFinale.Add(back.gameObject);
        }
    }
}
