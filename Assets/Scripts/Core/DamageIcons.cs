using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **物理伤害**（玩家本轮新增的三类）。
    ///
    /// 与法师的 10 种"魔法伤害类型"（<see cref="DamageType"/>）并列，
    /// 构成速查表上的**三大类**：
    ///   · 魔法伤害（10 种，见 <see cref="DamageType"/>）
    ///   · 物理伤害（3 种，见本枚举）
    /// </summary>
    public enum PhysicalDamageType
    {
        None = 0,

        /// <summary>挥砍：刀剑类造成的伤害。</summary>
        Slashing = 1,

        /// <summary>钝击：钝器造成的伤害。</summary>
        Bludgeoning = 2,

        /// <summary>穿刺：远程武器或刺剑类造成的伤害。</summary>
        Piercing = 3,
    }

    /// <summary>
    /// **伤害图标**：每种伤害一个符号，**由代码绘制**（不是字体字符）。
    ///
    /// ⚠️ 为什么不用字体字符：
    ///    本项目的中文字体（Noto Sans CJK SC）里
    ///    **没有** ⚔ 剑 / 🔨 锤 / 🏹 弓 / ⚡ 闪电 / ❄ 雪花 …（已逐个查过 cmap），
    ///    而回退字体也不全有 —— 结果就是"图标显示成空白或方框"。
    ///    所以改为**自己画**：形状与颜色 100% 可控、不依赖任何字体。
    ///
    /// ⚠️ 为什么放在 Core：
    ///    UI 是叶子层、**测试项目引用不到** —— 画法放在那里就没法测。
    ///    这里只产出"像素"，由界面转成 <c>Texture2D</c>。
    /// </summary>
    public static class DamageIcons
    {
        /// <summary>图标边长（像素）。</summary>
        public const int Size = 28;

        /// <summary>物理伤害的统一颜色 —— 中性灰（玩家要求"保持造成伤害显示的颜色"）。</summary>
        public const int PhysicalColor = 0x9AA0AC;

        // ============================================================
        // 形状语言（够画这些图标了，刻意保持很小）
        // ============================================================

        private enum Kind { Rect, Circle, Ring, Triangle, Poly, Line }

        private sealed class Shape
        {
            public Kind Kind;
            public float X, Y, W, H;
            public float Rot;              // 旋转（度）
            public bool Filled = true;
            public int Thickness = 2;
            public float[] Points;         // 归一化 (0..1) 的多边形顶点
        }

        private static Shape R(float x, float y, float w, float h, float rot = 0f)
            => new Shape { Kind = Kind.Rect, X = x, Y = y, W = w, H = h, Rot = rot };

        private static Shape C(float x, float y, float r)
            => new Shape { Kind = Kind.Circle, X = x, Y = y, W = r };

        private static Shape G(float x, float y, float r, int thick = 2)
            => new Shape { Kind = Kind.Ring, X = x, Y = y, W = r, Thickness = thick };

        private static Shape T(float x, float y, float w, float h, float rot = 0f)
            => new Shape { Kind = Kind.Triangle, X = x, Y = y, W = w, H = h, Rot = rot };

        private static Shape L(float x1, float y1, float x2, float y2, int thick = 2)
            => new Shape { Kind = Kind.Line, X = x1, Y = y1, W = x2, H = y2, Thickness = thick };

        private static Shape P(params float[] pts)
            => new Shape { Kind = Kind.Poly, Points = pts };

        // ============================================================
        // 各伤害的图形（归一化坐标：0..1，左上原点）
        // ============================================================

        /// <summary>
        /// 取某个伤害的图形。
        ///
        /// 设计原则：**形状本身要能表达语义**（不是随便一个几何块），
        /// 且 13 个互不相同、缩到 28px 也认得出。
        /// </summary>
        private static List<Shape> ShapesOf(string key)
        {
            switch (key)
            {
                // ---------- 魔法伤害（10 种）----------
                case "thunder":      // 残响：同心声波
                    return new List<Shape>
                    {
                        G(0.5f, 0.5f, 0.11f, 2), G(0.5f, 0.5f, 0.23f, 2),
                        G(0.5f, 0.5f, 0.35f, 2), C(0.5f, 0.5f, 0.05f),
                    };

                case "acid":         // 强酸：倒三角 + 滴落
                    return new List<Shape>
                    {
                        T(0.5f, 0.36f, 0.52f, 0.42f, 180f), C(0.5f, 0.80f, 0.08f),
                    };

                case "poison":       // 中毒：骷髅
                    return new List<Shape>
                    {
                        C(0.5f, 0.40f, 0.26f),
                        R(0.36f, 0.30f, 0.11f, 0.11f), R(0.53f, 0.30f, 0.11f, 0.11f),
                        R(0.42f, 0.58f, 0.16f, 0.12f),
                        R(0.36f, 0.74f, 0.06f, 0.10f), R(0.47f, 0.74f, 0.06f, 0.10f),
                        R(0.58f, 0.74f, 0.06f, 0.10f),
                    };

                case "radiant":      // 目盲：被强光刺到的眼睛
                    return new List<Shape>
                    {
                        C(0.5f, 0.5f, 0.32f), C(0.5f, 0.5f, 0.13f),
                        L(0.12f, 0.86f, 0.88f, 0.14f, 3),
                    };

                case "necrotic":     // 腐朽：断口十字
                    return new List<Shape>
                    {
                        // 四段围成"缺口在中心"的十字（每段都够长，28px 才认得出）
                        R(0.435f, 0.06f, 0.13f, 0.32f), R(0.435f, 0.62f, 0.13f, 0.32f),
                        R(0.06f, 0.435f, 0.32f, 0.13f), R(0.62f, 0.435f, 0.32f, 0.13f),
                    };

                case "fire":         // 燃烧：火苗
                    return new List<Shape>
                    {
                        // 纯外焰 —— 原来还叠了一个内心圆，缩到 28px 会糊成一团
                        P(0.50f, 0.03f, 0.74f, 0.40f, 0.85f, 0.60f, 0.78f, 0.86f,
                          0.50f, 0.97f, 0.22f, 0.86f, 0.15f, 0.60f, 0.26f, 0.40f),
                    };

                case "cold":         // 寒冷：雪花
                    return new List<Shape>
                    {
                        L(0.5f, 0.10f, 0.5f, 0.90f, 2), L(0.12f, 0.30f, 0.88f, 0.70f, 2),
                        L(0.12f, 0.70f, 0.88f, 0.30f, 2),
                    };

                case "lightning":    // 闪电：三道折线
                    return new List<Shape>
                    {
                        P(0.56f, 0.04f, 0.30f, 0.52f, 0.46f, 0.52f,
                          0.38f, 0.96f, 0.74f, 0.44f, 0.56f, 0.44f, 0.70f, 0.04f),
                    };

                case "force":        // 力场：带核心的力场泡
                    return new List<Shape>
                    {
                        G(0.5f, 0.5f, 0.34f, 3), C(0.5f, 0.5f, 0.11f),
                    };

                case "psychic":      // 心灵：思维辐射
                    return new List<Shape>
                    {
                        C(0.5f, 0.5f, 0.20f), C(0.5f, 0.42f, 0.05f),
                        L(0.50f, 0.06f, 0.50f, 0.20f, 2), L(0.50f, 0.80f, 0.50f, 0.94f, 2),
                        L(0.06f, 0.50f, 0.20f, 0.50f, 2), L(0.80f, 0.50f, 0.94f, 0.50f, 2),
                        L(0.16f, 0.16f, 0.26f, 0.26f, 2), L(0.74f, 0.74f, 0.84f, 0.84f, 2),
                        L(0.16f, 0.84f, 0.26f, 0.74f, 2), L(0.74f, 0.26f, 0.84f, 0.16f, 2),
                    };

                // ---------- 物理伤害（3 种）----------
                case "slashing":     // 挥砍：刀
                    return new List<Shape>
                    {
                        // ⚠️ 一把**完整的刀**：刀身 + 刀尖 + 护手 + 刀柄，
                        //    全部沿同一条 45° 对角线。
                        //    （我第一版四块各自旋转 45° 但没有对齐，
                        //      看起来是四段虚线，玩家一眼就认不出是刀。）
                        R(0.56f, 0.42f, 0.12f, 0.52f, 45f),     // 刀身
                        T(0.86f, 0.18f, 0.17f, 0.19f, 225f),    // 刀尖
                        R(0.30f, 0.68f, 0.38f, 0.10f, 135f),    // 护手（垂直于刀）
                        R(0.175f, 0.805f, 0.10f, 0.26f, 45f),   // 刀柄
                    };

                case "bludgeoning":  // 钝击：锤
                    return new List<Shape>
                    {
                        // 宽扁锤头 + 粗柄 + 柄尾
                        // （柄太细会看不出是锤 —— 第一版就是这样）
                        R(0.5f, 0.19f, 0.70f, 0.34f),
                        R(0.5f, 0.60f, 0.18f, 0.56f),
                        R(0.5f, 0.925f, 0.40f, 0.11f),
                    };

                case "piercing":     // 穿刺：箭矢
                    return new List<Shape>
                    {
                        // 箭头（斜向右上）
                        T(0.78f, 0.24f, 0.26f, 0.30f, 45f),
                        // 箭杆
                        L(0.16f, 0.84f, 0.74f, 0.28f, 3),
                        // 尾羽
                        L(0.18f, 0.66f, 0.36f, 0.84f, 3),
                        L(0.30f, 0.58f, 0.48f, 0.76f, 3),
                    };

                default:
                    return new List<Shape> { C(0.5f, 0.5f, 0.18f) };
            }
        }

        // ============================================================
        // 对外：键 → 像素
        // ============================================================

        /// <summary>魔法伤害类型的图标键。</summary>
        public static string KeyOf(DamageType t)
        {
            switch (t)
            {
                case DamageType.Thunder: return "thunder";
                case DamageType.Acid: return "acid";
                case DamageType.Poison: return "poison";
                case DamageType.Radiant: return "radiant";
                case DamageType.Necrotic: return "necrotic";
                case DamageType.Fire: return "fire";
                case DamageType.Cold: return "cold";
                case DamageType.Lightning: return "lightning";
                case DamageType.Force: return "force";
                case DamageType.Psychic: return "psychic";
                default: return "force";
            }
        }

        /// <summary>物理伤害类型的图标键。</summary>
        public static string KeyOf(PhysicalDamageType t)
        {
            switch (t)
            {
                case PhysicalDamageType.Slashing: return "slashing";
                case PhysicalDamageType.Bludgeoning: return "bludgeoning";
                case PhysicalDamageType.Piercing: return "piercing";
                default: return "slashing";
            }
        }

        /// <summary>
        /// 把某个图标画成 **RGBA32** 像素（长度 = <see cref="Size"/>² × 4，行序**自下而上**）。
        ///
        /// ⚠️ 行序自下而上是为了**直接**喂给 <c>Texture2D.LoadRawTextureData</c> /
        ///    <c>SetPixels32</c>（Unity 的纹理原点在左下）。
        ///
        /// <paramref name="rgb"/> 是图形颜色；背景透明。
        /// </summary>
        public static byte[] Render(string key, int rgb)
        {
            return Render(Size, key, rgb);
        }

        /// <summary>指定尺寸的版本（供测试用更小的尺寸快速跑）。</summary>
        public static byte[] Render(int size, string key, int rgb)
        {
            var px = new byte[size * size * 4];
            byte r = (byte)((rgb >> 16) & 0xFF);
            byte g = (byte)((rgb >> 8) & 0xFF);
            byte b = (byte)(rgb & 0xFF);

            List<Shape> shapes = ShapesOf(key);
            var mask = new bool[size * size];

            for (int i = 0; i < shapes.Count; i++)
                Rasterize(shapes[i], size, mask);

            for (int y = 0; y < size; y++)
            {
                // 行序翻转：mask 是"左上原点"，输出要"左下原点"
                int src = y * size;
                int dst = (size - 1 - y) * size;

                for (int x = 0; x < size; x++)
                {
                    if (!mask[src + x]) continue;

                    int o = (dst + x) * 4;
                    px[o] = r;
                    px[o + 1] = g;
                    px[o + 2] = b;
                    px[o + 3] = 255;
                }
            }

            return px;
        }

        // ============================================================
        // 光栅化（把形状语言画进 mask；坐标都是归一化 0..1）
        // ============================================================

        private static void Rasterize(Shape s, int n, bool[] mask)
        {
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n;
                    float v = (y + 0.5f) / n;

                    // 旋转：把采样点反向旋转到形状的局部坐标
                    float lu = u, lv = v;
                    if (s.Rot != 0f)
                    {
                        float rad = -s.Rot * 3.14159265f / 180f;
                        float dx = u - s.X, dy = v - s.Y;
                        lu = s.X + dx * Cos(rad) - dy * Sin(rad);
                        lv = s.Y + dx * Sin(rad) + dy * Cos(rad);
                    }

                    if (Inside(s, lu, lv, u, v)) mask[y * n + x] = true;
                }
            }
        }

        private static bool Inside(Shape s, float lu, float lv, float u, float v)
        {
            switch (s.Kind)
            {
                case Kind.Rect:
                    return System.Math.Abs(lu - s.X) <= s.W * 0.5f
                        && System.Math.Abs(lv - s.Y) <= s.H * 0.5f;

                case Kind.Circle:
                    return Dist2(lu, lv, s.X, s.Y) <= s.W * s.W;

                case Kind.Ring:
                    {
                        float d2 = Dist2(lu, lv, s.X, s.Y);
                        float r = s.W;
                        float inner = r - s.Thickness / (float)Size;
                        if (inner < 0f) inner = 0f;
                        return d2 <= r * r && d2 >= inner * inner;
                    }

                case Kind.Triangle:
                    {
                        // 等腰三角形：顶点在 (X, Y-H/2)，底边在 Y+H/2
                        float half = s.W * 0.5f;
                        float top = s.Y - s.H * 0.5f;
                        float bot = s.Y + s.H * 0.5f;

                        if (lv < top || lv > bot) return false;
                        float t = (lv - top) / (bot - top);
                        float w = half * t;
                        return System.Math.Abs(lu - s.X) <= w;
                    }

                case Kind.Line:
                    return DistToSegment(u, v, s.X, s.Y, s.W, s.H)
                           <= (s.Thickness * 0.5f) / Size;

                case Kind.Poly:
                    return InsidePoly(s.Points, u, v);

                default:
                    return false;
            }
        }

        private static bool InsidePoly(float[] pts, float u, float v)
        {
            bool inside = false;
            int n = pts.Length / 2;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = pts[i * 2], yi = pts[i * 2 + 1];
                float xj = pts[j * 2], yj = pts[j * 2 + 1];

                if ((yi > v) != (yj > v)
                    && u < (xj - xi) * (v - yi) / (yj - yi) + xi)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static float Dist2(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx, dy = ay - by;
            return dx * dx + dy * dy;
        }

        private static float DistToSegment(float px, float py, float ax, float ay,
                                            float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float len2 = dx * dx + dy * dy;
            if (len2 <= 0f) return Sqrt(Dist2(px, py, ax, ay));

            float t = ((px - ax) * dx + (py - ay) * dy) / len2;
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;

            return Sqrt(Dist2(px, py, ax + t * dx, ay + t * dy));
        }

        private static float Sqrt(float x)
            => (float)System.Math.Sqrt(x);

        private static float Sin(float x) => (float)System.Math.Sin(x);
        private static float Cos(float x) => (float)System.Math.Cos(x);
    }
}
