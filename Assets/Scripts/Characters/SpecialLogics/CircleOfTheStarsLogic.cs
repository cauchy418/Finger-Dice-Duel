using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>德鲁伊·星辰结社：双手和 10 时进入星耀形态。</summary>
    public sealed class CircleOfTheStarsLogic : CharacterTemplate,
        IAttackTypeProvider, ITurnStartHook, ITurnEndHook, IDruidForm, IAiMoveAdvisor, IStatusApplyingAttack
    {
        public enum StarryForm { None, Archer, Chalice, Dragon }
        public StarryForm Form { get; private set; } = StarryForm.None;

        private int _lastOfferTurn=-1;
        public string CircleKey => "stars";
        public int FormValue => (int)Form;
        public override string DisplayName => "德鲁伊·星辰结社";
        public override int BaseAttackRange => 3;

        public bool TryEnter(StarryForm form, PlayerState owner, GameManager manager)
        {
            if (Form != StarryForm.None || !System.Enum.IsDefined(typeof(StarryForm),form) || form == StarryForm.None || owner == null) return false;
            if (owner.LeftHand.Value + owner.RightHand.Value != 10) return false;
            Form = form;
            manager?.NoteSkill(owner, "星耀形态", DruidForms.Name(CircleKey,(int)form));
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

        public override void OnMatchReset(PlayerState owner, GameManager manager) {Form = StarryForm.None;_lastOfferTurn=-1;}

        public void OnTurnStarted(PlayerState owner, GameManager manager)
        {
            if (!ReferenceEquals(manager?.CurrentPlayer, owner)) return;
            if(Form!=StarryForm.None || _lastOfferTurn==manager.TurnCount)return;
            if(owner.LeftHand.Value+owner.RightHand.Value!=10 || !owner.IsAlive)return;
            _lastOfferTurn=manager.TurnCount;
            DruidForms.OfferEntry(owner,manager,CircleKey,value=>{if(value!=0)TryEnter((StarryForm)value,owner,manager);});
        }

        public void OnTurnEnded(PlayerState owner,GameManager manager)
            => DruidForms.OfferExit(owner,manager,this,()=>TryExit(owner,manager));
        public StatusEffect StatusToApplyOnHit(AttackContext context)
            => StatusNames.StatusOf(AttackDamageTypes.ToDamageType(context.AttackType));

        public override int ScoreMoveBonus(MoveValueContext context)
        {
            if(FormValue==0)return context.ActedValueAfter+context.OtherHandValue==10?80:0;
            if(Form==StarryForm.Chalice&&context.ActedValueAfter==9)return 45;
            return 0;
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

        public override void OnResolveAttack(AttackContext context)
        {if(FormValue!=0 && context.AttackType==AttackDamageType.None)context.AttackType=AttackTypeOf(context);}

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

            var targets=new System.Collections.Generic.List<PlayerState>();var hands=new System.Collections.Generic.List<Hand>();
            foreach(var p in manager.Players) if(p.IsAlive && !ReferenceEquals(p,owner)){targets.Add(p);hands.Add(p.LeftHand);}
            if(targets.Count==0)return;
            var request=new WizardChoice(owner,"圣杯 · 选择额外回复目标（9点）",hands,true,hand=>{
                var target=manager.OwnerOfHand(hand);if(target==null||!target.IsAlive||ReferenceEquals(target,owner))return;
                target.Heal(9);manager.NoteSkill(owner,"圣杯座",$"额外回复 {target.Name} 9 点");
            },targetsPlayer:true,declineLabel:"不额外回复");
            if(manager.DeferWizardChoice?.Invoke(request)==true)return;
            var extra=manager.GetDruidPolicy(owner)?.ChaliceTarget(owner,targets,manager);
            request.Choose(extra?.LeftHand);
        }

        /// <summary>巨龙座：需要第二次加手；具体第二次被加数由上层选择。</summary>
        public bool NeedsSecondAddend => Form == StarryForm.Dragon;
    }
}
