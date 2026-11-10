using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>普通实战和胜率采样共用配置；实验仍可直接构造 GreedyChoiceProvider。</summary>
    public static class ProductionAi
    {
        public static GreedyChoiceProvider Create(GameManager game = null, bool learnedFocus = true)
        {
            return new GreedyChoiceProvider
            {
                Game = game,
                UseChainBreak = true,
                UseValidatedTuning = true,
                UseLearnedFocus = learnedFocus,
            };
        }
    }
}
