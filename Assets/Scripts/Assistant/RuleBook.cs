using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 规则说明的文案。
    ///
    /// 为什么独立成一层：规则是**数据**，放在 UI 里就没法验证；
    /// 而"规则说明和引擎实际行为是否一致"正是最容易出错、
    /// 也最该被测试盯住的地方（写错规则比写错代码更误导玩家）。
    /// </summary>
    public static class RuleBook
    {
        /// <summary>
        /// 攻击点数，**直接从引擎的效果判定表推导**，
        /// 而不是在文案里写死 —— 否则改了规则，文案就过时了。
        /// </summary>
        public static string AttackValues()
        {
            var values = new List<int>(3);
            for (int v = Hand.MinValue; v <= Hand.MaxValue; v++)
            {
                HandEffectType effect = Hand.GetEffectType(v);
                if (effect == HandEffectType.Attack4
                    || effect == HandEffectType.Attack6
                    || effect == HandEffectType.Attack8)
                {
                    values.Add(v);
                }
            }

            return string.Join(" / ", values);
        }

        /// <summary>护盾对应的数字（同样从引擎推导）。</summary>
        public static int ShieldValue()
        {
            for (int v = Hand.MinValue; v <= Hand.MaxValue; v++)
                if (Hand.GetEffectType(v) == HandEffectType.Shield) return v;

            return 5;
        }

        /// <summary>基础规则（不含角色技能）。</summary>
        public static string BasicRules()
        {
            var sb = new StringBuilder();

            sb.AppendLine("【一回合做什么】");
            sb.AppendLine("先选自己的一只可用手，再选另一名存活角色的一只可用手作加数。只改变运算手，加数手不变；队友也能提供加数。");
            sb.AppendLine("结果取个位，0变1。例如3＋9＝12→2，2＋8＝10→1。通常不能借自己的另一只手。");
            sb.AppendLine();
            sb.AppendLine("【数字效果】");
            sb.AppendLine($"{AttackValues()}：攻击；{Hand.HealAmount}：回复 {Hand.HealAmount} 点生命；{ShieldValue()}：合盾；其他数字无效果。");
            sb.AppendLine("攻击的基础伤害等于攻击数字；回复不超过生命上限。效果仅在运算完成时触发，手值保留不会重复触发。初始分配的5不是盾，9也不立即回复。");
            sb.AppendLine();
            sb.AppendLine("【护盾与伤害】");
            sb.AppendLine("运算得到攻击数字后，再选射程内的目标；目标不必是加数手的主人。距离按存活角色座位环的最短路径计算，阵亡者移出；∞表示通常射程不限。");
            sb.AppendLine("每段伤害先算基础伤害＋攻击加成−防御加成，最低0，再结算状态和防护。正防御减伤，负防御增加承伤；多段攻击逐段结算。");
            sb.AppendLine("完整盾抵挡一段攻击后破碎；有两面盾时由防守方选盾。主动用盾手运算会先碎盾。闪避或取消不会造成命中。");
            sb.AppendLine("同类型免疫使伤害为0，并阻止本次魔法附加状态及元素反应；否则先算易伤×1.5，再算抗性减半，分别向上取整。免疫不清除已有状态。");
            sb.AppendLine("一次攻击落地后，全部临时抗性、易伤和免疫一起消耗；多段共享本次保护，完全盾挡、闪避或取消不消耗。永久抗性不随攻击消耗。");
            sb.AppendLine();
            sb.AppendLine("【开局与操作】");
            sb.AppendLine("入门通常从1/1开始。经典1v1双方各投1d20：小点先分配，大点先行动；同点同时分配。2～18拆为两个1～9；1由对手指定两手，19至少一手9，20任意分配。");
            sb.AppendLine("经典2v2按两队最大骰点、再比最小骰点定座位，较大队坐1与4；通常按4→3→2→1分配手值，特殊骰点按界面提示处理，1号位先行动。");
            sb.AppendLine("冻结、双手不可用或无合法运算时可跳过；主动跳过须满足界面所示条件。跳过仍推进回合并结算状态。无合法攻击目标时可放弃攻击，保留运算后的手值。");
            sb.AppendLine("右上“战斗记录”可展开；姓名旁图标可查看状态与持续时间。状态通常持续目标自己的2个回合，同种刷新、不叠加强度，具体以图标说明为准。");
            sb.AppendLine("束缚使每个自身回合随机一手不能运算，仍可被借用；油腻在运算后有50%概率倒伏2回合。火焰命中油腻时清油腻、伤害×1.5，照常附燃烧。");
            sb.AppendLine("秘法手册分伤害类型、负面状态、元素反应、抗性／易伤／免疫、角色标记五页。元素反应共九条，包括强酸与中毒互相刷新至2回合。");
            sb.AppendLine();
            sb.AppendLine("【胜负与模式】");
            sb.AppendLine($"1v1 生命归零即败；2v2 一队全灭即败（1、4 同队，2、3 同队）。打满 {GameManager.DefaultRoundLimit} 轮，比个人／队伍总血量，相同平局。一轮为所有存活角色各行动一次。");
            sb.AppendLine("8人阵营局分守序／中立／混乱三队，人数3／3／2；最后存活阵营胜，50轮后比阵营总生命，并列平局。双将需选两名不同的同阵营子职；攻防相加，生命上限和射程取较大值。");
            sb.AppendLine("阵营局直接击杀敌方，可额外行动剩余敌方人数次；误杀队友，其他阵营各行动一次。奖惩不推进轮次。");
            sb.AppendLine($"身份局：1 主公、2 忠臣、4 反贼、1 内奸，{GameManager.IdentityRoundLimit} 轮上限（到限未胜为平局）。主公基础上限＋5、攻防＋1；守序「明主」每名存活守序单位防御＋1，中立「霸王」每名存活中立单位攻击＋1，混乱「暴君」每名存活混乱单位体力上限＋5（均含自己，死亡实时刷新）。主忠消灭反贼与内奸即胜，内奸须独活。主公倒下通常反贼胜；若主公前三阵亡立即判反贼胜。内奸在剩 5 人时亮身份并强化；亮身份后主公倒下且仍有反贼，忠臣退场，内奸与反贼继续。忠臣可用替死待命救主公。");
            sb.AppendLine("身份局击杀反贼可额外行动剩余反贼数次；主公误杀忠臣，存活反贼各行动一次。身份与技能细节见图鉴及局内提示。");
            sb.AppendLine("身份局开局按实际阵线计数：主公／忠臣为2名队友、5名对手；反贼为3名队友、4名对手；内奸为0名队友、7名对手。人数随阵亡变化，未知身份的座位不标队友或对手。");

            return sb.ToString();
        }

        /// <summary>角色技能一览（按 <paramref name="roster"/> 的顺序）。</summary>
        public static string CharacterList(IReadOnlyList<CharacterDefinition> roster)
        {
            if (roster == null || roster.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("【角色技能】");
            sb.AppendLine();

            for (int i = 0; i < roster.Count; i++)
            {
                CharacterDefinition def = roster[i];
                sb.Append("· ").Append(def.DisplayName).Append("（")
                  .Append(CharacterCardText.DescribeStats(def)).Append("）").AppendLine();
                sb.Append("    ").AppendLine(def.Description);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 完整的规则说明，用于右上角问号弹窗。
        ///
        /// ⚠️ **不再列出角色介绍**（玩家要求）。
        ///
        ///    原因：主菜单右上角已经有**图鉴**，里面每个角色都有
        ///    技能、操作指导、如何针对与特殊交互 —— 比这里一行字详细得多。
        ///    两处各写一份只会**互相过时**。所以这里只留一句指引。
        /// </summary>
        public static string Full(IReadOnlyList<CharacterDefinition> roster)
            => BasicRules()
             + "\n【角色】\n"
             + "共同规则以此页为准；各子职的操作与特殊效果请查主菜单下方「角色图鉴」。";

        /// <summary>
        /// **当前已经可以玩的模式**（玩家要求：
        /// "更新没有开发模式的占位符，显示可游玩的所有模式而不只有 1v1"）。
        ///
        /// ⚠️ 原来这里只写了"1v1 · 人机对战"，但 2v2 其实**已经能玩**，
        ///    所以那是一条**过时且误导**的文案。
        /// </summary>
        public static IReadOnlyList<string> PlayableModes()
            => new List<string>
            {
                "1 v 1 · 人机对战（可选入门 / 经典，入门可选难度）",
                "2 v 2 · 人机练习（四人同屏）",
                "8 人阵营混战 · 人机（单将，8 人）",
                "8 人双将阵营混战 · 人机（双将，8 人）",
                "8 人身份对局 · 人机（随机身份，8 人）",
            };

        /// <summary>"敬请期待"弹窗的文案。</summary>
        public static string ComingSoon(string feature)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{feature}正在开发中，敬请期待。");
            sb.AppendLine();
            sb.AppendLine("目前可以玩的模式：");

            IReadOnlyList<string> modes = PlayableModes();
            for (int i = 0; i < modes.Count; i++) sb.AppendLine("· " + modes[i]);

            return sb.ToString();
        }
    }
}
