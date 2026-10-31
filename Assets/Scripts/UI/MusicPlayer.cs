using FingerDiceDuel.Assistant;
using UnityEngine;

namespace FingerDiceDuel.UI
{
    /// <summary>DSP-scheduled intro/loop pairs and a phase-aligned, separately mixed percussion stem.</summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        public const string PreferenceKey = "FingerDiceDuel.Audio.MusicEnabled";
        private sealed class Bank
        {
            public AudioSource Intro, Loop, DrumIntro, DrumLoop;
            public float Gain, Target;
            public void Stop() { Intro.Stop(); Loop.Stop(); DrumIntro.Stop(); DrumLoop.Stop(); Gain = Target = 0; }
        }
        private readonly Bank[] _banks = new Bank[2];
        private int _active;
        private bool _ready, _hasCue;
        private float _drums;
        private bool _intense;
        public bool MusicEnabled { get; private set; }
        public MusicCue CurrentCue { get; private set; }
        public bool PercussionActive => _intense;
        public float PercussionGain => _drums;
        public void Initialize()
        {
            if (_ready) return;
            _ready = true; MusicEnabled = PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            for (int i = 0; i < 2; i++)
                _banks[i] = new Bank { Intro = Source("Intro" + i), Loop = Source("Loop" + i), DrumIntro = Source("DrumIntro" + i), DrumLoop = Source("DrumLoop" + i) };
        }
        private AudioSource Source(string label)
        {
            var child = new GameObject("Music" + label); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = 0; source.volume = 0; source.priority = 96; return source;
        }
        public void SetMusicEnabled(bool enabled)
        {
            Initialize(); MusicEnabled = enabled;
            PlayerPrefs.SetInt(PreferenceKey, enabled ? 1 : 0); PlayerPrefs.Save(); ApplyVolumes();
        }
        public void SetCue(MusicCue cue, bool percussion)
        {
            Initialize();
            if (!_hasCue || CurrentCue != cue)
            {
                CurrentCue = cue; _hasCue = true;
                _banks[_active].Target = 0; _active = 1 - _active;
                var bank = _banks[_active]; bank.Stop(); bank.Target = 1;
                string name = cue == MusicCue.Dusk ? "dusk" : cue == MusicCue.Dawn ? "dawn" : "battlefield";
                bank.Intro.clip = Resources.Load<AudioClip>("Audio/Music/" + name + "_intro");
                bank.Loop.clip = Resources.Load<AudioClip>("Audio/Music/" + name + "_loop");
                bank.DrumIntro.clip = cue == MusicCue.Dusk ? null : Resources.Load<AudioClip>("Audio/Music/" + name + "_drums_intro");
                bank.DrumLoop.clip = cue == MusicCue.Dusk ? null : Resources.Load<AudioClip>("Audio/Music/" + name + "_drums_loop");
                double begin = AudioSettings.dspTime + .15;
                if (bank.Intro.clip != null && bank.Loop.clip != null)
                {
                    double join = begin + (double)bank.Intro.clip.samples / bank.Intro.clip.frequency;
                    Schedule(bank.Intro, begin, false); Schedule(bank.Loop, join, true);
                    Schedule(bank.DrumIntro, begin, false); Schedule(bank.DrumLoop, join, true);
                }
            }
            _intense = cue != MusicCue.Dusk && percussion;
        }
        private static void Schedule(AudioSource source, double when, bool loop)
        { source.loop = loop; if (source.clip != null) source.PlayScheduled(when); }
        private void Update()
        {
            if (!_ready) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            _drums = Mathf.MoveTowards(_drums, _intense ? 1 : 0, dt / 1.5f);
            foreach (var bank in _banks)
            {
                bank.Gain = Mathf.MoveTowards(bank.Gain, bank.Target, dt / 1.6f);
                if (bank.Target == 0 && bank.Gain == 0) bank.Stop();
            }
            ApplyVolumes();
        }
        private void ApplyVolumes()
        {
            if (!_ready) return;
            foreach (var bank in _banks)
            {
                bank.Intro.mute = bank.Loop.mute = bank.DrumIntro.mute = bank.DrumLoop.mute = !MusicEnabled;
                bank.Intro.volume = bank.Loop.volume = bank.Gain * .48f;
                bank.DrumIntro.volume = bank.DrumLoop.volume = bank.Gain * _drums * .34f;
            }
        }
        private void OnDisable() { if (_ready) foreach (var bank in _banks) bank.Stop(); _hasCue = false; }
    }
}
