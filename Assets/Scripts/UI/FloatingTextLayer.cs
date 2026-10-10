using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 飘字层：在角色**名字那一行**上方飘出伤害 / 格挡 / 闪避 / 回复。
    ///
    /// 为什么单独一层：角色行是静态布局，飘字要能自由出现在任意位置并自行销毁，
    /// 混在一起会让 <see cref="GameBoardUI"/> 变成一团。这一层只负责"播"，
    /// "该飘什么、飘在谁头上、间隔多久"由 <see cref="AnimationDirector"/> 决定
    /// （那部分是纯数据，已被测试覆盖）。
    /// </summary>
    public sealed class FloatingTextLayer : MonoBehaviour
    {
        /// <summary>飘字字号（比手牌数字略大，便于一眼捕捉）。</summary>
        public const int FloatFontSize = 36;

        /// <summary>上飘距离（像素）。</summary>
        public const float RiseDistance = 60f;

        /// <summary>单条飘字的总时长（秒）。</summary>
        public const float Lifetime = 0.9f;

        /// <summary>开始淡出的时间比例（前 40% 保持不透明）。</summary>
        public const float FadeStartRatio = 0.4f;

        /// <summary>同一位置的飘字错开多少像素，避免重叠成一片。</summary>
        private const float LaneOffsetPx = 46f;

        private RectTransform _root;

        private void EnsureRoot()
        {
            if (_root != null) return;

            _root = transform as RectTransform;
        }

        /// <summary>
        /// 播放一批飘字。每个条目自带"相对延迟"，所以同一批会依次错开出现。
        /// </summary>
        /// <param name="texts">要播放的飘字（含锚点坐标，坐标为画布本地坐标）。</param>
        public void Play(IReadOnlyList<FloatSpawn> texts)
        {
            if (texts == null || texts.Count == 0) return;

            EnsureRoot();
            if (_root == null) return;

            for (int i = 0; i < texts.Count; i++)
            {
                FloatSpawn spawn = texts[i];
                CreateOne(spawn, i);
            }
        }

        private void CreateOne(FloatSpawn spawn, int index)
        {
            var go = new GameObject("Float", typeof(Text));
            go.transform.SetParent(_root, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(220f, 56f);

            // 已经在飘的、以及同一批里的其他条目，各自往下错开一点，避免叠在一起
            float lane = index * LaneOffsetPx;
            rect.anchoredPosition = spawn.AnchoredPosition + new Vector2(0f, -lane);

            Text text = go.GetComponent<Text>();
            text.text = spawn.Text;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            UiFont.ApplyBold(text);      // 飘字用粗体更容易看清
            text.fontSize = FloatFontSize;
            text.color = ColorOf(spawn.Kind);

            var anim = go.AddComponent<FloatAnimator>();
            anim.Begin(rect, spawn.DelaySeconds, text);
        }

        /// <summary>各类飘字的颜色。</summary>
        public static Color ColorOf(FloatKind kind)
        {
            switch (kind)
            {
                case FloatKind.Damage: return new Color(1f, 0.36f, 0.32f);      // 红
                case FloatKind.Blocked: return new Color(0.82f, 0.85f, 0.92f);  // 灰白
                case FloatKind.Dodged: return new Color(0.45f, 0.88f, 0.95f);   // 青
                case FloatKind.Heal: return new Color(0.42f, 0.92f, 0.50f);     // 绿
                default: return Color.white;
            }
        }
    }

    /// <summary>
    /// 一条待播放的飘字：内容 + 种类 + 锚点（画布本地坐标）+ 延迟。
    ///
    /// 与 <see cref="AnimationDirector"/> 的 <c>FloatTextSpec</c> 的区别：
    /// 后者的 Target 是 <see cref="PlayerState"/>（纯逻辑），
    /// 这里已经把"那位玩家的名字行在哪里"解析成了坐标。
    /// </summary>
    public readonly struct FloatSpawn
    {
        public string Text { get; }
        public FloatKind Kind { get; }
        public Vector2 AnchoredPosition { get; }
        public float DelaySeconds { get; }

        public FloatSpawn(string text, FloatKind kind, Vector2 anchoredPosition, float delaySeconds)
        {
            Text = text ?? "";
            Kind = kind;
            AnchoredPosition = anchoredPosition;
            DelaySeconds = delaySeconds;
        }
    }

    /// <summary>
    /// 单条飘字的动画：先等 <c>delay</c> 秒，再上飘并淡出，最后自毁。
    ///
    /// 挂在飘字自己的 GameObject 上，因此多条飘字可以**同时**按各自的延迟独立播放
    /// —— 这也是"错开 0.25 秒"能实现的原因。
    /// </summary>
    public sealed class FloatAnimator : MonoBehaviour
    {
        private RectTransform _rect;
        private Text _text;
        private Vector2 _start;
        private float _delay;
        private float _elapsed;
        private bool _started;

        public void Begin(RectTransform rect, float delaySeconds, Text text)
        {
            _rect = rect;
            _text = text;
            _start = rect.anchoredPosition;
            _delay = Mathf.Max(0f, delaySeconds);
        }

        private void Update()
        {
            if (_rect == null) { Destroy(gameObject); return; }

            _elapsed += Time.unscaledDeltaTime;

            if (!_started)
            {
                if (_elapsed < _delay) return;
                _started = true;
                _elapsed = 0f;
            }

            float t = Mathf.Clamp01(_elapsed / FloatingTextLayer.Lifetime);

            // 上飘：缓出（一开始快、后面慢），看起来更"弹"
            float rise = 1f - Mathf.Pow(1f - t, 3f);
            _rect.anchoredPosition = _start + new Vector2(0f, FloatingTextLayer.RiseDistance * rise);

            // 淡出：前段保持不透明
            if (_text != null)
            {
                float fade = t <= FloatingTextLayer.FadeStartRatio
                    ? 1f
                    : 1f - (t - FloatingTextLayer.FadeStartRatio) / (1f - FloatingTextLayer.FadeStartRatio);

                Color c = _text.color;
                c.a = Mathf.Clamp01(fade);
                _text.color = c;
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
