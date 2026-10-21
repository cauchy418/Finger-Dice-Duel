using System.Collections.Generic;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>Resources 中的战斗素材。纹理导入类型不影响加载，不改写引擎生成的 meta。</summary>
    public static class BattleArt
    {
        public const string Root = "Art/Battle/v1/";
        public static readonly Color Gold = new Color(0.88f, 0.73f, 0.47f);
        public static readonly Color Ink = new Color(0.75f, 0.73f, 0.66f);
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string path)
        {
            Sprite sprite;
            if (Cache.TryGetValue(path, out sprite)) return sprite;
            var texture = Resources.Load<Texture2D>(Root + path);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Border(path));
                sprite.name = path;
            }
            else Debug.LogWarning("[BattleArt] Missing Resources/" + Root + path);
            Cache[path] = sprite;
            return sprite;
        }

        private static Vector4 Border(string path)
        {
            if (path.StartsWith("UI/button_")) return new Vector4(48, 32, 48, 32);
            if (path.StartsWith("UI/hand_frame_") || path == "UI/panel_log" || path == "UI/panel_tooltip")
                return new Vector4(48, 48, 48, 48);
            if (path == "UI/label_strip") return new Vector4(40, 12, 40, 12);
            if (path == "UI/health_track") return new Vector4(24, 16, 24, 16);
            if (path == "UI/health_fill") return new Vector4(2, 8, 2, 8);
            return Vector4.zero;
        }

        public static void Apply(Image image, string path, bool sliced = false)
        {
            if (image == null) return;
            var sprite = Get(path);
            if (sprite == null) return;
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.white;
        }

        public static void Button(Button button)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            Apply(image, "UI/button_normal", true);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Get("UI/button_hover"),
                pressedSprite = Get("UI/button_pressed"),
                selectedSprite = Get("UI/button_hover"),
                disabledSprite = Get("UI/button_disabled")
            };
            // 键盘选中不应让上一张牌在下一阶段继续表现为已选。
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = Gold;
        }

        public static string StatusKey(StatusEffect status) => StatusNames.IconKeyOf(status);

        public static string DamageKey(DamageType type)
            => AttackDamageTypes.IconKeyOf(AttackDamageTypes.FromDamageType(type));
    }
}
