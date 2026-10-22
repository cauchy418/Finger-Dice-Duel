using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// **开局选点策略**：在合法拆分里挑一个 AI 想要的。
    ///
    /// ## 决策顺序（按玩家确认的优先级）
    ///
    /// ```
    /// ① 先最大化自己的价值
    /// ② 若两个拆分对自己**相近**（差距 ≤ InfoAdvantageBand），
    ///    则挑一个"给对手价值更低"的
    /// ③ 若我是后手（看得到对手已选的拆分），再把"克制它"计入
    /// ```
    ///
    /// 玩家明确要求过：**优先最大化自己**，只有在两者结果相似时才考虑对手 ——
    /// 因为分配用的是"我自己投出的点数"，本来就不是我能随意决定给他什么。
    ///
    /// ## 为什么"给对手价值"不是决定性的
    ///
    /// 我投出的点数 = 我两只手之和（19/20 除外），所以**能给对手的数是受限的**。
    /// 例如投 17 只能 `8/9` 或 `9/8` —— 必然给对手一个 8 或 9。
    /// 所以这条只能当**次要的取舍依据**，不能当主目标。
    ///
    /// 纯逻辑、不依赖 Unity，可在无头测试里跑。
    /// </summary>
    public sealed class DiceSplitAdvisor
    {
        /// <summary>
        /// "对自己价值相近"的容差。
        ///
        /// 两个拆分分差在这个范围内才去看"给对手的价值" ——
        /// 也就是不让对手因素翻过自己因素。
        /// </summary>
        public int InfoAdvantageBand { get; set; } = 15;

        /// <summary>"克制对手已选"的权重（仅在我是后手、且知道对手选了什么时有意义）。</summary>
        public int CounterWeight { get; set; } = 10;

        /// <summary>
        /// 对手因素最多能翻转多少分。
        ///
        /// ⚠️ 这个上限**必须**存在：对手分的绝对范围（基线分约 100~200）
        /// 远大于自己分的差（通常几十），若不设限，
        /// "给对手更低分"会**盖过"自己更好"** —— 违反玩家确认的优先级
        /// （先最大化自己，只有相近时才看对手）。实测发生过：
        /// 野蛮人·野兽道途投 11 时选了对自己只有 203 分的 `2/9`，
        /// 而不是 275 分的 `4/7`。
        /// </summary>
        public int OpponentPenaltyCap { get; set; } = 6;

        /// <summary>
        /// 选一个拆分。
        /// </summary>
        /// <param name="me">我的角色。</param>
        /// <param name="opponent">对手的角色（可空）。</param>
        /// <param name="legalSplits">**合法**候选（必须来自 DiceRollSetup，绝不自己拼）。</param>
        /// <param name="opponentSplit">对手已经选好的拆分（后手才有；否则 null）。</param>
        public HandSplit Choose(CharacterDefinition me, CharacterDefinition opponent,
                                IReadOnlyList<HandSplit> legalSplits,
                                HandSplit? opponentSplit = null)
        {
            if (legalSplits == null || legalSplits.Count == 0)
                throw new ArgumentException("候选不能为空。", nameof(legalSplits));

            HandValueProfile mine = HandValueProfiles.For(me, opponent);
            HandValueProfile theirs = HandValueProfiles.ValueToOpponent(opponent);

            // 先算每个拆分的两把分数
            int count = legalSplits.Count;
            var myScore = new int[count];
            var theirScore = new int[count];
            var counterScore = new int[count];

            for (int i = 0; i < count; i++)
            {
                HandSplit s = legalSplits[i];
                myScore[i] = mine.Score(s);
                theirScore[i] = theirs.Score(s);

                if (opponentSplit.HasValue)
                    counterScore[i] = CounterBonus(mine, s, opponentSplit.Value);
            }

            // 先找"对自己最好"的那一档
            int bestMy = myScore[0];
            for (int i = 1; i < count; i++)
                if (myScore[i] > bestMy) bestMy = myScore[i];

            // 在"与自己最好成绩相近"的候选里挑
            int bestIndex = -1;
            int bestTotal = int.MinValue;

            for (int i = 0; i < count; i++)
            {
                if (myScore[i] < bestMy - InfoAdvantageBand) continue;   // 差太多，不考虑

                // 对手因素的上限取决于"我离自己的最好成绩还有多远" ——
                // 这样它只能在"相近"的候选之间起**打破平局**的作用，
                // 永远不会把"对我明显更好"的拆分挤掉。
                int slack = (myScore[i] - (bestMy - InfoAdvantageBand)) / 2;
                int cap = slack < OpponentPenaltyCap ? slack : OpponentPenaltyCap;

                int penalty = theirScore[i];
                if (penalty > cap) penalty = cap;

                int total = myScore[i] * 100
                          - penalty                                      // 给对手的越低越好（最多 cap 分）
                          + counterScore[i] * CounterWeight;

                if (total > bestTotal)
                {
                    bestTotal = total;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0) bestIndex = IndexOfBest(myScore);   // 保险
            return legalSplits[bestIndex];
        }

        /// <summary>
        /// 后手的信息优势：这个拆分**克制对手已选**的程度。
        ///
        /// 思路（不做过度设计）：看"我的手 + 对手已选的手"能不能形成有用的结果。
        /// 能形成得越多，说明我这组数字和他是"同一类节奏"，可以顺势压制；
        /// 同时若我这组能让**他**的关键目标更难达成，也加分。
        ///
        /// ⚠️ 这只是**小幅**调整（<see cref="CounterWeight"/> 只有 10），
        /// 不允许盖过"自己的价值"。
        /// </summary>
        private static int CounterBonus(HandValueProfile mine, HandSplit mySplit, HandSplit theirSplit)
        {
            int bonus = 0;

            // ① 我的数字与对手的数字相加能形成"我要的"结果 -> 顺手
            bonus += EffectHits(mine, mySplit.Left, theirSplit.Left);
            bonus += EffectHits(mine, mySplit.Left, theirSplit.Right);
            bonus += EffectHits(mine, mySplit.Right, theirSplit.Left);
            bonus += EffectHits(mine, mySplit.Right, theirSplit.Right);

            // ② 对手选了很高的数值（高攻）-> 我更该留防御型的数字
            //    这里用"他的拆分本身对他的价值"做参照：
            //    他越满意，我越需要能挡住的数字。
            if (theirSplit.Total >= 14 && mine.WeightOf(5) >= 50)
            {
                // 他数值很大 -> 我若有 5 可以合盾挡一次
                if (mySplit.Left == 5 || mySplit.Right == 5) bonus += 2;
            }

            return bonus;
        }

        /// <summary>两个数字相加的结果是不是我想要的（权重高于基线即算"想要"）。</summary>
        private static int EffectHits(HandValueProfile mine, int a, int b)
        {
            int result = Hand.ComputeSum(a, b);
            int w = mine.WeightOf(result);
            int baseline = HandValueProfile.Baseline[result];

            // 只有"我比基线更看重这个结果"时才加分
            return w > baseline ? 1 : 0;
        }

        private static int IndexOfBest(int[] scores)
        {
            int best = 0;
            for (int i = 1; i < scores.Length; i++)
                if (scores[i] > scores[best]) best = i;

            return best;
        }
    }
}
