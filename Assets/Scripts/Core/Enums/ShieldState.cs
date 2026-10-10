namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 一只手的护盾生命周期状态。
    ///   None    -> 未合盾（普通状态，可参与运算）
    ///   Formed  -> 已合盾（可抵挡一次攻击；运算前若被指定为源手则自动破碎）
    ///   Broken  -> 已破碎（本回合内的瞬时状态，用于避免连锁破盾递归）
    /// </summary>
    public enum ShieldState
    {
        None = 0,
        Formed = 1,
        Broken = 2
    }
}
