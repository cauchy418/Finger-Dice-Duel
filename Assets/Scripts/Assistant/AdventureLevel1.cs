using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// **冒险模式 · 剧情模式 · 第一关「第一章：边境哨站·初战」**（用户已确认，2026-10-24）。
    ///
    /// 定位：一场 7 回合、**绝对不可失败**的剧本化新手教学。
    /// 玩家 = 战士·勇士（攻+1/防0/生命20）；对手 = 老兵（白板，攻0/防0，
    /// **生命 12** —— 剧情口径"身负旧伤的老兵"）。
    ///
    /// 教学序列（逐回合数值已演算核对，见 Docs/Adventure/level-01.md）：
    ///   1. 攻击（1+3=4 → 5 伤，老兵 12→7）；2. 老兵还手 4 伤（玩家 20→16）；
    ///   3. 合盾（1+4=5）；4. 老兵攻击（1+5=6）被盾挡下、盾碎；
    ///   5. 回血（5+4=9，16→20 上限截断）；
    ///   6. 满十归一（老兵 6+4=10 → 1，无效果）；
    ///   7. 半自由决胜（提示 4+4=8，放开全部点击；老兵进入收官保护）。
    ///
    /// 架构说明：
    ///   · 本类只是**导演**：挂在调用方建好的 <see cref="GameManager"/> 与
    ///     <see cref="BoardController"/> 上，不建对局、不碰 Core。
    ///   · **点击守门在界面层**（调用方把 <see cref="AllowClick"/> 接进
    ///     OnHandClicked / SetInteractable）。不能放进 BoardController：
    ///     AI 的 MakeAutomaticMove 复用 ClickHand 路径，控制器内守门
    ///     会把老兵的脚本出招也挡死。
    ///   · 老兵 = <see cref="ScriptedMoveSelector"/>（剧本步 + 收官保护），
    ///     由调用方经 <see cref="Bind"/> 绑到控制器。
    /// </summary>
    public sealed class AdventureLevel1 : AdventureLesson
    {
        public const string Title = "第一章：边境哨站·初战";
        public new const string GuideAvatarKey = "Adventure/guide_soldier";
        public new const string GuideName = "老兵";

        /// <summary>老兵（白板）的生命上限：剧情口径"身负旧伤"。</summary>
        public const int SoldierMaxHealth = 12;

        /// <summary>开场剧情文案（点「跟随指引」后进入拍 1）。</summary>
        public const string IntroText =
            "边境哨站。身负旧伤的老兵拦住了去路。\n" +
            "「新兵，先学会指尖骰术，再谈闯荡。」\n" +
            "—— 两只手指相加，个位定乾坤：4/6/8 攻击、5 合盾、9 回血。";

        /// <summary>胜利要点卡文案。</summary>
        public const string VictoryText =
            "本课要点\n" +
            "4/6/8：攻击，基础伤害等于点数。\n" +
            "5：合盾，抵挡一段攻击后破碎。\n" +
            "9：回复9点生命，不超过上限。\n" +
            "1/2/3/7：无基础效果；满十归一，10变1。\n\n" +
            "下一课：勇士「精通重击」。\n" +
            "运算后双手和10，发起攻击的基础伤害×1.5。";

        private AdventureLevel1(GameManager game, BoardController board) : base(game, board)
        {
            BuildBeats();
        }

        public override int LevelNumber => 1;
        public override string DisplayTitle => Title;
        public override string OpeningText => IntroText;
        public override string SummaryText => VictoryText;
        protected override int OpeningMoveCount => 3;
        protected override ScriptedMoveSelector CreateOpeningScript()
            => new ScriptedMoveSelector().Add(0, 1).Add(1, 1).Add(1, 0);

        // ============================================================
        // 建关
        // ============================================================

        /// <summary>
        /// 把关卡导演挂到**已建好**的对局上（玩家 = PlayerA = 勇士，老兵 = PlayerB）。
        /// 调用方随后：①<see cref="PrepareSoldier"/>（或自行定制）；②<see cref="Bind"/> 绑老兵 AI；
        /// ③按正常 1v1 流程推上屏并 BeginTurn。
        /// </summary>
        public static AdventureLevel1 Attach(GameManager game, BoardController board)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            return new AdventureLevel1(game, board);
        }

        /// <summary>
        /// 教学版老兵定制：改名"老兵"、起手 3/1、生命 12（剧情"旧伤"）。
        /// 必须在 BeginTurn 之前调用（SetMaxHealth 会把当前生命夹到新上限）。
        /// </summary>
        public static void PrepareSoldier(PlayerState soldier)
        {
            if (soldier == null) return;
            soldier.Rename(GuideName);
            soldier.LeftHand.SetValue(3);
            soldier.RightHand.SetValue(1);
            soldier.SetMaxHealth(SoldierMaxHealth);
        }

        private void BuildBeats()
        {
            // —— 拍 1：攻击教学（1+3=4 → 4+1攻=5 伤；老兵 12→7）——
            _beats.Add(new Beat
            {
                Narration = "先点自己的左手1，再点老兵左手3。\n" +
                            "1+3=4，发动攻击。\n" +
                            "4/6/8是攻击数字，基础伤害等于点数。\n" +
                            "勇士攻击+1，本次造成5伤。\n" +
                            "最后点老兵，确认攻击目标。",
                ActedHand = _player.LeftHand,
                AddendHand = _soldier.LeftHand,
                Complete = l => l.Soldier.Health == SoldierMaxHealth - 5
                                && l.Player.LeftHand.Value == 4,
            });

            // —— 拍 2：老兵还手（老兵 3+1=4 → 4 伤；玩家 20→16）——
            _beats.Add(new Beat
            {
                Narration = "老兵用同样的方式还手了——注意你的生命条，被攻击就会掉血。" +
                            "别慌，老兵这就教你防御。",
                Complete = l => l.Player.Health == 16,
            });

            // —— 拍 3：合盾教学（1+4=5 → 盾）——
            _beats.Add(new Beat
            {
                Narration = "结果是【5】就是合盾！点你的右手（1），再点老兵的左手（4）——" +
                            "1+4=5，那只手变成一面盾，可完全挡下一次攻击。" +
                            "注意：合盾作用于自己，没有第三步。",
                ActedHand = _player.RightHand,
                AddendHand = _soldier.LeftHand,
                Complete = l => l.Player.RightHand.State == ShieldState.Formed,
            });

            // —— 拍 4：盾挡教学（老兵 1+5=6 攻击 → 被盾挡下、盾碎）——
            _beats.Add(new Beat
            {
                Narration = "观察护盾如何挡下老兵这次攻击。\n" +
                            "抵挡一段后，护盾变为破碎；手上的数字5仍可用于下一次运算。",
                Complete = l => l.Player.RightHand.State == ShieldState.Broken
                                && l.Soldier.RightHand.Value == 6,
            });

            // —— 拍 5：回血教学（5+4=9 → 回 9；16→20 上限截断；玩家的回合）——
            _beats.Add(new Beat
            {
                Narration = "该你出招了：结果是【9】就是回复！点你的碎盾手（5），" +
                            "再点老兵的左手（4）——5+4=9，回复 9 点生命。" +
                            "注意：回复不能超过上限，超出的部分会浪费。",
                ActedHand = _player.RightHand,
                AddendHand = _soldier.LeftHand,
                Complete = l => l.Player.Health == l.Player.MaximumHealth,
            });

            // —— 拍 6：满十归一教学（老兵 6+4=10 → 1，无效果；老兵的回合）——
            _beats.Add(new Beat
            {
                Narration = "老兵演示 6+4=10——满十归一！个位变成【1】。" +
                            "1/2/3/7 都没有效果，只改变手值。凑数时要小心十。",
                Complete = l => l.Soldier.RightHand.Value == 1,
            });

            // —— 拍 7：半自由决胜（提示 4+4=8；放开全部点击）——
            _beats.Add(new Beat
            {
                Narration = "老兵还剩7点生命，试着赢下切磋！提示：左手 4 + 老兵左手 4 = 8。" +
                            "这一步由你决定。老兵停止进攻，你可以放心尝试。",
                FreeClicks = true,
                Complete = l => l.IsComplete,
            });
        }

    }
}
