using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>德鲁伊·月亮结社：双手和 10 时进入荒野形态。</summary>
    public sealed class CircleOfTheMoonLogic : CharacterTemplate,
        IHandEffectRewriter, ISelectiveHandEffectRewriter, IHandEffectDisplay,
        IAttackTypeProvider, IOnDamageLanded, ITurnStartHook, ITurnEndHook, IDruidForm, IAiMoveAdvisor, IStatusApplyingAttack
    {
        public enum WildShape { None, Bear, Wolf, Eagle, Snake }
        public WildShape Form { get; private set; } = WildShape.None;

        private int _lastOfferTurn=-1;
        public string CircleKey => "moon";
        public int FormValue => (int)Form;
        public bool NeedsSecondAddend => false;
        public override string DisplayName => "德鲁伊·月亮结社";
        public override int BaseAttackRange => Form == WildShape.Eagle ? 3 : 1;

        public bool TryEnter(WildShape form, PlayerState owner, GameManager manager)
        {
            if (Form != WildShape.None || !System.Enum.IsDefined(typeof(WildShape),form) || form == WildShape.None || owner == null) return false;
            if (owner.LeftHand.Value + owner.RightHand.Value != 10) return false;
            Form = form;
            manager?.NoteSkill(owner, "荒野形态", DruidForms.Name(CircleKey,(int)form),owner,new SkillVisualData(variant:"moon_"+form.ToString().ToLowerInvariant()));
            return true;
        }

        public bool TryExit(PlayerState owner, GameManager manager)
        {
            if (Form == WildShape.None || owner == null) return false;
            if (owner.LeftHand.Value != owner.RightHand.Value) return false;
            var previous=Form;Form = WildShape.None;
            manager?.NoteSkill(owner, "解除荒野", "恢复基础形态",owner,new SkillVisualData(value:0,variant:"moon_"+previous.ToString().ToLowerInvariant()));
            return true;
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager) {Form = WildShape.None;_lastOfferTurn=-1;}

        public void OnTurnStarted(PlayerState owner, GameManager manager)
        {
            if (!ReferenceEquals(manager?.CurrentPlayer, owner)) return;
            if(Form!=WildShape.None || _lastOfferTurn==manager.TurnCount)return;
            if(owner.LeftHand.Value+owner.RightHand.Value!=10 || !owner.IsAlive)return;
            _lastOfferTurn=manager.TurnCount;
            DruidForms.OfferEntry(owner,manager,CircleKey,value=>{if(value!=0)TryEnter((WildShape)value,owner,manager);});
        }

        public void OnTurnEnded(PlayerState owner,GameManager manager)
            => DruidForms.OfferExit(owner,manager,this,()=>TryExit(owner,manager));
        public StatusEffect StatusToApplyOnHit(AttackContext context)
            => StatusNames.StatusOf(AttackDamageTypes.ToDamageType(context.AttackType));

        public override int ScoreMoveBonus(MoveValueContext context)
        {
            if(FormValue==0)return context.ActedValueAfter+context.OtherHandValue==10?80:0;
            if(Form==WildShape.Eagle&&context.ActedValueAfter==6)return -100;
            if(Form==WildShape.Bear&&context.ActedValueAfter==4)return 15;
            return 0;
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

        public override void OnResolveAttack(AttackContext context)
        {if(FormValue!=0 && context.AttackType==AttackDamageType.None)context.AttackType=AttackTypeOf(context);}

        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            if (Form == WildShape.Eagle) return AttackDamageType.Piercing;
            if (Form == WildShape.Snake) return AttackDamageType.Poison;
            return AttackDamageType.Bludgeoning;
        }

        public void OnDamageLanded(AttackContext context, GameManager manager)
        {
            if (Form != WildShape.Wolf || context == null || context.AppliedDamage <= 0) return;
            int heal = context.AppliedDamage / 2;
            if (heal > 0) context.Attacker?.Heal(heal);
        }
    }
}
