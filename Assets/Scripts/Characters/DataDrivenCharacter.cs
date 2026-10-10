namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 白板角色：不携带任何特殊逻辑，仅走 Core 默认规则。
    /// 可作为占位或未配置特殊能力的角色实例。
    /// </summary>
    public class DataDrivenCharacter : CharacterTemplate
    {
        public override string DisplayName { get; }

        public DataDrivenCharacter(string displayName = "Blank")
        {
            DisplayName = string.IsNullOrEmpty(displayName) ? "Blank" : displayName;
        }
    }
}
