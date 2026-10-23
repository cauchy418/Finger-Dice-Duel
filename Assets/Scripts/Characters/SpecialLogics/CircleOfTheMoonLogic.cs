using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>德鲁伊·月亮结社：双手和 10 时进入荒野形态。</summary>
    public sealed class CircleOfTheMoonLogic : CharacterTemplate,
        IHandEffectRewriter, ISelectiveHandEffectRewriter, IHandEffectDisplay,
        IAttackTypeProvider, IOnHitLanded, ITurnStartHook
    {
        public enum WildShape { None, Bear, Wolf, Eagle, Snake }
        public WildShape Form { get; private set; } = WildShape.None;

        public override string DisplayName => "德鲁伊·月亮结社";
        public override int BaseAttackRange => Form == WildShape.Eagle ? 3 : 1;

        public bool TryEnter(WildShape form, PlayerState owner, GameManager manager)
        {
            if (Form != WildShape.None || form == WildShape.None || owner == null) return false;
            if (owner.LeftHand.Value + owner.RightHand.Value != 10) return false;
            Form = form;
            manager?.NoteSkill(owner, "荒野形态", form.ToString());
            return true;
        }

        public bool TryExit(PlayerState owner, GameManager manager)
        {
            if (Form == WildShape.None || owner == null) return false;
            if (owner.LeftHand.Value != owner.RightHand.Value) return false;
            Form = WildShape.None;
            manager?.NoteSkill(owner, "解除荒野", "恢复基础形态");
            return true;
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager) => Form = WildShape.None;

        public void OnTurnStarted(PlayerState owner, GameManager manager)
        {
            if (!ReferenceEquals(manager?.CurrentPlayer, owner)) return;
            if (Form != WildShape.None) return;
            if (owner.LeftHand.Value + owner.RightHand.Value == 10)
                TryEnter(WildShape.Bear, owner, manager); // 无 UI 时的默认形态
        }

        public override int AttackBonus
        {
            get
            {
                switch (Form)
                {
                    case WildShape.Bear: return -1;
                    case WildShape.Wolf: return 2;
                    case WildShape.Eagle: return 1;
                    default: return 0;
                }
            }
        }

        public override int DefenseBonus
        {
            get
            {
                switch (Form)
                {
                    case WildShape.Bear: return 2;
                    case WildShape.Wolf: return -1;
                    case WildShape.Eagle: return -1;
                    default: return 0;
                }
            }
        }

        public bool HandlesResult(int value)
            => (Form == WildShape.Bear && value == 4) || (Form == WildShape.Eagle && value == 6);

        public HandEffectType RewriteEffect(PlayerState actor, Hand actedHand, int targetValue, GameManager manager)
        {
            if (Form == WildShape.Bear && actedHand?.Value == 4) return HandEffectType.Shield;
            if (Form == WildShape.Eagle && actedHand?.Value == 6) return HandEffectType.None;
            return Hand.GetEffectType(actedHand?.Value ?? 0);
        }

        public HandEffectType DisplayedEffectOf(int resultValue)
        {
            if (Form == WildShape.Bear && resultValue == 4) return HandEffectType.Shield;
            if (Form == WildShape.Eagle && resultValue == 6) return HandEffectType.None;
            return Hand.GetEffectType(resultValue);
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            if (Form == WildShape.Eagle) return AttackDamageType.Piercing;
            if (Form == WildShape.Snake) return AttackDamageType.Poison;
            return AttackDamageType.Bludgeoning;
        }

        public void OnHitLanded(AttackContext context, GameManager manager)
        {
            if (Form != WildShape.Wolf || context == null || context.AppliedDamage <= 0) return;
            int heal = context.AppliedDamage / 2;
            if (heal > 0) context.Attacker?.Heal(heal);
        }
    }
}
