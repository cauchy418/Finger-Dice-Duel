namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 手指效果类型，根据运算结果的个位数决定。
    /// </summary>
    public enum HandEffectType
    {
        None = 0,       // 1/2/3/7 等无特殊效果的数字
        Shield = 5,     // 合拢护盾
        Attack4 = 4,    // 攻击4点
        Attack6 = 6,    // 攻击6点
        Attack8 = 8,    // 攻击8点
        Heal = 9        // 回复9点
    }
}
