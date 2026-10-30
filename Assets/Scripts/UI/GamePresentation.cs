using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 表现层总控：把 <see cref="AnimationPlan"/> 变成实际画面效果
    /// （飘字 + 受击闪烁 + 屏幕震动）。
    ///
    /// 为什么单独一层而不是塞进 <see cref="GameBoardUI"/>：
    /// 那个文件已经承担了布局与状态重绘；表现播放是另一件事，
    /// 混在一起会让两边都难改。这里只依赖"能被闪的牌"与"能震的节点"。
    ///
    /// "该播什么"全部由 <see cref="AnimationDirector"/> 决定（纯数据、可测），
    /// 本类只负责"按计划播"。
    /// </summary>
    public sealed class GamePresentation : MonoBehaviour
    {
        /// <summary>总开关：关掉后不播任何打击感（方便对比 / 排查）。</summary>
        public bool EnableImpacts = true;

        private FloatingTextLayer _floats;
        private ScreenShaker _shaker;
        private SfxPlayer _sfx;

        /// <summary>受击闪烁用的覆盖图，按玩家分组（每个玩家 2 张牌）。</summary>
        private readonly Dictionary<PlayerState, List<Image>> _flashByPlayer
            = new Dictionary<PlayerState, List<Image>>();

        /// <summary>名字行的文字，按玩家分组（用于"轮到谁"的过渡）。</summary>
        private readonly Dictionary<PlayerState, Text> _nameByPlayer
            = new Dictionary<PlayerState, Text>();

        /// <summary>当前高亮的行动方（避免每帧重复触发过渡）。</summary>
        private PlayerState _activeHighlighted;

        /// <summary>初始化：传入飘字层、震动组件，以及双方各自的闪烁覆盖图。</summary>
        public void Initialize(FloatingTextLayer floats, ScreenShaker shaker, SfxPlayer sfx = null)
        {
            _floats = floats;
            _shaker = shaker;
            _sfx = sfx;
        }

        /// <summary>音效总开关。</summary>
        public bool SoundEnabled
        {
            get => _sfx == null || _sfx.Enabled;
            set { if (_sfx != null) _sfx.Enabled = value; }
        }

        /// <summary>播放一个独立音效（例如回合切换）。</summary>
        public void PlaySfx(SfxKind kind)
        {
            if (_sfx == null) return;
            _sfx.Play(new SfxSpec(kind, 0f, 0f, 0f, 0f));
        }

        /// <summary>登记某位玩家的名字行（用于"轮到谁"的高亮）。</summary>
        public void RegisterNameRow(PlayerState player, Text nameText)
        {
            if (player == null || nameText == null) return;
            _nameByPlayer[player] = nameText;
        }

        /// <summary>
        /// 回合切换过渡：把当前行动方的名字点亮并轻微放大，其他人恢复常态。
        ///
        /// 为什么需要：原来只有底部提示条写"该谁了"，玩家很容易看漏。
        /// 让名字本身有强弱对比，一眼就知道轮到谁。
        /// 同一位行动方重复调用不会重复播动画（<see cref="_activeHighlighted"/> 去重）。
        /// </summary>
        public void ShowActivePlayer(PlayerState active)
        {
            if (ReferenceEquals(active, _activeHighlighted)) return;
            _activeHighlighted = active;

            foreach (var pair in _nameByPlayer)
            {
                Text text = pair.Value;
                if (text == null) continue;

                bool isActive = ReferenceEquals(pair.Key, active);

                var tween = text.GetComponent<NameRowTween>();
                if (tween == null) tween = text.gameObject.AddComponent<NameRowTween>();

                tween.TransitionTo(isActive);
            }
        }

        /// <summary>行动方名字高亮时的颜色。</summary>
        public static Color ActiveNameColor => new Color(1f, 0.88f, 0.45f);

        /// <summary>非行动方名字的颜色。</summary>
        public static Color IdleNameColor => new Color(0.78f, 0.82f, 0.90f);

        /// <summary>登记某位玩家的受击闪烁覆盖图（通常是他那两张手牌）。</summary>
        public void RegisterFlashTargets(PlayerState player, List<Image> flashes)
        {
            if (player == null || flashes == null) return;
            _flashByPlayer[player] = flashes;
        }

        /// <summary>播放一次结算的全部表现。</summary>
        public void Play(AnimationPlan plan, PlayerState playerA, PlayerState playerB)
        {
            if (plan == null) return;

            // 飘字：把逻辑上的"目标玩家"解析成坐标
            if (_floats != null && plan.Floats.Count > 0)
            {
                var spawns = new List<FloatSpawn>(plan.Floats.Count);
                for (int i = 0; i < plan.Floats.Count; i++)
                {
                    FloatTextSpec spec = plan.Floats[i];

                    // ⚠️ **必须把颜色一起传下去** —— 玩家要求
                    //    "格挡弹的颜色也要和攻击属性一致"。
                    //    漏传这一处时，`FloatSpawn.ColorRgb` 恒为 0，
                    //    界面就退回 `Kind` 的默认色（格挡是灰白），
                    //    等于"伤害变了、格挡没变"。
                    spawns.Add(new FloatSpawn(spec.Text, spec.Kind,
                                              AnchorOf(spec.Target, playerA, playerB),
                                              spec.DelaySeconds,
                                              spec.ColorRgb, FloatIcon(spec, plan.SoundSource)));
                }
                _floats.Play(spawns);
            }

            if (!EnableImpacts) return;

            // 打击感：按段错开震屏 + 闪牌
            for (int i = 0; i < plan.Impacts.Count; i++)
            {
                HitImpactSpec impact = plan.Impacts[i];

                if (_shaker != null)
                {
                    _shaker.Shake(impact.ShakeMagnitude,
                                  AnimationDirector.ShakeDurationFor(impact.Tier),
                                  impact.DelaySeconds);
                }

                FlashCards(impact.Target, impact.Tier, impact.DelaySeconds);
            }

            // 音效：与画面共用同一份节奏（同名常量），所以天然同步
            if (_sfx != null) _sfx.PlayTurn(plan.SoundSource);
        }

        /// <summary>停止所有表现并还原（退出对局 / 重开时调用）。</summary>
        public void StopAll()
        {
            if(_floats!=null)foreach(Transform child in _floats.transform)Destroy(child.gameObject);
            if (_shaker != null) _shaker.Stop();

            foreach (var pair in _flashByPlayer)
                foreach (Image img in pair.Value)
                    if (img != null) SetAlpha(img, 0f);

            // 名字高亮也一并复位，否则退出后残留"轮到谁"的亮色
            _activeHighlighted = null;
            foreach (var pair in _nameByPlayer)
            {
                Text text = pair.Value;
                if (text == null) continue;

                text.color = IdleNameColor;
                text.rectTransform.localScale = Vector3.one;

                var tween = text.GetComponent<NameRowTween>();
                if (tween != null) Destroy(tween);
            }
        }

        private void FlashCards(PlayerState player, HitTier tier, float delay)
        {
            if (player == null) return;
            if (!_flashByPlayer.TryGetValue(player, out List<Image> flashes)) return;

            float strength = StrengthFor(tier);
            if (strength <= 0f) return;

            foreach (Image img in flashes)
            {
                if (img == null) continue;

                var flash = img.GetComponent<CardFlasher>();
                if (flash == null) flash = img.gameObject.AddComponent<CardFlasher>();

                flash.Begin(strength, delay);
            }
        }

        /// <summary>各档的闪烁强度（覆盖图的初始不透明度）。</summary>
        public static float StrengthFor(HitTier tier)
        {
            switch (tier)
            {
                case HitTier.Light: return 0.35f;
                case HitTier.Medium: return 0.55f;
                case HitTier.Heavy: return 0.80f;
                default: return 0f;
            }
        }

        private static string FloatIcon(FloatTextSpec spec, TurnResult result)
        {
            if (spec.Kind == FloatKind.Heal) return "Overlay/heal";
            if (spec.ColorRgb == 0) return null;
            var type = result != null ? result.AttackType : AttackDamageType.None;
            if (type != AttackDamageType.None && AttackDamageTypes.ColorOf(type) == spec.ColorRgb)
                return "Damage/" + AttackDamageTypes.IconKeyOf(type);
            // 持续伤害可与本次普通攻击不同，颜色来自逻辑层的同一类型表。
            foreach (DamageType magic in StatusNames.AllTypes)
            {
                var damage = AttackDamageTypes.FromDamageType(magic);
                if (AttackDamageTypes.ColorOf(damage) == spec.ColorRgb)
                    return "Damage/" + AttackDamageTypes.IconKeyOf(damage);
            }
            return null;
        }

        private Vector2 AnchorOf(PlayerState player, PlayerState a, PlayerState b)
        {
            if (_floats == null) return Vector2.zero;

            // 复用 GameBoardUI 的锚点解析（飘字层是它的子节点，坐标系一致）
            var board = _floats.GetComponentInParent<GameBoardUI>();
            return board != null ? board.FloatAnchorOf(player, a, b) : Vector2.zero;
        }

        private static void SetAlpha(Image img, float a)
        {
            Color c = img.color;
            c.a = a;
            img.color = c;
        }
    }

    /// <summary>
    /// 名字行的"轮到谁"过渡：在常态与高亮态之间插值颜色与缩放。
    ///
    /// 为什么不做成一次性动画：行动方会在整个回合里保持高亮，
    /// 所以是"过渡到一个新状态并停在那里"，而不是"播一次就恢复"。
    /// </summary>
    public sealed class NameRowTween : MonoBehaviour
    {
        /// <summary>过渡时长（秒）。</summary>
        public const float TransitionDuration = 0.22f;

        /// <summary>高亮时的缩放倍数。</summary>
        public const float ActiveScale = 1.12f;

        private Text _text;
        private float _progress = 1f;      // 1 = 已到位
        private bool _targetActive;
        private bool _currentActive;

        private void Awake()
        {
            _text = GetComponent<Text>();
            _currentActive = false;
            _progress = 1f;
        }

        /// <summary>过渡到"高亮 / 常态"。已经在目标状态时不做任何事。</summary>
        public void TransitionTo(bool active)
        {
            if (_text == null) _text = GetComponent<Text>();
            if (_text == null) return;

            if (_targetActive == active && _progress >= 1f) return;

            _targetActive = active;
            _progress = 0f;
            _currentActive = !active;      // 从相反状态开始插值
        }

        private void Update()
        {
            if (_text == null) return;
            if (_progress >= 1f) return;

            _progress += Time.unscaledDeltaTime / TransitionDuration;
            if (_progress > 1f) _progress = 1f;

            Color from = _currentActive ? GamePresentation.ActiveNameColor : GamePresentation.IdleNameColor;
            Color to = _targetActive ? GamePresentation.ActiveNameColor : GamePresentation.IdleNameColor;

            _text.color = Color.Lerp(from, to, _progress);

            float fromScale = _currentActive ? NameRowTween.ActiveScale : 1f;
            float toScale = _targetActive ? NameRowTween.ActiveScale : 1f;
            float scale = Mathf.Lerp(fromScale, toScale, _progress);

            _text.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>
    /// 单张牌的受击闪烁：覆盖一层白色，快速淡出。
    ///
    /// 挂在覆盖图上，因此多张牌可以各自独立闪。
    /// </summary>
    public sealed class CardFlasher : MonoBehaviour
    {
        /// <summary>闪烁时长（秒）。</summary>
        public const float FlashDuration = 0.16f;

        private Image _image;
        private float _strength;
        private float _delay;
        private bool _running;

        private void Awake()
        {
            _image = GetComponent<Image>();
            SetAlpha(0f);
        }

        public void Begin(float strength, float delay)
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null) return;

            _strength = Mathf.Clamp01(strength);
            _delay = Mathf.Max(0f, delay);
            _running = true;
            SetAlpha(_strength);
        }

        private void Update()
        {
            if (!_running || _image == null) return;

            if (_delay > 0f)
            {
                _delay -= Time.unscaledDeltaTime;
                SetAlpha(0f);       // 等自己那一下时保持不亮
                return;
            }

            float alpha = _image.color.a - (_strength / FlashDuration) * Time.unscaledDeltaTime;
            if (alpha <= 0f)
            {
                SetAlpha(0f);
                _running = false;
                return;
            }

            SetAlpha(alpha);
        }

        private void SetAlpha(float a)
        {
            if (_image == null) return;

            Color c = _image.color;
            c.a = a;
            _image.color = c;
        }
    }
}
