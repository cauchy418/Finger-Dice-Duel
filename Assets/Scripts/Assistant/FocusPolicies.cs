using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// **按角色查表的集火策略**（由 2v2 对照实验定下来的）。
    ///
    /// 数据来源：[`Tools/focus-report.txt`](../../../Tools/focus-report.txt)
    ///   24 角色 × 600 局 × 2 段种子 × 3 组 = 86,400 局；
    ///   同局三组共享阵容 / 初始手值 / 术士骰子，只有受试角色的 AI 切换策略。
    ///
    /// 结论（focusSelf = 优先打离自己最近的敌人）：
    ///   · **总体 +3.5 点**（49.7% -> 53.2%），16 个角色显著提升、1 个显著下降；
    ///   · 提升最大的：邪术师 +17.1、刺客 +9.2、战士 +7.4、忍者 +6.7、
    ///     野蛮人 +6.6、武僧 +6.3、术士 +5.5、士兵 +4.6、赌徒 +4.5、游侠 +4.1；
    ///   · 显著变差：**狼人 -3.7**；猎人 -2.3、魔术师 -2.1（不显著，
    ///     但两段种子都为负）。
    ///   · **focusMate 总体只有 +0.1**，远弱于 Self，且没有必须用的理由 —— 不采用。
    ///
    /// 生产策略因此改成**黑名单**：
    ///   · 默认所有角色都用 `Self`；
    ///   · 猎人 / 狼人 / 魔术师保持 baseline（实验里它们用 Self 更差）；
    ///   · **邪术师也保持 baseline**：它自己用 Self 是 +17.1，但恢复 0/−1
    ///     并删除"回复减半"之后，它的 2v2 已经是 **61.6% 胜率 / 8.7% 平局**；
    ///     先把平局问题单独处理，暂不叠加集火。
    ///
    /// 1v1 只有一位对手，`ResolveFocusTarget` 直接返回 null，
    /// 所以对 1v1 行为**没有任何影响**（不会给攻击偷加常数）。
    /// </summary>
    internal static class FocusPolicies
    {
        /// <summary>
        /// 不启用 focusSelf 的角色。
        ///
        /// 猎人 / 狼人 / 魔术师是实验里 Self 更差的；邪术师要先处理 8.7% 的 2v2 平局率。
        /// 改名单前请先重跑 <c>FocusPolicyExperimentTests</c>。
        /// </summary>
        private static readonly HashSet<string> NoFocusRoleIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                CharacterDefinition.WarlockId,     // 2v2 61.6% / 8.7% 平局，先处理平局
                CharacterDefinition.HunterId,      // Self -2.3（两段都为负）
                CharacterDefinition.WerewolfId,    // Self -3.7（显著）
                CharacterDefinition.MagicianId,    // Self -2.1（两段都为负，未达显著）
            };

        /// <summary>这个角色在生产 AI 里该不该默认集火（以及用哪一种）。</summary>
        public static GreedyChoiceProvider.FocusPolicy For(CharacterDefinition definition)
        {
            if (definition == null) return GreedyChoiceProvider.FocusPolicy.None;
            return NoFocusRoleIds.Contains(definition.Id)
                ? GreedyChoiceProvider.FocusPolicy.None
                : GreedyChoiceProvider.FocusPolicy.Self;
        }
    }
}
