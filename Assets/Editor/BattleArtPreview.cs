using System;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>可重复的真实对局预览与美术接入检查。仅编译进编辑器。</summary>
    [InitializeOnLoad]
    public static class BattleArtPreview
    {
        private const string Pending = "FingerDiceDuel.BattleArtPreview.Pending";
        private static readonly List<string> Checks = new List<string>();
        private static double _readyAfter;
        private static bool _prepared;
        private static double _captureAfter;

        static BattleArtPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) _readyAfter = EditorApplication.timeSinceStartup + 1;
            };
        }

        [MenuItem("Finger Dice Duel/Preview/1v1 Battle Art")]
        public static void Run()
        {
            _prepared = false;
            SessionState.SetBool(Pending, true);
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            _readyAfter = EditorApplication.timeSinceStartup + 1;
        }

        private static void Update()
        {
            if (_captureAfter > 0 && EditorApplication.timeSinceStartup > _captureAfter && EditorApplication.isPlaying)
            {
                _captureAfter = 0;
                ScreenCapture.CaptureScreenshot(Output("game-view.png"));
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
                EditorApplication.isCompiling || EditorApplication.timeSinceStartup < _readyAfter) return;
            var bootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
            if (bootstrap == null) return;
            if (!_prepared)
            {
                bootstrap.PlayerBIsAI = false;
                bootstrap.StartMatch(CharacterDefinition.Champion, CharacterDefinition.SchoolOfEvocation);
                _prepared = true;
                _readyAfter = EditorApplication.timeSinceStartup + 0.5;
                return;
            }
            SessionState.SetBool(Pending, false);
            try { ValidateAndCapture(bootstrap); }
            catch (Exception error)
            {
                Debug.LogException(error);
                File.WriteAllText(Output("validation.txt"), "FAILED\n" + error);
            }
        }

        private static T Field<T>(object owner, string name)
            => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Checks.Add("PASS " + message);
        }

        private static string Output(string file)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Art/Integration/1v1"));
            Directory.CreateDirectory(path);
            return Path.Combine(path, file);
        }

        private static void ValidateAndCapture(GameBootstrap bootstrap)
        {
            Checks.Clear();
            var board = Field<GameBoardUI>(bootstrap, "_board");
            var game = Field<GameManager>(bootstrap, "_game");
            var controller = Field<BoardController>(bootstrap, "_controller");
            var root = Field<RectTransform>(board, "_canvasRoot");
            Check(root.gameObject.activeInHierarchy, "1v1 canvas visible");
            Check(root.GetComponent<Canvas>().sortingOrder > 0, "battle canvas draws above menu background after captures");
            Check(UiFont.Title != null && UiFont.Title != UiFont.Bold, "Source Han Serif static title font loaded");
            foreach (CharacterDefinition definition in CharacterDefinition.BuiltIn)
            {
                string key = definition.Id == "blank" ? "fighter_champion" : definition.Id;
                Check(BattleArt.Get("Subclasses/" + key) != null, "class emblem " + definition.Id);
            }
            foreach (AttackDamageType type in Enum.GetValues(typeof(AttackDamageType)))
                if (type != AttackDamageType.None)
                    Check(BattleArt.Get("Damage/" + AttackDamageTypes.IconKeyOf(type)) != null, "damage icon " + type);
            foreach (StatusEffect status in StatusNames.AllStatuses)
                Check(BattleArt.Get("Status/" + BattleArt.StatusKey(status)) != null, "status icon " + status);
            for (int i = 0; i <= 9; i++)
            {
                Check(BattleArt.Get("Hands/hand_left_" + i) != null, "left hand " + i);
                Check(BattleArt.Get("Hands/hand_right_" + i) != null, "right hand " + i);
            }
            Transform areaA = root.Find("AreaA"), areaB = root.Find("AreaB");
            // 用命名筛选，避免布局装饰增减影响测试的点击目标。
            var handsA = areaA.GetComponentsInChildren<Button>();
            var handsB = areaB.GetComponentsInChildren<Button>();
            Check(handsA.Length == 2 && handsB.Length == 2, "exactly two clickable hands per side");
            Check(handsA[0].interactable, "actor hand initially clickable");
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, handsA[0].transform.position)
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject == handsA[0].gameObject,
                "visible hand receives UI raycast (first=" + (hits.Count > 0 ? hits[0].gameObject.name : "none")
                + ", point=" + pointer.position + ", screen=" + Screen.width + "x" + Screen.height + ")");
            handsA[0].onClick.Invoke();
            Check(ReferenceEquals(controller.SelectedHand, game.PlayerA.LeftHand), "click routes into controller selection");
            Check(handsA[0].transform.Find("Frame").GetComponent<Image>().sprite.name.EndsWith("selected"), "selected hand uses gold frame");
            handsB[0].onClick.Invoke();
            Check(game.PlayerA.LeftHand.Value == 2, "target click resolves real 1+1 operation");
            Check(handsA[0].transform.Find("Gesture").GetComponent<Image>().sprite.name.EndsWith("left_2"), "gesture updates after operation");
            bootstrap.StartMatch(CharacterDefinition.Champion, CharacterDefinition.SchoolOfEvocation);
            game = Field<GameManager>(bootstrap, "_game");
            Capture(board, "battle-initial.png");
            game.PlayerA.LeftHand.SetValue(4);
            game.PlayerA.RightHand.SetValue(6);
            game.PlayerB.LeftHand.SetValue(5);
            game.PlayerB.RightHand.SetValue(8);
            game.PlayerA.SetHealthExact(13);
            game.PlayerB.SetHealthExact(17);
            foreach (StatusEffect status in StatusNames.AllStatuses) game.PlayerB.ApplyStatus(status);
            board.SetInteractable((owner, hand) => true);
            board.Refresh(game.PlayerA, game.PlayerB, game.PlayerA.LeftHand, turnCount: 7, turnLimit: 50);
            board.SetHint("选择对方一只手，完成本次运算", "已选左手 4 · 数字位于手型下方");
            board.SetLog(new[] { "第 7 轮", "战士·勇士选择左手 4。", "法师·塑能学派：强酸使防御降低 2。", "护盾只在合成后生效。", "将鼠标移至此处滚轮可查看战报。" });
            Check(!handsB[0].transform.Find("HandState").GetComponent<Image>().enabled, "initial value 5 does not imply a formed shield");
            Check(Mathf.Abs(Field<Image>(board, "_hpBarFillA").fillAmount - 0.65f) < 0.001f, "health fill uses current health ratio");
            Check(Field<StatusIconBar>(board, "_statusIconsB").VisibleCount == 9, "all nine status icons visible");
            Check(!Bounds(root.Find("Title") as RectTransform).Overlaps(Bounds(areaB.Find("StatusIconsB") as RectTransform)), "all nine statuses avoid match title");
            Check(!Bounds(root.Find("BattleSubtitle") as RectTransform).Overlaps(Bounds(areaB.Find("StatusIconsB") as RectTransform)), "all nine statuses avoid subtitle");
            Capture(board, "battle-states.png");
            Capture(board, "battle-1280x720.png", 1280, 720);
            Capture(board, "battle-1600x1200.png", 1600, 1200);
            board.SetAcceptButtonVisible(true);
            board.SetDeclineButtonVisible(true);
            board.PlaceChoiceButtonsNextTo(game.PlayerB);
            Check(!Bounds(Field<Button>(board, "_acceptButton").transform as RectTransform).Overlaps(Bounds(Field<RectTransform>(board, "_battleHint"))), "opponent decision button avoids hint panel");
            Check(!Bounds(Field<Button>(board, "_declineButton").transform as RectTransform).Overlaps(Bounds(Field<RectTransform>(board, "_battleLog"))), "opponent decision button avoids combat log");
            Capture(board, "battle-decision.png");
            board.SetAcceptButtonVisible(false);
            board.SetDeclineButtonVisible(false);
            game.PlayerB.LeftHand.SetShieldState(ShieldState.Formed);
            game.PlayerA.RightHand.Lock();
            board.Refresh(game.PlayerA, game.PlayerB, game.PlayerA.LeftHand);
            Check(handsB[0].transform.Find("HandState").GetComponent<Image>().enabled, "formed shield overlay visible");
            Check(handsA[1].transform.Find("Locked").GetComponent<Image>().enabled, "locked hand overlay visible");
            Capture(board, "battle-shield-lock.png");
            board.SetDamageTypePanelVisible(true);
            Capture(board, "damage-choice.png");
            board.SetDamageTypePanelVisible(false);
            foreach (int value in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
            {
                game.PlayerA.LeftHand.SetValue(value);
                game.PlayerA.RightHand.SetValue(value);
                board.Refresh(game.PlayerA, game.PlayerB, null);
                Check(handsA[0].transform.Find("Gesture").GetComponent<Image>().sprite.name.EndsWith("left_" + value), "bound left hand value " + value);
                Check(handsA[1].transform.Find("Gesture").GetComponent<Image>().sprite.name.EndsWith("right_" + value), "bound right hand value " + value);
            }
            var history = new List<string>();
            for (int i = 0; i < 80; i++) history.Add("战报滚动检查 " + i);
            board.SetLog(history);
            board.SetHistoryOpen(true);
            Canvas.ForceUpdateCanvases();
            var scroll = Field<ScrollRect>(board, "_battleLogScroll");
            Check(scroll.content.rect.height > scroll.viewport.rect.height, "combat log grows beyond masked viewport");
            Check(Field<Text>(board, "_logText").text.Contains("战报滚动检查 0"), "combat log retains earlier entries for scrolling");
            board.SetHistoryOpen(false);
            // 真正切换多人布局再回到 1v1，防止右侧战报与两人面板残留到多人场景。
            var players = new[] { game.PlayerA, game.PlayerB, new PlayerState("队友"), new PlayerState("对手") };
            board.BindPlayersRing(game.PlayerA, players, 1);
            Check(!areaA.gameObject.activeSelf && !areaB.gameObject.activeSelf, "ring switch hides painted player areas");
            board.RefreshRing(players, null, showSkipButton: true);
            board.SetSkillActionContext(HandEffect.None, game.PlayerA, game.PlayerA, null, false);
            var ringSkip = Field<Button>(board, "_skipButton").GetComponent<RectTransform>();
            Check(ringSkip.parent.name == "Content", "ring switch places skill button in scrollable action tray");
            Check(!board.IsHistoryOpen, "ring switch keeps history closed by default");
            board.BindPlayers(game.PlayerA, game.PlayerB);
            Check(areaA.gameObject.activeSelf && areaB.gameObject.activeSelf, "return to 1v1 restores painted player areas");
            Check(Field<RectTransform>(board, "_battleLog").sizeDelta.x == 460, "return to 1v1 restores right log width");
            // 退出检验夹具，留下一局正常可玩的 1v1。
            bootstrap.StartMatch(CharacterDefinition.Champion, CharacterDefinition.SchoolOfEvocation);
            _captureAfter = EditorApplication.timeSinceStartup + 0.5;
            File.WriteAllLines(Output("validation.txt"), Checks);
            Debug.Log("[BattleArtPreview] " + Checks.Count + " checks passed. " + Output("battle-initial.png"));
        }

        /// <summary>用 Unity 相机渲染运行时 Canvas；截图后还原 Overlay 模式。</summary>
        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void Capture(GameBoardUI board, string file, int width = 1920, int height = 1080)
        {
            Canvas canvas = Field<RectTransform>(board, "_canvasRoot").GetComponent<Canvas>();
            var go = new GameObject("BattlePreviewCamera", typeof(Camera));
            Camera camera = go.GetComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.cullingMask = ~0;
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderMode mode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                Canvas.ForceUpdateCanvases();
                foreach (Button button in Field<RectTransform>(board, "_canvasRoot").GetComponentsInChildren<Button>())
                {
                    if (button.name != "PaintedHandCard") continue;
                    var visible = Bounds(button.transform as RectTransform);
                    var canvasBounds = Bounds(canvas.transform as RectTransform);
                    Check(canvasBounds.Contains(visible.min) && canvasBounds.Contains(visible.max), "hands within canvas at " + width + "x" + height);
                }
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Output(file), pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(go);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
