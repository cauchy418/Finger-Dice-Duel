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
            sb.AppendLine("每回合你必须用自己的一只手，和对手的一只手相加，做一次运算。");
            sb.AppendLine("被操作的是你自己那只手：它的数字变成运算结果。");
            sb.AppendLine();
            sb.AppendLine("【运算】");
            sb.AppendLine("两数相加后取个位数；和为 10 则变成 1。");
            sb.AppendLine("例：3 + 9 = 12 → 2　　2 + 8 = 10 → 1　　1 + 9 = 10 → 1");
            sb.AppendLine();
            sb.AppendLine("【结果代表什么】");
            sb.AppendLine($"{AttackValues()} = 攻击，造成对应点数的伤害");
            sb.AppendLine($"{Hand.HealAmount} = 回复 {Hand.HealAmount} 点生命（不超过上限）");
            sb.AppendLine($"{ShieldValue()} = 合拢手掌形成护盾，可抵挡下一次攻击");
            sb.AppendLine("其他数字 = 无效果");
            sb.AppendLine();
            sb.AppendLine("【护盾与伤害】");
            sb.AppendLine("护盾只能挡一次攻击，挡下后破碎。若双手都是护盾，");
            sb.AppendLine("由**防守方**决定用哪一面去挡。");
            sb.AppendLine();
            sb.AppendLine("【攻防加成】");
            sb.AppendLine("每次攻击的伤害 = 基础点数 ÷ 段数 + 攻方攻击加成 - 守方防御加成。");
            sb.AppendLine("负的防御表示易伤：受到的伤害增加。");
            sb.AppendLine();
            sb.AppendLine("【胜负】");
            sb.AppendLine($"生命先归零者败。超过 {GameManager.TurnLimit} 回合仍未分出胜负则判和局。");

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

        /// <summary>完整的规则说明（基础 + 角色），用于右上角问号弹窗。</summary>
        public static string Full(IReadOnlyList<CharacterDefinition> roster)
            => BasicRules() + "\n" + CharacterList(roster);

        /// <summary>"敬请期待"弹窗的文案。</summary>
        public static string ComingSoon(string feature)
            => $"{feature}正在开发中，敬请期待。\n\n" +
               "目前可以玩的是：\n" +
               "· 1v1 · 人机对战";
    }
}
