using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>德鲁伊·星辰结社：双手和 10 时进入星耀形态。</summary>
    public sealed class CircleOfTheStarsLogic : CharacterTemplate,
        IAttackTypeProvider, ITurnStartHook
    {
        public enum StarryForm { None, Archer, Chalice, Dragon }
        public StarryForm Form { get; private set; } = StarryForm.None;

        public override string DisplayName => "德鲁伊·星辰结社";
        public override int BaseAttackRange => 3;

        public bool TryEnter(StarryForm form, PlayerState owner, GameManager manager)
        {
            if (Form != StarryForm.None || form == StarryForm.None || owner == null) return false;
            if (owner.LeftHand.Value + owner.RightHand.Value != 10) return false;
            Form = form;
            manager?.NoteSkill(owner, "星耀形态", form.ToString());
            return true;
        }

        public bool TryExit(PlayerState owner, GameManager manager)
        {
            if (Form == StarryForm.None || owner == null) return false;
            if (owner.LeftHand.Value != owner.RightHand.Value) return false;
            Form = StarryForm.None;
            manager?.NoteSkill(owner, "解除星耀", "恢复基础形态");
            return true;
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager) => Form = StarryForm.None;

        public void OnTurnStarted(PlayerState owner, GameManager manager)
        {
            if (!ReferenceEquals(manager?.CurrentPlayer, owner)) return;
            if (Form != StarryForm.None) return;
            if (owner.LeftHand.Value + owner.RightHand.Value == 10)
                TryEnter(StarryForm.Archer, owner, manager); // 无 UI 时的默认形态
        }

        public override int AttackBonus
        {
            get
            {
                if (Form == StarryForm.Archer) return 2;
                if (Form == StarryForm.Dragon) return 1;
                return 0;
            }
        }

        public override int DefenseBonus
        {
            get
            {
                if (Form == StarryForm.Archer) return -1;
                if (Form == StarryForm.Dragon) return 1;
                return 0;
            }
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            if (Form == StarryForm.Archer) return AttackDamageType.Radiant;
            int distance = context?.Manager?.DistanceBetween(context.Attacker, context.Defender) ?? 1;
            return distance <= 1 ? AttackDamageType.Bludgeoning : AttackDamageType.Piercing;
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
                                                  int actedHandValueBefore, int targetValue,
                                                  HandEffectType effect, GameManager manager,
                                                  IPreTurnChoiceSink choiceSink)
        {
            if (Form != StarryForm.Chalice || effect != HandEffectType.Heal
                || actedHand == null || actedHand.Value != 9 || manager == null) return;

            PlayerState extra = null;
            for (int i = 0; i < manager.Players.Count; i++)
            {
                PlayerState p = manager.Players[i];
                if (p == null || !p.IsAlive || ReferenceEquals(p, owner)) continue;
                if (extra == null || p.Health < extra.Health) extra = p;
            }
            if (extra == null) return;
            extra.Heal(9);
            manager.NoteSkill(owner, "圣杯座", $"额外回复 {extra.Name} 9 点");
        }

        /// <summary>巨龙座：需要第二次加手；具体第二次被加数由上层选择。</summary>
        public bool NeedsSecondAddend => Form == StarryForm.Dragon;
    }
}
