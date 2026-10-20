using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>按实际字形的可见顶点居中，避免 CJK 字体的行框与数字墨迹中心不同。</summary>
    public static class UiInkAlignment
    {
        public static void Center(Text text, Vector2 center)
        {
            text.rectTransform.anchoredPosition = center;
            var generator = text.cachedTextGenerator;
            generator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size));
            var vertices = generator.verts;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool found = false;
            for (int i = 0; i + 3 < vertices.Count; i += 4)
            {
                Vector2 a = vertices[i].position, b = vertices[i + 2].position;
                if (Mathf.Abs(a.x - b.x) < 0.01f || Mathf.Abs(a.y - b.y) < 0.01f) continue;
                for (int j = 0; j < 4; j++)
                {
                    Vector2 point = vertices[i + j].position;
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
                found = true;
            }
            if (found) text.rectTransform.anchoredPosition = center - (min + max) * (0.5f / text.pixelsPerUnit);
        }
    }
}
