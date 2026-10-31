using FingerDiceDuel.Assistant;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBootstrap
    {
        private readonly MusicPolicy _musicPolicy = new MusicPolicy();
        private MusicPlayer _music;
        private ScreenGroup _screenSettings;
        private Button _musicToggle;
        private void BuildMusicSettings()
        {
            _music = gameObject.AddComponent<MusicPlayer>(); _music.Initialize();
            _screenSettings = new ScreenGroup();
            _screenSettings.Add(MakeSubTitle(_selectRoot, "SettingsTitle", "设置").gameObject);
            _musicToggle = FrontEndArt.Button(_selectRoot, "MusicToggle", "", new Vector2(0, 65), new Vector2(520, 80), fontSize: 30);
            _musicToggle.onClick.AddListener(() => { _music.SetMusicEnabled(!_music.MusicEnabled); RefreshMusicSetting(); });
            _screenSettings.Add(_musicToggle.gameObject);
            _screenSettings.Add(FrontEndArt.Text(_selectRoot, "MusicSettingHint", "关闭背景音乐与鼓点，保留战斗音效。", new Vector2(0, -40), new Vector2(780, 60), 23).gameObject);
            _screenSettings.Add(MakeBackButton(_selectRoot, GameScreen.MainMenu, -240).gameObject);
            RefreshMusicSetting();
        }
        private void RefreshMusicSetting()
        { _musicToggle.GetComponentInChildren<Text>().text = _music.MusicEnabled ? "背景音乐：开启" : "背景音乐：关闭"; }
        private void UpdateMusic()
        {
            if (_music == null) return;
            bool combat = _screen == GameScreen.InMatch || _screen == GameScreen.DiceRoll || _screen == GameScreen.TwoOnTwoInitiative;
            _musicPolicy.Sync(_game, combat, _adventure?.LevelNumber ?? 0);
            _music.SetCue(_musicPolicy.Cue, _musicPolicy.Percussion);
        }
    }
}
