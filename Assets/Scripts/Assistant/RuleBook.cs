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
            sb.AppendLine("选择自己的一只手，与另一名角色的一只手相加。只改变自己用于运算的手；攻击时再选择受击角色，加数可以来自队友。");
            sb.AppendLine("取和的个位；结果为 0 时按 1 处理。例如 3+9→2，2+8→1。锁定或暗置的手不能主动运算。");
            sb.AppendLine();
            sb.AppendLine("【数字效果】");
            sb.AppendLine($"{AttackValues()}：攻击；{Hand.HealAmount}：回复 {Hand.HealAmount} 点生命；{ShieldValue()}：合盾；其他数字无效果。");
            sb.AppendLine("效果只在本次运算结算时触发一次。回复不能超过体力上限，满血回复无收益。角色能力可以改写数字效果，具体以图鉴和局内提示为准。");
            sb.AppendLine();
            sb.AppendLine("【护盾与伤害】");
            sb.AppendLine("一面盾挡一段攻击，挡后破碎；同时有两面盾时由防守方选择。用已成型的盾参与运算，会先碎盾。初始手值为 5 不算盾。");
            sb.AppendLine("每段伤害 = max(0，基础伤害 ÷ 段数 + 攻击加成 − 防御加成)。负防御会增加伤害。");
            sb.AppendLine("攻击必须在射程内；距离按存活角色座位环的最短路径计算，阵亡角色移出。闪避、护盾、减伤和免疫按技能结算。");
            sb.AppendLine("负面状态与伤害类型联动请查看「伤害与状态」中的秘法手册。");
            sb.AppendLine("圣武士算出 4/6 按合盾结算并触发冲击，无须额外确认普通攻击目标。");
            sb.AppendLine();
            sb.AppendLine("【开局与操作】");
            sb.AppendLine("经典 1v1 各投 1d20：小点先分配手值，大点先行动；普通点数拆成两个 1～9。1 由对手分配，19 至少一手为 9，20 任意分配。初始手值不触发效果。");
            sb.AppendLine("2v2 经典由掷骰定座位，按 4→3→2→1 分配，1 号位先行动。入门使用预设开局。");
            sb.AppendLine("技能触发时按提示选择目标或放弃；具体技能效果以角色图鉴和局内提示为准。右上战斗记录可展开查看。");
            sb.AppendLine("冻结或无合法运算时可跳过；双盾、潜行或同命按条件主动跳过。跳过仍结算状态。无合法攻击目标时，放弃攻击保留运算结果、不造成伤害。");
            sb.AppendLine();
            sb.AppendLine("【胜负与模式】");
            sb.AppendLine($"1v1 生命归零即败；2v2 一队全灭即败（1、4 同队，2、3 同队）。打满 {GameManager.DefaultRoundLimit} 轮，比个人／队伍总血量，相同平局。一轮为所有存活角色各行动一次。");
            sb.AppendLine("8 人阵营局分守序／中立／混乱三队：3／3／2 人；最后存活阵营胜，50 轮后比阵营总血量。双将同时拥有两名角色的能力。");
            sb.AppendLine("阵营局直接击杀敌方，可额外行动剩余敌方人数次；误杀队友，其他阵营各行动一次。奖惩不推进轮次。");
            sb.AppendLine($"身份局：1 主公、2 忠臣、4 反贼、1 内奸，{GameManager.IdentityRoundLimit} 轮上限（到限未胜为平局）。主公上限＋10、攻防＋1；主忠消灭反贼与内奸即胜，内奸须独活。主公倒下通常反贼胜；若主公前三阵亡立即判反贼胜。内奸在剩 5 人时亮身份并强化；亮身份后主公倒下且仍有反贼，忠臣退场，内奸与反贼继续。忠臣可用替死待命救主公。");
            sb.AppendLine("身份局击杀反贼可额外行动剩余反贼数次；主公误杀忠臣，存活反贼各行动一次。身份与技能细节见图鉴及局内提示。");

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
             + "各角色的技能、操作指导与针对手段，详情见图鉴\n"
             + "（主菜单下方「角色图鉴」）。";

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
