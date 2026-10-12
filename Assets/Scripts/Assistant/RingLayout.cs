using System.Collections.Generic;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 主视角看到的**四个位置**（四人 UI 的布局规格）。
    ///
    /// 玩家确认的布局：
    ///   · **主视角自己**永远在**下方正中**
    ///   · **两名对手**固定在**偏左上**与**偏右上**
    ///   · **队友**占**偏左下**或**偏右下**，取决于他是我的**上家还是下家**
    ///     （`SeatLayout` 保证队友必然是其中之一）
    ///
    /// 具体例子（玩家给的）：
    ///   · 我是 P2、队友 P3 在我之后行动（下家）-> 队友在**偏右下**
    ///   · 我是 P3、队友 P2 在我上家        -> 队友在**偏左下**
    ///
    /// 为什么把布局做成"数据 + 纯函数"：
    ///   位置推导曾经被我算错过（队友公式写反），而它散在 UI 里
    ///   就只能靠肉眼看出来。放在这里可以**穷举四个座位**做断言。
    /// </summary>
    public enum RingPosition
    {
        /// <summary>下方正中：主视角自己。</summary>
        SelfBottom = 0,

        /// <summary>偏左上：一名对手。</summary>
        UpperLeft = 1,

        /// <summary>偏右上：一名对手。</summary>
        UpperRight = 2,

        /// <summary>偏左下：队友（当队友是上家时）。</summary>
        LowerLeft = 3,

        /// <summary>偏右下：队友（当队友是下家时）。</summary>
        LowerRight = 4,
    }

    /// <summary>一个位置上放谁（按座位号 + 相对关系）。</summary>
    public struct RingSlot
    {
        /// <summary>该位置对应的**座位号**（1..4）。</summary>
        public int Seat;

        /// <summary>该位置的角色：自己 / 队友 / 对手。</summary>
        public RingRelation Relation;

        /// <summary>该位置在屏幕上的方位。</summary>
        public RingPosition Position;
    }

    /// <summary>某人与主视角的关系。</summary>
    public enum RingRelation
    {
        Self = 0,
        Teammate = 1,
        Opponent = 2,
    }

    /// <summary>四人 UI 的布局推导（纯函数，无状态）。</summary>
    public static class RingLayout
    {
        /// <summary>
        /// 给定"主视角坐在几号位"，算出**四个位置**分别放谁。
        ///
        /// 顺序固定为 <see cref="RingPosition"/> 的枚举顺序
        /// （自己、左上、右上、左下或右下），便于界面按固定顺序建面板。
        ///
        /// 只有**四个**位置，因为四个人各占一个：
        ///   自己（下方正中）+ 队友（左下或右下）+ 两名对手（左上、右上）。
        ///
        /// ⚠️ 我一度想加"第五个位置"（把另一侧下方位也放一名对手），
        ///    那必然**重复同一个人** —— 四个人就是四个位置。
        ///    这一条是靠"穷举四个座位检查不重不漏"发现的。
        ///
        /// 对手左右分配：按**座位号升序**，第一个放左上、第二个放右上
        /// （你没有指定"哪个对手在左"，用座位号保证稳定而不引入随机）。
        /// </summary>
        public static IReadOnlyList<RingSlot> Build(int mySeat)
        {
            int me = SeatLayout.Normalize(mySeat);
            int mate = SeatLayout.TeammateSeat(me);

            // 队友在上家侧 -> 偏左下；在下家侧 -> 偏右下
            bool mateIsPrevious = SeatLayout.TeammateIsPrevious(me);
            var matePosition = mateIsPrevious
                ? RingPosition.LowerLeft
                : RingPosition.LowerRight;

            // 两名对手按座位号升序：**第一个放左上、第二个放右上**
            //（界面左右对称；"哪个对手在左"没有明确规则，用座位号保证稳定）
            var foes = new List<int>(2);
            for (int seat = 1; seat <= SeatLayout.SeatCount; seat++)
                if (seat != me && seat != mate) foes.Add(seat);

            return new List<RingSlot>(4)
            {
                new RingSlot { Seat = me, Relation = RingRelation.Self,
                               Position = RingPosition.SelfBottom },

                new RingSlot { Seat = foes[0], Relation = RingRelation.Opponent,
                               Position = RingPosition.UpperLeft },

                new RingSlot { Seat = foes[1], Relation = RingRelation.Opponent,
                               Position = RingPosition.UpperRight },

                new RingSlot { Seat = mate, Relation = RingRelation.Teammate,
                               Position = matePosition },
            };
        }

        /// <summary>取"某个方位"上的座位号（没有则 −1）。</summary>
        public static int SeatAt(IReadOnlyList<RingSlot> slots, RingPosition position)
        {
            if (slots == null) return -1;

            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Position == position) return slots[i].Seat;

            return -1;
        }

        /// <summary>取"某个方位"上的关系（没有则返回 <see cref="RingRelation.Self"/>）。</summary>
        public static RingRelation RelationAt(IReadOnlyList<RingSlot> slots,
                                              RingPosition position)
        {
            if (slots == null) return RingRelation.Self;

            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Position == position) return slots[i].Relation;

            return RingRelation.Self;
        }

        /// <summary>
        /// 四个方位的**位置编号约定**（供界面换算成屏幕坐标）。
        ///
        /// ⚠️ 这里**不能返回 UnityEngine.Vector2** ——
        /// Assistant 层不引用 UnityEngine（分层铁律，已踩过一次）。
        ///
        /// 用一个统一的**归一化坐标**（x: −1 最左 .. +1 最右；
        /// y: −1 最下 .. +1 最上），界面乘以画面尺寸即可。
        /// 好处是"四个位置不重叠""自己在最下"这类几何性质也能被无头测试断言。
        /// </summary>
        public static void NormalizedOffset(RingPosition position, out float x, out float y)
        {
            // 数值经过实机截图校准：早期版本队友用 ±0.92，
            // 在 1920 宽下会到 ±883px，加上 560px 的面板就**超出屏幕**
            // （截图里左下角的队友面板被切掉了）。
            // 现在最大 |x| = 0.72（±691px），面板半宽 350px -> 最远 1041px，
            // 仍在屏幕内。
            switch (position)
            {
                case RingPosition.SelfBottom: x = 0f; y = -0.56f; break;
                case RingPosition.UpperLeft: x = -0.42f; y = 0.62f; break;
                case RingPosition.UpperRight: x = 0.42f; y = 0.62f; break;
                case RingPosition.LowerLeft: x = -0.66f; y = -0.20f; break;
                case RingPosition.LowerRight: x = 0.66f; y = -0.20f; break;
                default: x = 0f; y = 0f; break;
            }
        }
    }
}
