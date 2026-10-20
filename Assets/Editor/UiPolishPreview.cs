using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using FingerDiceDuel.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FingerDiceDuel.EditorTools
{
    [InitializeOnLoad]
    public static class UiPolishPreview
    {
        private const string Pending = "FingerDiceDuel.UiPolishPreview";
        private static readonly Queue<Action> Steps = new Queue<Action>();
        private static readonly List<string> Checks = new List<string>();
        private static GameBootstrap _boot;
        private static bool _ready, _background;
        private static double _after;
        private static GameManager Game => Field<GameManager>(_boot, "_game");
        private static BoardController Controller => Field<BoardController>(_boot, "_controller");
        private static GameBoardUI Board => Field<GameBoardUI>(_boot, "_board");
        private static RectTransform Root => _boot.CurrentScreen == GameScreen.InMatch
            ? Field<RectTransform>(Board, "_canvasRoot") : Field<RectTransform>(_boot, "_selectRoot");

        static UiPolishPreview() { EditorApplication.update += Update; }
        [MenuItem("Finger Dice Duel/Preview/UI Alignment and Skills")]
        public static void Run()
        {
            _ready = false;
            SessionState.SetBool(Pending, true);
            EditorApplication.isPaused = false;
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
                    _boot = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
                    if (_boot == null) return;
                    _background = Application.runInBackground; Application.runInBackground = true;
                    Prepare(); _ready = true;
                }
                if (Steps.Count == 0)
                {
                    SessionState.SetBool(Pending, false); Application.runInBackground = _background;
                    File.WriteAllLines(Output("validation.txt"), Checks);
                    Debug.Log("[UiPolishPreview] " + Checks.Count + " checks passed.");
                    return;
                }
                Steps.Dequeue()();
                EditorApplication.QueuePlayerLoopUpdate(); UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                _after = EditorApplication.timeSinceStartup + 0.45;
            }
            catch (Exception error)
            {
                SessionState.SetBool(Pending, false); Application.runInBackground = _background;
                File.WriteAllLines(Output("validation.txt"), Checks.Concat(new[] { "FAILED " + error }));
                Debug.LogException(error);
            }
        }
        private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
        private static object Invoke(object value, string name) => value.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(value, null);
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Checks.Add("PASS " + message);
        }
        private static void Add(Action action) { Steps.Enqueue(action); }
        private static void WaitFrames(int count = 6) { for (int i = 0; i < count; i++) Add(() => { }); }
        private static void Refresh() { Invoke(_boot, "RefreshView"); Canvas.ForceUpdateCanvases(); }
        private static string Output(string name)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Art/Integration/ui-polish"));
            Directory.CreateDirectory(path); return Path.Combine(path, name);
        }
        private static Button Find(string name) => Root.GetComponentsInChildren<Button>().Single(b => b.gameObject.activeInHierarchy && b.name == name);
        private static void Click(string name)
        {
            var button = Find(name);
            Capture(null);
            Check(button.interactable, "interactive " + name);
            var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject, "raycast reaches " + name);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
        private static void Shot(string name) { Add(() => { CheckActions(); Capture(name + ".png"); }); }
        private static void CheckActions()
        {
            if (_boot.CurrentScreen != GameScreen.InMatch) return;
            var actions = Root.GetComponentsInChildren<Button>().Where(b => b.name == "AcceptButton" || b.name == "DeclineButton" || b.name.StartsWith("SkillChoice") || b.name == "SkipTurnButton" || b.name == "GambleButton" || b.name == "StealButton").ToArray();
            foreach (var button in actions)
            {
                var rect = Bounds((RectTransform)button.transform);
                Check(Bounds(Root).Contains(rect.min) && Bounds(Root).Contains(rect.max), "action inside canvas " + button.name);
                Check(button.transform.Find("SkillIcon")?.GetComponent<Image>().sprite != null, "painted skill emblem " + button.name);
                var label = button.GetComponentInChildren<Text>();
                Check(label.preferredHeight <= label.rectTransform.rect.height + 0.5f, "skill label fits " + button.name);
                Check(!rect.Overlaps(Bounds(Field<RectTransform>(Board, "_battleLog"))), "skill avoids log " + button.name);
                Check(!rect.Overlaps(Bounds(Field<RectTransform>(Board, "_battleHint"))), "skill avoids hint " + button.name);
                foreach (var hand in Root.GetComponentsInChildren<Button>().Where(b => b.name == "PaintedHandCard"))
                    Check(!rect.Overlaps(Bounds((RectTransform)hand.transform)), "skill avoids hand " + button.name);
            }
            for (int i = 0; i < actions.Length; i++) for (int j = i + 1; j < actions.Length; j++)
                Check(!Bounds((RectTransform)actions[i].transform).Overlaps(Bounds((RectTransform)actions[j].transform)), "skill buttons do not overlap");
        }
        private static void Start(CharacterDefinition a, CharacterDefinition b, int al, int ar, int bl, int br, bool bFirst = false)
        {
            _boot.PlayerBIsAI = false; _boot.StartMatch(a, b);
            Game.PlayerA.LeftHand.SetValue(al); Game.PlayerA.RightHand.SetValue(ar);
            Game.PlayerB.LeftHand.SetValue(bl); Game.PlayerB.RightHand.SetValue(br);
            Controller.SetFirstPlayer(bFirst ? Game.PlayerB : Game.PlayerA); Controller.BeginTurn(); Refresh();
        }
        private static void Dodge(int left, int right)
        {
            Start(CharacterDefinition.Ranger, CharacterDefinition.Soldier, left, right, 3, 1, true);
            Controller.ClickHand(Game.PlayerB.LeftHand); Controller.ClickHand(Game.PlayerA.LeftHand);
            if (Controller.Phase == BoardPhase.ChoosingTargetPlayer) Controller.ConfirmTargetPlayer();
            Refresh();
            Check(Controller.PendingEffect == HandEffect.RangerDodge, "real ranger defense request");
        }
        private static void CheckTile(Button tile)
        {
            var icon = tile.transform.Find("Emblem") as RectTransform;
            var frame = tile.transform.Find("TileFrame") as RectTransform;
            var label = tile.GetComponentInChildren<Text>();
            Check(Vector3.Distance(icon.position, frame.position) < 0.01f, "emblem centered " + label.text);
            Check(!Bounds(icon).Overlaps(Bounds(label.rectTransform)), "emblem clears name " + label.text);
            Check(label.font == UiFont.Bold && label.fontSize >= 20, "readable bold name " + label.text);
        }
        private static void Prepare()
        {
            Checks.Clear(); Steps.Clear();
            File.WriteAllText(Output("validation.txt"), "RUNNING\n");
            Add(() =>
            {
                Start(CharacterDefinition.Soldier, CharacterDefinition.Fighter, 1, 1, 1, 1);
                _boot.ShowScreen(GameScreen.DiceRoll);
                var screen = Field<DiceRollScreen>(_boot, "_diceRoll"); screen.Begin(Game);
                var controller = Field<MatchStartController>(screen, "_controller"); controller.RollWith(2, 11);
                Field<Button>(screen, "_rollButton").gameObject.SetActive(false);
                screen.StartCoroutine((IEnumerator)Invoke(screen, "PlayRollAnimation"));
            });
            WaitFrames();
            Add(() =>
            {
                var die = Field<D20DieView>(Field<DiceRollScreen>(_boot, "_diceRoll"), "_dieA");
                var text = Field<Text>(die, "_number");
                for (int value = 1; value <= 20; value++)
                {
                    die.SetFace(value, true);
                    var vertices = text.cachedTextGenerator.verts.Select(v => (Vector2)v.position).ToArray();
                    var min = new Vector2(vertices.Min(v => v.x), vertices.Min(v => v.y));
                    var max = new Vector2(vertices.Max(v => v.x), vertices.Max(v => v.y));
                    Vector2 actual = (min + max) * (0.5f / text.pixelsPerUnit) + text.rectTransform.anchoredPosition;
                    Check(Vector2.Distance(actual, Field<Vector2>(die, "_faceCenter")) < 0.1f, "visible numeral centered " + value);
                }
                die.SetFace(2, true);
            }); Shot("dice-centered");
            Add(() => { _boot.ShowScreen(GameScreen.MainMenu); Invoke(_boot, "OnCodexClicked"); });
            Add(() => { foreach (var tile in Root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("CodexTile"))) CheckTile(tile); }); Shot("codex-centered");
            Add(() => _boot.ShowScreen(GameScreen.CharacterSelect));
            Add(() => { foreach (var tile in Root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Card"))) CheckTile(tile); }); Shot("selection-centered");
            Add(() => Dodge(5, 1)); Shot("ranger-dodge");
            Add(() => Click("AcceptButton"));
            Add(() => { Check(Game.PlayerA.RightHand.Value == 2, "single dodge spends the selected one"); Check(Game.PlayerA.Health == Game.PlayerA.MaximumHealth, "single dodge prevents damage"); });
            Add(() => Dodge(1, 1)); Shot("ranger-dodge-two-hands");
            Add(() => Click("SkillChoice1"));
            Add(() => { Check(Game.PlayerA.RightHand.Value == 2 && Game.PlayerA.LeftHand.Value == 1, "right dodge button selects the right hand only"); Check(Game.PlayerA.Health == Game.PlayerA.MaximumHealth, "two-candidate dodge prevents damage"); });
            Add(() => Dodge(5, 1));
            Add(() => Click("DeclineButton"));
            Add(() => Check(Game.PlayerA.RightHand.Value == 1 && Game.PlayerA.Health < Game.PlayerA.MaximumHealth, "declining dodge keeps the hand and takes damage"));
            Add(() =>
            {
                Start(CharacterDefinition.Ranger, CharacterDefinition.Soldier, 5, 2, 1, 1);
                Controller.ClickHand(Game.PlayerA.LeftHand); Controller.ClickHand(Game.PlayerB.LeftHand); Refresh();
                Check(Controller.PendingEffect == HandEffect.RangerPowerShot, "real power-shot request");
            }); Shot("ranger-power-shot");
            Add(() => Click("AcceptButton"));
            Add(() => { if (Controller.Phase == BoardPhase.ChoosingTargetPlayer) { Controller.ConfirmTargetPlayer(); Refresh(); } Check(Game.PlayerA.RightHand.Value == 3, "power-shot button spends the two"); });
            Add(() =>
            {
                Start(CharacterDefinition.Assassin, CharacterDefinition.Soldier, 6, 7, 1, 1);
                Controller.ClickHand(Game.PlayerA.LeftHand); Controller.ClickHand(Game.PlayerB.LeftHand); Refresh();
                Check(Game.PlayerA.Character is IAttackTargetRule rule && rule.IsImmuneToAttack(Game.PlayerA), "assassin enters stealth through real operation");
                Controller.ClickHand(Game.PlayerB.LeftHand); Controller.ClickHand(Game.PlayerA.LeftHand); Refresh();
                Check(Controller.CanGiveUpAttack, "stealth blocks opposing attack");
            }); Shot("stealth-blocks-attack");
            Add(() => Click("DeclineButton"));
            Add(() => { Check(Controller.CanSkipTurn, "stealth skip available"); Refresh(); Check(Find("SkipTurnButton").GetComponentInChildren<Text>().text.Contains("潜行"), "stealth action explicitly named"); }); Shot("assassin-stealth");
            Add(() => Click("SkipTurnButton"));
            Add(() => { Controller.SetFirstPlayer(Game.PlayerA); Controller.BeginTurn(); Refresh(); Check(!Controller.CanSkipTurn, "stealth skip cannot be used twice"); });
            Add(() => { Start(CharacterDefinition.Gambler, CharacterDefinition.Soldier, 1, 1, 1, 1); Controller.ClickHand(Game.PlayerA.LeftHand); Refresh(); }); Shot("gambler-action");
            Add(() => Click("GambleButton"));
            Add(() => Check(Controller.LastGambleRoll >= 1 && Controller.LastGambleRoll <= 9, "gamble button still rolls via controller"));
            Add(() => { Start(CharacterDefinition.Thief, CharacterDefinition.Soldier, 1, 2, 4, 6); Controller.ClickHand(Game.PlayerA.LeftHand); Refresh(); }); Shot("thief-action");
            Add(() => Click("StealButton"));
            AddAllRoleChecks();
            Add(() => { _boot.ShowScreen(GameScreen.MainMenu); _boot.PlayerBIsAI = true; });
        }

        private static void Operation()
        {
            Controller.ClickHand(Game.PlayerA.LeftHand); Controller.ClickHand(Game.PlayerB.LeftHand);
            if (Controller.Phase == BoardPhase.ChoosingTargetPlayer) Controller.ConfirmTargetPlayer();
            Refresh();
        }

        private static void AddAllRoleChecks()
        {
            foreach (var definition in CharacterDefinition.BuiltIn)
            {
                var role = definition;
                Add(() =>
                {
                    Start(role, CharacterDefinition.Soldier, 1, 1, 1, 1);
                    Check(Field<Image>(Board, "_badgeA").sprite.name.EndsWith(role.Id), "battle emblem available " + role.DisplayName);
                    CheckActions();
                });
            }
            Add(() => { Start(CharacterDefinition.Barbarian, CharacterDefinition.Soldier, 4, 1, 1, 2); Operation(); Check(Controller.PendingEffect == HandEffect.BarbarianLockTarget, "barbarian lock request"); });
            Shot("barbarian-lock"); Add(() => Click("SkillChoice1"));
            Add(() => Check(Game.PlayerB.RightHand.IsLocked, "lock button locks chosen hand"));
            Add(() => { Start(CharacterDefinition.Monk, CharacterDefinition.Soldier, 5, 1, 3, 1, true); Game.PlayerA.LeftHand.SetShieldState(ShieldState.Taichi); Controller.ClickHand(Game.PlayerB.LeftHand); Controller.ClickHand(Game.PlayerA.RightHand); if (Controller.Phase == BoardPhase.ChoosingTargetPlayer) Controller.ConfirmTargetPlayer(); Refresh(); Check(Controller.PendingEffect == HandEffect.MonkTaichi, "monk taichi request"); });
            Shot("monk-taichi"); Add(() => Click("AcceptButton")); Add(() => Check(Game.PlayerA.LeftHand.Value == 0, "taichi button consumes five"));
            Add(() => { Start(CharacterDefinition.Hexer, CharacterDefinition.Monk, 4, 4, 0, 1, true); Controller.ClickHand(Game.PlayerB.LeftHand); Controller.ClickHand(Game.PlayerA.LeftHand); if (Controller.Phase == BoardPhase.ChoosingTargetPlayer) Controller.ConfirmTargetPlayer(); Refresh(); Check(Controller.PendingEffect == HandEffect.HexerCancel, "hexer cancel request"); });
            Shot("hexer-cancel"); Add(() => Click("SkillChoice1")); Add(() => Check(Game.PlayerA.RightHand.Value == 5 && Game.PlayerA.Health == Game.PlayerA.MaximumHealth, "cancel button raises selected hand and prevents attack"));
            Add(() => { Start(CharacterDefinition.Hexer, CharacterDefinition.Soldier, 1, 1, 1, 1); Game.PlayerA.TakeDamage(4); Refresh(); });
            Shot("hexer-health"); Add(() => Click("SkipTurnButton")); Add(() => { Controller.ClickHand(Game.PlayerB.LeftHand); Refresh(); Check(Game.PlayerB.Health == Game.PlayerA.Health, "hexer skip opens health target and applies same health"); });
            Add(() => { Start(CharacterDefinition.Priest, CharacterDefinition.Soldier, 3, 1, 1, 1); Game.PlayerA.TakeDamage(9); Operation(); Check(Controller.PendingEffect == HandEffect.PriestAttackOrHeal, "priest attack or heal request"); });
            Shot("priest-attack-or-heal"); Add(() => Click("AcceptButton")); Shot("priest-heal-target"); Add(() => Click("SkillChoice0")); Add(() => Check(Game.PlayerA.Health > 11, "priest target button heals chosen player"));
            Add(() => { Start(CharacterDefinition.Priest, CharacterDefinition.Soldier, 8, 9, 1, 1); Game.PlayerA.TakeDamage(15); Operation(); Check(Controller.PendingEffect == HandEffect.PriestExtraHealTarget, "priest extra-nine heal request"); });
            Shot("priest-double-nine"); Add(() => Click("SkillChoice0"));
            Add(() => { Check(Controller.PendingEffect == HandEffect.PriestHealTarget, "normal nine heal target also available"); }); Shot("priest-extra-heal"); Add(() => Click("SkillChoice0"));
            Add(() => { Start(CharacterDefinition.Swordsman, CharacterDefinition.Soldier, 3, 4, 1, 1); Operation(); Check(Controller.PendingEffect == HandEffect.SwordsmanFollowUp, "swordsman follow-up request"); });
            Shot("swordsman-follow-up"); Add(() => Click("AcceptButton"));
            Add(() => { Start(CharacterDefinition.Hunter, CharacterDefinition.Soldier, 1, 2, 1, 1); Operation(); Check(Controller.PendingEffect == HandEffect.HunterAmbush, "hunter ambush request"); });
            Shot("hunter-ambush"); Add(() => Click("AcceptButton")); Add(() => Check(Game.PlayerA.LeftHand.IsConcealed, "ambush button conceals the operated hand"));
            Add(() => { Start(CharacterDefinition.Paladin, CharacterDefinition.Soldier, 3, 1, 1, 1); Operation(); Check(Controller.PendingEffect == HandEffect.PaladinShockTarget, "paladin shock request"); });
            Shot("paladin-shock"); Add(() => Click("SkillChoice0")); Add(() => Check(Game.PlayerB.Health < Game.PlayerB.MaximumHealth, "shock target button deals damage"));
            Add(() => { Start(CharacterDefinition.Fighter, CharacterDefinition.Soldier, 3, 4, 1, 1); Game.PlayerA.RightHand.PerformOperation(1); Operation(); Check(Controller.PendingEffect == HandEffect.FighterRangeBreak, "fighter range request"); });
            Shot("fighter-range"); Add(() => Click("AcceptButton"));
            Add(() => { Start(CharacterDefinition.Fighter, CharacterDefinition.Soldier, 4, 4, 4, 4); Game.PlayerA.LeftHand.PerformOperation(1); Game.PlayerA.RightHand.PerformOperation(1); Game.PlayerB.LeftHand.PerformOperation(1); Game.PlayerB.RightHand.PerformOperation(1); Operation(); Check(Controller.PendingEffect == HandEffect.FighterChainBreak, "fighter multi-target chain request"); });
            Shot("fighter-chain"); Add(() => Click("SkillChoice2")); Add(() => Check(Game.PlayerB.RightHand.State == ShieldState.Broken, "chain button breaks selected shield"));
            Add(() => { Start(CharacterDefinition.Wizard, CharacterDefinition.Soldier, 3, 1, 1, 1); Operation(); Check(Controller.PendingEffect == HandEffect.WizardAbandonAttack, "wizard enchant request"); });
            Shot("wizard-enchant"); Add(() => Click("AcceptButton")); Shot("wizard-types"); Add(() => Click("Type5")); Add(() => Check(Controller.PendingEffect != HandEffect.WizardDamageType, "damage type button completes type selection"));
            Add(() => { Start(CharacterDefinition.Bard, CharacterDefinition.Soldier, 1, 1, 1, 1); Game.PlayerA.TakeDamage(3); Refresh(); Check(Board.IsBardChoicePanelVisible, "bard opens real choice panel"); });
            Shot("bard-kind"); Add(() => Click("Bard增幅攻击")); Shot("bard-count"); Add(() => Click("Bard1 人  ±3")); Shot("bard-target"); Add(() => Click("BardTarget0"));
            Add(() => Click("Bard确认")); Add(() => Check(!Board.IsBardChoicePanelVisible, "bard buttons finish complete choice flow"));
        }
        private static void Capture(string name)
        {
            var canvas = Root.GetComponent<Canvas>(); var go = new GameObject("PolishPreviewCamera", typeof(Camera));
            var camera = go.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(1920, 1080, 24); var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var previous = RenderTexture.active; var mode = canvas.renderMode; var priorCamera = canvas.worldCamera; float distance = canvas.planeDistance;
            try
            {
                camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                Canvas.ForceUpdateCanvases(); camera.Render();
                if (name != null)
                {
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Output(name), pixels.EncodeToPNG());
                }
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = priorCamera; canvas.planeDistance = distance; camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(go); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
