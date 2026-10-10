using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 出招选择器：从合法运算中挑一步。
    /// 与 <see cref="IChoiceProvider"/>（负责"盾选哪面/是否发动技能"）分开，
    /// 因为出招需要看局面，而抉择接口接收的是已经准备好的候选列表。
    /// </summary>
    public interface IMoveSelector
    {
        /// <summary>选出一步合法运算；无合法步时返回 <c>default(LegalMove)</c>（其 Actor 为 null）。</summary>
        LegalMove ChooseMove(GameManager game, PlayerState actor);

        /// <summary>用于日志与统计的简短标识。</summary>
        string Describe { get; }
    }
}
