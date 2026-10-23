using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>技能的命名选项；数值为技能内部标识，不伪装成手牌。</summary>
    public sealed class SkillChoiceOption
    {
        public int Value { get; }
        public string Label { get; }
        public string IconKey { get; }
        public string Description { get; }
        public SkillChoiceOption(int value,string label,string iconKey,string description="")
        { Value=value;Label=label;IconKey=iconKey;Description=description; }
    }
    public interface ISkillChoicePolicy
    {
        int ChooseSkillOption(string key,IReadOnlyList<SkillChoiceOption> options,PlayerState owner,PlayerState target,GameManager game);
        PlayerState HunterMarkTarget(PlayerState owner,IReadOnlyList<PlayerState> candidates,GameManager game);
    }
    public static class SkillChoicePolicy
    {
        public static int Choose(string key,IReadOnlyList<SkillChoiceOption> options,PlayerState owner,PlayerState target,GameManager game)
        {
            int value=game?.GetSkillPolicy(owner)?.ChooseSkillOption(key,options,owner,target,game)??options[0].Value;
            foreach(var option in options) if(option.Value==value) return value;
            return options[0].Value;
        }
    }

    public static class MartialSkillChoices
    {
        public const string OpenHand="open_hand", FourElements="four_elements", Draconic="draconic";
        public static List<SkillChoiceOption> OpenHandOptions(int ki)
        {
            var options=new List<SkillChoiceOption>();
            if(ki>=4) options.Add(new SkillChoiceOption(-1,"共鸣 · 4气","Effects/ki","附加共鸣，下次攻击引爆。"));
            if(ki>=2) foreach(var status in new[]{StatusEffect.KnockedDown,StatusEffect.Stunned})
                options.Add(new SkillChoiceOption((int)status,StatusNames.Of(status)+" · 2气","Status/"+StatusNames.IconKeyOf(status),StatusNames.Describe(status)));
            options.Add(new SkillChoiceOption(0,"保留气点","Effects/ki","本次不消耗气点。"));return options;
        }
        public static List<SkillChoiceOption> Elements(bool draconic,bool includeDecline=true)
        {
            var list=new List<SkillChoiceOption>();
            var types=draconic?new[]{AttackDamageType.Cold,AttackDamageType.Fire,AttackDamageType.Lightning,AttackDamageType.Poison,AttackDamageType.Acid}
                :new[]{AttackDamageType.Fire,AttackDamageType.Cold,AttackDamageType.Lightning,AttackDamageType.Thunder,AttackDamageType.Acid};
            foreach(var type in types) list.Add(new SkillChoiceOption((int)type,AttackDamageTypes.NameOf(type)+(draconic?"":" · 2气"),"Damage/"+AttackDamageTypes.IconKeyOf(type),draconic?"永久抗性；攻击改为此元素。":"本次攻击改为此元素。"));
            if(!draconic&&includeDecline) list.Add(new SkillChoiceOption(0,"保留气点","Effects/ki","本次保持原攻击类型。"));return list;
        }
    }

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
        public IReadOnlyList<SkillChoiceOption> Options { get; } = Array.Empty<SkillChoiceOption>();
        private readonly Action<int> _applyOption;
        public WizardChoice(PlayerState owner,string prompt,IReadOnlyList<SkillChoiceOption> options,Action<int> apply)
        { Owner=owner;Prompt=prompt;Candidates=Array.Empty<Hand>();Options=options;_applyOption=apply; }
        public bool ChooseOption(int value)
        {
            foreach(var option in Options) if(option.Value==value) {_applyOption?.Invoke(value);return true;}
            return false;
        }
        public WizardChoice(PlayerState owner, string prompt, IReadOnlyList<Hand> candidates,
                            bool allowDecline, Action<Hand> apply, bool targetsPlayer=false, string declineLabel="结束选择 / 不发动")
        { Owner=owner; Prompt=prompt; Candidates=candidates; AllowDecline=allowDecline; _apply=apply;
          TargetsPlayer=targetsPlayer;DeclineLabel=declineLabel; }
        public bool Choose(Hand hand)
        {
            if(Options.Count>0) return false;
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
