using System.Collections;
using System.Collections.Generic;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBootstrap
    {
        private static readonly string[] FactionNames = { "守序", "中立", "混乱" };
        private ScreenGroup _screenClassic, _screenDualDuelStyle, _screenSeatDraw;
        private Button _soloStart, _teamStart, _drawContinue;
        private Button _draftSelfSlot, _draftMateSlot;
        private bool _pickingDraftMate;
        private Text _drawTitle, _drawHint;
        private MatchDrawCards _seatDrawCards, _identityDraftCards, _factionDraftCards, _dualDraftCards;
        private Coroutine _seatFlip, _characterFlip;
        private int _drawnCard = -1;
        private List<int> _identityDrawSeats;

        private List<CharacterDefinition> _classicCandidates, _classicAiCandidates;
        private CharacterDefinition _classicSelf, _classicMate, _classicFoe, _classicFoe2;
        private List<CharacterDefinition> _dualCandidates;
        private CharacterDefinition[] _dualChallengeFoes;
        private List<CharacterDefinition>[] _factionDeals, _identityDeals;

        private static List<CharacterDefinition> Deal(List<CharacterDefinition> pool, int count)
        {
            var candidates = pool.GetRange(0, count);
            pool.RemoveRange(0, count);
            return candidates;
        }

        private List<CharacterDefinition> ShuffledRoster(System.Random rng)
        {
            var pool = new List<CharacterDefinition>(_roster);
            Shuffle(pool, rng);
            return pool;
        }

        private void BuildDraftMenus()
        {
            _screenClassic = BuildPlayStyleMenu("ClassicStyle", "经典玩法", GameScreen.ModeMenu, StartClassicSelection);
            _screenDualDuelStyle = BuildPlayStyleMenu("DualStyle", "双将人机对决", GameScreen.DualGeneralMenu, StartDualSelection);

            _screenSeatDraw = new ScreenGroup();
            _drawTitle = MakeSubTitle(_selectRoot, "SeatDrawTitle", "抽取阵营");
            _screenSeatDraw.Add(_drawTitle.gameObject);
            _seatDrawCards = new MatchDrawCards(_selectRoot, "SeatDrawCards", new Vector2(0, 56), DrawSeatCard);
            _screenSeatDraw.Add(_seatDrawCards.Root.gameObject);
            _drawHint = FrontEndArt.Text(_selectRoot, "SeatDrawHint", "", new Vector2(0, -95), new Vector2(1200, 64), 27);
            _screenSeatDraw.Add(_drawHint.gameObject);
            _drawContinue = FrontEndArt.Button(_selectRoot, "SeatDrawContinue", "亮出候选", new Vector2(180, -198), new Vector2(300, 64));
            _drawContinue.onClick.AddListener(OpenDrawCandidates);
            _screenSeatDraw.Add(_drawContinue.gameObject);
            var back = FrontEndArt.Button(_selectRoot, "SeatDrawBack", "返回", new Vector2(-180, -198), new Vector2(240, 64));
            back.onClick.AddListener(() => ShowScreen(_screen == GameScreen.IdentityDraw ? GameScreen.IdentityBattleMenu
                : _dualFactionMode ? GameScreen.DualFactionMenu : GameScreen.FactionBattleMenu));
            _screenSeatDraw.Add(back.gameObject);
        }

        private ScreenGroup BuildPlayStyleMenu(string id, string title, GameScreen parent, System.Action<bool> select)
        {
            var group = new ScreenGroup();
            group.Add(MakeSubTitle(_selectRoot, id + "Title", title).gameObject);
            group.Add(MakeSubModeButton(_selectRoot, id + "Practice", new Vector2(0, 60),
                "演练", "按现有方式自由选将", () => select(false)).gameObject);
            group.Add(MakeSubModeButton(_selectRoot, id + "Challenge", new Vector2(0, -96),
                "挑战", "从随机候选中选将", () => select(true)).gameObject);
            group.Add(MakeBackButton(_selectRoot, parent).gameObject);
            return group;
        }

        private void StartClassicSelection(bool challenge)
        {
            Mode = PlayMode.Classic;
            _classicCandidates = null;
            if (challenge)
            {
                int count = _isTwoOnTwo ? 8 : 6;
                var rng = new System.Random();
                var pool = ShuffledRoster(rng);
                _classicCandidates = Deal(pool, count);
                _classicAiCandidates = Deal(pool, count);
                _classicSelf = _classicMate = null;
                _pickingDraftMate = false;
                _classicFoe = _classicAiCandidates[0];
                _classicFoe2 = _isTwoOnTwo ? _classicAiCandidates[1] : null;
            }
            GoToCharacterSelect();
        }

        private void ConfigureClassicCandidates()
        {
            bool challenge = _classicCandidates != null;
            if (_isTwoOnTwo)
            {
                _selectRoot.Find("TtCharTitle").GetComponent<Text>().text = challenge ? "经典挑战 · 2 v 2 选将" : "2 v 2 角色选择";
                _selectRoot.Find("我的角色").GetComponent<Text>().text = challenge ? "共享候选 · 分配给自己和队友" : "我的角色 · 职业 → 子职";
                _selectRoot.Find("队友角色（人机）").GetComponent<Text>().text = challenge ? "队伍配置" : "队友角色（人机） · 职业 → 子职";
                _rosterView2Me.ConfigureGrid(new Vector2(-410, -25), new Vector2(780, 450), challenge ? 4 : 3);
                _rosterView2Mate.ConfigureGrid(new Vector2(410, -25), new Vector2(780, 450), challenge ? 4 : 3);
                _teamStart.interactable = !challenge;
                if (challenge) _detail2Me.text = _detail2Mate.text = "尚未选择角色";
                else
                {
                    _rosterView2Me.SetRoster(_roster, _indexA);
                    _rosterView2Mate.SetRoster(_roster, _indexMate);
                    RefreshDetail(_detail2Me, _roster[_indexA]);
                    RefreshDetail(_detail2Mate, _roster[_indexMate]);
                }
                return;
            }

            _selectTitle.text = challenge ? "经典挑战 · 选择角色" : "选择角色";
            _selectRoot.Find("玩家角色").GetComponent<Text>().text = challenge ? "玩家角色 · 请选择" : "玩家角色 · 职业 → 子职";
            _selectRoot.Find("AI 角色").GetComponent<Text>().text = challenge ? "AI 候选 · 随机结果已高亮" : "AI 角色 · 职业 → 子职";
            _rosterViewA.SetRoster(_classicCandidates ?? _roster, challenge ? -1 : _indexA, challenge);
            _rosterViewB.SetRoster(challenge ? _classicAiCandidates : _roster, challenge ? 0 : _indexB, challenge);
            var aiInput = _rosterViewB.Root.GetComponent<CanvasGroup>();
            if (aiInput == null) aiInput = _rosterViewB.Root.gameObject.AddComponent<CanvasGroup>();
            aiInput.interactable = !challenge;
            _soloStart.interactable = !challenge;
            if (challenge)
            {
                RefreshDetail(_detailB, _classicFoe);
                _detailA.text = "尚未选择角色";
            }
            else
            {
                RefreshDetail(_detailA, _roster[_indexA]);
                RefreshDetail(_detailB, _roster[_indexB]);
            }
        }

        private bool SelectClassicCandidate(int index, bool mate)
        {
            if (_classicCandidates == null) return false;
            var chosen = _classicCandidates[index];
            var view = _isTwoOnTwo ? _rosterView2Me : _rosterViewA;
            if (ReferenceEquals(chosen, mate ? _classicSelf : _classicMate))
            {
                view.SetRoster(_classicCandidates, _classicCandidates.IndexOf(mate ? _classicMate : _classicSelf), flat: true);
                return true;
            }
            if (mate) _classicMate = chosen;
            else _classicSelf = chosen;
            RefreshDetail(mate ? _detail2Mate : _isTwoOnTwo ? _detail2Me : _detailA, chosen);
            _soloStart.interactable = _classicSelf != null;
            _teamStart.interactable = _classicSelf != null && _classicMate != null;
            if (_isTwoOnTwo)
            {
                if (!mate && _classicMate == null) _pickingDraftMate = true;
                RefreshDraftSlots();
            }
            return true;
        }

        private void BuildDraftSlots()
        {
            _draftSelfSlot = FrontEndArt.Button(_selectRoot, "DraftSelfSlot", "", new Vector2(410, 120), new Vector2(620, 78));
            _draftMateSlot = FrontEndArt.Button(_selectRoot, "DraftMateSlot", "", new Vector2(410, 16), new Vector2(620, 78));
            _draftSelfSlot.onClick.AddListener(() => { _pickingDraftMate = false; RefreshDraftSlots(); });
            _draftMateSlot.onClick.AddListener(() => { _pickingDraftMate = true; RefreshDraftSlots(); });
            _screenTwoOnTwoCharacter.Add(_draftSelfSlot.gameObject);
            _screenTwoOnTwoCharacter.Add(_draftMateSlot.gameObject);
        }

        private void RefreshDraftSlots()
        {
            bool challenge = _classicCandidates != null;
            _draftSelfSlot.gameObject.SetActive(challenge);
            _draftMateSlot.gameObject.SetActive(challenge);
            _rosterView2Mate.Root.gameObject.SetActive(!challenge);
            if (!challenge) return;
            _draftSelfSlot.GetComponentInChildren<Text>().text = (_pickingDraftMate ? "自己" : "正在选择自己") + " · " + (_classicSelf?.DisplayName ?? "未选");
            _draftMateSlot.GetComponentInChildren<Text>().text = (_pickingDraftMate ? "正在选择队友" : "队友") + " · " + (_classicMate?.DisplayName ?? "未选");
            _rosterView2Me.SetRoster(_classicCandidates, _classicCandidates.IndexOf(_pickingDraftMate ? _classicMate : _classicSelf), flat: true);
        }

        private void StartDualSelection(bool challenge)
        {
            _dualFactionMode = false;
            _dualCandidates = null;
            _dualChallengeFoes = null;
            if (challenge)
            {
                var rng = new System.Random();
                var pool = ShuffledRoster(rng);
                _dualCandidates = Deal(pool, 6);
                var foes = Deal(pool, 6);
                var pairs = new List<CharacterDefinition[]>();
                for (int i = 1; i < foes.Count; i++)
                    for (int j = 0; j < i; j++)
                        if (DualGeneralFactory.SameAxis(foes[j], foes[i]))
                            pairs.Add(new[] { foes[j], foes[i] });
                _dualChallengeFoes = pairs[rng.Next(pairs.Count)];
            }
            ResetDualSelection();
            ShowScreen(GameScreen.DualGeneralSelect);
        }

        private void BeginFactionDraw(bool dual)
        {
            _dualFactionMode = dual;
            _dualCandidates = null;
            _dualChallengeFoes = null;
            var rng = new System.Random();
            int smallFaction = rng.Next(3);
            var poolByFaction = new List<CharacterDefinition>[3];
            for (int f = 0; f < 3; f++) poolByFaction[f] = new List<CharacterDefinition>();
            foreach (var role in ShuffledRoster(rng)) poolByFaction[FactionOf(role)].Add(role);
            for (int f = 0; f < 3; f++)
                if (poolByFaction[f].Count < (f == smallFaction ? 10 : 15))
                { ShowComingSoon("该阵营的角色不足以分配本局候选。"); return; }

            var deals = new List<List<CharacterDefinition>>(8);
            for (int f = 0; f < 3; f++)
            {
                int seats = f == smallFaction ? 2 : 3;
                for (int seat = 0; seat < seats; seat++) deals.Add(Deal(poolByFaction[f], 5));
            }
            Shuffle(deals, rng);
            _factionDeals = deals.ToArray();
            ShowSeatDraw(GameScreen.FactionDraw, "抽取阵营");
        }

        private void ShowSeatDraw(GameScreen screen, string title)
        {
            _drawnCard = -1;
            _drawTitle.text = title;
            _drawHint.text = "选择一张牌翻开";
            _drawContinue.interactable = false;
            _seatDrawCards.ResetScale();
            for (int i = 0; i < MatchDrawCards.Count; i++) _seatDrawCards.Back(i, true, true);
            ShowScreen(screen);
        }

        private void DrawSeatCard(int index)
        {
            if (_drawnCard >= 0) return;
            _drawnCard = index;
            for (int i = 0; i < MatchDrawCards.Count; i++) _seatDrawCards.Back(i, true, false);

            Sprite mark;
            string label;
            if (_screen == GameScreen.IdentityDraw)
            {
                _idHumanSeat = _identityDrawSeats[index];
                var role = _idRoles[_idHumanSeat];
                mark = MatchMarks.Identity(role);
                label = IdentityRoleInfo.DisplayName(role);
            }
            else
            {
                var chosen = _factionDeals[index];
                _factionDeals[index] = _factionDeals[0];
                _factionDeals[0] = chosen;
                int faction = FactionOf(chosen[0]);
                mark = MatchMarks.Alignment(faction);
                label = FactionNames[faction];
            }
            _seatFlip = StartCoroutine(FlipSeatCard(index, mark, label));
        }

        private IEnumerator FlipSeatCard(int index, Sprite mark, string label)
        {
            yield return _seatDrawCards.Flip(index, () => _seatDrawCards.Face(index, mark, label, false, true));
            _drawHint.text = "抽中 · " + label;
            _drawContinue.interactable = true;
            _seatFlip = null;
        }

        private void OpenDrawCandidates()
        {
            if (!_drawContinue.interactable || _drawnCard < 0) return;
            if (_screen == GameScreen.IdentityDraw)
            {
                PrepareIdentityCandidates();
                _idHumanPick = null;
                _idIndex = -1;
                RefreshIdentitySelect();
                ShowScreen(GameScreen.IdentitySelect);
                RevealCharacters(_identityDraftCards, CurrentIdentityList(), RefreshIdentitySelect);
            }
            else if (_dualFactionMode)
            {
                _dualCandidates = _factionDeals[0];
                ResetDualSelection();
                ShowScreen(GameScreen.DualGeneralSelect);
                RevealCharacters(_dualDraftCards, _dualCandidates, RefreshDualSelect);
            }
            else
            {
                _fbIndex = -1;
                RefreshFactionSelect();
                ShowScreen(GameScreen.FactionSelect);
                RevealCharacters(_factionDraftCards, _factionDeals[0], RefreshFactionSelect);
            }
        }

        private void RevealCharacters(MatchDrawCards cards, IReadOnlyList<CharacterDefinition> candidates, System.Action refresh)
        {
            cards.ResetScale();
            for (int i = 0; i < MatchDrawCards.Count; i++) cards.Back(i, i < candidates.Count, false);
            _characterFlip = StartCoroutine(FlipCharacters(cards, candidates, refresh));
        }

        private IEnumerator FlipCharacters(MatchDrawCards cards, IReadOnlyList<CharacterDefinition> candidates, System.Action refresh)
        {
            yield return cards.Flip(0, () => RenderCharacterCards(cards, candidates, -1, false), candidates.Count);
            _characterFlip = null;
            refresh();
        }

        private static void RenderCharacterCards(MatchDrawCards cards, IReadOnlyList<CharacterDefinition> candidates,
            int selected, bool clickable = true, CharacterDefinition primary = null, CharacterDefinition secondary = null)
        {
            for (int i = 0; i < MatchDrawCards.Count; i++)
            {
                if (candidates == null || i >= candidates.Count) { cards.Back(i, false, false); continue; }
                var candidate = candidates[i];
                bool picked = i == selected || ReferenceEquals(candidate, primary) || ReferenceEquals(candidate, secondary);
                var emblem = BattleArt.Get("Subclasses/" + (candidate.Id == "blank" ? "fighter_champion" : candidate.Id));
                string label = candidate.DisplayName.Replace(" · ", "\n").Replace("·", "\n");
                cards.Face(i, emblem, label, clickable && !(secondary == null && ReferenceEquals(candidate, primary)), picked);
            }
        }

        private void StopDraftAnimations()
        {
            if (_seatFlip != null) { StopCoroutine(_seatFlip); _seatFlip = null; _seatDrawCards.ResetScale(); }
            if (_characterFlip == null) return;
            StopCoroutine(_characterFlip);
            _characterFlip = null;
            _identityDraftCards.ResetScale();
            _factionDraftCards.ResetScale();
            _dualDraftCards.ResetScale();
        }
    }
}
