using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 角色卡上的文案生成（身材摘要、技能摘要）。
    ///
    /// 为什么单独一层：UI 的 <c>CharacterRosterView</c> 在 <c>FingerDiceDuel.UI</c> 下，
    /// 而**测试工程在无头环境编译，引用不到 UI 命名空间**。
    /// 文案是纯字符串处理，放在这里就能被测试覆盖 ——
    /// 尤其"狼人的实际生命上限是 25 而不是 20"这种事，显示错了玩家会以为卡片在骗人。
    /// </summary>
    public static class CharacterCardText
    {
        /// <summary>卡片上的身材摘要，例如 "25 生命    攻 -2    防 -2"。</summary>
        public static string DescribeStats(CharacterDefinition def)
        {
            if (def == null) return "";

            // ⚠️ 攻防也要问**运行时实例**，不能读定义里的静态值。
            //
            // 先例：狼人的 +5 生命由角色逻辑提供，不在数据层。
            // 骑士同理且更明显 —— 它的攻击加成**恒等于攻击距离**，
            // 会随碎盾次数变化；而定义里的 AttackBonus 写的是 0。
            // 玩家实测反馈："进入游戏显示骑士的攻防就是 +1/-1" ——
            // 棋盘上是对的（用运行时值），但角色选择卡上显示 +0/-1。
            int maxHealth = CharacterFactory.MaxHealthOf(def);

            int attack = def.AttackBonus;
            int defense = def.DefenseBonus;

            PlayerState probe = CharacterFactory.CreatePlayer(def.DisplayName, def);
            if (probe?.Character != null)
            {
                attack = probe.Character.AttackBonus;
                defense = probe.Character.DefenseBonus;
            }

            return $"{maxHealth} 生命    攻 {Signed(attack)}    防 {Signed(defense)}";
        }

        /// <summary>
        /// 技能摘要：卡片放不下完整说明，截断成一句。
        /// 优先取第一个句号 / 分号之前的内容。
        /// </summary>
        public static string Shorten(string description, int maxChars = 26)
        {
            if (string.IsNullOrEmpty(description)) return "";
            if (maxChars < 1) maxChars = 1;

            int cut = description.Length;
            foreach (char mark in new[] { '。', '；', ';' })
            {
                int at = description.IndexOf(mark);
                if (at >= 0 && at < cut) cut = at;
            }

            string head = description.Substring(0, cut);
            return head.Length <= maxChars ? head : head.Substring(0, maxChars) + "…";
        }

        private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();
    }
}
