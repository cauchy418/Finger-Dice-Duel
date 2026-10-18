using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// **吟游诗人的 AI**：决定"要不要发动 / 选哪一项 / 选几个目标 / 选谁"。
    ///
    /// 放在 Characters 层（而不是 Assistant）：它只依赖 Core 的类型，
    /// 而且和角色规则放在一起更好维护；Assistant 那边只负责"什么时候问"。
    /// </summary>
    public static class BardAi
    {
        /// <summary>一次发动决定。</summary>
        public struct Decision
        {
            public bool Activate;
            public BardLogic.BuffKind Kind;
            public List<PlayerState> Targets;
        }

        /// <summary>
        /// 决策规则（尽量简单、可解释）：
        ///   · 能**削弱对手防御**且对手血量低 -> 削弱防御（最能滚雪球）
        ///   · 否则若自己血量低 -> 增幅自己防御
        ///   · 否则 -> 削弱对手攻击（减伤）或增幅自己攻击
        /// 目标数固定用 3（强度 ±1）还是 1（强度 ±3）？
        ///   · 单目标强度最大；只有 1v1 时天然只有 1~2 个目标。
        ///   · 这里按"能选到的对手数"取，优先 1 个对手（强度 ±3）。
        /// </summary>
        public static Decision Decide(PlayerState bard, GameManager game)
        {
            var none = new Decision { Activate = false };
            if (bard == null || game == null) return none;
            if (!BardLogic.CanActivateFor(bard)) return none;
            if (!(bard.Character is BardLogic logic)) return none;

            List<PlayerState> eligible = logic.EligibleTargets(bard, game);
            if (eligible.Count == 0) return none;

            // 找出"敌人"（不同队的、活着的）
            var enemies = new List<PlayerState>(2);
            var allies = new List<PlayerState>(2);
            for (int i = 0; i < eligible.Count; i++)
            {
                PlayerState p = eligible[i];
                if (ReferenceEquals(p, bard) || game.IsTeammate(bard, p)) allies.Add(p);
                else enemies.Add(p);
            }

            BardLogic.BuffKind kind;
            var targets = new List<PlayerState>(3);

            bool selfHurt = bard.Health * 2 <= bard.MaximumHealth;

            if (enemies.Count > 0 && selfHurt)
            {
                // 自己危险：削弱对手攻击（少挨打）
                kind = BardLogic.BuffKind.WeakenAttack;
                targets.Add(enemies[0]);
            }
            else if (enemies.Count > 0)
            {
                // 正常：削弱对手防御，让后续攻击更疼
                kind = BardLogic.BuffKind.WeakenDefense;
                targets.Add(enemies[0]);
            }
            else if (allies.Count > 0)
            {
                // 没有敌人可选（理论上 1v1 不会）：给自己/队友加攻击
                kind = BardLogic.BuffKind.BoostAttack;
                targets.Add(bard);
            }
            else
            {
                return none;
            }

            // 还有余量时把同类的目标补到 3 个（强度会从 ±3 降到 ±2 / ±1）
            // ⚠️ 这里**故意只选 1 个**：强度 ±3 的价值高于摊薄成 ±1。
            return new Decision { Activate = true, Kind = kind, Targets = targets };
        }

        /// <summary>执行一次 AI 决策。</summary>
        public static bool TryActivate(PlayerState bard, GameManager game)
        {
            if (bard == null || game == null) return false;
            if (!(bard.Character is BardLogic logic)) return false;

            Decision d = Decide(bard, game);
            if (!d.Activate) return false;

            return logic.Activate(bard, game, d.Kind, d.Targets) > 0;
        }
    }
}
