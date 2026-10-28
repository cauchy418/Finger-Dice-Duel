using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>前台共用的资源、九宫格边距与交互状态；不依赖贴图导入为 Sprite。</summary>
    public static class FrontEndArt
    {
        // Use measured glyph widths, keep numeric units together, and avoid orphan punctuation.
        public static string WrapReadableText(Text label,string value,float maxWidth)
        {
            value=(value??"").Replace("\r\n","\n").Replace("\r","\n");
            value=System.Text.RegularExpressions.Regex.Replace(value,@"(?<=\d) +(?=[\u4e00-\u9fff%])|(?<=[\u4e00-\u9fff]) +(?=[+−-]?\d)","");
            var settings=label.GetGenerationSettings(new Vector2(maxWidth,0));
            settings.horizontalOverflow=HorizontalWrapMode.Overflow;
            var generator=new TextGenerator();
            System.Func<string,float> width=t=>generator.GetPreferredWidth(t,settings)/label.pixelsPerUnit;
            var result=new List<string>();
            foreach(var paragraph in value.Split('\n'))
            {
                var lines=new List<string>();string line="";
                foreach(string token in ReadableTokens(paragraph))
                {
                    if(line.Length>0&&width(line+token)>maxWidth-8)
                    {
                        lines.Add(line.TrimEnd());line="";
                    }
                    line+=line.Length==0?token.TrimStart():token;
                }
                if(line.Length>0) lines.Add(line.TrimEnd());
                if(lines.Count>1&&width(lines[lines.Count-1])<label.fontSize*3)
                {
                    int last=lines.Count-1;string previous=lines[last-1];
                    // Move complete tokens: substring balancing can split 1.5 or 2回合.
                    while(width(lines[last])<label.fontSize*5 && previous.Length>0)
                    {
                        string tail=LastReadableToken(previous);
                        if(previous.Length==tail.Length || width(tail+lines[last])>maxWidth-8)break;
                        lines[last]=tail+lines[last];previous=previous.Substring(0,previous.Length-tail.Length);
                    }
                    lines[last-1]=previous.TrimEnd();
                }
                result.Add(string.Join("\n",lines));
            }
            return string.Join("\n",result);
        }

        private static string LastReadableToken(string value)
        {
            var tokens=ReadableTokens(value);
            return tokens.Count>0?tokens[tokens.Count-1]:"";
        }

        private static List<string> ReadableTokens(string value)
        {
            var tokens=new List<string>();string opening="";
            foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(value,@"被动技能|主动技能|战争祝福|职业|子职|束缚|油腻|倒伏|圣光|燃烧|残响|强酸|中毒|目盲|腐朽|覆霜|感电|恐慌|流血|潮湿|冻结|震慑|跛脚|魅惑|失能|眩晕|基础伤害|攻击|运算|左手|右手|队友|对手|教官|老兵|目标|确认|点数|[+−-]?\d+(?:[/.～→+−×]\d+)*(?:回合|生命|点|层|%)?|[A-Za-z]+|."))
            {
                string token=match.Value;
                if("（《【「『(".Contains(token)){opening+=token;continue;}
                if(opening.Length==0&&tokens.Count>0&&"，。；：！？、）》】%」』)]".Contains(token))
                    tokens[tokens.Count-1]+=token;
                else{tokens.Add(opening+token);opening="";}
            }
            if(opening.Length>0)tokens.Add(opening);
            return tokens;
        }

        /// <summary>旁白按面板宽度连续排版；在固定高度内保持完整文字。</summary>
        public static void SetNarrationText(Text label,string value,float width,int fontSize,bool reflow=false)
        {
            if(reflow)value=(value??"").Replace("\r","").Replace("\n"," ");
            for(int size=fontSize;size>=17;size--)
            {
                label.fontSize=size;
                label.text=WrapReadableText(label,value,width);
                if(label.preferredHeight<=label.rectTransform.rect.height+1)break;
            }
        }
        public static readonly Color Gold = new Color(0.89f, 0.76f, 0.53f);
        public static readonly Color Ink = new Color(0.82f, 0.82f, 0.78f);
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string key)
        {
            Sprite result;
            if (Cache.TryGetValue(key, out result)) return result;
            var texture = Resources.Load<Texture2D>("Art/FrontEnd/v1/" + key);
            if (texture == null) { Debug.LogError("[FrontEndArt] Missing " + key); return null; }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            result = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, Border(key));
            result.name = key;
            Cache[key] = result;
            return result;
        }

        private static Vector4 Border(string key)
        {
            if (key == "UI/screen_frame") return new Vector4(96, 96, 96, 96);
            if (key.StartsWith("UI/panel_") || key.StartsWith("UI/roster_tile_")) return new Vector4(80, 80, 80, 80);
            if (key.StartsWith("UI/mode_button_")) return new Vector4(64, 48, 64, 48);
            if (key.StartsWith("UI/tab_") || key.StartsWith("UI/list_row_")) return new Vector4(40, 32, 40, 32);
            if (key.StartsWith("UI/icon_button_")) return new Vector4(32, 32, 32, 32);
            if (key == "UI/title_banner") return new Vector4(80, 32, 80, 32);
            if (key == "UI/divider_horizontal") return new Vector4(80, 8, 80, 8);
            if (key == "UI/divider_vertical") return new Vector4(8, 80, 8, 80);
            if (key.StartsWith("UI/scroll_")) return new Vector4(8, 24, 8, 24);
            return Vector4.zero;
        }

        public static void Apply(Image image, string key, bool sliced = true)
        {
            image.sprite = Get(key);
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.color = Color.white;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        public static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Image(Transform parent, string name, string key, Vector2 pos, Vector2 size, bool sliced = true)
        {
            var rect = Rect(parent, name, pos, size);
            var image = rect.gameObject.AddComponent<Image>();
            Apply(image, key, sliced);
            image.raycastTarget = false;
            return image;
        }

        public static Image Emblem(Transform parent, string name, string id, Vector2 pos, Vector2 size)
        {
            var image = Rect(parent, name, pos, size).gameObject.AddComponent<Image>();
            image.sprite = BattleArt.Get("Subclasses/" + (id == "blank" ? "fighter_champion" : id));
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        public static Text Text(Transform parent, string name, string value, Vector2 pos,
            Vector2 size, int fontSize = 24, TextAnchor anchor = TextAnchor.MiddleCenter, bool title = false)
        {
            var text = Rect(parent, name, pos, size).gameObject.AddComponent<Text>();
            text.font = title ? UiFont.Title : UiFont.Regular;
            text.fontSize = fontSize;
            text.color = title ? Gold : Ink;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        public static Button Button(Transform parent, string name, string label, Vector2 pos,
            Vector2 size, string family = "tab", int fontSize = 24)
        {
            var rect = Rect(parent, name, pos, size);
            rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            Text(rect, "Label", label, Vector2.zero, size - new Vector2(30, 10), fontSize);
            StyleButton(button, family);
            return button;
        }

        public static void StyleButton(Button button, string family = "tab", bool selected = false)
        {
            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            string prefix = "UI/" + family + "_";
            Apply(image, prefix + (selected ? "selected" : "normal"));
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Get(prefix + "hover"), pressedSprite = Get(prefix + "pressed"),
                selectedSprite = Get(prefix + (selected ? "selected" : "hover")), disabledSprite = Get(prefix + "disabled")
            };
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = Gold;
        }

        /// <summary>徽记在装饰框正中，名称单独占下方一行，避免框饰穿过名字。</summary>
        public static void RoleTile(Button button, string id, string displayName, int fontSize, string iconKey = null)
        {
            var rect = (RectTransform)button.transform;
            float height = rect.sizeDelta.y;
            float frameHeight = height - 34;
            Vector2 center = new Vector2(0, 17);
            var frame = Image(rect, "TileFrame", "UI/roster_tile_normal", center,
                new Vector2(rect.sizeDelta.x, frameHeight));
            button.GetComponent<Image>().color = Color.clear;
            button.targetGraphic = frame;
            StyleButton(button, "roster_tile");
            float iconSize = Mathf.Min(rect.sizeDelta.x - 20, frameHeight - 12);
            var halo = Image(rect, "SelectionHalo", "UI/selection_halo", center, new Vector2(iconSize + 12, iconSize + 12), false);
            halo.gameObject.SetActive(false);
            if (iconKey == null) Emblem(rect, "Emblem", id, center, new Vector2(iconSize, iconSize));
            else
            {
                var emblem = Rect(rect, "Emblem", center, new Vector2(iconSize, iconSize)).gameObject.AddComponent<Image>();
                BattleArt.Apply(emblem, iconKey);
                emblem.preserveAspect = true;
                emblem.raycastTarget = false;
            }
            var label = button.GetComponentInChildren<Text>();
            label.text = displayName;
            label.font = UiFont.Bold;
            label.fontSize = fontSize;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 16; label.resizeTextMaxSize = fontSize;
            label.color = new Color(0.98f, 0.89f, 0.66f);
            label.rectTransform.sizeDelta = new Vector2(rect.sizeDelta.x + 4, 36);
            label.verticalOverflow = VerticalWrapMode.Overflow;
            UiInkAlignment.Center(label, new Vector2(0, -height / 2 + 14));
            label.transform.SetAsLastSibling();
        }

        /// <summary>实际可拖动、可滚轮的区域。内容高度由调用者或 ContentSizeFitter 提供。</summary>
        public static ScrollRect Scroll(Transform parent, string name, Vector2 pos, Vector2 size, out RectTransform content)
        {
            var go = new GameObject(name, typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            go.GetComponent<Image>().color = Color.clear;
            content = Rect(rect, "Content", Vector2.zero, new Vector2(size.x - 24, size.y));
            content.anchorMin = content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 1);
            var scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = rect;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 44;
            var track = Image(rect, "Scrollbar", "UI/scroll_track", new Vector2(size.x * 0.5f - 8, 0), new Vector2(12, size.y));
            track.raycastTarget = true;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            var handle = Image(track.transform, "Handle", "UI/scroll_thumb_normal", Vector2.zero, new Vector2(12, 80));
            handle.rectTransform.sizeDelta = Vector2.zero;
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.transition = Selectable.Transition.SpriteSwap;
            bar.spriteState = new SpriteState { highlightedSprite = Get("UI/scroll_thumb_hover"), pressedSprite = Get("UI/scroll_thumb_pressed") };
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
    }
}
