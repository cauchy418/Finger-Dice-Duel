using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>留白的绘画骰面与实时点数；本视图不生成随机数、不决定先攻。</summary>
    public sealed class D20DieView
    {
        private RectTransform _die;
        private Text _number, _caption;
        private string _name;
        public RectTransform Root { get; private set; }
        public int? Face { get; private set; }

        public static D20DieView Build(Transform parent, string name, string caption, Vector2 pos, float size)
        {
            var result = new D20DieView();
            result.Root = FrontEndArt.Rect(parent, name, pos, new Vector2(size, size + 64));
            var image = FrontEndArt.Image(result.Root, "PaintedD20", "Illustrations/d20_initiative", Vector2.zero, new Vector2(size, size), false);
            image.preserveAspect = true;
            result._die = image.rectTransform;
            result._number = FrontEndArt.Text(result._die, "Face", "?", new Vector2(0, -size * 0.035f),
                new Vector2(size * 0.46f, size * 0.5f), Mathf.RoundToInt(size * 0.24f));
            result._number.font = UiFont.Bold;
            result._number.verticalOverflow = VerticalWrapMode.Overflow;
            result._number.color = FrontEndArt.Gold;
            var shadow = result._number.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.02f, 0.01f, 0, 0.9f);
            shadow.effectDistance = new Vector2(1, -2);
            result._caption = FrontEndArt.Text(result.Root, "Caption", caption,
                new Vector2(0, -size * 0.57f), new Vector2(size + 110, 48), size >= 240 ? 25 : 21);
            result._name = caption;
            return result;
        }

        public void SetName(string name) { _name = name; _caption.text = name; }
        public void SetFace(int? value, bool final = false)
        {
            Face = value;
            _number.text = value.HasValue ? value.Value.ToString() : "?";
            _number.color = final && value == 1 ? new Color(1, 0.42f, 0.33f) : FrontEndArt.Gold;
            _caption.text = _name + (final && value == 20 ? " · 大成功" : final && value == 1 ? " · 大失败" : "");
        }
        public void Pose(float scale, float degrees)
        {
            _die.localScale = Vector3.one * scale;
            _die.localEulerAngles = new Vector3(0, 0, degrees);
            // 数字保持正向；缩放随骰子运动，字体不旋转。
            _number.rectTransform.localEulerAngles = new Vector3(0, 0, -degrees);
        }
    }
}
