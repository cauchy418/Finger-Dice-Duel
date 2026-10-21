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
        private static bool _turnExitsOnly = SessionState.GetBool(Pending + ".TurnExits", false);
        private static float _savedThinkSeconds;
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
            _turnExitsOnly = false;
            SessionState.SetBool(Pending + ".TurnExits", false);
            StartPreview();
        }
        [MenuItem("Finger Dice Duel/Preview/Turn Skip and Give Up")]
        public static void RunTurnExits()
        {
            _turnExitsOnly = true;
            SessionState.SetBool(Pending + ".TurnExits", true);
            StartPreview();
        }
        private static void StartPreview()
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
                    _savedThinkSeconds = _boot.AiThinkSeconds;
                    if (_turnExitsOnly) _boot.AiThinkSeconds = 1000;
                    Prepare(); _ready = true;
                }
                if (Steps.Count == 0)
                {
                    SessionState.SetBool(Pending, false); Application.runInBackground = _background;
                    if (_turnExitsOnly) _boot.AiThinkSeconds = _savedThinkSeconds;
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
                if (_turnExitsOnly && _boot != null) _boot.AiThinkSeconds = _savedThinkSeconds;
                File.WriteAllLines(Output("validation.txt"), Checks.Concat(new[] { "FAILED " + error }));
                Debug.LogException(error);
            }
        }
        private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
        private static object Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(value, args);
        private static void Set(object value, string name, object data) => value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, data);
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
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, _turnExitsOnly ? "../Art/Integration/turn-exits" : "../Art/Integration/ui-polish"));
            Directory.CreateDirectory(path); return Path.Combine(path, name);
        }
        private static Button Find(string name) => Root.GetComponentsInChildren<Button>().Single(b => b.gameObject.activeInHierarchy && b.name == name);
        private static void Click(string name)
            => Click(Find(name));
        private static void Click(Button button)
        {
            string name = button.name;
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
            if (_turnExitsOnly) { PrepareTurnExits(); return; }
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
            Add(() => { Start(CharacterDefinition.Paladin, CharacterDefinition.Soldier, 3, 1, 1, 1); Controller.ClickHand(Game.PlayerA.LeftHand); Controller.ClickHand(Game.PlayerB.LeftHand); Refresh(); Check(Controller.PendingEffect == HandEffect.PaladinShockTarget, "paladin four directly asks shock without normal attack target"); });
            Shot("paladin-shock"); Add(() => Click("SkillChoice0")); Add(() => { Check(Game.PlayerB.Health < Game.PlayerB.MaximumHealth, "shock target button deals damage"); Check(Game.PlayerA.LeftHand.IsShield && Controller.LastTurn.Effect == HandEffectType.Shield, "paladin four resolves as shield"); Board.SetHistoryOpen(true); Refresh(); Check(string.Join("\n", Controller.Log).Contains("【冲击】"), "named shock visible in battle history"); });
            Shot("paladin-history");
            Add(() => { Start(CharacterDefinition.Paladin, CharacterDefinition.Soldier, 5, 1, 1, 1); Controller.ClickHand(Game.PlayerA.LeftHand); Controller.ClickHand(Game.PlayerB.LeftHand); Refresh(); Check(Controller.PendingEffect == HandEffect.PaladinShockTarget, "paladin six directly asks shock without normal attack target"); });
            Shot("paladin-six-shock"); Add(() => Click("SkillChoice0"));
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
        private static void HandClick(bool playerA, int index = 0)
        {
            var cards = Field<Array>(Board, playerA ? "_cardsA" : "_cardsB");
            var card = cards.GetValue(index);
            Click((Button)card.GetType().GetField("Button").GetValue(card));
        }
        private static void CheckLive(string label)
        {
            Check(!Game.IsGameOver && Controller.Phase != BoardPhase.GameOver, "match remains live " + label);
            Check(!Field<GameObject>(Board, "_gameOverPanel").activeSelf, "no restart overlay " + label);
        }
        private static void PrepareTurnExits()
        {
            Add(() => { Start(CharacterDefinition.Wizard, CharacterDefinition.Fighter, 1, 1, 3, 1); Game.PlayerA.ApplyStatus(StatusEffect.Frozen); Controller.BeginTurn(); Refresh(); CheckLive("frozen human"); Check(Find("SkipTurnButton").interactable, "frozen human sees skip"); Check(!Controller.CanClick(Game.PlayerA.LeftHand), "frozen hands disabled"); });
            Shot("frozen-human");
            Add(() => { Click("SkipTurnButton"); CheckLive("after frozen skip"); Check(ReferenceEquals(Controller.ActivePlayer, Game.PlayerB), "frozen skip hands turn to opponent"); Check(Game.PlayerA.StatusTurnsLeft(StatusEffect.Frozen) == 1, "frozen ticks once"); });
            Add(() =>
            {
                Start(CharacterDefinition.Wizard, CharacterDefinition.Fighter, 1, 1, 3, 1);
                Controller.BindPlayer(Game.PlayerB, new GreedyChoiceProvider(), Field<SimpleChoiceProvider>(_boot, "_choiceB"));
                Game.PlayerB.ApplyStatus(StatusEffect.Frozen); Controller.SetFirstPlayer(Game.PlayerB); Controller.BeginTurn(); Refresh();
                CheckLive("frozen AI fighter");
                Check(!Field<Button>(Board, "_skipButton").gameObject.activeInHierarchy, "AI turn has no human skip button");
                _boot.AiThinkSeconds = 0; Invoke(_boot, "Update"); _boot.AiThinkSeconds = 1000;
                CheckLive("AI automatic skip"); Check(ReferenceEquals(Controller.ActivePlayer, Game.PlayerA), "bootstrap update automatically skips frozen AI");
                Check(Game.PlayerB.StatusTurnsLeft(StatusEffect.Frozen) == 1, "AI frozen duration ticks");
            });
            Shot("frozen-ai-resumed");
            for (int mask = 0; mask < 4; mask++)
            {
                int value = mask;
                Add(() =>
                {
                    Start(CharacterDefinition.Soldier, CharacterDefinition.Fighter, 1, 1, 1, 1);
                    if ((value & 1) == 0) Game.PlayerA.LeftHand.Lock(); else Game.PlayerA.LeftHand.Conceal();
                    if ((value & 2) == 0) Game.PlayerA.RightHand.Lock(); else Game.PlayerA.RightHand.Conceal();
                    Controller.BeginTurn(); Refresh(); CheckLive("unusable hands " + value); Click("SkipTurnButton");
                    Check(Game.TurnCount == 1, "unusable hand button advances once " + value);
                });
            }
            Add(() =>
            {
                Start(CharacterDefinition.Soldier, CharacterDefinition.Fighter, 5, 5, 1, 1);
                Game.PlayerA.LeftHand.SetShieldState(ShieldState.Formed); Game.PlayerA.RightHand.SetShieldState(ShieldState.Formed); Refresh();
                Click("SkipTurnButton"); Check(Game.PlayerA.HasDoubleShield, "voluntary skip preserves shields");
            });
            Add(() =>
            {
                Start(CharacterDefinition.Soldier, CharacterDefinition.Fighter, 1, 5, 1, 1);
                Game.PlayerA.LeftHand.Lock(); Game.PlayerA.RightHand.SetShieldState(ShieldState.Formed); Refresh();
                Check(!Field<Button>(Board, "_skipButton").gameObject.activeInHierarchy, "one locked hand and one shield cannot skip");
            });
            Add(() =>
            {
                Start(CharacterDefinition.Assassin, CharacterDefinition.Fighter, 7, 7, 1, 1);
                ((FingerDiceDuel.Characters.SpecialLogics.AssassinLogic)Game.PlayerA.Character).EnterStealthForTest(); Refresh();
                Click("SkipTurnButton"); Controller.SetFirstPlayer(Game.PlayerA); Controller.BeginTurn(); Refresh();
                Check(!Controller.CanSkipTurn, "assassin stealth skip only once");
            });
            Add(() =>
            {
                Start(CharacterDefinition.Hexer, CharacterDefinition.Fighter, 1, 1, 1, 1);
                Game.PlayerA.TakeDamage(4); Game.PlayerA.ApplyStatus(StatusEffect.Acid); Refresh(); Click("SkipTurnButton");
                Check(Controller.Phase == BoardPhase.ChoosingHealthSwapTarget, "hexer skip still asks for target"); HandClick(false);
                Check(Game.PlayerB.Health == 16 && Game.PlayerA.StatusTurnsLeft(StatusEffect.Acid) == 1, "hexer target click resolves skill and duration");
            });
            foreach (var status in new[] { StatusEffect.Stunned, StatusEffect.Panic })
            {
                var effect = status;
                Add(() =>
                {
                    Start(CharacterDefinition.Soldier, CharacterDefinition.Fighter, 1, 1, 3, 1);
                    Game.PlayerA.ApplyStatus(effect, source: Game.PlayerB); Controller.BeginTurn(); Refresh(); HandClick(true); HandClick(false);
                    Check(Controller.CanGiveUpAttack, "give up offered for " + effect);
                    Check(Find("DeclineButton").GetComponentInChildren<Text>().text == "放弃攻击", "give up label " + effect);
                    Check(!Field<Button>(Board, "_skipButton").gameObject.activeInHierarchy, "target confirmation cannot be bypassed by skip");
                }); Shot("give-up-" + effect.ToString().ToLowerInvariant());
                Add(() => { Click("DeclineButton"); CheckLive("after give up"); Check(Game.PlayerA.LeftHand.Value == 4 && Game.PlayerB.Health == 20, "give up retains sum without damage"); });
            }
            Add(() =>
            {
                Start(CharacterDefinition.Soldier, CharacterDefinition.Assassin, 1, 1, 7, 7);
                ((FingerDiceDuel.Characters.SpecialLogics.AssassinLogic)Game.PlayerB.Character).EnterStealthForTest(); Refresh(); HandClick(true); HandClick(false); Click("DeclineButton");
                Check(Game.PlayerA.LeftHand.Value == 8 && Game.PlayerB.Health == 20, "stealth target give up retains sum");
            });
            foreach (int count in new[] { 4, 8 }) for (int seat = 0; seat < count; seat++)
            {
                int n = count, index = seat;
                Add(() =>
                {
                    StartExitRing(n, index); var actor = Controller.ActivePlayer; actor.ApplyStatus(StatusEffect.Frozen); Controller.BeginTurn(); Refresh();
                    CheckLive(n + "p frozen seat " + index);
                    if (index == n - 1) Capture(n + "p-frozen-skip.png");
                    Click("SkipTurnButton");
                    Check(actor.StatusTurnsLeft(StatusEffect.Frozen) == 1, "ring frozen button ticks seat " + index);
                    Check(!ReferenceEquals(actor, Controller.ActivePlayer), "ring button advances seat " + index);
                });
                if (seat == count - 1) Add(() => Capture(n + "p-skip-resumed.png"));
                Add(() =>
                {
                    var actor = Game.Players[index]; actor.RemoveStatus(StatusEffect.Frozen); actor.ApplyStatus(StatusEffect.Stunned); actor.LeftHand.SetValue(1);
                    var target = Game.Players.First(p => !ReferenceEquals(p, actor)); target.LeftHand.SetValue(3);
                    Controller.SetFirstPlayer(actor); Controller.BeginTurn(); Controller.ClickHand(actor.LeftHand); Controller.ClickHand(target.LeftHand); Refresh();
                    Check(Controller.CanGiveUpAttack, n + "p no legal target exit at seat " + index);
                    if (index == n - 1) Capture(n + "p-give-up.png");
                    Click("DeclineButton");
                    Check(actor.LeftHand.Value == 4, "ring give up preserves result at seat " + index);
                    Check(actor.StatusTurnsLeft(StatusEffect.Stunned) == 1, "ring give up finishes status at seat " + index);
                    CheckLive(n + "p after giving up");
                });
            }
            Add(() => { _boot.PlayerBIsAI = true; _boot.ShowScreen(GameScreen.MainMenu); });
        }
        private static void StartExitRing(int count, int seat)
        {
            Start(CharacterDefinition.Soldier, CharacterDefinition.Fighter, 1, 1, 1, 1);
            var players = new List<PlayerState>(); var sinks = new List<SimpleChoiceProvider>();
            var lookup = Field<Dictionary<PlayerState, SimpleChoiceProvider>>(_boot, "_sinkByPlayer"); lookup.Clear();
            for (int i = 0; i < count; i++)
            {
                var p = CharacterFactory.CreatePlayer("座位" + (i + 1), CharacterDefinition.Soldier);
                var sink = new SimpleChoiceProvider(); players.Add(p); sinks.Add(sink); lookup[p] = sink;
            }
            Set(_boot, "_isTwoOnTwo", count == 4); Set(_boot, "_isEightPlayer", count == 8); Set(_boot, "_isIdentity", count == 8);
            if (count == 4) Invoke(_boot, "BuildTwoOnTwoMatch", players, sinks, seat, null);
            else
            {
                var roles = new[] { IdentityRole.Lord, IdentityRole.Loyalist, IdentityRole.Rebel, IdentityRole.Rebel, IdentityRole.Loyalist, IdentityRole.Rebel, IdentityRole.Rebel, IdentityRole.Traitor };
                var game = GameManager.CreateIdentityEight(players, sinks.ConvertAll(s => (IChoiceProvider)s), roles);
                Set(_boot, "_game", game); Set(_boot, "_controller", new BoardController(game, sinks[0], sinks[1]));
                Set(_boot, "_eightSeated", players); Set(_boot, "_eightHumanPlayer", players[seat]);
            }
            for (int i = 0; i < count; i++) Controller.BindPlayer(players[i], null, sinks[i]);
            Controller.SetFirstPlayer(players[seat]); Invoke(_boot, "StartPreparedMatch"); Refresh();
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
