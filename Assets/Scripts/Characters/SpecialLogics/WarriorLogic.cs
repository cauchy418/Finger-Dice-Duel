using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 战士（Warrior）特殊逻辑：
    /// 
    /// 【技能一 · 连锁破盾】
    ///   当战士自身的一面盾牌破碎时（无论是被攻击破碎，还是运算前主动破碎已有盾牌），
    ///   战士可以选择场上任意一面【完整（Formed）】的盾牌，使其立即破碎。
    ///   —— 该选择是可选的，可以放弃发动。
    ///   —— 此过程不会撤销/改变战士自己那面盾牌破碎的事实。
    ///   —— 被连锁破碎的目标盾若属于对手，同样会触发对手的 OnOwnShieldBroken（可能产生进一步连锁）。
    /// 
    /// 【技能二 · 盾牌修复】
    ///   当战士新运算出 5、在某只手上合成一面盾牌时，
    ///   若其【另一只手】为破碎（Broken）状态的盾牌，则可将其修复为 Formed。
    /// 
    /// 依赖：
    ///   - IChoiceProvider：提供“是否发动 / 选择哪一面盾牌”的抉择接口，避免在逻辑层耦合 UI。
    ///   - GameManager.BreakShieldAndNotify(...)：统一破盾入口，保证连锁反应链正确触发。
    /// </summary>
    public class WarriorLogic : CharacterTemplate
    {
        public override string DisplayName => "Warrior";

        /// <summary>
        /// 自身盾牌破碎时触发。
        /// 破碎既可能来自：对方攻击 或 运算前的主动破盾。
        /// GameManager 需要确保这两种场景都会调用本钩子。
        /// </summary>
        public override void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager)
        {
            if (manager == null) return;

            var candidates = CollectFormedShields(manager, exclude: brokenHand);
            if (candidates.Count == 0) return;

            var provider = manager.GetChoiceProvider(owner);
            if (provider == null) return;

            var picked = provider.AskWarriorChainBreakTarget(owner, candidates);
            if (picked.Shield == null) return;         // 玩家放弃
            if (picked.Owner == null) return;
            if (picked.Shield.State != ShieldState.Formed) return; // 防御性判断

            manager.BreakShieldAndNotify(picked.Owner, picked.Shield);
        }

        private static List<ChainBreakTarget> CollectFormedShields(GameManager manager, Hand exclude)
        {
            var list = new List<ChainBreakTarget>(4);
            AddIfFormed(list, manager.PlayerA, manager.PlayerA.LeftHand, exclude);
            AddIfFormed(list, manager.PlayerA, manager.PlayerA.RightHand, exclude);
            AddIfFormed(list, manager.PlayerB, manager.PlayerB.LeftHand, exclude);
            AddIfFormed(list, manager.PlayerB, manager.PlayerB.RightHand, exclude);
            return list;
        }



        /// <summary>
        /// 合成新盾时触发：如果另一只手是“破碎”盾，则修复为完整盾。
        /// 注意：这里的修复是被动、自动的（规则表述并未强调“可选”），
        /// 如需改为“可选”，把下面 if 内改为通过 IChoiceProvider 询问即可。
        /// </summary>
        public override void OnShieldFormed(PlayerState owner, Hand shieldHand, GameManager manager)
        {
            if (owner == null || shieldHand == null) return;

            var otherHand = owner.GetOtherHand(shieldHand);
            if (otherHand != null && otherHand.State == ShieldState.Broken)
            {
                otherHand.SetShieldState(ShieldState.Formed);
            }
        }

        /// <summary>
        /// 整场比赛开始时清空状态。战士的技能不持有跨回合状态，
        /// 但显式声明"已复位"便于后续扩展（例如记录本局连锁次数）。
        /// </summary>
        public override void OnMatchReset(PlayerState owner, GameManager manager) { }

        /// <summary>默认攻击 +1（可被 CharacterDefinition.AttackBonus 覆盖）。</summary>
        public override int DefaultAttackBonus => 1;
        public override int DefaultDefenseBonus => 0;


        private static void AddIfFormed(List<ChainBreakTarget> list, PlayerState owner, Hand h, Hand exclude)
        {
            if (h == exclude) return;
            if (h.State == ShieldState.Formed) list.Add(new ChainBreakTarget(owner, h));
        }
    }
}
