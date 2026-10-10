using System.Collections.Generic;
using UnityEngine;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 音效播放层：按 <see cref="SoundDirector"/> 给的**纯数据参数**
    /// 现场合成 AudioClip 并播放，因此**不需要任何音频资源文件**。
    ///
    /// 为什么程序化合成而不是放音频文件：
    ///   · 不需要外部资源、没有授权问题；
    ///   · 音效参数是纯数据，可以被无头测试覆盖（"该响哪个音"可验证）；
    ///   · 想换成真实音效时，只需替换本类，参数层不动。
    ///
    /// 采样用简单的"方波 + 指数衰减包络"，听感是复古电子音，够用且轻量。
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        /// <summary>总开关。关掉后完全不合成、不播放。</summary>
        public bool Enabled = true;

        /// <summary>总体音量。</summary>
        [Range(0f, 1f)] public float MasterVolume = 0.7f;

        /// <summary>采样率。偏低一点足够用，合成更快。</summary>
        private const int SampleRate = 22050;

        /// <summary>同时可播放的音源数量。</summary>
        private const int VoiceCount = 6;

        private AudioSource[] _voices;
        private int _nextVoice;

        /// <summary>合成过的音效缓存（同参数只合成一次）。</summary>
        private readonly Dictionary<string, AudioClip> _cache
            = new Dictionary<string, AudioClip>();

        /// <summary>延迟播放的队列项。</summary>
        private struct Pending
        {
            public SfxSpec Spec;
            public float DueAt;
        }

        private readonly List<Pending> _pending = new List<Pending>();

        private void Awake()
        {
            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);

                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;      // 2D
                _voices[i] = src;
            }
        }

        /// <summary>播放一次结算对应的音效序列。</summary>
        public void PlayTurn(TurnResult result)
        {
            if (!Enabled || result == null) return;

            List<SfxSpec> specs = SoundDirector.Build(result);
            for (int i = 0; i < specs.Count; i++) Play(specs[i]);
        }

        /// <summary>播放单个音效（带延迟）。</summary>
        public void Play(SfxSpec spec)
        {
            if (!Enabled || spec.Duration <= 0f || spec.Volume <= 0f || _voices == null) return;

            if (spec.DelaySeconds <= 0f)
            {
                PlayNow(spec);
                return;
            }

            _pending.Add(new Pending
            {
                Spec = spec,
                DueAt = Time.unscaledTime + spec.DelaySeconds,
            });
        }

        private void Update()
        {
            if (_pending.Count == 0) return;

            float now = Time.unscaledTime;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].DueAt > now) continue;

                PlayNow(_pending[i].Spec);
                _pending.RemoveAt(i);
            }
        }

        /// <summary>停止一切并清空待播队列（退出对局 / 重开时调用）。</summary>
        public void StopAll()
        {
            _pending.Clear();

            if (_voices == null) return;
            for (int i = 0; i < _voices.Length; i++)
                if (_voices[i] != null) _voices[i].Stop();
        }

        private void PlayNow(SfxSpec spec)
        {
            AudioClip clip = GetOrCreateClip(spec);
            if (clip == null) return;

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            voice.volume = Mathf.Clamp01(spec.Volume * MasterVolume);
            voice.clip = clip;
            voice.Play();
        }

        /// <summary>同参数的音效只合成一次，之后复用。</summary>
        private AudioClip GetOrCreateClip(SfxSpec spec)
        {
            string key = $"{spec.StartFrequency:0.#}_{spec.EndFrequency:0.#}_{spec.Duration:0.###}";

            if (_cache.TryGetValue(key, out AudioClip cached) && cached != null) return cached;

            AudioClip clip = Synthesize(spec);
            if (clip != null) _cache[key] = clip;

            return clip;
        }

        /// <summary>
        /// 合成一个音效：频率从 start 滑到 end，方波，指数衰减包络。
        ///
        /// 用方波而不是正弦是为了"更有打击感"（谐波丰富，听感更硬）。
        /// </summary>
        public static AudioClip Synthesize(SfxSpec spec)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * spec.Duration));
            var data = new float[sampleCount];

            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;

                // 频率线性滑动
                float freq = Mathf.Lerp(spec.StartFrequency, spec.EndFrequency, t);
                phase += freq / SampleRate;

                // 方波
                float square = (phase % 1f) < 0.5f ? 1f : -1f;

                // 指数衰减 + 极短的淡入（避免起音爆音）
                float decay = Mathf.Exp(-5f * t);
                float attack = Mathf.Min(1f, i / (SampleRate * 0.004f));

                data[i] = square * decay * attack * 0.5f;
            }

            AudioClip clip = AudioClip.Create(
                $"sfx_{spec.Kind}", sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);

            return clip;
        }
    }
}
