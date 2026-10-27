using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 测试夹具辅助：把对象摆成想要的局面。
    ///
    /// 为什么需要它：规则限定"每回合只有行动方的一只手能改变"，因此想用正常回合
    /// 把某只手凑到指定数值，需要一长串回合，测试会变得很难读、也很难维护。
    /// 这里直接用 <see cref="Hand.PerformOperation"/> 在**测试方法自己新建的对象**上
    /// 摆好初始局面，然后再让被测逻辑去跑。
    ///
    /// 注意：只能在 <see cref="GameManager"/> 开始跑之前使用；游戏进行中摆数值
    /// 会破坏"只有行动方的手改变"这个契约，那样的测试没有意义。
    /// </summary>
    internal static class TestSetup
    {
        // 独立测试结果载体，避免依赖运行时程序集的 internal setter。
        internal sealed class SamplingResult
        {
            public PlayerState Winner;
            public bool WinnerIsPlayerA,WinnerIsPlayerB,ReachedTurnLimit,HitTurnLimit;
            public int Turns;
        }
        // 批量采样使用实机控制器，避免绕过赌博、偷窃、援护及技能抉择。
        public static void SeedSampling(GameManager game,int seed)
        {
            game.StatusRandom=new System.Random(unchecked(seed+71));
            StatusRules.ResetInteractionRandomForTest(unchecked(seed+73));
            for(int i=0;i<game.Players.Count;i++)
                foreach(var part in CharacterBehaviors.Implementers<ICharacterBehavior>(game.Players[i]))
                {
                    int value=unchecked(seed+i*97);
                    if(part is StormSorceryLogic storm)storm.Rng=new System.Random(unchecked(value+79));
                    if(part is WildMagicLogic wild)wild.Rng=new System.Random(unchecked(value+89));
                }
        }
        public static void SeedControllerSampling(BoardController board,System.Collections.Generic.IReadOnlyList<IMoveSelector> selectors,int seed)
        {
            board.GambleRng=new System.Random(unchecked(seed+101));
            for(int i=0;i<selectors.Count;i++)if(selectors[i] is GreedyChoiceProvider ai)ai.SetRandomSeedForTest(unchecked(seed+i*97+103));
            // 幽灵由控制器内置AI行动，同样可能受到魅惑。
            var field=typeof(BoardController).GetField("_specterAi",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            if(field?.GetValue(board) is GreedyChoiceProvider specter)specter.SetRandomSeedForTest(unchecked(seed+109));
            else throw new System.InvalidOperationException("采样缺少幽灵AI随机源");
        }
        public static SamplingResult RunSampling(GameManager game,System.Collections.Generic.IReadOnlyList<IMoveSelector> selectors,int seed)
        {
            SeedSampling(game,seed);
            var sinks=new SimpleChoiceProvider[selectors.Count];
            for(int i=0;i<sinks.Length;i++)sinks[i]=(SimpleChoiceProvider)game.GetChoiceProvider(game.Players[i]);
            var board=new BoardController(game,sinks[0],sinks[1],selectors[0],selectors[1]){GambleRng=new System.Random(unchecked(seed+101))};
            for(int i=0;i<selectors.Count;i++)board.BindPlayer(game.Players[i],selectors[i],sinks[i]);
            SeedControllerSampling(board,selectors,seed);
            board.SetFirstPlayer(game.CurrentPlayer);board.BeginTurn();int guard=0;
            while(!game.IsGameOver&&guard++<20000)
            {
                board.AutoResolveAiCollegeOfValorChoice();
                bool progress=board.Phase==BoardPhase.ChoosingAttackerHand||board.Phase==BoardPhase.ChoosingGamble
                    ?board.MakeAutomaticMove():board.ResolvePendingAiChoice();
                if(!progress&&!game.IsGameOver&&!(board.CanSkipTurn&&board.SkipTurn()))
                    throw new System.InvalidOperationException("采样控制器卡住："+board.DebugPhaseInfo());
            }
            if(!game.IsGameOver)throw new System.InvalidOperationException("采样控制器安全步数上限");
            return new SamplingResult{Winner=game.Winner,WinnerIsPlayerA=ReferenceEquals(game.Winner,game.PlayerA),
                WinnerIsPlayerB=ReferenceEquals(game.Winner,game.PlayerB),Turns=game.TurnCount,
                ReachedTurnLimit=game.RoundCount>=game.RoundLimit,HitTurnLimit=false};
        }
        /// <summary>把一只手摆成 1..9 的指定数值（护盾状态清为 None）。</summary>
        public static void SetHand(Hand hand, int targetValue)
        {
            if (targetValue < Hand.MinValue || targetValue > Hand.MaxValue)
                throw new System.ArgumentOutOfRangeException(
                    nameof(targetValue), $"手值必须在 {Hand.MinValue}..{Hand.MaxValue}。");

            hand.SetValue(targetValue);

            if (hand.Value != targetValue)
                throw new System.InvalidOperationException(
                    $"TestSetup.SetHand 内部错误：目标 {targetValue}，实际 {hand.Value}。");
        }

        /// <summary>把双方两只手都摆成指定数值。</summary>
        public static void SetBothHands(PlayerState player, int value)
        {
            SetHand(player.LeftHand, value);
            SetHand(player.RightHand, value);
        }

        /// <summary>
        /// 把双方的 4 只手全部摆成指定数值（默认 1，即原始开局状态）。
        /// 需要"完全确定"的局面时先调用它，可以避免依赖前面步骤留下的数值。
        /// </summary>
        public static void SetAllHands(PlayerState a, PlayerState b, int value = 1)
        {
            SetBothHands(a, value);
            SetBothHands(b, value);
        }

        /// <summary>
        /// 2v2：把**四名**玩家的所有手牌设成同一个值。
        ///
        /// 与 <see cref="SetAllHands"/> 同一个用途 —— 让数值完全确定，
        /// 避免断言依赖前面回合留下的数字。
        /// </summary>
        public static void SetAllFourHands(GameManager game, int value = 1)
        {
            if (game == null) return;

            SetBothHands(game.PlayerA, value);
            SetBothHands(game.PlayerB, value);
            if (game.PlayerC != null) SetBothHands(game.PlayerC, value);
            if (game.PlayerD != null) SetBothHands(game.PlayerD, value);
        }

        /// <summary>
        /// 断言一只手当前就是指定数值，不是就立刻报错。
        ///
        /// 用途：把"这个测试依赖某只手恰好是几"这件事写成显式检查。
        /// 这样一旦推理有误，失败点会落在真正的错误位置，
        /// 而不是在后面某个断言上以一种难以理解的方式爆掉。
        /// </summary>
        public static void RequireHand(Hand hand, int expected, string what)
        {
            if (hand.Value != expected)
                throw new System.InvalidOperationException(
                    $"测试前置条件不成立：{what} 应为 {expected}，实际 {hand.Value}。");
        }

        /// <summary>把生命调整到指定值（只减不增）。</summary>
        public static void SetHealth(PlayerState player, int health)
        {
            int delta = player.Health - health;
            if (delta > 0) player.TakeDamage(delta);
        }
    }
}
