using UnityEngine;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 界面字体加载。集中在一处，方便以后换字体。
    ///
    /// 字体：<b>Noto Sans CJK SC</b>（SIL Open Font License 1.1，可商用可分发；
    /// 见 <c>Resources/Fonts/NotoSansCJKsc-LICENSE.txt</c>）。
    /// 一个文件同时覆盖中文与拉丁字母，所以**不需要额外的英文字体**。
    ///
    /// 为什么用 <c>Medium</c> 而不是 <c>Regular</c>：
    /// 之前用的 Noto Sans SC 是**可变字体**（variable font），引擎只会取它的默认实例，
    /// 渲染出来偏细、发虚。换成静态 OTF 后正文字重改用 Medium，小字号下明显更清楚；
    /// 标题用真的 Bold（而不是让引擎做**伪粗体**合成）。
    ///
    /// 查找顺序（任何一步失败都继续往下试，保证界面不会因为缺字体而变空白）：
    ///   1. <c>Resources/Fonts/NotoSansCJKsc-Medium.otf</c> —— 正文
    ///   2. <c>Resources/Fonts/NotoSansCJKsc-Bold.otf</c> —— 标题 / 强调
    ///   3. Unity 内置 <c>LegacyRuntime.ttf</c>
    ///   4. Unity 内置 <c>Arial.ttf</c>（更旧的版本）
    ///
    /// 注意：字体资源放在 <c>Assets/Resources/Fonts/</c> 下，用 <see cref="Resources.Load{T}"/>
    /// 按**相对 Resources 的路径且不带扩展名**读取。
    /// </summary>
    public static class UiFont
    {
        /// <summary>正文字体在 Resources 下的路径（不带扩展名）。</summary>
        public const string RegularResourcePath = "Fonts/NotoSansCJKsc-Medium";

        /// <summary>粗体在 Resources 下的路径（不带扩展名）。</summary>
        public const string BoldResourcePath = "Fonts/NotoSansCJKsc-Bold";

        private static Font _regular;
        private static Font _bold;
        private static bool _resolved;

        /// <summary>正文字体（首次调用时解析并缓存）。</summary>
        public static Font Regular { get { Resolve(); return _regular; } }

        /// <summary>
        /// 粗体。若没有独立的粗体文件，则回退为正文字体，
        /// 由调用方决定是否再用 <see cref="FontStyle.Bold"/> 做合成粗体。
        /// </summary>
        public static Font Bold { get { Resolve(); return _bold; } }

        /// <summary>项目是否提供了独立的粗体文件（否则是伪粗体）。</summary>
        public static bool HasRealBold
        {
            get { Resolve(); return _bold != null && !ReferenceEquals(_bold, _regular); }
        }

        /// <summary>取界面字体（等价于 <see cref="Regular"/>，保留旧调用点可用）。</summary>
        public static Font Get() => Regular;

        /// <summary>把字体应用到一段文字（正文字重）。</summary>
        public static void Apply(UnityEngine.UI.Text text)
        {
            if (text == null) return;

            text.font = Regular;
            text.fontStyle = FontStyle.Normal;
        }

        /// <summary>
        /// 把字体应用到一段文字并加粗。
        ///
        /// 有真粗体就用真粗体（并把 fontStyle 置回 Normal，避免二次合成）；
        /// 没有才让引擎做伪粗体。
        /// </summary>
        public static void ApplyBold(UnityEngine.UI.Text text)
        {
            if (text == null) return;

            if (HasRealBold)
            {
                text.font = Bold;
                text.fontStyle = FontStyle.Normal;
            }
            else
            {
                text.font = Regular;
                text.fontStyle = FontStyle.Bold;
            }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _regular = TryResource(RegularResourcePath)
                       ?? Builtin("LegacyRuntime.ttf")
                       ?? Builtin("Arial.ttf");
            _bold = TryResource(BoldResourcePath) ?? _regular;

            if (_regular == null)
            {
                Debug.LogWarning("[FingerDiceDuel] 未找到任何可用字体，将使用 Unity 默认字体。" +
                                 "中文可能显示为方块。");
            }
        }

        private static Font TryResource(string path)
        {
            Font font = Resources.Load<Font>(path);

            if (font == null)
                Debug.LogWarning($"[FingerDiceDuel] 未能加载 Resources/{path}（中文可能显示为方块）。");

            return font;
        }

        private static Font Builtin(string name)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(name);
            }
            catch
            {
                return null;
            }
        }
    }
}
