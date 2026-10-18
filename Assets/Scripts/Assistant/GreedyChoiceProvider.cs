using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// AI 难度档位。
    ///
    /// 用**评分抖动**实现：难度越高抖动越小（见
    /// <see cref="GreedyChoiceProvider.ApplyDifficulty"/>）。
    /// 为什么不另外写一套"聪明/笨"的策略：抖动这一条就足以
    /// 让简单档偶尔失误、困难档始终最优，而且不会出现两套逻辑各自漂移。
    /// </summary>
    public enum AiDifficulty
    {
        /// <summary>简单：抖动 ±25，偶尔选明显次优的解。</summary>
        Easy = 0,

        /// <summary>普通：抖动 ±8，大方向稳定。</summary>
        Normal = 1,

        /// <summary>困难：不抖动，始终选最高分。</summary>
        Hard = 2,
    }

    /// <summary>
    /// 贪心 AI：用一组启发式评分选招。
    /// 目的不是"很强"，而是"合理且确定"，用于冒烟测试与基准对局。
    ///
    /// 评分 = 结果效果的固定档位 + **当前角色的特性加分**。
    ///
    /// 基础档位（数值可调，改这里即可调整风格）：
    ///   4 → 44    6 → 66    8 → 88
    ///   5 → 50（合盾）
    ///   9 → 90，但仅在生命低于阈值时才优先（否则不浪费回复）
    ///   其他 → 0
    ///
    /// ⚠️ 特性加分是后加的：早期版本只认识白板 / 战士，
    /// 牧师、游侠、狼人、剑客、忍者、刺客的 AI **完全不会用技能**。
    /// 现在每个角色都有对应的评分与抉择策略。
    /// </summary>
    public sealed class GreedyChoiceProvider : AbstractChoiceProvider, IMoveSelector,
                                               IDamageTypeChoiceSink
    {
        // ============================================================
        // **集火策略**（2v2 针对性实验 / 策略用）
        // ============================================================

        /// <summary>
        /// 2v2 的集火口径。
        ///
        /// 环上队友永远相邻（1&amp;4 / 2&amp;3），所以每位玩家身边有两位对手：
        ///   · **自己车道**的敌人 = 离自己最近的那位（通常就是相邻座位）；
        ///   · **队友车道**的敌人 = 离队友最近的那位。
        ///
        /// 近战（射程 1）天然只能打自己车道；远程（射程 ≥ 2）才跨得过去。
        /// 所以"集火 Mate"在近战身上**不该偷偷退化成打自己车道** ——
        /// 打不到就等价于不集火，否则对照组会被污染。
        /// </summary>
        public enum FocusPolicy
        {
            /// <summary>不集火：完全沿用原来的六级目标优先级。</summary>
            None = 0,

            /// <summary>集火**自己车道**的敌人（离自己最近的对手）。</summary>
            Self = 1,

            /// <summary>集火**队友车道**的敌人（离队友最近的对手；打不到则不生效）。</summary>
            Mate = 2,
        }

        /// <summary>集火策略。默认 <see cref="FocusPolicy.None"/> = 与旧行为一致。</summary>
        public FocusPolicy Focus { get; set; } = FocusPolicy.None;

        /// <summary>
        /// 是否启用**按角色查表**的默认集火策略（见 <see cref="FocusPolicies"/>）。
        ///
        /// 默认 <c>false</c>：GreedyChoiceProvider 本身保持"实验基线"语义。
        /// 生产 UI（<c>GameBootstrap.MakeAI</c>）与 2v2 统计测试台会显式打开；
        /// 集火实验的 baseline 组必须保持 false，否则对照会被生产策略污染。
        /// </summary>
        public bool UseLearnedFocus { get; set; }

        /// <summary>
        /// 显式指定的集火目标（优先级高于 <see cref="Focus"/>；null = 按策略动态解析）。
        ///
        /// 只在"**打得到**"时加分（落在目标优先级里，只有合法走法能拿到），
        /// 所以不会让 AI 硬打射程外的人。
        /// </summary>
        public PlayerState FocusTarget { get; set; }

        /// <summary>
        /// 集火给"攻击该目标"增加的分值。
        ///
        /// ⚠️⚠️ **必须是 25000 这个量级，不能是 40。**
        ///
        ///    `ScoreMove` 里的"六级目标优先级"基数是 10000：
        ///    单是"双方都能打到"就 +10000，血量差最高再 +10000 ——
        ///    40 分在 20000 分的目标优先级面前**完全不会改变选择**。
        ///    （这是上一版留下的真实问题：三组实验会得到一张全相同的表，
        ///      看起来像"集火无效"，其实旋钮根本没接到决策上。）
        ///
        ///    25000 &gt; 目标优先级最大可能值（≈20210），又 &lt; 斩杀 100000：
        ///    集火压过"打谁更好"，但压不过"这一下能赢"。
        /// </summary>
        public int FocusBonus { get; set; } = 25000;

        /// <summary>
        /// 本次 <see cref="ChooseMove"/> 实际想打的人（供界面层确认目标用）。
        ///
        /// 规则允许"被加数手主"与"实际挨打的人"不同；无头测试台与
        /// <c>BoardController</c> 都靠这个值把"AI 的目标意图"传下去。
        /// 没有它时，实机 AI 会永远打 <c>OpponentsOf[0]</c>（第一个对手），
        /// 集火与六级目标优先级都白算。
        /// </summary>
        public PlayerState IntendedVictim { get; private set; }

        /// <summary>
        /// **一次 ChooseMove 内**解析出来的集火目标（缓存）。
        ///
        /// 为什么必须缓存：<see cref="ScoreMove"/> 会为每一手棋算目标优先级，
        /// 如果每次都重新解析，就会为**每一手**分配两个 List
        /// （`OpponentsOf` / `TeammatesOf`）—— 86,400 局的实验里这是
        /// 上千万次分配，既慢又会把 GC 压到异常（实测出现过原生层崩溃）。
        /// </summary>
        private PlayerState _focusTargetForScoring;

        /// <summary>当前评分用的集火目标；不在 ChooseMove 内时按需解析。</summary>
        private PlayerState FocusTargetForScoring(GameManager game, PlayerState actor)
            => _focusTargetForScoring ?? ResolveFocusTarget(game, actor);

        /// <summary>
        /// 解析本回合的集火目标（只返回存活对手；没有则 null）。
        ///
        /// 口径（与 <c>Tools/focus-report.txt</c> 的实验定义一致）：
        ///   · Self = 离自己最近的对手（同距离取血少的，再取座位号小的）；
        ///   · Mate = 离队友最近的对手；队友已阵亡 / 不存在时退回 Self；
        ///   · Mate 目标打不到时不退回 Self（否则近战会"假装集火"）。
        /// </summary>
        public PlayerState ResolveFocusTarget(GameManager game, PlayerState actor)
        {
            if (game == null || actor == null) return null;

            // 1v1（或只剩一位可攻击对手）：集火没有"选谁"的余地。
            // 直接返回 null，避免给所有攻击加一个常数、改变攻/盾取舍。
            if (game.OpponentsOf(actor).Count < 2) return null;

            // 显式指定优先（实验 / 测试可直接钉死目标）
            if (FocusTarget != null && FocusTarget.IsAlive) return FocusTarget;

            FocusPolicy policy = Focus;
            if (policy == FocusPolicy.None && UseLearnedFocus)
                policy = FocusPolicies.For(actor.Definition);

            if (policy == FocusPolicy.None) return null;

            PlayerState selfTarget = NearestOpponent(game, actor);
            if (policy == FocusPolicy.Self) return selfTarget;

            PlayerState mate = null;
            IReadOnlyList<PlayerState> mates = game.TeammatesOf(actor);
            for (int i = 0; i < mates.Count; i++)
            {
                if (mates[i] != null && mates[i].IsAlive) { mate = mates[i]; break; }
            }

            if (mate == null) return selfTarget;

            PlayerState mateTarget = NearestOpponent(game, mate);
            return mateTarget ?? selfTarget;
        }

        /// <summary>离 <paramref name="player"/> 最近的存活对手（含当前打不到的人）。</summary>
        private static PlayerState NearestOpponent(GameManager game, PlayerState player)
        {
            IReadOnlyList<PlayerState> foes = game.OpponentsOf(player);
            PlayerState best = null;
            int bestDistance = int.MaxValue;
            int bestHealth = int.MaxValue;
            int bestSeat = int.MaxValue;

            for (int i = 0; i < foes.Count; i++)
            {
                PlayerState foe = foes[i];
                if (foe == null || !foe.IsAlive) continue;

                int distance = game.DistanceBetween(player, foe);
                if (distance < 0) continue;

                int health = foe.Health;
                int seat = game.SeatOf(foe);

                bool better = distance < bestDistance
                    || (distance == bestDistance && health < bestHealth)
                    || (distance == bestDistance && health == bestHealth && seat < bestSeat);

                if (better)
                {
                    best = foe;
                    bestDistance = distance;
                    bestHealth = health;
                    bestSeat = seat;
                }
            }

            return best;
        }

        // ============================================================
        // 法师的伤害类型选择（IDamageTypeChoiceSink）
        // ============================================================

        /// <summary>AI 选定的伤害类型（供 <see cref="WizardLogic"/> 回读）。</summary>
        public DamageType ChosenDamageType { get; set; } = DamageType.None;

        /// <summary>本次是否会放弃攻击去铺状态。</summary>
        public bool WantsToAbandonAttack { get; set; }

        /// <summary>
        /// 由 Core 注入当前对局（见 <see cref="IDamageTypeChoiceSink.SetGameForChoices"/>）。
        ///
        /// 与 <see cref="Game"/> 是同一个字段 —— 只是给 Core 一个不用反射的入口。
        /// </summary>
        public void SetGameForChoices(GameManager game) => Game = game;

        /// <summary>
        /// 由调用方注入："**这一击能不能直接斩杀**"。
        ///
        /// 能斩杀时就不该放弃攻击去挂状态 —— 立刻赢下比赛胜过铺一个状态。
        /// 未注入时按"不能斩杀"处理（保守：允许铺状态）。
        /// </summary>
        public System.Func<bool> WizardAbandonLethalGuard { get; set; }

        /// <summary>
        /// **法师要不要放弃这一次攻击**（玩家本轮给的策略）。
        ///
        /// 玩家原话：
        ///   "法师在对方未进斩杀线前，若自己没有放弃过攻击，
        ///     合出 4 会倾向于放弃攻击，6 不一定会放弃，8 基本不放弃"
        ///
        /// 落到实现：
        ///   · 结果 4 -> 倾向放弃（**高概率**）
        ///   · 结果 6 -> 不一定（**一半左右**；局面好就不放弃）
        ///   · 结果 8 -> 基本不放弃（**低概率**）
        ///
        /// ⚠️ 为什么用**概率**而不是硬规则：
        ///    玩家说的是"倾向于 / 不一定 / 基本不"，这本来就不是确定性策略。
        ///    而且纯确定性会让 21 人平衡测试的胜率变成一个"点"，
        ///    无法反映"这套策略到底好不好"。
        ///    这里的概率是**固定的种子随机**，所以胜率可复现。
        /// </summary>
        private bool WizardShouldAbandon(int result, PlayerState self)
        {
            // 只有 4 / 6 / 8 是"攻击数字"，其余（9 回复等）根本不进这个抉择
            if (result != 4 && result != 6 && result != 8) return false;

            // ⚠️⚠️ **"能不能斩杀"只认调用方注入的判据**
            //    （<see cref="WizardAbandonLethalGuard"/>，由 BoardController
            //     用真实的这一击去算），**没有注入就按"不能斩杀"处理**。
            //
            //    我先前自己写了一个"看手值估斩杀"的启发式 —— 结果是**灾难**：
            //    `myMax + foeMax >= foe.Health` 在开局就几乎恒真，
            //    于是 4 次抉择里 3 次被它挡掉，法师**永远不铺状态**。
            //    教训：**不要用粗估去替代真实判据**；
            //    宁可"没判据时允许铺"（玩家策略本来就是"未进斩杀线前就该铺"）。
            if (WizardAbandonLethalGuard?.Invoke() == true) return false;

            int roll = WizardRoll();          // 0..99

            switch (result)
            {
                // ⚠️ 玩家原话是"4 会倾向于放弃、6 不一定、8 基本不放弃"。
                //    但**放弃太多会让法师永远在铺垫、永远不出手** ——
                //    单局轨迹显示：一旦铺上类型，后面几回合 dmg 常常是 0
                //    （打不中），类型反而被"消耗它的那一次"白白吃掉。
                //    所以把概率收敛一些，让它真的打得出去。
                case 4: return roll < 60;     // 倾向于放弃
                case 6: return roll < 30;     // 不一定会放弃
                default: return roll < 10;    // 8：基本不放弃
            }
        }

        /// <summary>
        /// 法师当前的掷点数（供"倾向 / 不一定 / 基本不"用）。
        ///
        /// ⚠️ 用一个**独立的确定性序列**而不是 <c>UnityEngine.Random</c>：
        ///    无头平衡测试要可复现，两个 `Random` 混用会让胜率每次不同。
        /// </summary>
        private int WizardRoll()
        {
            _wizardSeed = _wizardSeed * 1103515245 + 12345;
            return (_wizardSeed >> 16) & 0x7FFF % 100;
        }

        private int _wizardSeed = 20240816;

        /// <summary>
        /// **法师选哪一种伤害类型**（玩家本轮给的策略）。
        ///
        /// 玩家原话：
        ///   "视情况选择各种技能：
        ///     若要打伤害选择毒素/火焰，
        ///     自己危险去控制对手选择寒冷/心灵，
        ///     多人打伤害选择闪电，
        ///     多人模式下控制对手选择光耀/雷鸣，
        ///     准备斩杀对手选择力场/暗蚀/强酸"
        /// </summary>
        private DamageType WizardPickDamageType(PlayerState self)
        {
            if (self == null || Game == null) return DamageType.Fire;
            IReadOnlyList<PlayerState> foes = Game.OpponentsOf(self);
            PlayerState foe = foes.Count > 0 ? foes[0] : null;

            bool multi = foes.Count > 1;
            bool meInDanger = self.Health * 100 <= self.MaximumHealth * 45;

            // ⚠️⚠️ **"准备斩杀"必须是真条件，不能当兜底。**
            //
            //    我第一版把力场写成了兜底默认值（对手没盾没回复就返回力场），
            //    结果 AI 几乎每次都选力场 —— 而**力场按设计不挂任何状态**
            //    （玩家原话："力场 同咒术师方法，无法被任何方式抵挡/闪避"，
            //      它的价值是穿透 + 必中，不是状态）。
            //
            //    表现（探针轨迹）：40 局里法师铺了 80 次类型、全部是力场，
            //    对手**一次状态都没挂上**，`Ai_mage_applies_a_status` 一直红。
            //    而且这个"铺了等于没铺"还白扔了一回合，反而拉低胜率。
            //
            //    现在只在"真的够得着斩杀"时才用力场。
            bool canFinish = CanFinishWithOneHit(self, foe);

            // ① 准备斩杀 -> 力场（穿盾且不可闪避，必中）/ 暗蚀（封回复）/ 强酸（削防）
            //
            //    ⚠️⚠️ **力场要克制地选**（玩家要求：
            //       "提高其他属性被选择的比例，略微降低选力场的情况"）。
            //
            //    原因：**力场不挂任何状态**（设计如此）——
            //    选了它就等于"白扔一回合、什么也没铺上"。
            //    原来它在"能斩杀"这一档里是**默认值**，结果 AI 选了 120 次力场、
            //    其他属性 0 次，对手**一次状态都没中过**。
            //
            //    现在只在"对手有盾"时才用它（那时穿透才真的值钱）；
            //    其余斩杀场景改用有毒/有状态的类型 —— 既压血又铺状态。
            if (canFinish)
            {
                if (foe != null && (foe.LeftHand.IsShield || foe.RightHand.IsShield))
                    return DamageType.Force;          // 有盾 -> 穿透最稳

                if (foe != null && WizardCanHeal(foe)) return DamageType.Necrotic;

                // 没盾、也不能回血 -> 用**带状态**的类型收尾（不强求必中，但铺得上状态）
                return DamageType.Poison;
            }

            // ② 自己危险 -> 控制对手：寒冷（结果减半）/ 心灵（降攻且打不到自己）
            if (meInDanger) return DamageType.Cold;

            // ③ 多人 -> 打伤害用闪电（感电会波及相邻），控制用光耀 / 雷鸣
            if (multi)
            {
                if (!meInDanger) return DamageType.Lightning;

                return DamageType.Radiant;    // 目盲：对手只能选距离 1 的手
            }

            // ④ 常规打伤害 -> 毒素（不可被抵挡）/ 火焰（每回合 3 点）
            //    对手能回血时优先封回复
            if (foe != null && WizardCanHeal(foe)) return DamageType.Necrotic;

            return DamageType.Poison;
        }

        /// <summary>
        /// 自己**这一击真的够得着斩杀**吗（对手剩余血量在自己最大手之内）。
        ///
        /// ⚠️ 刻意保守：只有"自己最大的手已经 ≥ 对手血量"才算。
        ///    判错了也不会太糟 —— 判成"不能斩"就只是多铺一个状态，
        ///    而判成"能斩"却打不死，就白扔了一回合。
        /// </summary>
        private static bool CanFinishWithOneHit(PlayerState self, PlayerState foe)
        {
            if (self == null || foe == null || !foe.IsAlive) return false;

            int myMax = 0;
            if (self.LeftHand != null) myMax = System.Math.Max(myMax, self.LeftHand.Value);
            if (self.RightHand != null) myMax = System.Math.Max(myMax, self.RightHand.Value);

            return myMax > 0 && myMax >= foe.Health;
        }

        /// <summary>
        /// 对手是否**有可能回血**（法师选"腐朽"的判据）。
        ///
        /// 用"角色自带回复能力"判断，而不是猜他的手牌 ——
        /// 牧师 / 巨人 / 狼人都有明确的回复机制。
        /// </summary>
        private static bool WizardCanHeal(PlayerState foe)
        {
            if (foe?.Character == null) return false;

            // 有回复能力的角色都实现 IHealAmountProvider 或 IHealOverride
            if (foe.Character is IHealAmountProvider) return true;
            if (foe.Character is IHealOverride) return true;

            return false;
        }

        /// <summary>
        /// 生命低于等于该值时，回复的评分提高。
        ///
        /// 重要：这个阈值必须显著低于"最大可造成伤害"，否则会出现"双方都优先回血、
        /// 谁也打不死谁"的僵局。实测野蛮人镜像在阈值 ≥12 时会 100% 打成僵局，
        /// 阈值 ≤8 时 21 回合内正常结束。默认值 8 是据此选定的。
        /// </summary>
        /// <summary>
        /// "血少到该优先回血"的血量线。
        ///
        /// ⚠️ 调这个数会**同时影响所有能回血的角色**（牧师 / 巨人 / 狼人），
        ///    所以它是**最敏感**的平衡旋钮之一。
        ///
        /// 循环赛实测（每对 40 局，17 角色）：8 时牧师的胜率只有 **30%**、
        /// 而且 640 局里 **226 局是同归于尽**的平局 ——
        /// 因为 ≤8 血时回血（120 分）永远压过攻击（最高 88 分），
        /// 牧师于是"一直回血、打不死别人，最后与对方互换"。
        /// 调到 4 之后牧师明显敢打了。
        /// </summary>
        public int HealPriorityHealth { get; set; } = 4;

        /// <summary>
        /// 牧师认为"自己处于危险值"的血量**百分比**（玩家给的规则）。
        ///
        /// "牧师只需要检查在**危险时刻优先给回血**，否则**全部进攻**"。
        ///
        /// ⚠️ 用百分比而不是绝对值：不同角色的生命上限不同
        ///    （巨人 25、狼人 25、其余 20），绝对阈值在它们之间没有可比性。
        ///
        /// 35% 大致对应"再挨一到两下就没了"。
        /// </summary>
        public int PriestHealHealthPercent { get; set; } = 35;

        /// <summary>
        /// 当前对局（由 <see cref="BoardController"/> 在询问之前注入）。
        ///
        /// ⚠️ 为什么需要：有些抉择的"正确答案"要看**全场信息** ——
        ///    例如牧师该给谁回血（自己这一队里血最低的存活者），
        ///    那要遍历全部参战玩家与队伍关系，`ChooseHandFor` 的入参里没有。
        ///    没有它就只能退回"取第一个候选"（= 永远回自己）。
        /// </summary>
        public GameManager Game { get; set; }

        /// <summary>是否发动战士连锁破盾。默认放弃（避免自作自受地破自己的盾）。</summary>
        public bool UseChainBreak { get; set; }

        /// <summary>
        /// 低于该伤害就不发动剑客的追加攻击。
        /// 追加会把"另一只手"改写成主攻击结果，太弱的追加不值得换掉原来的数字。
        /// </summary>
        public int SwordsmanFollowUpMinDamage { get; set; } = 3;


        public string Describe => $"贪心AI(chainBreak={UseChainBreak})";

        public LegalMove ChooseMove(GameManager game, PlayerState actor)
        {
            if (game == null || actor == null) return default;

            IReadOnlyList<LegalMove> moves = game.GetLegalMoves(actor);
            if (moves.Count == 0) return default;

            // 集火目标**每回合只解析一次**（阵亡后环会收缩，所以不能跨回合缓存）。
            _focusTargetForScoring = ResolveFocusTarget(game, actor);

            try
            {
                LegalMove best = moves[0];
                int bestScore = int.MinValue;

                for (int i = 0; i < moves.Count; i++)
                {
                    // 难度噪声：简单档会随机抖动，于是偶尔选次优解
                    // （否则同一个局面永远走同一步，连打两局完全可预判）。
                    int noise = _noisy ? _rng.Next(-NoiseAmplitude, NoiseAmplitude + 1) : 0;
                    int score = ScoreMove(moves[i], actor, game) + noise;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = moves[i];
                    }
                }

                // 把"AI 想打谁"留给界面层 / 测试台。
                // ⚠️ 集火奖励已经放进 ScoreMove 的目标优先级里（只对攻击生效），
                //    这里不再重复加分 —— 否则"用集火目标的手合盾 / 回血"
                //    也会被当成集火，AI 会为了贪分去做防守动作。
                IntendedVictim = ResolveIntendedVictim(best, actor, game);
                return best;
            }
            finally
            {
                _focusTargetForScoring = null;
            }
        }

        /// <summary>
        /// 这一手**实际该打谁**：优先取被加数的手主（对手）；
        /// 用队友的手 / 魔术师自加时，退回"第一位对手"（或本回合的集火目标）。
        ///
        /// 攻击目标本来就能与被加数手主不同（玩家确认的第 ③ 步），
        /// 所以这个值必须显式传给结算层，不能靠"手主"猜。
        /// </summary>
        private PlayerState ResolveIntendedVictim(LegalMove move, PlayerState actor,
                                                  GameManager game)
        {
            if (game == null || actor == null) return null;
            if (move.Actor == null || move.TargetHand == null) return null;

            bool isAttack = IsAttackResult(move, actor);

            // ① 被加数是**敌人的手**：这一手本来就打在TA身上（也是评分时的目标）。
            //
            //    ⚠️ 必须先看这个，不能无条件用集火目标：
            //       集火分只压过普通目标，压不过"斩杀 100000"。
            //       如果这一手是斩杀另一名敌人，却把实际目标改成集火目标，
            //       就会**把到手的胜利送掉**（测试抓到过）。
            PlayerState owner = game.OwnerOfHand(move.TargetHand);
            if (owner != null && !ReferenceEquals(owner, actor)
                && !game.IsTeammate(actor, owner) && owner.IsAlive)
                return owner;

            // ② 被加数是自己的手（魔术师自加）/ 队友的手：攻击目标本来就能单独选，
            //    此时才用集火目标（含"打不到就回到第一位对手"）。
            if (isAttack)
            {
                PlayerState focus = FocusTargetForScoring(game, actor);
                if (focus != null && game.CanReach(actor, focus)) return focus;
            }

            IReadOnlyList<PlayerState> foes = game.OpponentsOf(actor);
            return foes.Count > 0 ? foes[0] : null;
        }

        /// <summary>这一手的结果在 AI 眼里是不是攻击（含魔术师借来的攻击）。</summary>
        private static bool IsAttackResult(LegalMove move, PlayerState actor)
        {
            if (move.ActedHand == null || move.TargetHand == null) return false;

            int result = Hand.ComputeSum(move.ActedHand.Value, move.TargetHand.Value);
            HandEffectType effect = Hand.GetEffectType(result);

            if (IsAttackEffect(effect)) return true;

            return actor?.Character is IHandEffectDisplay display
                && IsAttackEffect(display.DisplayedEffectOf(result));
        }

        private static bool IsAttackEffect(HandEffectType effect)
            => effect == HandEffectType.Attack4
               || effect == HandEffectType.Attack6
               || effect == HandEffectType.Attack8;

        /// <summary>
        /// "留下的手值"在评分里占多大比重。
        ///
        /// 效果档位是 44/50/66/88 这个量级，而手值权重已按
        /// <see cref="HandValueProfiles.MoveScoreScale"/> 缩到 1.5~9.5（同量级）。
        /// 取 12 时：游侠把 (1,3) 变成 (2,3) 会掉约
        /// (8.5+4.5 - 2.0-4.5) × 12 ≈ 78 分 —— 高于"多打 2 点伤害"（22 分），
        /// 也就是它会为了保住 1 而放弃一点输出。
        ///
        /// 调大 -> AI 更保守（更愿意屯手值）；调小 -> 更激进。
        /// </summary>
        public int HandValueWeight { get; set; } = 12;

        // ============================================================
        // 难度
        // ============================================================

        /// <summary>
        /// 一手棋评分上叠加的**随机抖动幅度**。
        ///
        /// 为什么需要：不加抖动时 AI 是**完全确定性**的 ——
        /// 同一个局面永远走同一步，连打两局完全可预判，玩起来很机械。
        ///
        /// 同时也是**难度旋钮**：抖动越大，越容易选到次优解。
        /// 效果档位是 44 / 50 / 66 / 88，手值项再叠几十 ——
        /// 所以 ±25 足以让"明显更好的选择"偶尔被换掉，而 ±8 只影响差不多的选项。
        /// </summary>
        public int NoiseAmplitude { get; set; }

        /// <summary>是否启用难度噪声（不启用则评分完全确定）。</summary>
        public bool UseNoise
        {
            get => _noisy;
            set => _noisy = value;
        }

        private bool _noisy;
        private readonly System.Random _rng = new System.Random();

        /// <summary>
        /// 按难度档位配置这个 AI。
        ///
        /// | 档位 | 抖动 | 表现 |
        /// |---|---|---|
        /// | 简单 | ±25 | 明显更好的选择也会偶尔被换掉 -> 像新手 |
        /// | 普通 | ±8 | 大方向稳定，细节有变化 |
        /// | 困难 | 0 | 当前的最优行为（完全不抖） |
        /// </summary>
        public void ApplyDifficulty(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy:
                    NoiseAmplitude = 25;
                    _noisy = true;
                    break;

                case AiDifficulty.Hard:
                    NoiseAmplitude = 0;
                    _noisy = false;
                    break;

                default:                    // Normal
                    NoiseAmplitude = 8;
                    _noisy = true;
                    break;
            }
        }

        /// <summary>
        /// 一手棋的评分：基础档位 + 角色特性加分 + **留下的手值** + 一些条件次分。
        ///
        /// `HandValueLookup` 是**实例属性**而不是静态注入 ——
        /// 静态注入会在测试之间互相污染（一个测试改了表，另一个测试跟着变）。
        /// </summary>
        /// <summary>
        /// **攻击自己队友**的惩罚。
        ///
        /// 为什么需要：规则的顺序是"打敌人 → 没敌人才打队友 → 连队友都打不到才放弃"，
        /// 而原来的评分**只看效果与手值**，完全不知道目标是谁 ——
        /// 于是 AI 会把队友当成"一个可以用掉废手的靶子"。
        ///
        /// 实测（批量测试）：60 局 2v2 里出现 **616 次**攻击落在队友身上
        /// （玩家也实测报告过："二号位咒术师攻击了自己的队友游侠"）。
        ///
        /// 取值远大于任何正面评分（效果档位 44/66/88、手值项几十），
        /// 所以**只要有敌人可打就绝不会打队友**；但它是惩罚而不是硬禁止 ——
        /// 当队友是唯一合法目标时（规则允许这种情形）仍然打得出去。
        /// </summary>
        public int TeammateAttackPenalty { get; set; } = 10000;

        private int ScoreMove(LegalMove move, PlayerState actor, GameManager game)        {
            int acted = move.ActedHand.Value;
            int target = move.TargetHand.Value;
            int result = Hand.ComputeSum(acted, target);

            int score;
            switch (Hand.GetEffectType(result))
            {
                case HandEffectType.Attack4: score = 44; break;
                case HandEffectType.Attack6: score = 66; break;
                case HandEffectType.Attack8: score = 88; break;

                case HandEffectType.Shield: score = 50; break;

                case HandEffectType.Heal:
                    // 满血时回复毫无价值，压低评分
                    score = actor.Health >= actor.MaximumHealth
                        ? 5
                        : (actor.Health <= HealPriorityHealth ? 120 : 30);
                    break;

                default: score = 0; break;
            }

            // ⚠️⚠️ **角色把自己某个结果值变成攻击时，评分必须跟着认。**
            //
            //    实测（探针）暴露的根因：魔术师的"自加凑出 2"是攻击，
            //    但那是 `RewriteEffect` **借**来的 Attack4 ——
            //    `Hand.GetEffectType(2)` 仍然是 `None`，
            //    于是上面那个 switch 给它 **0 分**。
            //    结果：碰对手凑出 4 得 44 分，自加凑出 2 得 0 分 ——
            //    **AI 必然去碰对手的手，永远不自加**（招牌能力等于废掉）。
            //
            //    修法：用角色自己的 `IHandEffectDisplay.DisplayedEffectOf`
            //    问它"这个结果值在你眼里是什么效果" ——
            //    界面与评分走**同一个口径**，Assistant 层依旧不认识具体角色。
            if (score == 0 && actor?.Character is IHandEffectDisplay display)
            {
                switch (display.DisplayedEffectOf(result))
                {
                    case HandEffectType.Attack4: score = 44; break;
                    case HandEffectType.Attack6: score = 66; break;
                    case HandEffectType.Attack8: score = 88; break;
                }
            }

            // ---- 角色特性加分 ----
            // 反转依赖：不是"AI 认识狼人"，而是"角色告诉 AI 这一手值多少"。
            // 这样 Assistant 层不必引用 Characters 层（项目约定）。
            Hand other = GetOtherHand(actor, move.ActedHand);

            if (actor.Character is IAiMoveAdvisor advisor)
            {
                score += advisor.ScoreMoveBonus(new MoveValueContext(
                    move.ActedHand, result, move.TargetHand, other.Value));
            }

            // ---- 留下的手值将来能干什么 ----
            //
            // ⚠️ 这一条是**游侠必须有的**：只看"这次运算产出了什么效果"的话，
            // 游侠会拿唯一的 1 去打输出，下回合就闪不了 ——
            // 实测现象："游侠不会主动运算出 1"。
            //
            // 运算后：被操作的手变成**结果值**，另一只手不变。
            score += HandValueScore(actor, result, other.Value);

            // ---- 带条件的次分 ----
            // 打掉对手已成型的盾，比让它继续挡着划算
            if (move.TargetHand.State == ShieldState.Formed) score += 8;

            // 把对手的手打成 2，会让它下一手容易凑出 3（偏弱），略加分
            if (target == 2) score += 5;

            // ---- "别反哺"：这一手会不会让我**下一手更难打出高价值** ----
            //
            // ⚠️ 玩家要求"避免对手下一手能合成更高价值"，但**核实后发现
            //    这个游戏里不存在"把差手值留给对手"这回事**：
            //    `GetLegalMoves` 允许把**任何其他玩家**的手当加数，
            //    而被加数的手值**不会被改变** ——
            //    所以对手的可选集只取决于他自己手上的两只手，与我的留牌无关。
            //
            //    真正的"反哺"是**同一件事的另一面**：我留下的牌决定**我自己**
            //    下一手能打出什么。所以这里评估"我这边的下一手潜力"。
            score += DenyScore(actor, game, result, other.Value);

            // ---- 打队友是坏事 ----            //
            // 规则的顺序是"打敌人 → 没敌人才打队友"，但评分原来只看效果与手值，
            // 于是 AI 会把队友当靶子（玩家实测 + 批量测试复现：
            // 60 局 2v2 里 616 次攻击落在队友身上）。
            //
            // 只在**确实会造成攻击伤害**时才罚：用队友的手当被加数、
            // 或对队友合盾 / 治疗，都不是"攻击队友"。
            //
            // ⚠️ 判断"目标手的主人是谁"必须查**全部参战玩家**，
            //    不能用"是不是 PlayerA/PlayerB"那种两人假设。
            if (game != null && actor != null && move.TargetHand != null)
            {
                PlayerState owner = game.OwnerOfHand(move.TargetHand);

                // ⚠️⚠️ **必须排除"被加数就是自己的手"**（魔术师的自加）。
                //
                //    `IsTeammate(a, a)` 会返回 **true**（自己和自己是"同队"），
                //    于是魔术师**自加凑出攻击**（1+1=2 正是攻击）时，
                //    这一手会被当成"打队友"而**扣分** ——
                //    AI 于是系统性地回避自加，改去碰对手的手。
                //
                //    实测（探针，30 局）：魔术师**自加次数恒为 0**，
                //    而它的招牌能力恰恰就是自加。这就是根因。
                bool isSelfAddend = ReferenceEquals(owner, actor);

                if (!isSelfAddend && owner != null && game.IsTeammate(actor, owner))
                {
                    HandEffectType eff = Hand.GetEffectType(result);
                    if (eff == HandEffectType.Attack4
                        || eff == HandEffectType.Attack6
                        || eff == HandEffectType.Attack8)
                    {
                        score -= TeammateAttackPenalty;
                    }
                }
            }

            // ---- **情境化阻挡**（玩家给的新规则）----
            //
            // "在敌方血量低时，要大幅增加阻止对方合成盾 / 闪避 / 太极和回复数字的意愿；
            //  在自己血量低时，要大幅增加阻止对方合成攻击数字的意愿。
            //  但阻止对方的减值数值**不能比让自己合成高价值的数值多**，
            //  比率要调整到合适的值如 3：7 左右。"
            score += SituationalDenyScore(move, actor, game);

            // ---- 2v2 的攻击目标优先级（玩家给的 6 级顺序）----
            //
            // ⚠️⚠️ **必须按"实际会挨打的人"算，不能按"被加数的手主"算。**
            //
            //    魔术师可以拿**自己的手**当被加数 —— 这时被加数的手主是**自己**，
            //    但攻击照样落在**对手**身上（借自己的手只是为了凑数字）。
            //
            //    原来的写法直接传 `move.TargetHand` 给 TargetPriorityScore，
            //    而它对"手主是自己"直接返回 0 ——
            //    于是自加凑出的攻击**拿不到任何目标优先级分**。
            //    实测（探针）：自加 176 分 vs 碰对手 322 分，差 146 分，
            //    全部来自这一项 -> **AI 永远选碰对手，魔术师的招牌能力废掉**。
            //
            //    预测挨打对象：结果是攻击时 = 默认攻击目标 = 第一位对手
            //    （自加不改变"打谁"；范围之外的极端情况由结算侧兜底）。
            int resultForPriority = Hand.ComputeSum(move.ActedHand.Value, move.TargetHand.Value);
            bool priorityIsAttack =
                Hand.GetEffectType(resultForPriority) == HandEffectType.Attack4
                || Hand.GetEffectType(resultForPriority) == HandEffectType.Attack6
                || Hand.GetEffectType(resultForPriority) == HandEffectType.Attack8
                || (actor?.Character is IHandEffectDisplay dsp
                    && IsAttackEffect(dsp.DisplayedEffectOf(resultForPriority)));

            if (priorityIsAttack && actor != null && game != null
                && game.OwnerOfHand(move.TargetHand) != null
                && ReferenceEquals(game.OwnerOfHand(move.TargetHand), actor))
            {
                // 自加：被加数是自己的手 -> 挨打的是对手，按对手算
                PlayerState victim = game.OpponentsOf(actor).Count > 0
                    ? game.OpponentsOf(actor)[0] : null;
                score += TargetPriorityScoreFor(move, actor, game, victim);
            }
            else
            {
                score += TargetPriorityScore(move, actor, game);
            }

            // ---- **集火**（在原目标优先级之上加一层，只影响"攻击集火目标"）----
            //
            // 为什么放在这里、而不是 `TargetPriorityScoreFor` 里：
            //   那个函数也被合盾 / 回血调用（历史路径），放进去会让
            //   "用集火目标的手合盾"白拿 25000 分（测试抓到过）。
            //   也**不能**改成"所有攻击减去最优目标分"的归一化写法：
            //   那会把合盾 / 回血相对抬高，1v1 胜率整体漂移（实验过）。
            //
            // 1v1 里 `ResolveFocusTarget` 直接返回 null（只有一位对手），
            // 所以 baseline 行为与旧版完全一致。
            if (priorityIsAttack && actor != null && game != null
                && IsAttackResult(move, actor))
            {
                PlayerState victim = ReferenceEquals(game.OwnerOfHand(move.TargetHand), actor)
                    ? (game.OpponentsOf(actor).Count > 0 ? game.OpponentsOf(actor)[0] : null)
                    : game.OwnerOfHand(move.TargetHand);

                PlayerState focus = FocusTargetForScoring(game, actor);
                if (focus != null && ReferenceEquals(focus, victim)
                    && game.CanReach(actor, victim))
                    score += FocusBonus;
            }

            // ---- **斩杀优先**（玩家要求："在能斩杀对方时，要大幅优先决定攻击"）----
            //
            // 斩杀线 = **本次攻击的最大伤害**，即
            //   基础值 + 攻方攻击加成 − 受方防御加成
            // 再叠加**角色自己的**修正（游侠有 2 时强化、剑客双持追加、
            // 刺客潜行 +7、巨人两段求和…见 <see cref="ILethalThresholdAdvisor"/>）。
            //
            // ⚠️ 必须放在最后、且量级**远大于**其他所有项 ——
            //    能一击带走时，前面那些"目标优先级""屯牌价值"全都不重要了。
            if (IsLethal(move, actor, game, result))
                score += LethalBonus;

            return score;
        }

        /// <summary>
        /// **斩杀赏金**。
        ///
        /// 取 100000 —— 比目标优先级最高档（10000）还大一个数量级，
        /// 因为"能不能赢"压过"打谁更好"。
        /// </summary>
        public int LethalBonus { get; set; } = 100000;

        /// <summary>
        /// 这一手能不能**直接斩杀**目标。
        ///
        /// 伤害估算必须**与真实结算同源**（<see cref="AttackContext.ApplyStatBonuses"/>），
        /// 否则会出现"AI 以为是斩杀、其实差 1 点"。
        /// 角色特有的增伤通过 <see cref="ILethalThresholdAdvisor"/> 叠上来。
        /// </summary>
        private bool IsLethal(LegalMove move, PlayerState actor, GameManager game, int result)
        {
            if (game == null || actor == null) return false;

            HandEffectType effect = Hand.GetEffectType(result);
            bool isAttack = effect == HandEffectType.Attack4
                            || effect == HandEffectType.Attack6
                            || effect == HandEffectType.Attack8;
            if (!isAttack) return false;

            PlayerState victim = game.OwnerOfHand(move.TargetHand);
            if (victim == null || !victim.IsAlive) return false;

            // ⚠️ 打队友不算"斩杀"（那是负收益，另有惩罚）
            if (game.IsTeammate(actor, victim)) return false;

            // 角色特有的增伤 / 额外段数（游侠强化、剑客追加、刺客潜行、巨人两段…）
            // 由 TotalLethalDamage 统一处理（传**原始值**给角色自算）。
            return TotalLethalDamage(move, actor, victim, result) >= victim.Health;
        }

        /// <summary>
        /// 估算这一手对某人的**总伤害**（含角色特有机理）。
        ///
        /// 提取出来是为了让"斩杀判定"与"测试"走同一条路径 ——
        /// 否则测试验证的是一份逻辑、实际用的是另一份。
        /// </summary>
        public int TotalLethalDamage(LegalMove move, PlayerState actor, PlayerState victim,
                                     int result)
        {
            if (actor == null || victim == null) return 0;

            int atkBonus = actor.Character?.AttackBonus ?? 0;
            int defBonus = victim.Character?.DefenseBonus ?? 0;

            var advisor = actor.Character as ILethalThresholdAdvisor;
            if (advisor != null)
            {
                int other = GetOtherHand(actor, move.ActedHand)?.Value ?? 0;
                return advisor.EstimateLethalDamage(
                    new LethalContext(move.ActedHand, move.TargetHand, result, other),
                    result, atkBonus, defBonus);
            }

            return AttackContext.ApplyStatBonuses(result, atkBonus, defBonus,
                                                  ignorePositiveDefense: false);
        }

        /// <summary>
        /// 估算一次攻击对某人的实际伤害（与真实结算同一套加减）。
        ///
        /// 只算**单段**；多段的合计由角色的
        /// <see cref="ILethalThresholdAdvisor"/> 负责。
        /// </summary>
        private static int EstimateAttackDamage(PlayerState attacker, PlayerState victim,
                                                int result)
        {
            return AttackContext.ApplyStatBonuses(result,
                                                  attacker.Character?.AttackBonus ?? 0,
                                                  victim.Character?.DefenseBonus ?? 0,
                                                  ignorePositiveDefense: false);
        }

        // ============================================================
        // 2v2 的**攻击目标优先级**（玩家给的顺序）
        // ============================================================
        //
        // 1. 优先攻击"自己和队友都能打到"的对手
        // 2. 优先攻击血量更低的对手
        // 3. 优先攻击没有防御（护盾 / 闪避 / 太极）的对手
        // 4. 优先攻击防御更低的对手
        // 5. 优先攻击攻击更高的对手
        //
        // ⚠️ 这是**字典序**（先满足第 1 条，同档内才比第 2 条…），
        //    所以权重必须是**递减的若干数量级**，且最高一档
        //    **大于其余所有档之和** —— 否则"低优先级项累加"
        //    会翻掉高优先级的判断。
        //
        //    取基数 10000：最高档 10000 > 1000+100+10+1 = 1111，安全。

        private const int TargetPriorityBase = 10000;

        // ============================================================
        // 情境化阻挡（玩家给的新规则）
        // ============================================================
        //
        // 规则原文：
        //   · **敌方血量低** -> 大幅增加"阻止他合成**盾 / 闪避 / 太极 / 回复**数字"的意愿
        //   · **自己血量低** -> 大幅增加"阻止他合成**攻击**数字"的意愿
        //   · 但"阻止对方"的减值**不能比**"让自己合成高价值"的数值多，
        //     比率调到 **3 : 7** 左右
        //
        // ## 3 : 7 是怎么落地的
        //
        // 走子总分里"自己的价值"与"对方的价值"原本就分开算：
        //   · **我方**：<see cref="HandValueScore"/> -> `preference / 10 * HandValueWeight`
        //     其中 `HandValueWeight` 默认 12 -> 满权重 12 分
        //   · **对方**：本方法 -> `* DenyRatioPercent / 100`
        //
        // 所以只要取 **DenyRatioPercent ≈ 43**，
        // 对方那一侧的权重就是 `12 * 0.43 ≈ 5`，
        // 与我方的 12 之比正好是 **5 : 12 ≈ 3 : 7**。
        //
        // ⚠️ 关键：`DenyRatioPercent` 是**作用在对方价值之上的缩放**，
        //    所以它天然保证了"阻挡的减值**永远不会超过**自己合成的收益" ——
        //    这正是玩家那条约束的意思。

        /// <summary>
        /// "阻挡对方"相对于"发展自己"的比率（百分比）。
        ///
        /// 43 ≈ 3/7 的比值（5 : 12）。**调平衡改这里。**
        /// </summary>
        public int DenyRatioPercent { get; set; } = 43;

        /// <summary>
        /// 把"这次运算会让**对方**拿到什么"折算成扣分。
        ///
        /// 只有**情境触发**时才生效（自己或对方血量低），
        /// 否则返回 0 —— 平时不需要多算这一项。
        /// </summary>
        private int SituationalDenyScore(LegalMove move, PlayerState actor, GameManager game)
        {
            if (game == null || actor == null) return 0;

            PlayerState victim = game.OwnerOfHand(move.TargetHand);
            if (victim == null || !victim.IsAlive) return 0;

            // 只对**对手**算（给队友的数字无所谓）
            if (ReferenceEquals(victim, actor) || game.IsTeammate(actor, victim)) return 0;

            // ⚠️ 被加数的手值**不会被改变**（见 GetLegalMoves 的说明），
            //    所以"我给他留下什么"这个说法在这款游戏里不成立 ——
            //    他手上有什么就是什么，与我无关。
            //
            //    真正起作用的方向是：**运算后我自己的两只手会变成什么**，
            //    因为**对手下一手可以拿它当被加数**。
            //    运算源会变成结果值，另一只手不变 —— 两只都要算。
            int otherValue = GetOtherHand(actor, move.ActedHand)?.Value ?? 0;
            int actedAfter = move.ActedHand.Value;

            // 用**对手自己的价值表**衡量：他看重什么，我就少留什么
            HandValueProfile foeProfile = HandValueProfiles.For(victim.Definition);
            if (foeProfile == null) return 0;

            bool foeDanger = IsLowHealth(victim);
            bool meDanger = IsLowHealth(actor);
            if (!foeDanger && !meDanger) return 0;

            // 取"两只手里对对手更有用的那一只" —— 这是我留下的最大敞口
            int exposure = System.Math.Max(DenyWeightOf(foeProfile, actedAfter),
                                           DenyWeightOf(foeProfile, otherValue));

            int denyWeight = 0;

            if (meDanger)
            {
                // 我快死了 -> 最怕他凑出**攻击**（用我留下的数字当被加数）
                denyWeight += exposure;
            }

            if (foeDanger)
            {
                // 他快死了 -> 最怕他凑出**盾 / 闪避 / 太极 / 回复**
                // 这些在他的价值表里同样是高值（盾 = 5、回复 = 9）
                denyWeight += exposure;
            }

            // ⚠️ 负数（= 扣分），且**按比率缩放** —— 保证永不超过我方收益
            int score = -(denyWeight * DenyRatioPercent) / 100;

            // ---- 魔术师专属（玩家给的规则）----
            //
            // 规则原话：
            //   "ai 应该是：避免送给比魔术师两手和价值更大的数字。
            //    即魔术师两手和价值很低，则需要针对；
            //    若魔术师两手和价值很高，针对就可以放缓 ——
            //    因为针对了魔术师也可以合成高价值数字。"
            //
            // 直白说：他已经能自己凑出来的数字，送给他**没有额外价值**；
            // 只有**超出他自造能力**的那个数字才值得挡。
            score += MagicianExposurePenalty(victim, actedAfter);
            score += MagicianExposurePenalty(victim, otherValue);

            return score;
        }

        /// <summary>
        /// **给魔术师留下的数字超出自造能力时的扣分**（玩家给的两条规则）。
        ///
        /// 魔术师可以拿自己的手当被加数，所以他**自己就能造出**
        /// "两手之和（归一化）"以内的任何数字。因此：
        ///   · 我留的数字 **≤ 他的自造上限** -> 他本来就能做，扣 0；
        ///   · 超出越多越危险 -> 按超出量扣分。
        ///
        /// 这自然实现了玩家说的两句话：
        ///   他两手和**很低** -> 自造上限低 -> 高数字都算"超出" -> **针对很强**；
        ///   他两手和**很高** -> 自造上限高 -> 高数字也不再超出 -> **针对放缓**。
        ///
        /// 对非魔术师返回 0（这条只服务于魔术师）。
        /// </summary>
        private static int MagicianExposurePenalty(PlayerState victim, int value)
        {
            if (victim?.Hands == null || value <= 0) return 0;

            // 只对魔术师算 —— 用能力接口判定，不硬编码角色类型
            var self = victim.Character as ISelfAddendProvider;
            if (self == null || !self.AllowsSelfAddend) return 0;

            int left = victim.LeftHand?.Value ?? 0;
            int right = victim.RightHand?.Value ?? 0;

            // 他能自己造出的最大值：两手之和（按十进制归一化）
            int selfMax = Hand.ComputeSum(left, right);

            int over = value - selfMax;
            if (over <= 0) return 0;

            // 每超出 1 点扣 6 分 —— 与其它情景化阻挡同量级（约 9~16）。
            const int PerPoint = 6;
            const int MaxPenalty = 30;      // 封顶，避免盖过我方自身收益
            int penalty = over * PerPoint;
            return penalty > MaxPenalty ? MaxPenalty : penalty;
        }

        /// <summary>
        /// 该手值在对手眼里值多少（用来衡量"留给他好不好"）。
        ///
        /// 直接用对手的价值表权重 —— 他看重什么就扣得多，
        /// 不需要在这里硬编码"哪些数字是盾 / 闪避 / 攻击"。
        /// </summary>
        private static int DenyWeightOf(HandValueProfile foeProfile, int value)
            => foeProfile.WeightOf(value) * DenyWeightScale / 100;

        /// <summary>
        /// 把对手的价值权重缩到与效果分同量级。
        ///
        /// 权重约 50~90 -> 乘 42/100 之后约 21~38，
        /// 再乘 <see cref="DenyRatioPercent"/>（43%）-> 约 9~16。
        /// 与我方 <see cref="HandValueWeight"/>（12）之比约为 **3 : 7**。
        /// </summary>
        private const int DenyWeightScale = 42;

        /// <summary>"血量低"的判定（双方共用）。</summary>
        private bool IsLowHealth(PlayerState p)
            => p != null && p.MaximumHealth > 0
               && p.Health * 100 <= p.MaximumHealth * DenyDangerHealthPercent;

        /// <summary>触发"情境化阻挡"的血量百分比（默认 40%）。</summary>
        public int DenyDangerHealthPercent { get; set; } = 40;


        /// <summary>
        /// 给走子评分（<b>仅供测试</b>）。
        ///
        /// 目标的优先级规则（血量 / 防御 / 攻击）只能从"两个候选之间谁分高"
        /// 来验证，所以需要把评分暴露出来 —— 否则测试只能靠"打很多局统计"
        /// 来间接推断，既慢又不确定。
        /// </summary>
        public int ScoreMoveForTest(LegalMove move, PlayerState actor, GameManager game)
            => ScoreMove(move, actor, game);

        /// <summary>该手牌是否"有防御手段"（护盾 / 太极）。</summary>
        private static bool HasDefense(Hand hand)
            => hand != null && (hand.IsShield || hand.IsTaichi);

        /// <summary>
        /// 把"该打谁"按玩家给的顺序打分（按**被加数的手主**）。
        ///
        /// 量级远大于效果档位（44~88），所以目标选择**压过**
        /// "这一手打多少" —— 这是刻意的：选错目标比少打两点严重得多。
        /// </summary>
        private int TargetPriorityScore(LegalMove move, PlayerState actor, GameManager game)
        {
            if (game == null || actor == null) return 0;

            PlayerState target = game.OwnerOfHand(move.TargetHand);
            return TargetPriorityScoreFor(move, actor, game, target);
        }

        /// <summary>
        /// 目标优先级评分（<paramref name="target"/> 就是**实际会挨打的人**）。
        ///
        /// 与"被加数手主"分开是因为：魔术师自加时
        /// "被加数的手主"是**自己**，而挨打的是**对手** ——
        /// 需要允许显式传入真正的挨打对象。
        /// </summary>
        private int TargetPriorityScoreFor(LegalMove move, PlayerState actor,
                                           GameManager game, PlayerState target)
        {
            if (game == null || actor == null || target == null) return 0;

            // 只对**对手**算这套优先级（打队友另有惩罚）
            if (ReferenceEquals(target, actor) || game.IsTeammate(actor, target)) return 0;

            int score = 0;

            // ⓪ **8 人身份局：只使用公开信息 + 自己的身份。**
            //    反贼知道主公是谁；内奸在只剩自己与主公时才动主公；
            //    主公/忠臣没有公开的反贼目标，沿用普通优先级。
            if (game.IsIdentityMode)
            {
                IdentityRole actorRole = game.IdentityOf(actor);
                IdentityRole targetRole = game.IdentityOf(target);
                bool targetIsLord = targetRole == IdentityRole.Lord;

                if (actorRole == IdentityRole.Rebel && targetIsLord)
                {
                    score += 1000000;
                }
                else if (IdentityRoleInfo.IsLordSide(actorRole)
                         && game.HasAttackedLord(target))
                {
                    // 主忠方：攻击过主公的人按“反贼嫌疑”优先处理。
                    score += 900000;
                }
                else if (actorRole == IdentityRole.Traitor)
                {
                    int aliveNonLord = 0;
                    IReadOnlyList<PlayerState> all = game.Players;
                    for (int i = 0; i < all.Count; i++)
                        if (all[i] != null && all[i].IsAlive
                            && game.IdentityOf(all[i]) != IdentityRole.Lord)
                            aliveNonLord++;

                    if (targetIsLord && aliveNonLord <= 1) score += 1000000;
                    else if (targetIsLord) score -= 500000;
                }
            }

            // ⓪ **8 人阵营混战：先打人多的敌方阵营**。
            //    这是玩家定的第一优先级；每多一名存活成员 +40000，
            //    比 2v2 六级目标优先级（最高约 20210）和集火分 25000 都高。
            //    1v1 / 2v2 的队伍数 ≤ 4，不进入这条分支。
            if (game.Players.Count > 4)
                score += game.AliveTeamCount(game.TeamOf(target)) * (TargetPriorityBase * 4);

            // ① 自己和队友都能打到 -> 最高优先
            if (BothTeamCanReachActor(actor, target, game))
                score += TargetPriorityBase;

            // ② 血量更低（血越少分越高）
            score += (100 - HealthPercentOf(target)) * (TargetPriorityBase / 100);

            // ③ 没有防御
            bool defended = HasDefense(target.LeftHand) || HasDefense(target.RightHand);
            if (!defended) score += TargetPriorityBase / 100;

            // ④ 防御更低（数值越小=防御越低，越优先）
            int def = target.Character?.DefenseBonus ?? 0;
            score += (10 - Clamp(def + 5, 0, 10)) * 10;

            // ⑤ 攻击更高（威胁更大，先打掉）
            int atk = target.Character?.AttackBonus ?? 0;
            score += Clamp(atk + 5, -10, 10);

            return score;
        }

        private static int Clamp(int v, int lo, int hi)
            => v < lo ? lo : (v > hi ? hi : v);

        /// <summary>目标血量的百分比（0~100，越低越优先）。</summary>
        private static int HealthPercentOf(PlayerState p)
        {
            if (p == null || p.MaximumHealth <= 0) return 100;

            return Clamp(p.Health * 100 / p.MaximumHealth, 0, 100);
        }

        /// <summary>
        /// "我和队友都能打到这个目标吗"。
        ///
        /// 1v1（没有队友）时恒不成立 —— 那是对的：
        /// 没有队友就谈不上"都能打到"。
        /// </summary>
        private static bool BothTeamCanReachActor(PlayerState actor, PlayerState target,
                                                  GameManager game)
        {
            if (actor == null || target == null || game == null) return false;
            if (!CanReach(actor, target, game)) return false;

            bool hasTeammate = false;
            IReadOnlyList<PlayerState> all = game.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;
                if (ReferenceEquals(p, actor)) continue;
                if (!game.IsTeammate(actor, p)) continue;

                hasTeammate = true;
                if (!CanReach(p, target, game)) return false;
            }

            return hasTeammate;
        }

        /// <summary>某玩家能否打到另一个玩家（比较攻击距离）。</summary>
        private static bool CanReach(PlayerState from, PlayerState to, GameManager game)
        {
            if (from == null || to == null || game == null) return false;

            return game.DistanceBetween(from, to) <= GameManager.BaseRangeOf(from);
        }

        /// <summary>
        /// "运算后留下的两只手值"值多少。
        ///
        /// 两部分，**缩放的待遇不同**：
        ///
        ///   ① **手值权重**（游侠的 1、刺客的 7 之类）：
        ///      这是"屯资源的偏好"，属于次要动机，所以缩到与效果分同量级。
        ///
        ///   ② **组合规则**（狼人凑 7、剑客双持）：
        ///      这是**角色签名能力的触发条件**，不是偏好 ——
        ///      应该像原先硬编码的 +200 那样有决定性。所以**不缩放**。
        ///
        /// ⚠️ 两个踩过的坑：
        ///   · 一开始把两者一起缩放，狼人为了多打 2 点伤害放弃了凑 7。
        ///   · 组合匹配曾用**对手的手值**去算 —— 但狼人的机制是
        ///     `运算后的手 + 另一只手 == 7`（见 WerewolfLogic.ScoreMoveBonus
        ///     的原始写法），对手的手值与此无关，导致凑 7 永远匹配不上。
        ///     正确的两个操作数是"运算后的手"与"自己的另一只手"。
        /// </summary>
        private int HandValueScore(PlayerState actor, int actedAfter, int otherHandValue)
        {
            HandValueProfile profile = HandValueProfiles.For(actor.Definition);
            if (profile == null) return 0;

            // ① 屯手值的偏好（次要，缩放）
            int preference = profile.WeightOf(actedAfter) + profile.WeightOf(otherHandValue);
            int score = preference / HandValueProfiles.MoveScoreScale * HandValueWeight;

            // ② 签名能力的触发（决定性，不缩放）
            for (int i = 0; i < profile.Combos.Count; i++)
            {
                if (profile.Combos[i].Matches(actedAfter, otherHandValue))
                    score += profile.Combos[i].Bonus;
            }

            // ③ **邪术师距离修正的"情境权重"**（玩家要求）
            score += WarlockDistanceAdjust(actor, actedAfter);

            return score;
        }

        /// <summary>
        /// **邪术师把对手推开的那两个回合里，双方的手牌价值观要变。**
        ///
        /// 玩家要求（原话）：
        ///   "把近距离角色推开的两个回合内，大幅增加攻击数字权重，
        ///    大幅降低 5 的权重（但要比无意义数字大）"；
        ///   "增加的是邪术师自己的数值，
        ///    被邪术师影响的角色在那两个回合里应该降低攻击数字的价值"。
        ///
        /// 为什么放这里而不是改静态权重表：
        ///   同一张手牌，在"对手被推开（打不到我）"和"没被推开"两种局面下价值不同，
        ///   静态表表达不了，只能按实时局面加/减分。
        ///
        /// 判据用**距离修正层**（`DistancePenaltyBetween > 0`）——
        ///   它就是"被邪术师推开的两个回合"这个状态本身。
        /// </summary>
        private int WarlockDistanceAdjust(PlayerState actor, int actedAfter)
        {
            if (Game == null || actor == null) return 0;

            bool isAttackNumber = actedAfter == 4 || actedAfter == 6 || actedAfter == 8;
            bool isFive = actedAfter == 5;

            // ---- ① 邪术师自己：对手被推开 -> 猛攻 ----
            if (actor.Character is Characters.SpecialLogics.WarlockLogic)
            {
                if (!AnyOpponentPushedBy(actor)) return 0;

                if (isAttackNumber) return AggressiveAttackBonus;   // 大幅增加
                if (isFive) return -CalmFivePenalty;                // 大幅降低
                return 0;                                            // 1/2/3/7 不动，于是 5 仍高于它们
            }

            // ---- ② 被推开的人：这两回合打不到人 -> 攻击数字贬值 ----
            if (IsPushedByWarlock(actor) && isAttackNumber) return -PushedAttackPenalty;

            return 0;
        }

        /// <summary>邪术师自己：是否有对手正被自己的距离修正推开。</summary>
        private bool AnyOpponentPushedBy(PlayerState warlock)
        {
            System.Collections.Generic.IReadOnlyList<PlayerState> all = Game.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || ReferenceEquals(p, warlock) || !p.IsAlive) continue;
                if (Game.DistancePenaltyBetween(warlock, p) > 0) return true;
            }
            return false;
        }

        /// <summary>我自己是否正被某位邪术师推开（距离修正 &gt; 0）。</summary>
        private bool IsPushedByWarlock(PlayerState me)
        {
            System.Collections.Generic.IReadOnlyList<PlayerState> all = Game.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || ReferenceEquals(p, me) || !p.IsAlive) continue;
                if (!(p.Character is Characters.SpecialLogics.WarlockLogic)) continue;
                if (Game.DistancePenaltyBetween(p, me) > 0) return true;
            }
            return false;
        }

        /// <summary>邪术师"猛攻"时给攻击数字的额外分（大幅）。</summary>
        private const int AggressiveAttackBonus = 260;

        /// <summary>邪术师"猛攻"时给 5 的减分（降到有意义数字之下、无意义数字之上）。</summary>
        private const int CalmFivePenalty = 180;

        /// <summary>被推开时攻击数字的贬值幅度。</summary>
        private const int PushedAttackPenalty = 200;

        private static Hand GetOtherHand(PlayerState actor, Hand acted)
            => ReferenceEquals(acted, actor.LeftHand) ? actor.RightHand : actor.LeftHand;

        /// <summary>
        /// "这一手之后，我自己**下一手**最好的结果值多少"。
        ///
        /// 玩家要求的"避免反哺"在这款游戏里的**正确形态**：
        ///   · 对手的可选集只取决于他自己手上的牌（我们留什么不影响他），
        ///     所以不存在"把差牌喂给对手"这回事
        ///   · 真正会被反哺的是**自己** —— 留下的两只手决定下一手能打出什么
        ///
        /// 所以这里模拟"下一回合我用留下的两只手之一去加场上任一对手的手"，
        /// 取其中**最好的效果分**（用与 <see cref="ScoreMove"/> 同一套档位），
        /// 再乘一个不大的权重 —— 它只该在"当前这一手差不多"时起决定作用。
        ///
        /// 实现上只枚举**效果档位**，不重复算组合 / 屯牌分，
        /// 避免与 <see cref="HandValueScore"/> 叠加成双份。
        /// </summary>
        private int DenyScore(PlayerState actor, GameManager game,
                              int actedAfter, int otherHandValue)
        {
            if (game == null || actor == null) return 0;

            // ⚠️ **有"签名组合规则"的角色跳过这一项**。
            //
            //    狼人要凑"双手和为 7"、剑客要双 4/6/8、刺客要双 7 ——
            //    这些都是**角色自己的完整计划**，而通用的"下一手潜力"
            //    与它们经常相反（例如 7 对狼人是死牌，但 7 的下一手潜力很高）。
            //    两者一起算会把签名计划压掉
            //    （测试当场抓到：狼人 20 回合都触发不了一次和 7）。
            HandValueProfile profile = HandValueProfiles.For(actor.Definition);
            if (profile != null && profile.Combos.Count > 0) return 0;

            int best = 0;

            // 两种"留下的手"（下一回合可能拿任一只当运算源）
            best = System.Math.Max(best, BestNextEffect(actor, game, actedAfter));
            best = System.Math.Max(best, BestNextEffect(actor, game, otherHandValue));

            return best * NextTurnPotentialWeight / 100;
        }

        /// <summary>用 <paramref name="myHand"/> 当运算源时，下一手最好的效果分。</summary>
        private static int BestNextEffect(PlayerState actor, GameManager game, int myHand)
        {
            int best = 0;

            IReadOnlyList<PlayerState> all = game.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;
                if (ReferenceEquals(p, actor)) continue;

                foreach (Hand theirs in p.Hands)
                {
                    int result = Hand.ComputeSum(myHand, theirs.Value);

                    int s;
                    switch (Hand.GetEffectType(result))
                    {
                        case HandEffectType.Attack4: s = 44; break;
                        case HandEffectType.Attack6: s = 66; break;
                        case HandEffectType.Attack8: s = 88; break;
                        case HandEffectType.Shield: s = 50; break;
                        case HandEffectType.Heal: s = 30; break;
                        default: s = 0; break;
                    }

                    if (s > best) best = s;
                }
            }

            return best;
        }

        /// <summary>
        /// "下一手潜力"在总分里的千分比权重。
        ///
        /// ⚠️ 取 120（最高约 +10 分）—— 我第一版取 300（+26）就把
        ///    游侠的"强化射击"压过去了（测试当场抓到：
        ///    它宁可留着 2 也不肯变 3 强化）。
        ///    所以它必须**明显小于**一次效果档位（44~88），
        ///    只在"这一手打什么已经定了"之后做微调。
        /// </summary>
        public int NextTurnPotentialWeight { get; set; } = 120;

        /// <summary>用数值较低的那面盾去挡，尽量保留高数字的手。</summary>
        public override Hand AskWhichShieldToBlock(PlayerState defender, Hand shieldA, Hand shieldB,
                                                   AttackContext context)
            => shieldA.Value <= shieldB.Value ? shieldA : shieldB;

        public override ChainBreakTarget AskWarriorChainBreakTarget(PlayerState fighter,
                                                                    IReadOnlyList<ChainBreakTarget> candidates)
        {
            if (!UseChainBreak || candidates == null || candidates.Count == 0) return default;

            // 优先破对手的盾
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].Owner != fighter) return candidates[i];

            return default;
        }

        /// <summary>
        /// 通用角色抉择的贪心策略（按角色特性逐条处理）：
        ///
        ///   · 锁手（野蛮人）：锁对手更大的那只手，限制其最强的一招。
        ///   · 强化射击（游侠）：一律发动 —— 白赚 50% 伤害，代价只是把一只 2 变成 3。
        ///   · 闪避（游侠）：一律用掉一只 1。被攻击时一只 1 本身毫无战斗力，
        ///     换掉一次完整攻击是纯赚（比"被盾挡下"还彻底）。
        ///   · 回血目标（牧师）：**永远回给自己**，绝不回给对手。
        ///   · 追加攻击（剑客）：伤害太低时不换（保留原来那手数字）。
        /// </summary>
        public override Hand ChooseHandFor(ChoiceRequest request, PlayerState decider)
        {
            IReadOnlyList<Hand> c = request.Candidates;
            if (c == null || c.Count == 0) return request.DeclineResult;

            switch (request.Effect)
            {
                case HandEffect.BarbarianLockTarget:
                    {
                        // 锁手该锁**对手最依赖的那只手**，而不是单纯"数值最大"。
                        //
                        // 判据：这只手能给对手带来多好的效果 ——
                        // 用**对手自己的价值表**算（他看重什么，锁掉什么最疼）。
                        //
                        // 为什么不是"数值最大"：数值大不等于有用
                        // （例如对狼人来说 7 反而是死牌、对巨人的 4/6/8 完全无效果）。
                        // 用对手自己的价值表就自动涵盖了这些反直觉的情况。
                        HandValueProfile foeProfile =
                            HandValueProfiles.For(Game?.OwnerOfHand(c[0])?.Definition);

                        Hand best = c[0];
                        int bestScore = int.MinValue;

                        for (int i = 0; i < c.Count; i++)
                        {
                            int s = foeProfile?.WeightOf(c[i].Value) ?? c[i].Value;

                            // 已成型的盾额外加分：锁住它等于让对手少一次免伤
                            if (c[i].IsShield) s += 30;

                            if (s > bestScore) { bestScore = s; best = c[i]; }
                        }

                        return best;
                    }

                case HandEffect.RangerPowerShot:
                    return c[0];       // 每次都给强化，白赚

                // ⚠️ 猎人**伏击**：条件已满足（两手同为 1/2/3）。
                //
                //    但不能**无条件**接受 —— 伏击**只有一次**（抵伤即消耗），
                //    无脑用掉会把自己一只手封住、却没换来任何东西。
                //    实测：无条件接受时猎人 1v1 胜率 49.6% -> 41.8%、
                //    平局率 3.7% -> **19.3%**（大量僵局）。
                //
                //    所以只在"**对手这一手可能打出攻击**"时才埋伏 ——
                //    这时伏击能**截掉他一次攻击**，才是真的赚。
                //    （判据：他两只手里有一只能与我的某个手值凑出 4/6/8。）
                case HandEffect.HunterAmbush:
                    return OpponentLikelyCanAttack(request) ? c[0] : request.DeclineResult;

                case HandEffect.RangerDodge:
                    return c[0];       // 被攻击时换掉一整次攻击，永远划算

                case HandEffect.PriestHealTarget:
                case HandEffect.PriestExtraHealTarget:
                    {
                        // 玩家给的规则：
                        //   只有 2v2 什么时候回给队友要写，
                        //   目前写成**优先给一队血量最低的人回复**。
                        //
                        // ⚠️ 这里**不能**再用"第一个候选就是自己的手"那个假设 ——
                        //    2v2 里队友是 C/D，候选顺序是"自己 -> 其他人"，
                        //    取第一个就等于永远只回自己（玩家实测就是这个症状）。
                        //
                        // 依赖方向：AI（Assistant 层）不能引用 Characters 层，
                        // 所以由角色通过 IHealTargetAdvisor 自报该给谁。
                        var advisor = decider?.Character as IHealTargetAdvisor;
                        PlayerState want = advisor?.BestHealTarget(decider, Game);

                        if (want != null)
                        {
                            for (int i = 0; i < c.Count; i++)
                            {
                                if (Game != null && ReferenceEquals(Game.OwnerOfHand(c[i]), want))
                                    return c[i];
                            }
                        }

                        return c[0];
                    }

                case HandEffect.PriestAttackOrHeal:
                    // 玩家给的规则：
                    //   "牧师只需要检查在**危险时刻优先给回血**，否则**全部进攻**"
                    //
                    // 所以判据是"自己是否处于危险值"（血量百分比），
                    // 而不是一个固定的绝对值 —— 绝对值在不同生命上限的角色
                    // 之间没有可比性（巨人 25、狼人 25、其余 20）。
                    //
                    // 返回的手既表示"要回复"，也表示"回给谁"。
                    // ⚠️ 候选顺序是"自己两只手在前、其他人两只手在后"，
                    //    所以发动时取第一个（= 自己的手）。
                    if (decider == null) return null;

                    bool danger = decider.Health * 100
                                  <= decider.MaximumHealth * PriestHealHealthPercent;

                    if (!danger) return null;      // 不危险 -> 全部进攻（放弃 = 当攻击打）

                    // 危险 -> 优先回血，且回给"自己这一队血最低的人"
                    var healAdvisor = decider.Character as IHealTargetAdvisor;
                    PlayerState healWant = healAdvisor?.BestHealTarget(decider, Game);

                    if (healWant != null && Game != null)
                    {
                        for (int i = 0; i < c.Count; i++)
                        {
                            if (ReferenceEquals(Game.OwnerOfHand(c[i]), healWant)) return c[i];
                        }
                    }

                    return c[0];

                case HandEffect.SwordsmanFollowUp:
                    {
                        int damage = FollowUpDamageLookup?.Invoke() ?? int.MaxValue;
                        return damage >= SwordsmanFollowUpMinDamage ? c[0] : null;
                    }

                // ---------- 法师 ----------
                case HandEffect.WizardAbandonAttack:
                    {
                        // 玩家给的方向（本轮）：
                        //   "法师在对方未进斩杀线前，若自己没有放弃过攻击，
                        //     合出 4 会倾向于放弃攻击，6 不一定会放弃，8 基本不放弃"
                        //
                        // 什么时候**一定不**放弃：
                        //   · 已经有一个待用的类型了 —— 再放弃只是白扔一回合；
                        //   · 这一击能**直接斩杀** —— 立刻结束比赛胜过挂状态。
                        if (decider == null || Game == null) return null;

                        // ⚠️ 用 Core 的 IMageState 而不是 WizardLogic ——
                        //    Assistant 层不能引用 Characters 层（架构铁律）。
                        var mage = decider.Character as IMageState;
                        if (mage == null) return null;

                        // ⚠️⚠️ **"不重复放弃"是 AI 的策略，不是规则。**
                        //
                        //    玩家澄清："在消耗前应该要是可以弹是否放弃的，
                        //    只是让 AI 算法不要重复选，
                        //    玩家实操可以自己决定是否要重复放弃。"
                        //
                        //    所以引擎（`WizardLogic.CollectPreTurnChoices`）
                        //    **永远都问**；克制放在这里 —— 已经铺好了就不要再铺，
                        //    否则只是白发一回合（AI 自己算得清这笔账）。
                        if (mage.HasPendingType) return null;      // 已经铺好了

                        if (WizardAbandonLethalGuard?.Invoke() == true) return null;   // 能斩杀

                        // 本次合出的结果（4/6/8）—— 决定放弃的倾向
                        int result = request.ResultValue;

                        if (!WizardShouldAbandon(result, decider)) return null;

                        WantsToAbandonAttack = true;
                        return c[0];
                    }

                case HandEffect.WizardDamageType:
                    {
                        ChosenDamageType = WizardPickDamageType(decider);
                        return c[0];
                    }

                case HandEffect.FighterChainBreak:
                    {
                        if (!UseChainBreak || c.Count == 0) return null;   // 默认放弃

                        // 优先破**对手**的盾：候选里自己的手在前、对手的在后，
                        // 所以挑第一个不属于自己的。
                        for (int i = 0; i < c.Count; i++)
                            if (decider == null || !decider.Owns(c[i])) return c[i];

                        return null;    // 只有自己的盾可破 -> 没必要（自伤）
                    }

                default:
                    return request.DeclineResult;
            }
        }

        /// <summary>
        /// **对手这一手是否可能打出攻击**（供猎人决定要不要伏击）。
        ///
        /// 判据：他两只手里有任意一只，能与**我的某个手值**凑出 4/6/8 ——
        /// 那就说明他下一手有能力攻击，伏击才值得用来**截掉他一次攻击**。
        ///
        /// ⚠️ 为什么必须加这个限制：伏击**只有一次**（抵伤即消耗）。
        ///    无条件接受会把自己一只手封住、却什么都没换来 ——
        ///    实测：猎人 1v1 胜率 49.6% -> 41.8%、平局率 3.7% -> 19.3%。
        /// </summary>
        private bool OpponentLikelyCanAttack(ChoiceRequest request)
        {
            GameManager game = Game;
            PlayerState decider = request.Decider;
            if (game == null || decider == null) return false;

            // 我的两只手值（他会拿它们当被加数）
            int[] mine = { decider.LeftHand?.Value ?? 0, decider.RightHand?.Value ?? 0 };

            for (int p = 0; p < game.Players.Count; p++)
            {
                PlayerState foe = game.Players[p];
                if (foe == null || !foe.IsAlive) continue;
                if (ReferenceEquals(foe, decider) || game.IsTeammate(decider, foe)) continue;

                int[] theirs = { foe.LeftHand?.Value ?? 0, foe.RightHand?.Value ?? 0 };

                for (int a = 0; a < theirs.Length; a++)
                {
                    if (theirs[a] <= 0) continue;
                    for (int b = 0; b < mine.Length; b++)
                    {
                        if (mine[b] <= 0) continue;

                        HandEffectType eff = Hand.GetEffectType(
                            Hand.ComputeSum(theirs[a], mine[b]));

                        if (eff == HandEffectType.Attack4
                            || eff == HandEffectType.Attack6
                            || eff == HandEffectType.Attack8)
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 由调用方注入："剑客这次追加攻击会造成多少伤害"。
        /// 这是一次结算过程中的临时信息，只有正在跑的那次运算知道。
        /// 未注入时按"值得发动"处理（保守起见不打折）。
        /// </summary>
        public Func<int> FollowUpDamageLookup { get; set; }
    }
}
