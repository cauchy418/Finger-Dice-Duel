using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// **吟游诗人·勇气学院（CollegeOfValor）**：攻 0 / 防 −1，**攻击距离 3**，守序中立，伤害类型**穿刺**。
    ///
    /// 玩家确认的规则：
    ///   · **每次行动结算完**检查：本回合**受过伤害**或**实际回复过体力**
    ///     （满血回复不算；状态伤害算）-> 可以发动技能
    ///   · 四步：① 是否发动 -> ② 增幅攻击 / 增幅防御 / 削弱攻击 / 削弱防御
    ///     -> ③ 选目标数 1~3（上限 = 范围内实际人数）-> ④ 选目标
    ///   · 强度 = **(4 − 目标数)**：1 人 ±3、2 人 ±2、3 人 ±1
    ///   · 所有目标**共用同一项**（同一属性、同一方向）
    ///   · 可以选**自己**；点同一人的两只手只算选到一次
    ///   · 加在**攻防加成**层（与强酸 −2 同层相加）
    ///   · 持续到你**下一次发动技能**（旧的立即全部移除）或**你死亡**
    ///
    /// ⚠️ 生效范围 = **他的攻击范围内**（3），不是"全场"。
    /// </summary>
    public sealed class CollegeOfValorLogic : CharacterTemplate, IAttackRangeProvider
    {
        public override string DisplayName => "吟游诗人·勇气学院";

        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => -1;

        /// <summary>攻击距离 **3**（玩家确认）。</summary>
        public override int BaseAttackRange => 3;

        /// <summary>最多选 3 个目标。</summary>
        public const int MaxTargets = 3;

        /// <summary>强度基数：强度 = 基数 − 目标数（所以 1 人 ±3、2 人 ±2、3 人 ±1）。</summary>
        public const int StrengthBase = 4;

        /// <summary>技能的四种选择。</summary>
        public enum BuffKind
        {
            /// <summary>增幅攻击（攻击加成 +强度）。</summary>
            BoostAttack = 0,
            /// <summary>增幅防御（防御加成 +强度）。</summary>
            BoostDefense = 1,
            /// <summary>削弱攻击（攻击加成 −强度）。</summary>
            WeakenAttack = 2,
            /// <summary>削弱防御（防御加成 −强度）。</summary>
            WeakenDefense = 3,
        }

        /// <summary>由目标数算强度（1 人 3、2 人 2、3 人 1）。</summary>
        public static int StrengthFor(int targetCount)
        {
            if (targetCount < 1) targetCount = 1;
            if (targetCount > MaxTargets) targetCount = MaxTargets;
            return StrengthBase - targetCount;
        }

        /// <summary>
        /// 本回合是否**可以发动技能**：受过伤害或实际回复过体力。
        /// （每次行动结算完之后由上层检查。）
        /// </summary>
        public static bool CanActivateFor(PlayerState collegeOfValor)
        {
            if (collegeOfValor == null || !collegeOfValor.IsAlive) return false;
            return collegeOfValor.TookDamageThisTurn || collegeOfValor.HealedThisTurn;
        }

        /// <summary>
        /// 发动技能：给 <paramref name="targets"/> 加上对应的攻防修正。
        ///
        /// ⚠️ 先**清除自己上一次给的**全部加成（玩家确认："直到你下一次发动技能"），
        ///    再给这一批目标加上新的。
        /// </summary>
        /// <returns>实际生效的目标数（0 表示没有合法目标、本次作废）。</returns>
        public int Activate(PlayerState collegeOfValor, GameManager manager,
                            BuffKind kind, IReadOnlyList<PlayerState> targets)
        {
            if (collegeOfValor == null || manager == null) return 0;

            // ① 旧的立即全部移除
            ClearOffsets(collegeOfValor, manager);

            if (targets == null || targets.Count == 0) return 0;

            int count = targets.Count;
            if (count > MaxTargets) count = MaxTargets;
            int strength = StrengthFor(count);

            int attackOffset = 0;
            int defenseOffset = 0;
            switch (kind)
            {
                case BuffKind.BoostAttack: attackOffset = strength; break;
                case BuffKind.BoostDefense: defenseOffset = strength; break;
                case BuffKind.WeakenAttack: attackOffset = -strength; break;
                case BuffKind.WeakenDefense: defenseOffset = -strength; break;
            }

            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                PlayerState t = targets[i];
                if (t == null) continue;
                t.SetCollegeOfValorOffset(collegeOfValor, attackOffset, defenseOffset);
                applied++;
                string effectName = attackOffset != 0 ? "攻击" : "防御";
                int delta = attackOffset != 0 ? attackOffset : defenseOffset;
                manager.NoteSkill(collegeOfValor, "战斗激励", $"{t.Name} {effectName}{delta:+0;-0;0}，持续至下次发动或自身阵亡");
            }

            return applied;
        }

        /// <summary>清除自己给过的全部加成（再次发动前 / 死亡时）。</summary>
        public void ClearOffsets(PlayerState collegeOfValor, GameManager manager)
        {
            if (manager == null) return;
            IReadOnlyList<PlayerState> all = manager.Players;
            for (int i = 0; i < all.Count; i++)
                all[i]?.ClearCollegeOfValorOffsetFrom(collegeOfValor);
        }

        /// <summary>
        /// 技能可选的**目标**：自己 + 攻击范围内的所有角色（含队友与对手）。
        ///
        /// 玩家确认：可以选自己；点同一人的两只手只算一次 ——
        /// 所以这里按**角色**列出，而不是按手牌。
        /// </summary>
        public List<PlayerState> EligibleTargets(PlayerState collegeOfValor, GameManager manager)
        {
            var list = new List<PlayerState>(4);
            if (collegeOfValor == null || manager == null) return list;

            IReadOnlyList<PlayerState> all = manager.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;

                if (ReferenceEquals(p, collegeOfValor)) { list.Add(p); continue; }   // 自己也算
                if (manager.CanReach(collegeOfValor, p)) list.Add(p);
            }

            return list;
        }

        /// <summary>伤害类型**穿刺**（不分距离）。</summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Piercing;
    }
}
