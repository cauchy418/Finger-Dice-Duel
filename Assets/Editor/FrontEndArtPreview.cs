using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FingerDiceDuel.EditorTools
{
    /// <summary>真实编辑器内的全前台页面、点击路径和布局检查；输出引擎渲染截图。</summary>
    [InitializeOnLoad]
    public static class FrontEndArtPreview
    {
        private const string Pending = "FingerDiceDuel.FrontEndArtPreview.Pending";
        private static readonly Queue<Action> Steps = new Queue<Action>();
        private static readonly List<string> Checks = new List<string>();
        private static GameBootstrap _bootstrap;
        private static RectTransform _root;
        private static double _after;
        private static bool _prepared;
        private static bool _screenshotPending;
        private static int _frameRetries;
        private static bool _savedBackground;
        private static bool _pumpingFrame;
        private sealed class AwaitCanvasFrame : Exception { public AwaitCanvasFrame(string message) : base(message) { } }

        static FrontEndArtPreview() { EditorApplication.update += Update; }

        [MenuItem("Finger Dice Duel/Preview/All Front End Art")]
        public static void Run()
        {
            _prepared = false;
            EditorApplication.isPaused = false;
            SessionState.SetBool(Pending, true);
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            _after = EditorApplication.timeSinceStartup + 1.5;
        }

        private static void Update()
        {
            if (_pumpingFrame || !SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
                EditorApplication.isCompiling || EditorApplication.timeSinceStartup < _after) return;
            try
            {
                // 最小化窗口时只会运行 Editor update，协程可能没有真实游戏帧；显式推进引擎一帧。
                if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                {
                    _pumpingFrame = true;
                    try { EditorApplication.Step(); EditorApplication.isPaused = false; }
                    finally { _pumpingFrame = false; }
                }
                if (!_prepared)
                {
                    _bootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
                    if (_bootstrap == null) return;
                    _root = Field<RectTransform>(_bootstrap, "_selectRoot");
                    _savedBackground = Application.runInBackground;
                    Application.runInBackground = true;
                    Prepare();
                    _prepared = true;
                }
                if (Steps.Count > 0)
                {
                    Steps.Peek()();
                    Steps.Dequeue();
                    _frameRetries = 0;
                    EditorApplication.QueuePlayerLoopUpdate();
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    _after = EditorApplication.timeSinceStartup + 0.5;
                }
                else
                {
                    SessionState.SetBool(Pending, false);
                    Application.runInBackground = _savedBackground;
                    if (_screenshotPending) return;
                    File.WriteAllLines(Output("validation.txt"), Checks);
                    Debug.Log("[FrontEndArtPreview] " + Checks.Count + " checks passed. " + Output("validation.txt"));
                }
            }
            catch (AwaitCanvasFrame pending)
            {
                // 编辑器的离屏截图会临时切换 Canvas 模式；等真实渲染帧恢复深度和裁剪，再做射线断言。
                if (++_frameRetries <= 120)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    _after = EditorApplication.timeSinceStartup + 0.1;
                    return;
                }
                SessionState.SetBool(Pending, false);
                Application.runInBackground = _savedBackground;
                File.WriteAllLines(Output("validation.txt"), Checks.Concat(new[] { "FAILED canvas did not become clickable: " + pending.Message }));
                Debug.LogError(pending.Message);
            }
            catch (Exception error)
            {
                SessionState.SetBool(Pending, false);
                Application.runInBackground = _savedBackground;
                File.WriteAllLines(Output("validation.txt"), Checks.Concat(new[] { "FAILED " + error }));
                Debug.LogException(error);
            }
        }

        private static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Checks.Add("PASS " + message);
        }
        private static string Output(string file)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Art/Integration/front-end"));
            Directory.CreateDirectory(path);
            return Path.Combine(path, file);
        }
        private static Button Button(string name) => _root.GetComponentsInChildren<Button>()
            .Single(b => b.gameObject.activeInHierarchy && b.name == name);
        private static void Click(string name) { Click(Button(name)); }
        private static void Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            // 最小化的编辑器不重绘 Game View；用实际相机渲染当前页以建立 Graphic 深度。
            if (button.GetComponent<Image>().depth < 0) Capture(null, UnityEngine.Screen.width, UnityEngine.Screen.height);
            Check(button.interactable && button.gameObject.activeInHierarchy, "clickable " + button.name);
            var data = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            if (hits.Count == 0 || (hits[0].gameObject != button.gameObject && !hits[0].gameObject.transform.IsChildOf(button.transform)))
                throw new AwaitCanvasFrame(button.name + " at " + data.position + " hits " + string.Join(",", hits.Select(h => h.gameObject.name)) + " depth=" + button.GetComponent<Image>().depth + " culled=" + button.GetComponent<Image>().canvasRenderer.cull);
            Check(true, "unobstructed raycast " + button.name);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        private static void Screen(GameScreen screen) => Check(_bootstrap.CurrentScreen == screen, "navigation " + screen);
        private static void Add(Action action) { Steps.Enqueue(action); }
        private static void Frames(int count = 5) { for (int i = 0; i < count; i++) Add(() => { }); }
        private static void Shot(string file)
        {
            Add(() =>
            {
                Check(_root.gameObject.activeInHierarchy, "shared menu canvas stays active " + file);
                Capture(file + ".png");
            });
        }
        private static void Roster(string field, int expected)
        {
            var roster = Field<CharacterRosterView>(_bootstrap, field);
            Check(roster.Count == expected, field + " count " + expected);
            var bounds = Bounds(roster.Root);
            foreach (var card in roster.Root.GetComponentsInChildren<Button>())
            {
                if (!card.name.StartsWith("Card")) continue;
                var area = Bounds((RectTransform)card.transform);
                Check(area.xMin >= bounds.xMin && area.xMax <= bounds.xMax, field + " within column bounds " + card.name);
                Check(((RectTransform)card.transform.Find("Emblem")).sizeDelta.x >= 170, field + " enlarged emblem " + card.name);
                Check(card.transform.Find("Emblem").GetComponent<Image>().sprite != null, field + " painted " + card.name);
            }
            var scroll = roster.Root.GetComponent<ScrollRect>();
            Check(scroll.content.rect.height > scroll.viewport.rect.height, field + " roster scrolls vertically");
            scroll.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, roster.Root.position) };
            scroll.OnInitializePotentialDrag(pointer);
            scroll.OnBeginDrag(pointer);
            pointer.position += new Vector2(0, 220);
            scroll.OnDrag(pointer);
            scroll.OnEndDrag(pointer);
            Check(scroll.content.anchoredPosition.y > 50, field + " pointer drag moves roster");
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
        }

        private static void RevealRosterCard(CharacterRosterView roster, int index)
        {
            var scroll = roster.Root.GetComponent<ScrollRect>();
            var card = (RectTransform)roster.Root.Find("Content/Card" + index);
            float top = -card.anchoredPosition.y - card.rect.height / 2;
            float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(top / overflow);
            Canvas.ForceUpdateCanvases(); Capture(null);
        }

        private static void Prepare()
        {
            Checks.Clear(); Steps.Clear();
            _bootstrap.PlayerBIsAI = true;
            Add(() => { _bootstrap.ShowScreen(GameScreen.MainMenu); Screen(GameScreen.MainMenu); });
            Shot("MainMenu");
            Add(() => Click("Help"));
            Shot("Rules");
            Add(() =>
            {
                var modal = Field<ModalPanel>(_bootstrap, "_modal");
                Check(modal.Scroll.content.rect.height > modal.Scroll.viewport.rect.height, "rules body scrolls");
                modal.Scroll.verticalNormalizedPosition = 0;
            });
            Shot("RulesBottom");
            Add(() => Click("Close"));
            Add(() => Click("ModeAdventure")); Shot("Adventure");
            Add(() => Click("AdvPve")); Shot("ComingSoon");
            Add(() => Click("Close"));
            foreach (GameScreen screen in new[] { GameScreen.Competition, GameScreen.Casual, GameScreen.OneOnOneMenu,
                GameScreen.TwoOnTwoMenu, GameScreen.ModeMenu, GameScreen.DifficultyMenu, GameScreen.DualGeneral,
                GameScreen.DualGeneralMenu, GameScreen.DualFactionMenu, GameScreen.EightPlayerMenu,
                GameScreen.FactionBattleMenu, GameScreen.IdentityBattleMenu })
            {
                GameScreen target = screen;
                Add(() => { _bootstrap.ShowScreen(target); Screen(target); CheckMenuLayout(target.ToString()); });
                Shot(target.ToString());
                Add(() => { Click(_root.GetComponentsInChildren<Button>().Single(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>()?.text == "返回")); Check(_bootstrap.CurrentScreen != target, "return from " + target); });
            }
            Add(() => _bootstrap.ShowScreen(GameScreen.MainMenu));
            Add(() => { Click("ModeCompetition"); Screen(GameScreen.Competition); });
            Add(() => { Click("Comp1v1"); Screen(GameScreen.OneOnOneMenu); });
            Add(() => { Click("ModeAi"); Screen(GameScreen.ModeMenu); });
            Add(() => { Click("ModeIntro"); Screen(GameScreen.DifficultyMenu); });
            Add(() => { Click("DiffEasy"); Screen(GameScreen.CharacterSelect); });
            Shot("CharacterSelectInitial");
            Add(() =>
            {
                Roster("_rosterViewA", 24); Roster("_rosterViewB", 24);
                RevealRosterCard(Field<CharacterRosterView>(_bootstrap, "_rosterViewA"), 23);
                Click(Field<CharacterRosterView>(_bootstrap, "_rosterViewA").Root.Find("Content/Card23").GetComponent<Button>());
                RevealRosterCard(Field<CharacterRosterView>(_bootstrap, "_rosterViewB"), 20);
                Click(Field<CharacterRosterView>(_bootstrap, "_rosterViewB").Root.Find("Content/Card20").GetComponent<Button>());
                Check(Field<CharacterRosterView>(_bootstrap, "_rosterViewA").SelectedIndex == 23, "last character can be selected");
                Check(Field<Text>(_bootstrap, "_detailA").text.Contains("吟游诗人"), "selection detail reflects Bard");
            });
            Shot("CharacterSelect");
            Add(() => Click("SelectBack"));
            Add(() => Click("ModeAi"));
            Add(() => Click("ModeClassic"));
            Add(() => { Click("Start"); Screen(GameScreen.DiceRoll); });
            Shot("DiceRoll");
            Add(() => Click("RollButton"));
            Frames(6);
            Add(() =>
            {
                var screen = Field<DiceRollScreen>(_bootstrap, "_diceRoll");
                if (Field<bool>(screen, "_animating")) throw new AwaitCanvasFrame("1v1 dice animation still running");
                var controller = Field<MatchStartController>(screen, "_controller");
                Check(Field<D20DieView>(screen, "_dieA").Face == controller.RollA && Field<D20DieView>(screen, "_dieB").Face == controller.RollB, "1v1 painted dice match controller rolls");
                _bootstrap.ShowScreen(GameScreen.DiceRoll);
                // 可注入的既有测试入口，覆盖大成功/大失败与最多 45 个拆分。
                screen.Begin(Field<GameManager>(_bootstrap, "_game"));
                controller = Field<MatchStartController>(screen, "_controller");
                controller.RollWith(20, 1);
                Field<Button>(screen, "_rollButton").gameObject.SetActive(false);
                screen.StartCoroutine((IEnumerator)typeof(DiceRollScreen).GetMethod("PlayRollAnimation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null));
            });
            Frames();
            Shot("DiceRollCriticals");
            Add(() =>
            {
                var screen = Field<DiceRollScreen>(_bootstrap, "_diceRoll");
                if (Field<bool>(screen, "_animating")) throw new AwaitCanvasFrame("critical dice animation still running");
                Check(Field<D20DieView>(screen, "_dieA").Face == 20 && Field<D20DieView>(screen, "_dieB").Face == 1, "critical result numerals remain accurate");
                Check(Field<RectTransform>(screen, "_optionsRoot").GetComponentsInChildren<Button>().Length == 45, "all 45 critical split choices available");
                var first = Field<RectTransform>(screen, "_optionsRoot").Find("Split0").GetComponent<Button>();
                var clip = Field<RectTransform>(screen, "_optionsRoot").parent as RectTransform;
                Check(Bounds(clip).Contains(Bounds((RectTransform)first.transform).center), "first split is visibly inside viewport");
                // 视图根挂在 UiCanvas，组件自身挂在 Bootstrap。
                var viewport = Field<RectTransform>(screen, "_optionsRoot").parent.GetComponent<ScrollRect>();
                Check(viewport.content.rect.height > viewport.viewport.rect.height, "critical options scroll within assigned region");
                viewport.verticalNormalizedPosition = 0;
            });
            Shot("DiceRollCriticalsBottom");
            Add(() => Click("FlipButton"));
            Shot("DiceRollFlipped");
            Add(() => Click("Split0"));
            Add(() =>
            {
                var controller = Field<MatchStartController>(Field<DiceRollScreen>(_bootstrap, "_diceRoll"), "_controller");
                Check(controller.Assignee == controller.PlayerA && !controller.IsNamingOpponent, "critical-failure choice advances to major-success player");
            });
            Add(() => { _bootstrap.ShowScreen(GameScreen.Competition); Click("Comp2v2"); });
            Add(() => Click("TtPractice"));
            Add(() => Click("ModeIntro"));
            Add(() =>
            {
                Screen(GameScreen.TwoOnTwoCharacter);
                Roster("_rosterView2Me", 24); Roster("_rosterView2Mate", 24);
                Check(Field<GameObject>(_bootstrap, "_seatPickerRoot").activeInHierarchy, "intro seat picker visible");
                Click("Seat4");
                Check(Field<Text>(_bootstrap, "_seatSummary").text.Contains("座位 4"), "seat selection updates summary");
            });
            Shot("TwoOnTwoCharacterIntro");
            Add(() => Click("TtCharBack"));
            Add(() => Click("ModeClassic"));
            Add(() => Check(!Field<GameObject>(_bootstrap, "_seatPickerRoot").activeInHierarchy, "classic hides manual seat picker"));
            Shot("TwoOnTwoCharacterClassic");
            Add(() => { Click("TtStart"); Screen(GameScreen.TwoOnTwoInitiative); });
            Add(() =>
            {
                var screen = Field<TwoOnTwoInitiativeScreen>(_bootstrap, "_screenTwoOnTwoInitiative");
                var controller = Field<TwoOnTwoStartController>(screen, "_controller");
                var human = Field<PlayerState>(screen, "_human");
                var players = new List<PlayerState> { human };
                for (int seat = 1; seat <= 4; seat++) if (!ReferenceEquals(controller.PlayerAtSeat(seat), human)) players.Add(controller.PlayerAtSeat(seat));
                screen.Begin(new TwoOnTwoStartController(players, new[] { 0, 0, 1, 1 }, new[] { 20, 14, 10, 6 }, new System.Random(41)));
            });
            Shot("TwoOnTwoInitiative");
            Add(() => Click("RollButton"));
            Frames();
            Add(() =>
            {
                var screen = Field<TwoOnTwoInitiativeScreen>(_bootstrap, "_screenTwoOnTwoInitiative");
                var controller = Field<TwoOnTwoStartController>(screen, "_controller");
                var dice = Field<D20DieView[]>(screen, "_dice");
                if (Field<Coroutine>(screen, "_rollAnimation") != null) throw new AwaitCanvasFrame("2v2 dice animation still running");
                for (int i = 0; i < 4; i++) Check(dice[i].Face == controller.RollOf(controller.PlayerAtSeat(i + 1)), "2v2 painted die result at seat " + (i + 1));
                Check(Field<RectTransform>(screen, "_optionsRoot").GetComponentsInChildren<Button>().Length == 45, "2v2 critical split choices available");
                var first = Field<RectTransform>(screen, "_optionsRoot").Find("Split0") as RectTransform;
                Check(Bounds(Field<RectTransform>(screen, "_optionsRoot").parent as RectTransform).Contains(Bounds(first).center), "2v2 first split visibly inside viewport");
            });
            Shot("TwoOnTwoSplits");
            Add(() => Click("Split0"));
            Add(() => Screen(GameScreen.InMatch));
            Add(() => { _bootstrap.ShowScreen(GameScreen.FactionBattleMenu); Click("FactionAi"); });
            Add(() => { Screen(GameScreen.FactionSelect); Roster("_fbRoster", 24); }); Shot("FactionSelect");
            Add(() => { _bootstrap.ShowScreen(GameScreen.IdentityBattleMenu); Click("IdentityAi"); });
            Add(() =>
            {
                Screen(GameScreen.IdentitySelect);
                var roster = Field<CharacterRosterView>(_bootstrap, "_idRoster");
                Check(roster.Count >= 23 && roster.Count <= 24, "identity excludes reserved lord character");
                Click(roster.Root.Find("Content/Card4").GetComponent<Button>());
            }); Shot("IdentitySelect");
            Add(() => { _bootstrap.ShowScreen(GameScreen.DualGeneralMenu); Click("DualAi"); });
            Shot("DualGeneralSelect");
            Add(() =>
            {
                var roster = Field<CharacterRosterView>(_bootstrap, "_dgRoster");
                Click(roster.Root.Find("Content/Card0").GetComponent<Button>());
                Check(roster.Count > 0 && roster.Count < 24, "dual secondary restricted to same faction");
                Check(!Button("DgStart").interactable, "dual start disabled before secondary");
            }); Shot("DualSecondarySelect");
            Add(() => Click(Field<CharacterRosterView>(_bootstrap, "_dgRoster").Root.Find("Content/Card0").GetComponent<Button>()));
            Add(() => Check(Button("DgStart").interactable, "dual start enabled after two choices"));
            Shot("DualReady");
            Add(() => { Click("DgReset"); Check(Field<CharacterRosterView>(_bootstrap, "_dgRoster").Count == 24, "dual reset restores full roster"); });
            Add(() => { _bootstrap.ShowScreen(GameScreen.DualFactionMenu); Click("DualFactionAi"); }); Shot("DualFactionSelect");
            Add(() => { _bootstrap.ShowScreen(GameScreen.MainMenu); Click("Codex"); });
            Add(() =>
            {
                var codex = Field<CodexScreen>(_bootstrap, "_codex");
                Check(codex.PageCount == 24 && codex.IsOverview, "codex opens all 24 characters in overview");
                foreach (var tile in codex.Root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("CodexTile")))
                {
                    var label = tile.GetComponentInChildren<Text>();
                    Check(label.preferredHeight <= label.rectTransform.rect.height, "codex name fits " + label.text);
                }
            }); Shot("CodexOverview");
            Add(() => Click("CodexTile23")); Shot("CodexDetail");
            Add(() => { Click("Next"); Check(Field<CodexScreen>(_bootstrap, "_codex").PageNumber == 1, "codex next wraps"); });
            Add(() => { Click("Prev"); Check(Field<CodexScreen>(_bootstrap, "_codex").PageNumber == 24, "codex previous wraps"); });
            Add(() => Click("FilterToggle"));
            Add(() => Click("Filter_守序"));
            Add(() => { Click("Filter_善良"); Check(Field<CodexScreen>(_bootstrap, "_codex").LawFilter == "守序", "codex filters combine"); }); Shot("CodexFiltered");
            Add(() => Click("Filter_守序"));
            Add(() => Click("Filter_善良"));
            Add(() => { Click("FilterToggle"); Check(Field<CodexScreen>(_bootstrap, "_codex").PageCount == 24, "codex filters clear"); });
            Add(() => Click("Close"));
            Add(() => { Screen(GameScreen.MainMenu); Click("MagicOrb"); });
            Add(() =>
            {
                var codex = Field<CodexScreen>(_bootstrap, "_codex");
                Check(codex.PageCount == 3, "handbook has three pages separate from characters");
                Check(!Field<Button>(codex, "_filterToggle").gameObject.activeSelf, "magic guide has no alignment filter");
                Check(codex.Root.GetComponentsInChildren<Image>().Where(i => i.name.StartsWith("PaintedIcon_") && i.sprite != null).Select(i => i.sprite.name).Distinct().Count() == 13, "all 13 distinct painted damage icons");
                Check(!codex.Root.GetComponentsInChildren<Text>().Any(t => t.name == "GroupTitle" && t.text == "元素反应"), "damage page does not duplicate reactions");
                Check(codex.Root.GetComponentsInChildren<Text>().Any(t => t.name == "GuideNote" && t.text.Contains("闪避")), "damage page explains hit rules");
            }); Shot("DamageGuide");
            Add(() =>
            {
                var scroll = Field<ScrollRect>(Field<CodexScreen>(_bootstrap, "_codex"), "_guideScroll");
                scroll.verticalNormalizedPosition = 0;
                Check(scroll.content.rect.height > scroll.viewport.rect.height, "damage guide scroll exposes physical types");
            }); Shot("DamageGuideBottom");
            Add(() => Click("Next"));
            Add(() =>
            {
                var codex = Field<CodexScreen>(_bootstrap, "_codex");
                var icons = codex.Root.GetComponentsInChildren<Image>().Where(i => i.name.StartsWith("PaintedIcon_") && i.sprite != null && i.sprite.name.StartsWith("Status/")).ToArray();
                Check(icons.Length == 18 && icons.Select(i => i.sprite.name).Distinct().Count() == 18, "all 18 distinct painted status icons");
                foreach (var status in StatusNames.AllStatuses)
                    Check(icons.Any(i => i.sprite.name == "Status/" + StatusNames.IconKeyOf(status)), "correct icon for " + StatusNames.Of(status));
                Check(codex.Root.GetComponentsInChildren<Text>().Any(t => t.name == "GuideNote" && t.text.Contains("只刷新")), "status duration and refresh rule visible");
            });
            Shot("StatusGuide");
            Add(() => Field<ScrollRect>(Field<CodexScreen>(_bootstrap, "_codex"), "_guideScroll").verticalNormalizedPosition = 0.5f);
            Shot("StatusGuideMiddle");
            Add(() => Field<ScrollRect>(Field<CodexScreen>(_bootstrap, "_codex"), "_guideScroll").verticalNormalizedPosition = 0);
            Shot("StatusGuideBottom");
            Add(() =>
            {
                var codex = Field<CodexScreen>(_bootstrap, "_codex");
                var scroll = Field<ScrollRect>(codex, "_guideScroll");
                var final = scroll.content.Find("Entry_dizzy") as RectTransform;
                Check(final != null && Bounds(scroll.viewport).Contains(Bounds(final).center), "last status reachable by scrolling");
            });
            Add(() => Click("Next"));
            Add(() =>
            {
                var codex = Field<CodexScreen>(_bootstrap, "_codex");
                Check(codex.PageNumber == 3, "element reactions have their own third page");
                var entries = codex.Root.GetComponentsInChildren<Text>().Where(t => t.name.StartsWith("Entry_")).ToArray();
                Check(entries.Length == 6, "exactly six element reactions");
                Check(entries.All(t => !t.text.Contains("心灵")), "psychic is absent from reactions");
                Check(codex.Root.GetComponentsInChildren<Text>().Any(t => t.name == "GuideNote" && t.text.Contains("向上取整")), "reaction page explains damage rounding");
                var scroll = Field<ScrollRect>(codex, "_guideScroll");
                foreach (var entry in entries) Check(Bounds(scroll.viewport).Contains(Bounds(entry.rectTransform).center), "reaction visible " + entry.text);
            }); Shot("ElementReactions");
            Add(() => { Click("Next"); Check(Field<CodexScreen>(_bootstrap, "_codex").PageNumber == 1, "handbook wraps after reaction page"); });
            Add(() => Click("Close"));
            Add(() =>
            {
                Screen(GameScreen.MainMenu);
                Capture("MainMenu-1366x768.png", 1366, 768);
                Capture("MainMenu-1920x1200.png", 1920, 1200);
                _screenshotPending = false;
                ScreenCapture.CaptureScreenshot(Output("main-game-view.png"));
            });
        }

        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
        private static void CheckMenuLayout(string page)
        {
            var buttons = _root.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeInHierarchy).ToArray();
            var canvas = Bounds(_root);
            for (int i = 0; i < buttons.Length; i++)
            {
                var rect = Bounds((RectTransform)buttons[i].transform);
                Check(canvas.Contains(rect.min) && canvas.Contains(rect.max), page + " button in canvas " + buttons[i].name);
                Check(buttons[i].GetComponent<Image>().sprite != null, page + " skinned " + buttons[i].name);
                for (int j = 0; j < i; j++) Check(!rect.Overlaps(Bounds((RectTransform)buttons[j].transform)), page + " buttons do not overlap " + buttons[i].name + "/" + buttons[j].name);
            }
        }

        private static void Capture(string file, int width = 1920, int height = 1080)
        {
            var canvas = _root.GetComponent<Canvas>();
            var go = new GameObject("FrontEndPreviewCamera", typeof(Camera));
            var camera = go.GetComponent<Camera>();
            camera.enabled = false; camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var mode = canvas.renderMode; var previousCamera = canvas.worldCamera; float distance = canvas.planeDistance;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                if (!string.IsNullOrEmpty(file)) File.WriteAllBytes(Output(file), pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = distance;
                camera.targetTexture = null; RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(go);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
