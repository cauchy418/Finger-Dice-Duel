using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;
using FingerDiceDuel.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FingerDiceDuel.EditorTools
{
    [InitializeOnLoad]
    public static class MultiplayerArtPreview
    {
        private const string Pending = "FingerDiceDuel.MultiplayerArtPreview";
        private static readonly Queue<Action> Steps = new Queue<Action>();
        private static readonly List<string> Checks = new List<string>();
        private static GameBootstrap _boot;
        private static bool _ready, _background;
        private static double _after;
        private static List<PlayerState> _players;
        private static PlayerState _me;
        private static GameManager Game => Field<GameManager>(_boot, "_game");
        private static BoardController Controller => Field<BoardController>(_boot, "_controller");
        private static GameBoardUI Board => Field<GameBoardUI>(_boot, "_board");
        private static RectTransform Root => Field<RectTransform>(Board, "_canvasRoot");
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        private static object Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
        private static string Output(string name)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Art/Integration/multiplayer"));
            Directory.CreateDirectory(folder); return Path.Combine(folder, name);
        }
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Checks.Add("PASS " + message);
        }
        private static void Refresh() { Invoke(_boot, "RefreshView"); Canvas.ForceUpdateCanvases(); }
        private static void CheckActionLabels()
        {
            foreach (var button in Root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("SkillChoice") || b.name == "AcceptButton" || b.name == "DeclineButton" || b.name == "SkipTurnButton"))
            {
                var text = button.GetComponentInChildren<Text>();
                Check(text.preferredHeight <= text.rectTransform.rect.height + 0.5f, "complete skill label visible " + button.name);
            }
        }
        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
        private static Button Find(string name) => Root.GetComponentsInChildren<Button>().Single(b => b.name == name);
        private static void Click(Button button)
        {
            Capture(null);
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(button.interactable && hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject, "click reaches " + button.name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static void Click(string name) => Click(Find(name));
        private static void Add(Action action) => Steps.Enqueue(action);
        private static void Shot(string name, int width = 1920, int height = 1080) => Add(() => { Canvas.ForceUpdateCanvases(); CheckActionLabels(); Capture(name + ".png", width, height); });

        static MultiplayerArtPreview() { EditorApplication.update += Update; }
        [MenuItem("Finger Dice Duel/Preview/Multiplayer Battle Art")]
        public static void Run()
        {
            _ready = false; SessionState.SetBool(Pending, true); EditorApplication.isPaused = false;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            _after = EditorApplication.timeSinceStartup + 1.5;
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < _after) return;
            try
            {
                if (!_ready)
                {
                    _boot = UnityEngine.Object.FindObjectOfType<GameBootstrap>(); if (_boot == null) return;
                    _background = Application.runInBackground; Application.runInBackground = true; Prepare(); _ready = true;
                }
                if (Steps.Count == 0)
                {
                    SessionState.SetBool(Pending, false); Application.runInBackground = _background;
                    File.WriteAllLines(Output("validation.txt"), Checks); Debug.Log("[MultiplayerArtPreview] " + Checks.Count + " checks passed."); return;
                }
                Steps.Dequeue()(); EditorApplication.QueuePlayerLoopUpdate(); UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                _after = EditorApplication.timeSinceStartup + 0.45;
            }
            catch (Exception error)
            {
                SessionState.SetBool(Pending, false); Application.runInBackground = _background;
                File.WriteAllLines(Output("validation.txt"), Checks.Concat(new[] { "FAILED " + error })); Debug.LogException(error);
            }
        }

        private static void Build(int count, int myIndex, CharacterDefinition role = null, bool shields = false)
        {
            _boot.PlayerBIsAI = false; _boot.StartMatch(CharacterDefinition.Champion, CharacterDefinition.Champion);
            var definitions = new[] { CharacterDefinition.Champion, CharacterDefinition.Breaker, CharacterDefinition.SchoolOfEvocation, CharacterDefinition.Swashbuckler,
                CharacterDefinition.OathOfDevotion, CharacterDefinition.LifeDomain, CharacterDefinition.Assassin, CharacterDefinition.CollegeOfValor };
            _players = new List<PlayerState>(); var sinks = new List<SimpleChoiceProvider>();
            var lookup = Field<Dictionary<PlayerState, SimpleChoiceProvider>>(_boot, "_sinkByPlayer"); lookup.Clear();
            for (int i = 0; i < count; i++)
            {
                var player = CharacterFactory.CreatePlayer(i == myIndex ? "我" : "角色" + (i + 1), i == myIndex && role != null ? role : shields ? CharacterDefinition.Champion : definitions[i]);
                player.LeftHand.SetValue(shields ? 5 : i % 8 + 1); player.RightHand.SetValue(shields ? 5 : (i + 3) % 9 + 1);
                if (shields) { player.LeftHand.SetShieldState(ShieldState.Formed); player.RightHand.SetShieldState(ShieldState.Formed); }
                var sink = new SimpleChoiceProvider(); _players.Add(player); sinks.Add(sink); lookup[player] = sink;
            }
            _me = _players[myIndex];
            Set(_boot, "_isTwoOnTwo", count == 4); Set(_boot, "_isEightPlayer", count == 8); Set(_boot, "_isIdentity", count == 8); Set(_boot, "_isDualGeneral", false);
            if (count == 4) Invoke(_boot, "BuildTwoOnTwoMatch", _players, sinks, myIndex, null);
            else
            {
                var roles = new[] { IdentityRole.Lord, IdentityRole.Loyalist, IdentityRole.Rebel, IdentityRole.Rebel, IdentityRole.Loyalist, IdentityRole.Rebel, IdentityRole.Rebel, IdentityRole.Traitor };
                var game = GameManager.CreateIdentityEight(_players, sinks.ConvertAll(s => (IChoiceProvider)s), roles);
                var controller = new BoardController(game, sinks[0], sinks[1]);
                Set(_boot, "_game", game); Set(_boot, "_controller", controller); Set(_boot, "_eightSeated", _players); Set(_boot, "_eightHumanPlayer", _me);
            }
            for (int i = 0; i < count; i++) Controller.BindPlayer(_players[i], null, sinks[i]);
            Controller.SetFirstPlayer(_me); Invoke(_boot, "StartPreparedMatch"); Refresh();
        }
        private static RectTransform PanelOf(PlayerState owner)
        {
            string display = owner.DisplayNameOverride ?? owner.Definition.DisplayName;
            var gesture = Root.GetComponentsInChildren<Button>().First(b => b.name == "PaintedHandCard" && b.transform.parent.GetComponentsInChildren<Text>().Any(t => t.name == "Name" && t.text.Contains(display))
                && b.transform.parent.Find("Seat").GetComponent<Text>().text.StartsWith(Seat(owner)));
            return (RectTransform)gesture.transform.parent;
        }
        private static string Seat(PlayerState player) => new[] { "一", "二", "三", "四", "五", "六", "七", "八" }[_players.IndexOf(player)];

        private static void CheckRing(int count, int myIndex)
        {
            var panels = Root.GetComponentsInChildren<RectTransform>().Where(r => r.name.StartsWith("Ring") && r.parent == Root).ToArray();
            Check(panels.Length == count, "exactly " + count + " occupied panels at seat " + (myIndex + 1));
            Check(Root.GetComponentsInChildren<Button>().Count(b => b.name == "PaintedHandCard") == count * 2, "painted hand count " + count * 2);
            var self = Root.Find("RingSelfBottom");
            Check(self.Find("Seat").GetComponent<Text>().text.StartsWith(Seat(_me)) && self.Find("Seat").GetComponent<Text>().text.Contains("我"), "self seat and relation preserved " + (myIndex + 1));
            foreach (var panel in panels)
            {
                var rect = Bounds(panel); Check(Bounds(Root).Contains(rect.min) && Bounds(Root).Contains(rect.max), "panel inside canvas " + panel.name);
                Check(!rect.Overlaps(Bounds((RectTransform)Find("HistoryButton").transform)), "history button avoids " + panel.name);
                Check(!rect.Overlaps(Bounds(Field<RectTransform>(Board, "_battleHint"))), "hint clears " + panel.name);
                foreach (var other in panels) if (other != panel) Check(!rect.Overlaps(Bounds(other)), "panels separated " + panel.name + "/" + other.name);
                foreach (var hand in panel.GetComponentsInChildren<Button>()) if (hand.name == "PaintedHandCard")
                {
                    var box = Bounds((RectTransform)hand.transform);
                    Check(rect.Contains(box.min) && rect.Contains(box.max), "hand fits " + panel.name);
                    Check(hand.transform.Find("Gesture").GetComponent<Image>().sprite != null, "hand sprite loaded " + panel.name);
                }
                Check(panel.Find("SubclassEmblem").GetComponent<Image>().sprite != null, "class emblem loaded " + panel.name);
            }
            Check(!Board.IsHistoryOpen, "history closed when entering multiplayer");
        }
        private static void CheckStatuses(RectTransform panel)
        {
            var name = panel.Find("Name") as RectTransform;
            var bar = panel.GetComponentsInChildren<Image>().Where(i => i.name.StartsWith("Status") && i.name != "StatusIconsA" && i.name != "StatusIconsB").ToArray();
            Check(bar.Length == StatusNames.AllStatuses.Length, "all 18 statuses visible " + panel.name);
            foreach (var status in StatusNames.AllStatuses)
                Check(bar.Any(i => i.sprite != null && i.sprite.name == "Status/" + StatusNames.IconKeyOf(status)), "correct painted status " + StatusNames.Of(status) + " " + panel.name);
            foreach (var icon in bar)
            {
                var rect = Bounds(icon.rectTransform);
                Check(rect.xMin > Bounds(name).xMax, "negative status beside name " + panel.name);
                Check(!rect.Overlaps(Bounds(name)), "status clears name " + panel.name);
                Check(Bounds(Root).Contains(rect.min) && Bounds(Root).Contains(rect.max), "status inside canvas " + panel.name);
                Check(icon.transform.Find("Turns").GetComponent<Text>().text == "2", "status duration visible " + panel.name);
                foreach (var hand in panel.GetComponentsInChildren<Button>().Where(b => b.name == "PaintedHandCard"))
                    Check(!rect.Overlaps(Bounds((RectTransform)hand.transform)), "status clears hand " + panel.name);
            }
        }
        private static void Prepare()
        {
            Steps.Clear(); Checks.Clear(); File.WriteAllText(Output("validation.txt"), "RUNNING\n");
            Add(() =>
            {
                _boot.PlayerBIsAI = false; _boot.StartMatch(CharacterDefinition.Champion, CharacterDefinition.SchoolOfEvocation);
                Game.PlayerA.LeftHand.SetValue(5); Game.PlayerA.LeftHand.SetShieldState(ShieldState.Formed); Game.PlayerA.RightHand.SetValue(5);
                Game.PlayerB.LeftHand.SetValue(5); Game.PlayerB.LeftHand.SetShieldState(ShieldState.Broken); Game.PlayerB.RightHand.SetValue(5); Game.PlayerB.RightHand.SetShieldState(ShieldState.Taichi);
                foreach (var status in StatusNames.AllStatuses) { Game.PlayerA.ApplyStatus(status); Game.PlayerB.ApplyStatus(status); }
                Refresh();
                var a = Field<RectTransform>(Board, "_areaA"); var b = Field<RectTransform>(Board, "_areaB");
                var left = a.GetComponentsInChildren<Image>().First(i => i.name == "Gesture");
                Check(left.sprite.name.EndsWith("left_5_shield"), "formed shield uses closed five");
                Check(a.GetComponentsInChildren<Image>().Last(i => i.name == "Gesture").sprite.name.EndsWith("right_5"), "ordinary five stays spread");
                Check(b.GetComponentsInChildren<Image>().Where(i => i.name == "Gesture").All(i => !i.sprite.name.Contains("shield")), "broken shield and taichi keep ordinary gesture");
                Game.PlayerA.RightHand.SetValue(4); Game.PlayerA.RightHand.SetShieldState(ShieldState.Formed); Refresh();
                Check(a.GetComponentsInChildren<Image>().Last(i => i.name == "Gesture").sprite.name.EndsWith("right_4"), "oathOfDevotion non-five shield preserves its actual hand number");
                Game.PlayerA.RightHand.SetValue(5); Refresh();
                CheckStatuses(a); CheckStatuses(b); Check(!Board.IsHistoryOpen, "history hidden initially");
            }); Shot("shield-and-statuses");
            Add(() =>
            {
                Board.SetLog(Enumerable.Range(0, 80).Select(i => "战斗记录滚动检查 " + i).ToArray()); Click("HistoryButton");
                Check(Board.IsHistoryOpen, "history toggle opens log");
                var scroll = Field<ScrollRect>(Board, "_battleLogScroll");
                Check(scroll.content.rect.height > scroll.viewport.rect.height, "opened log can scroll full history");
                Check(Field<Text>(Board, "_logText").text.Contains("检查 0"), "earlier history retained");
            }); Shot("history-open"); Add(() => { Click("CloseHistory"); Check(!Board.IsHistoryOpen, "close history button works"); });
            for (int seat = 0; seat < 4; seat++)
            {
                int index = seat;
                Add(() => { Build(4, index); foreach (var status in StatusNames.AllStatuses) _me.ApplyStatus(status); Refresh(); CheckRing(4, index); CheckStatuses(PanelOf(_me)); });
                Shot("2v2-seat-" + (seat + 1));
            }
            Add(() =>
            {
                Build(4, 1, CharacterDefinition.Swashbuckler); _me.LeftHand.SetValue(1); _me.RightHand.SetValue(1);
                var attacker = _players[0]; attacker.LeftHand.SetValue(3);
                Controller.SetFirstPlayer(attacker); Controller.BeginTurn(); Controller.ClickHand(attacker.LeftHand); Controller.ClickHand(_me.LeftHand);
                if (Controller.Phase == BoardPhase.ChoosingTargetPlayer) Controller.ConfirmTargetPlayer(); Refresh();
                Check(Controller.PendingEffect == HandEffect.SwashbucklerDodge, "2v2 real swashbuckler defense request");
                Check(Find("SkillChoice1").transform.parent == Field<RectTransform>(Board, "_ringActionContent"), "multiplayer skills use action tray");
            }); Shot("2v2-swashbuckler-defense"); Add(() => { Click("SkillChoice1"); Check(_me.RightHand.Value == 2 && _me.Health == _me.MaximumHealth, "2v2 dodge button routes to right hand"); });
            for (int seat = 0; seat < 8; seat++)
            {
                int index = seat;
                Add(() => { Build(8, index); CheckRing(8, index); });
                if (seat == 0 || seat == 3) Shot("8p-seat-" + (seat + 1));
            }
            Add(() =>
            {
                foreach (var p in _players) foreach (var status in StatusNames.AllStatuses) p.ApplyStatus(status);
                Refresh(); foreach (var p in _players) CheckStatuses(PanelOf(p));
                for (int i = 0; i < 8; i++) Check(PanelOf(_players[i]).Find("Identity").GetComponent<Text>().text == (i == 0 || ReferenceEquals(_players[i], _me) ? IdentityRoleInfo.DisplayName(Game.IdentityOf(_players[i])) : "？"), "identity visibility preserved " + i);
            }); Shot("8p-all-statuses"); Shot("8p-1280x720", 1280, 720); Shot("8p-1600x1200", 1600, 1200);
            Add(() => { _players[3].DisplayNameOverride = "吟游诗人·勇气学院＋圣武士·奉献之誓"; Refresh(); Check(PanelOf(_players[3]).Find("Name").GetComponent<Text>().resizeTextForBestFit, "long dual name fits compact panel"); }); Shot("8p-long-name");
            Add(() =>
            {
                Build(8, 5, CharacterDefinition.LifeDomain); foreach (var p in _players) { p.LeftHand.SetValue(1); p.RightHand.SetValue(1); p.TakeDamage(5); }
                _me.LeftHand.SetValue(8); _me.RightHand.SetValue(9); Controller.BeginTurn(); Controller.ClickHand(_me.LeftHand); Controller.ClickHand(_players[0].LeftHand); Refresh();
                Check(Controller.PendingEffect == HandEffect.LifeDomainExtraHealTarget, "8p extra heal request");
                Check(Root.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("SkillChoice")) == 8, "all eight heal targets exposed without duplicate hands");
            }); Shot("8p-lifeDomain-targets"); Add(() => Click("SkillChoice7")); Add(() => Click("SkillChoice0"));
            Add(() => Check(Controller.PendingEffect == HandEffect.None, "eight-player heal buttons finish both requests"));
            Add(() =>
            {
                Build(8, 0, CharacterDefinition.Breaker, true); Controller.ClickHand(_me.LeftHand); Controller.ClickHand(_players[1].LeftHand); Refresh();
                Check(Controller.PendingEffect == HandEffect.BreakerChainBreak, "8p real chain break request");
                Check(Controller.ChoiceCandidates.Count > 8, "many shield candidates collected");
                Check(Root.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("SkillChoice")) == Controller.ChoiceCandidates.Count, "no multiplayer target truncated");
            }); Shot("8p-chain-scroll-top");
            Add(() => { Field<ScrollRect>(Board, "_ringActionScroll").verticalNormalizedPosition = 0; }); Shot("8p-chain-scroll-bottom");
            Add(() => { Click("SkillChoice" + (Controller.ChoiceCandidates.Count - 1)); Check(Controller.PendingEffect != HandEffect.BreakerChainBreak, "last scrolled shield target can be clicked"); });
            Add(() => { Click("HistoryButton"); Check(Board.IsHistoryOpen, "eight-player history opens"); }); Shot("8p-history");
            Add(() => { Click("HistoryButton"); Check(!Board.IsHistoryOpen, "history toggle closes drawer"); });
            Add(() => { Build(8, 7, CharacterDefinition.CollegeOfValor); _me.TakeDamage(3); Refresh(); Check(Board.IsCollegeOfValorChoicePanelVisible, "8p collegeOfValor opens skill panel"); Click("CollegeOfValor增幅攻击"); });
            Add(() => Click("CollegeOfValor1 人  ±3"));
            Add(() =>
            {
                var buttons = Root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("CollegeOfValor")).ToArray();
                Check(buttons.Count(b => b.name.StartsWith("CollegeOfValorTarget")) >= 7, "collegeOfValor exposes back-row targets");
                for (int i = 0; i < buttons.Length; i++) for (int j = 0; j < i; j++)
                    Check(!Bounds((RectTransform)buttons[i].transform).Overlaps(Bounds((RectTransform)buttons[j].transform)), "collegeOfValor targets clear confirm controls");
            }); Shot("8p-collegeOfValor-targets"); Add(() => Click("CollegeOfValorTarget6")); Add(() => { Click("CollegeOfValor确认"); Check(!Board.IsCollegeOfValorChoicePanelVisible, "back-row collegeOfValor target confirms correctly"); });
            Add(() => { Build(4, 2); Check(Root.GetComponentsInChildren<Text>().Where(t => t.name == "Identity").Count() == 0, "8p to 2v2 clears identity labels"); });
            Add(() =>
            {
                _boot.StartMatch(CharacterDefinition.Champion, CharacterDefinition.SchoolOfEvocation);
                Check(Root.GetComponentsInChildren<Button>().Count(b => b.name == "PaintedHandCard") == 4, "switch back restores only four 1v1 hands");
                Check(!Board.IsHistoryOpen && !Field<RectTransform>(Board, "_ringActionRoot").gameObject.activeSelf, "switch back clears ring tray and history");
            }); Shot("return-to-1v1");
            Add(() => { _boot.PlayerBIsAI = true; _boot.ShowScreen(GameScreen.MainMenu); });
        }
        private static void Capture(string name, int width = 1920, int height = 1080)
        {
            var canvas = Root.GetComponent<Canvas>(); var go = new GameObject("MultiplayerPreviewCamera", typeof(Camera));
            var camera = go.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(width, height, 24); var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var priorTarget = RenderTexture.active; var mode = canvas.renderMode; var priorCamera = canvas.worldCamera; float distance = canvas.planeDistance;
            try
            {
                camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                Canvas.ForceUpdateCanvases(); camera.Render();
                if (name != null) { RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); File.WriteAllBytes(Output(name), pixels.EncodeToPNG()); }
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = priorCamera; canvas.planeDistance = distance; camera.targetTexture = null; RenderTexture.active = priorTarget;
                UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(go); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
