using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>战报只描述已经结算的快照，不从后续手值推测结果。</summary>
    public static class BattleTurnText
    {
        public static IReadOnlyList<string> Lines(TurnResult t)
        {
            var lines = new List<string>();
            if (t?.Actor == null) return lines;
            if (t.ExtraAttack.HasValue)
            {
                var extra = t.ExtraAttack.Value;
                lines.Add(CombatText.Skill(t.Actor, "追加攻击", $"{t.Defender?.Name}：" +
                    (extra.WasBlocked ? "被护盾挡下" : $"造成 {extra.Dealt} 点固定伤害")));
            }
            lines.Add(Describe(t));
            return lines;
        }

        public static string Describe(TurnResult t)
        {
            if (t?.Actor == null) return "";
            string head;
            if (t.StealUsed)
                head = CombatText.Skill(t.Actor, "偷窃", $"{t.StealOriginal}↔{t.StealTaken}");
            else if (t.WasGamble)
                head = CombatText.Skill(t.Actor, "赌博", $"开赌：掷出 {t.GambleRoll}");
            else
            {
                int sum = Hand.ComputeSum(t.ActedHandValueBefore, t.TargetHandValue);
                head = $"{t.Actor.Name} {CombatText.HandName(t.Actor, t.ActedHand)}：{t.ActedHandValueBefore} + {t.TargetHandValue} = {sum}";
                if(t.SecondAddendValue>0){int final=Hand.ComputeSum(sum,t.SecondAddendValue);head+=$"；{sum} + {t.SecondAddendValue} = {final}";sum=final;}
                if (sum != t.ResultValue)
                    head += t.FreezeHalvedFrom > 0 ? $"【冰冻】→{t.ResultValue}" : $" → {t.ResultValue}";
            }
            if (t.ActionInvalidated) return head + " → 预言：仅改变数字，无其它效果";
            string target = t.Defender?.Name ?? "目标";
            if (t.StealUsed && !t.StealEffective) return head + " → 仅交换，不触发效果";
            if (t.HealTarget != null || t.Effect == HandEffectType.Heal)
            {
                var recipient = t.HealTarget ?? t.Actor;
                int actual = t.ActualHealed >= 0 ? t.ActualHealed :
                    (ReferenceEquals(recipient, t.Actor) ? t.Healed : t.HealAmount);
                return head + $" → 回复 {recipient.Name} {actual} 点生命" +
                    (actual == 0 ? "（已满血，回复浪费）" : "");
            }
            if (t.AttackSuppressed) return head + " → 放弃攻击";
            if (t.OutOfRange) return head + " → 超出射程，未攻击";
            switch (t.Effect)
            {
                case HandEffectType.Attack4:
                case HandEffectType.Attack6:
                case HandEffectType.Attack8:
                    string outcome;
                    if (t.WasCancelled) outcome = $"被{t.CancelledBy ?? "邪术师·至高妖精"}取消，造成 0 点伤害";
                    else if (t.HitCount > 1)
                    {
                        var parts = new List<string>();
                        if (t.BlockedHits > 0) parts.Add($"{t.BlockedHits} 段被护盾挡下");
                        if (t.DodgedHits > 0) parts.Add($"{t.DodgedHits} 段被闪避");
                        parts.Add($"共造成 {t.DamageDealt} 点伤害");
                        outcome = (t.HitCount == 2 ? "两段攻击" : $"{t.HitCount} 段攻击") + "，" + string.Join("，", parts);
                    }
                    else if (t.WasDodged) outcome = string.IsNullOrEmpty(t.DodgeReason) ? "被游荡者·风流剑客闪避" : t.DodgeReason;
                    else if (t.WasBlocked) outcome = "被护盾挡下";
                    else outcome = $"造成 {t.DamageDealt} 点伤害";
                    string type = t.AttackType == AttackDamageType.None ? "" : $"（{AttackDamageTypes.NameOf(t.AttackType)}）";
                    return head + $" → 攻击 {target}，{outcome}{type}";
                case HandEffectType.Shield: return head + " → 形成护盾";
                default: return head + (t.EnteredAmbush ? " → 进入伏击" : " → 无效果");
            }
        }
    }
}
