using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 开局掷骰的**动画时间轴**（纯数据，不依赖 Unity）。
    ///
    /// 拆出来是为了让它能被无头测试 —— 否则"第几秒显示什么数字"
    /// 只能靠在编辑器里盯着看，而本机没法自动化验证画面。
    ///
    /// ## 形态
    ///
    /// **数字快速滚动 → 逐渐减速 → 定格**。
    /// 帧序列在构造时就**按给定随机源一次性抽定**，
    /// 之后 <see cref="FaceAt"/> 是**纯函数**（同一时刻永远同一结果）。
    ///
    /// ⚠️ 这一点很重要：UI 每帧都会问"现在该显示几"，
    /// 如果那里现抽随机数，同一帧的不同绘制点会看到不同的值（闪错）。
    /// </summary>
    public sealed class DiceRollAnimationPlan
    {
        /// <summary>骰子面数（滚动时的取值范围）。</summary>
        public const int FaceCount = 20;

        /// <summary>整个动画时长（秒）。</summary>
        public float Duration { get; }

        /// <summary>起步时每次换号的间隔（秒）—— 越小滚得越快。</summary>
        public float FastInterval { get; }

        /// <summary>收尾时每次换号的间隔（秒）。</summary>
        public float SlowInterval { get; }

        /// <summary>第 i 帧的起始时刻（秒），单调递增。</summary>
        private readonly List<float> _frameTimes = new List<float>(256);

        /// <summary>第 i 帧显示的点数（最后一帧就是真实点数）。</summary>
        private readonly List<int> _frameFaces = new List<int>(256);

        public DiceRollAnimationPlan(int finalValue, Random rng = null,
                                     float duration = 1.6f,
                                     float fastInterval = 0.045f,
                                     float slowInterval = 0.32f)
        {
            Duration = duration > 0f ? duration : 1.6f;
            FastInterval = fastInterval > 0f ? fastInterval : 0.045f;
            SlowInterval = slowInterval > 0f ? slowInterval : 0.32f;

            var r = rng ?? new Random();

            // 按"递增的间隔"预生成帧序列，最后一定落在真实点数上
            float t = 0f;
            int guard = 0;
            const int GuardLimit = 20000;   // 防御性：间隔最小时也不会爆

            while (t < Duration && guard++ < GuardLimit)
            {
                _frameTimes.Add(t);
                _frameFaces.Add(r.Next(1, FaceCount + 1));
                t += IntervalAt(t / Duration);
            }

            // 定格帧
            _frameTimes.Add(Duration);
            _frameFaces.Add(finalValue);

            FinalValue = finalValue;
        }

        /// <summary>动画结束时应显示的点数。</summary>
        public int FinalValue { get; }

        /// <summary>帧数（含定格帧）。</summary>
        public int FrameCount => _frameTimes.Count;

        /// <summary>
        /// 在进度 <paramref name="t01"/>（0..1）时，下一次换号要等多久。
        ///
        /// 用**平方**减速：开头极快、结尾明显慢下来，定格感更强。
        /// </summary>
        public float IntervalAt(float t01)
        {
            float k = Clamp01(t01);
            float eased = k * k;
            return FastInterval + (SlowInterval - FastInterval) * eased;
        }

        /// <summary>进度（0..1）—— 上层用它驱动缩放等补间。</summary>
        public float ProgressAt(float time)
            => Duration <= 0f ? 1f : Clamp01(time / Duration);

        /// <summary>
        /// 该在 <paramref name="time"/> 这一时刻显示哪个数。
        ///
        /// **纯函数**：同一 <paramref name="time"/> 永远返回同一结果。
        /// </summary>
        public int FaceAt(float time)
            => _frameFaces[IndexAt(time)];

        /// <summary>该时刻处于第几帧。</summary>
        public int IndexAt(float time)
        {
            if (time <= 0f) return 0;

            // 帧数不多（通常几十帧），线性扫描足够且无浮点累加歧义
            int i = 0;
            while (i + 1 < _frameTimes.Count && _frameTimes[i + 1] <= time) i++;
            return i;
        }

        /// <summary>该帧的起始时刻（供调试/测试）。</summary>
        public float FrameTimeAt(int index)
            => _frameTimes[Math.Max(0, Math.Min(index, _frameTimes.Count - 1))];

        /// <summary>动画是否已结束（该定格了）。</summary>
        public bool IsFinished(float time) => time >= Duration;

        /// <summary>
        /// 缩放系数：开头轻微放大、结尾回落到 1（做出"咔哒落定"的感觉）。
        /// </summary>
        public float ScaleAt(float time)
        {
            float p = ProgressAt(time);
            if (p >= 1f) return 1f;

            float bump = 1f + 0.12f * (1f - p) * (1f - p);   // 1.12 -> 1.00
            return bump;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
