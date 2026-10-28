using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>游侠·猎人：7 标记、标记目标无视正防御并追加已损体力/5。</summary>
    public sealed class RangerHunterLogic : CharacterTemplate, IAttackTypeProvider, IHunterMarkSource, IAttackFinishedHook
    {
        private readonly HashSet<PlayerState> _marked = new HashSet<PlayerState>();
        private PlayerState _owner;

        public override string DisplayName => "游侠·猎人";
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => 3;

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _owner = owner;
            _marked.Clear();
        }

        public bool IsMarked(PlayerState target) => target != null && _marked.Contains(target);

        public void Mark(PlayerState target)
        {
            if (target == null || !target.IsAlive) return;
            _marked.Add(target);
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
                                                  int actedHandValueBefore, int targetValue,
                                                  HandEffectType effect, GameManager manager,
                                                  IPreTurnChoiceSink choiceSink)
        {
            if (owner == null || actedHand == null || actedHand.Value != 7) return;
            var targets=new List<PlayerState>();var hands=new List<Hand>();
            foreach(var p in manager.Players)
                if(p.IsAlive && !ReferenceEquals(p,owner) && manager.DistanceBetween(owner,p)<=3)
                {targets.Add(p);hands.Add(p.LeftHand);}
            if(targets.Count==0) return;
            var request=new WizardChoice(owner,"猎人印记 · 选择射程3内的角色",hands,true,hand=> {
                var target=manager.OwnerOfHand(hand);
                if(target==null || !target.IsAlive || ReferenceEquals(target,owner) || manager.DistanceBetween(owner,target)>3) return;
                Mark(target);manager.NoteSkill(owner,"猎人印记",$"{target.Name} 被标记");
            },targetsPlayer:true,declineLabel:"不标记");
            if(manager.DeferWizardChoice?.Invoke(request)==true) return;
            var policy=manager.GetSkillPolicy(owner);
            var pick=policy!=null?policy.HunterMarkTarget(owner,targets,manager):targets[0];
            request.Choose(pick?.LeftHand);
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context?.Defender == null || !_marked.Contains(context.Defender)) return;
            context.IgnorePositiveDefense = true;
            int lost = context.Defender.MaximumHealth - context.Defender.Health;
            if (lost > 0) context.FinalDamage += lost / 5;
            _marked.Remove(context.Defender);
            context.Manager?.NoteSkill(context.Attacker, "巨像屠夫", $"消耗 {context.Defender.Name} 的标记，已损体力/5 加伤");
        }

        public void OnAttackFinished(PlayerState owner, AttackContext attack, bool initiated, GameManager game)
        {
            if (!initiated || attack.IsHunterVolley || attack.IsSpecialAttack || attack.IsReflected
                || attack.IsStatusDamage || attack.WasCancelled || !owner.IsAlive || game == null) return;
            var used = new HashSet<PlayerState> { attack.TargetAtStart, attack.Defender };
            OfferVolley(owner, attack, game, used);
        }

        private void OfferVolley(PlayerState owner, AttackContext original, GameManager game, HashSet<PlayerState> used)
        {
            if (!owner.IsAlive || owner.IsPolymorphed || game.IsGameOver) return;
            var targets = new List<PlayerState>();
            var hands = new List<Hand>();
            foreach (var target in game.Players)
                if (target.IsAlive && !ReferenceEquals(target, owner) && !used.Contains(target)
                    && IsMarked(target) && game.CanReach(owner, target, game.RangeOverride)
                    && StatusRules.CanAttackTarget(owner, target, game)
                    && !((target.Character as IAttackTargetRule)?.IsImmuneToAttack(target) ?? false))
                { targets.Add(target); hands.Add(target.LeftHand); }
            if (targets.Count == 0) return;
            var request = new WizardChoice(owner, "灭族者 · 选择额外攻击的标记目标，或结束追猎", hands, true, hand =>
            {
                var target = game.OwnerOfHand(hand);
                if (target == null || !targets.Contains(target) || !target.IsAlive || !IsMarked(target)) return;
                used.Add(target);
                var extra = new AttackContext(owner, target, original.SourceHand, original.RawDamage)
                {
                    IsHunterVolley = true, AttackValueOverride = original.AttackValue,
                    SourceHandValueBefore = original.SourceHandValueBefore,
                    TargetHandValueBefore = original.TargetHandValueBefore,
                    SecondAddendValueBefore = original.SecondAddendValueBefore,
                    DamageMultiplier = original.DamageMultiplier,
                    AttackerProvider = game.GetChoiceProvider(owner),
                    AttackerChoiceSink = game.GetChoiceProvider(owner) as IPreTurnChoiceSink,
                    LethalLoyalistSacrifice = original.LethalLoyalistSacrifice,
                };
                System.Action resolve = () =>
                {
                    extra.Resolve(game.GetChoiceProvider(target), game);
                    game.NoteSkill(owner, "灭族者", target.Name + "：" + AttackDamageTypes.NameOf(extra.AttackType)
                        + " " + extra.AppliedDamage + "伤害" + (extra.WasCancelled ? "，取消" : extra.WasDodged ? "，闪避" : extra.WasBlocked ? "，格挡" : ""));
                    game.RefreshGameOver();
                    OfferVolley(owner, original, game, used);
                };
                if (game.DeferWizardAttack?.Invoke(extra, resolve) != true) resolve();
            }, targetsPlayer: true, declineLabel: "结束追猎");
            if (game.DeferWizardChoice?.Invoke(request) == true) return;
            var policy = game.GetSkillPolicy(owner) as IHunterVolleyPolicy;
            PlayerState pick = null;
            if (policy != null) pick = policy.HunterVolleyTarget(owner, targets, game);
            else foreach (var target in targets) if (!game.IsKnownAlly(owner, target)) { pick = target; break; }
            request.Choose(pick?.LeftHand);
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Piercing;
    }
}
