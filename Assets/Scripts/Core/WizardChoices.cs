using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>回合边界可暂停的技能请求；结算内只排队，不同步向人类索取输入。</summary>
    public sealed class WizardChoice
    {
        public PlayerState Owner { get; }
        public string Prompt { get; }
        public IReadOnlyList<Hand> Candidates { get; }
        public bool AllowDecline { get; }
        public bool TargetsPlayer { get; }
        public string DeclineLabel { get; }
        private readonly Action<Hand> _apply;
        public WizardChoice(PlayerState owner, string prompt, IReadOnlyList<Hand> candidates,
                            bool allowDecline, Action<Hand> apply, bool targetsPlayer=false, string declineLabel="结束选择 / 不发动")
        { Owner=owner; Prompt=prompt; Candidates=candidates; AllowDecline=allowDecline; _apply=apply;
          TargetsPlayer=targetsPlayer;DeclineLabel=declineLabel; }
        public bool Choose(Hand hand)
        {
            if (hand==null) { if (!AllowDecline) return false; }
            else { bool found=false; foreach(var h in Candidates) if(ReferenceEquals(h,hand)) found=true; if(!found) return false; }
            _apply?.Invoke(hand); return true;
        }
    }

    public interface IAbjurationSkill
    {
        int BarrierPoints { get; }
        AttackDamageType SelectedResistance { get; set; }
        void StoreBarrier(int points);
    }

    /// <summary>同一套策略供界面驱动和无头整局调用，角色层只依赖 Core 合约。</summary>
    public interface IWizardChoicePolicy
    {
        PlayerState ResistanceTarget(PlayerState owner, GameManager game);
        AttackDamageType ResistanceType(PlayerState owner, PlayerState target, GameManager game);
        bool ShouldStoreBarrier(PlayerState owner, int points, GameManager game);
        PlayerState NecroticTarget(PlayerState owner, GameManager game);
        int ExchangeValue(PlayerState owner, int value);
    }

    public static class ResistanceNames
    {
        public static readonly AttackDamageType[] All = {
            AttackDamageType.Slashing, AttackDamageType.Bludgeoning, AttackDamageType.Piercing,
            AttackDamageType.Thunder, AttackDamageType.Acid, AttackDamageType.Poison,
            AttackDamageType.Radiant, AttackDamageType.Necrotic, AttackDamageType.Fire,
            AttackDamageType.Cold, AttackDamageType.Lightning, AttackDamageType.Force, AttackDamageType.Psychic };
        // 防护学派限定六项；物理选择同时赋予三类抗性。All 保留通用十三类图标索引。
        public static readonly AttackDamageType[] Choices = {
            AttackDamageType.Bludgeoning, AttackDamageType.Thunder, AttackDamageType.Acid,
            AttackDamageType.Fire, AttackDamageType.Cold, AttackDamageType.Lightning };
        public static bool CanGrant(AttackDamageType type) => System.Array.IndexOf(Choices,type)>=0;
        public static string Key(AttackDamageType type) => "Resistance/resistance_" + type.ToString().ToLowerInvariant();
        public static string Name(AttackDamageType type) => AttackDamageTypes.NameOf(type) + "抗性";
    }
}
