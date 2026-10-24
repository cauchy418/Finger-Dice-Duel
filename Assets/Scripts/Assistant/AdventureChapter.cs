using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>章节目录与关卡专用对手；不加入竞技角色表。</summary>
    public static class AdventureChapter
    {
        public const int BattleCount = 10;
        public const string InstructorName = "哨站教官";
        public const string CompletionKey = "FingerDiceDuel.Adventure.Chapter1.Completed";
        public const string KnightId = "border_outpost_knight";
        public const string KnightEmblem = "Adventure/knight_original";
        public static readonly CharacterDefinition Knight = new CharacterDefinition(KnightId,
            "哨站骑士", SpecialLogicType.None, 1, 1, 20, "哨站终战对手：攻+1、防+1、距离1，无其它技能。", "Outpost Knight");
        public static System.Collections.Generic.IReadOnlyList<CharacterDefinition> FinaleRoster
        {
            get
            {
                var result = new System.Collections.Generic.List<CharacterDefinition>();
                foreach (var role in CharacterDefinition.BuiltIn)
                    if (role.Profession?.Id == "fighter" && CharacterFactory.IsRegistered(role.Logic)) result.Add(role);
                return result;
            }
        }
        public static string BattleName(int number)
            => new[] { "初战", "次战", "三战", "四战", "五战", "六战", "七战", "八战", "九战", "终战" }[number - 1];
        public static string Hint(int number)
            => new[] { "攻击 / 合盾 / 回复", "勇士 · 十点重击", "破阵者 · 连锁 / 修复 / 流血",
                "座位距离 / 碎盾换射程", "经典先攻 / 自由实战", "魔射手 · 穿盾 / 增伤 / 连击",
                "战斗大师 · 四种招式状态", "剑士 · 追加与主攻击", "战术家 · 体力差与攻防", "自由选角 / 经典骑士试炼" }[number - 1];
        public static CharacterDefinition PlayerDefinition(int number)
        {
            switch (number)
            {
                case 6: return CharacterDefinition.ArcaneArcher;
                case 7: return CharacterDefinition.BattleMaster;
                case 8: return CharacterDefinition.Swordsman;
                case 9: return CharacterDefinition.Tactician;
                default: return number < 3 ? CharacterDefinition.Champion : CharacterDefinition.Breaker;
            }
        }
    }
}
