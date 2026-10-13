namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **士兵**（攻 +1 / 防 +1，攻击距离 1）。
    ///
    /// 玩家要求："将现在的白板更换为士兵 Soldier +1/+1，
    /// 攻击距离 1，使其能够作为一个有点强度的角色。"
    ///
    /// 没有任何特殊能力 —— 它取代了原来的白板进入角色选择，
    /// 但比白板强（攻防各 +1），所以是一个"能打"的基础角色。
    /// </summary>
    public sealed class SoldierLogic : CharacterTemplate
    {
        public override string DisplayName => "士兵";

        public override int DefaultAttackBonus => 1;
        public override int DefaultDefenseBonus => 1;

        /// <summary>攻击距离 1（与白板一致）。</summary>
        public override int BaseAttackRange => 1;

        public override void OnMatchReset(PlayerState owner, GameManager manager) { }
    }
}
