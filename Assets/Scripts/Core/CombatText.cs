namespace FingerDiceDuel.Core
{
    /// <summary>技能战报与触发提示共用格式；只由实际结算处登记发动结果。</summary>
    public static class CombatText
    {
        public static string Skill(PlayerState source, string skill, string detail)
            => $"{source?.Name ?? "角色"}【{skill}】{detail}";

        public static string HandName(PlayerState owner, Hand hand)
            => owner != null && ReferenceEquals(owner.LeftHand, hand) ? "左手" : "右手";

        public static string ChoicePrompt(ChoiceRequest request, PlayerState chooser)
        {
            string skill, action;
            switch (request.Effect)
            {
                case HandEffect.ShieldBlock: skill = "格挡"; action = "选择一面盾抵挡。"; break;
                case HandEffect.PathOfTheWardenLockTarget: skill = "锁手"; action = "合盾后，可锁定对手一只手；盾碎即解除。"; break;
                case HandEffect.BreakerChainBreak: skill = "连锁破盾"; action = "自身盾将破碎，可连带打碎场上一面完整盾。"; break;
                case HandEffect.BreakerRangeBreak: skill = "碎盾换射程"; action = "可打碎另一只手的盾，使本次攻击距离无限。"; break;
                case HandEffect.OathOfDevotionShockTarget:
                    skill = "冲击"; action = (request.Variant == 1 ? "碎盾" : "合盾") + "触发：选择范围内一名角色，固定伤害 2，可格挡或闪避。"; break;
                case HandEffect.OathbreakerTransfer: skill = "转移攻击"; action = "持有5：选择一名持完整盾的非攻击者承受整次攻击，或不发动。"; break;
                case HandEffect.OathOfDevotionRedirect: skill = "代挡"; action = "可替范围内的受击角色承受本次攻击。"; break;
                case HandEffect.WayOfTheTaiChiTaichi: skill = "太极"; action = "可将一只太极手 5→0，减伤一半并反弹其余伤害。"; break;
                case HandEffect.TheArchfeyCancel: skill = "妖精戏法·取消攻击"; action = "可将同攻击数字的手永久＋1，取消本次攻击。"; break;
                case HandEffect.LifeDomainAttackOrHeal: skill = "攻击／回复"; action = "选择照常攻击，或将本次数字用于治疗。"; break;
                case HandEffect.LifeDomainHealTarget: skill = "治疗"; action = "选择一名角色回复生命（可选自己）。"; break;
                case HandEffect.LifeDomainExtraHealTarget: skill = "双九回复"; action = "双手为 9：选择一名角色，额外回复 9。"; break;
                case HandEffect.SwashbucklerDodge: skill = "闪避"; action = "可选一只手 1→2，闪避本段攻击。"; break;
                case HandEffect.SwashbucklerDuelistThrust: skill = "乘风突刺"; action = "可将另一只手 2→3，基础伤害×1.5，本次距离无限。"; break;
                case HandEffect.SwordsmanFollowUp:
                    int value = request.ResultValue > 0 ? request.ResultValue : request.Candidates[0].Value;
                    skill = "追加攻击"; action = $"双 {value}：可对当前目标追加固定 {value} 点伤害，无视攻防。"; break;
                case HandEffect.GloomStalkerAmbush: skill = "伏击"; action = "可暗置本次运算手；强化攻击并抵消下一段伤害。"; break;
                case HandEffect.PathOfTheBeastSurge: skill = "成长"; action = "双手和为 7：回复半数生命上限，永久攻防＋1。"; break;
                case HandEffect.SchoolOfEvocationAbandonAttack: skill = "附魔"; action = "可放弃本次攻击，为下一次攻击选择伤害类型。"; break;
                case HandEffect.SchoolOfEvocationDamageType: skill = "附魔"; action = "选择下一次攻击的伤害类型；新选择覆盖旧选择。"; break;
                default: return request.Prompt;
            }
            return Skill(request.Decider ?? chooser, skill, action);
        }
    }
}
