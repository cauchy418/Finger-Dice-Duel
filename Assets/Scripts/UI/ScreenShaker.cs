using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 屏幕震动。挂在 Canvas 根节点上，通过偏移它的 <c>anchoredPosition</c> 实现。
    ///
    /// 为什么要单独一个组件：震动需要每帧更新，而 <see cref="GameBoardUI"/> 是被
    /// "状态变化时刷新一次"驱动的，没有每帧逻辑。分开之后互不干扰。
    ///
    /// 关键点：**结束时必须把位置还原**，否则画面会永久偏掉。
    /// </summary>
    public sealed class ScreenShaker : MonoBehaviour
    {
        private RectTransform _target;
        private Vector2 _basePosition;

        private float _magnitude;
        private float _remaining;
        private float _duration;
        private float _delay;
        private bool _running;

        /// <summary>绑定要震动的节点（Canvas 根）。</summary>
        public void Bind(RectTransform target)
        {
            _target = target;
            if (_target != null) _basePosition = _target.anchoredPosition;
        }

        /// <summary>
        /// 开始一次震动。
        /// </summary>
        /// <param name="magnitude">最大偏移（像素）。</param>
        /// <param name="duration">持续时长（秒）。</param>
        /// <param name="delay">延迟多久开始（秒），用于多段之间错开。</param>
        public void Shake(float magnitude, float duration, float delay = 0f)
        {
            if (_target == null || magnitude <= 0f || duration <= 0f) return;

            // 已在震动时取较强的一次，避免后一次把强的盖掉
            if (_running && magnitude < _magnitude) return;

            _magnitude = magnitude;
            _duration = duration;
            _remaining = duration;
            _delay = Mathf.Max(0f, delay);
            _running = true;
        }

        private void Update()
        {
            if (!_running || _target == null) return;

            if (_delay > 0f)
            {
                _delay -= Time.unscaledDeltaTime;
                return;
            }

            _remaining -= Time.unscaledDeltaTime;

            if (_remaining <= 0f)
            {
                Stop();
                return;
            }

            // 幅度随时间衰减，看起来像"被打了一下然后稳住"
            float decay = _remaining / _duration;
            float amplitude = _magnitude * decay;

            // 左右为主、上下略小 —— 横向晃动比纯随机更容易被眼睛捕捉到
            float x = Random.Range(-1f, 1f) * amplitude;
            float y = Random.Range(-0.75f, 0.75f) * amplitude;

            _target.anchoredPosition = _basePosition + new Vector2(x, y);
        }

        /// <summary>立刻停止并还原位置。</summary>
        public void Stop()
        {
            _running = false;
            _remaining = 0f;
            _delay = 0f;
            _magnitude = 0f;

            if (_target != null) _target.anchoredPosition = _basePosition;
        }

        /// <summary>是否正在震动。</summary>
        public bool IsShaking => _running;

        private void OnDisable() => Stop();
    }
}
