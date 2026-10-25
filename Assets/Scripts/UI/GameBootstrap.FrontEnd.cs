using System;
using UnityEngine;
using UnityEngine.UI;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBootstrap
    {
        private Image _frontBackground;
        private GameObject _frontMenuPanel;
        private Text _identityDetail;

        private void BuildFrontEndDecoration(Image background)
        {
            _frontBackground = background;
            var cover = background.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = 16f / 9f;
            var frame = FrontEndArt.Image(_selectRoot, "FrontEndFrame", "UI/screen_frame", Vector2.zero, Vector2.zero);
            FrontEndArt.Stretch(frame.rectTransform, 14);
            _frontMenuPanel = FrontEndArt.Image(_selectRoot, "MenuPanel", "UI/panel_content", new Vector2(0, -15), new Vector2(1020, 910)).gameObject;
        }

        private void ApplyFrontEndScreen(GameScreen screen)
        {
            bool select = screen == GameScreen.AdventureFinaleSelect || screen == GameScreen.CharacterSelect || screen == GameScreen.TwoOnTwoCharacter ||
                screen == GameScreen.FactionSelect || screen == GameScreen.IdentitySelect || screen == GameScreen.DualGeneralSelect;
            bool dice = screen == GameScreen.DiceRoll || screen == GameScreen.TwoOnTwoInitiative;
            bool submenu = !select && !dice && screen != GameScreen.MainMenu && screen != GameScreen.Codex && screen != GameScreen.InMatch;
            _frontMenuPanel.SetActive(submenu);
            string key = screen == GameScreen.MainMenu ? "menu_candle_chamber" : screen == GameScreen.Codex ?
                "codex_scribes_desk" : select || dice ? "selection_oath_hall" : "menu_moonlit_archive";
            FrontEndArt.Apply(_frontBackground, "Backgrounds/" + key, false);
            _frontBackground.preserveAspect = false;
            if (_modal?.Root != null) _modal.Root.transform.SetAsLastSibling();
            if (screen == GameScreen.IdentitySelect) RefreshIdentityArt();
        }

        private void BuildSkinnedMainMenu()
        {
            _screenMainMenu = new ScreenGroup();
            var title = FrontEndArt.Text(_selectRoot, "MainTitle", "手指骰子对决", new Vector2(0, 350), new Vector2(1200, 156), 106, title: true);
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.04f, 0.02f, 0, 0.8f);
            shadow.effectDistance = new Vector2(2, -2);
            _screenMainMenu.Add(title.gameObject);
            _screenMainMenu.Add(FrontEndArt.Text(_selectRoot, "SubTitle", "F I N G E R   D I C E   D U E L", new Vector2(0, 252), new Vector2(1100, 44), 26).gameObject);
            _screenMainMenu.Add(FrontEndArt.Image(_selectRoot, "MainDivider", "UI/divider_horizontal", new Vector2(0, 202), new Vector2(740, 24)).gameObject);
            _screenMainMenu.Add(CreateArtModeButton(_selectRoot, "ModeAdventure", new Vector2(0, 90), "冒险模式", "边境哨站 · 十场训练", () => ShowScreen(GameScreen.Adventure), "adventure").gameObject);
            _screenMainMenu.Add(CreateArtModeButton(_selectRoot, "ModeCompetition", new Vector2(0, -73), "竞技模式", "对战：1v1 / 2v2 / 8 人", () => ShowScreen(GameScreen.Competition), "competition").gameObject);
            _screenMainMenu.Add(CreateArtModeButton(_selectRoot, "ModeCasual", new Vector2(0, -236), "娱乐模式", "双将等变体玩法", () => ShowScreen(GameScreen.Casual), "casual").gameObject);
            Footer("Codex", "角色图鉴", "codex", -375, OnCodexClicked);
            Footer("MagicOrb", "伤害与状态", "arcane_orb", -125, OnMagicOrbClicked);
            Footer("Help", "规则说明", "rules", 125, OnHelpClicked);
            Footer("Quit", "退出游戏", "quit", 375, QuitGame);
        }

        private void Footer(string name, string label, string icon, float x, Action action)
        {
            var button = FrontEndArt.Button(_selectRoot, name, label, new Vector2(x, -405), new Vector2(235, 58), fontSize: 22);
            FrontEndArt.Image(button.transform, "Icon", "Icons/" + icon, new Vector2(-83, 0), new Vector2(30, 30), false);
            var text = button.GetComponentInChildren<Text>();
            text.rectTransform.anchoredPosition = new Vector2(20, 0);
            text.rectTransform.sizeDelta = new Vector2(178, 48);
            button.onClick.AddListener(() => action());
            _screenMainMenu.Add(button.gameObject);
        }

        private Button CreateArtModeButton(RectTransform parent, string name, Vector2 pos, string title, string subtitle, Action action, string icon = "competition")
        {
            var button = FrontEndArt.Button(parent, name, "", pos, new Vector2(740, 132), "mode_button");
            var label = button.GetComponentInChildren<Text>();
            label.font = UiFont.Title;
            label.fontSize = 36;
            label.text = title;
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.sizeDelta = new Vector2(510, 54);
            label.rectTransform.anchoredPosition = new Vector2(80, 23);
            FrontEndArt.Image(button.transform, "ModeIllustration", "Illustrations/mode_" + icon, new Vector2(-270, 0), new Vector2(118, 118), false);
            FrontEndArt.Text(button.transform, "Sub", subtitle, new Vector2(80, -29), new Vector2(510, 40), 21, TextAnchor.MiddleLeft);
            button.onClick.AddListener(() => action?.Invoke());
            return button;
        }

        private void Place(string name, float x, float y, float width = 0, float height = 0)
        {
            var rect = _selectRoot.Find(name) as RectTransform;
            if (rect == null) throw new InvalidOperationException("Missing front-end node " + name);
            rect.anchoredPosition = new Vector2(x, y);
            if (width > 0) rect.sizeDelta = new Vector2(width, height);
            var text = rect.GetComponent<Text>();
            if (text != null && text.fontSize >= 40) { text.font = UiFont.Title; text.fontStyle = FontStyle.Normal; }
        }

        private void DetailLayout(Text text, float x, float y, float width, float height, bool emblem = false)
        {
            var panel = text.transform.parent as RectTransform;
            panel.anchoredPosition = new Vector2(x, y);
            panel.sizeDelta = new Vector2(width, height);
            FrontEndArt.Apply(panel.GetComponent<Image>(), "UI/panel_detail");
            text.rectTransform.sizeDelta = new Vector2(width - 116, height - 24);
            text.fontSize = 20;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = 20;
            text.alignment = TextAnchor.MiddleLeft;
            if (emblem)
            {
                FrontEndArt.Emblem(panel, "SelectedEmblem", "fighter_champion", new Vector2(0, height / 2 + 170), new Vector2(290, 290));
                FrontEndArt.Image(panel, "SelectedDivider", "UI/divider_horizontal", new Vector2(0, height / 2 + 10), new Vector2(width - 30, 24));
            }
        }

        private void ConfigureFrontEndSelections()
        {
            // 两侧各三列大徽记，纵向拖动 / 滚轮浏览完整名单。
            Place("玩家角色", -410, 365);
            Place("AI 角色", 410, 365);
            _rosterViewA.ConfigureGrid(new Vector2(-410, 70), new Vector2(780, 540), 3);
            _rosterViewB.ConfigureGrid(new Vector2(410, 70), new Vector2(780, 540), 3);
            DetailLayout(_detailA, -410, -280, 780, 130);
            DetailLayout(_detailB, 410, -280, 780, 130);
            Place("Start", 230, -415); Place("SelectBack", -230, -415);

            Place("SeatPicker", 0, 375, 1400, 100);
            Place("TtSeatSummary", 0, 286, 1600, 68);
            Place("我的角色", -410, 230);
            Place("队友角色（人机）", 410, 230);
            _rosterView2Me.ConfigureGrid(new Vector2(-410, -25), new Vector2(780, 450), 3);
            _rosterView2Mate.ConfigureGrid(new Vector2(410, -25), new Vector2(780, 450), 3);
            foreach (string name in new[] { "玩家角色", "AI 角色", "我的角色", "队友角色（人机）" })
                _selectRoot.Find(name).GetComponent<Text>().text = name + " · 职业 → 子职";
            DetailLayout(_detail2Me, -410, -320, 780, 120);
            DetailLayout(_detail2Mate, 410, -320, 780, 120);
            Place("TtFoes", 0, -404, 1600, 34);
            Place("TtStart", 230, -465); Place("TtCharBack", -230, -465);

            ConfigureSingle(_fbRoster, "FbTitle", "FbStatus", _fbDetail, "FbPicked", "FbStart", "FbBack");
            ConfigureSingle(_dgRoster, "DgTitle", "DgStatus", _dgDetail, "DgPicked", "DgStart", "DgBack");
            Place("DgReset", 270, -452, 220, 68);
            Place("IdTitle", 0, 460); Place("IdStatus", 0, 378, 1600, 48);
            Place("IdSeats", 0, 324, 1660, 48);
            _idRoster.ConfigureGrid(new Vector2(-260, -40), new Vector2(1100, 650));
            _identityDetail = MakeDetailPanel("IdentityDetail", new Vector2(600, -160));
            DetailLayout(_identityDetail, 600, -160, 430, 235, true);
            _screenIdentitySelect.Add(_identityDetail.transform.parent.gameObject);
            Place("IdPicked", 600, -345, 450, 44);
            Place("IdStart", 600, -452); Place("IdBack", -650, -452);
            _idRoster.SelectionChanged += index => RefreshIdentityArt();
            RefreshDetail(_detailA, _roster[_indexA]);
            RefreshDetail(_detailB, _roster[_indexB]);
            RefreshFactionSelect();
            RefreshDualSelect();
            foreach (var title in _selectRoot.GetComponentsInChildren<Text>(true))
                if (title.fontSize >= 40 && title.name != "Face") { title.font = UiFont.Title; title.fontStyle = FontStyle.Normal; }
        }

        private void ConfigureSingle(CharacterRosterView roster, string title, string status, Text detail, string picked, string start, string back)
        {
            Place(title, 0, 460); Place(status, 0, 370, 1600, 48);
            roster.ConfigureGrid(new Vector2(-260, -40), new Vector2(1100, 650));
            DetailLayout(detail, 600, -160, 430, 235, true);
            Place(picked, 600, -345, 450, 48);
            Place(start, 600, -452); Place(back, -650, -452);
        }

        private void RefreshIdentityArt()
        {
            if (_identityDetail != null && _idHumanPick != null) RefreshDetail(_identityDetail, _idHumanPick);
        }

        private void ResetProfessionNavigation(GameScreen screen)
        {
            if (screen == GameScreen.CharacterSelect) { _rosterViewA?.ShowProfessions(); _rosterViewB?.ShowProfessions(); }
            else if (screen == GameScreen.TwoOnTwoCharacter) { _rosterView2Me?.ShowProfessions(); _rosterView2Mate?.ShowProfessions(); }
            else if (screen == GameScreen.FactionSelect) _fbRoster?.ShowProfessions();
            else if (screen == GameScreen.IdentitySelect) _idRoster?.ShowProfessions();
            else if (screen == GameScreen.DualGeneralSelect) _dgRoster?.ShowProfessions();
            else if (screen == GameScreen.AdventureFinaleSelect) _finaleRoster?.ShowProfessions();
        }
    }
}
