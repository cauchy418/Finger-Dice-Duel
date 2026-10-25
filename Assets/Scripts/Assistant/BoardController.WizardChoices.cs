using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class BoardController
    {
        private readonly Queue<WizardChoice> _wizardChoices=new Queue<WizardChoice>();
        private readonly Queue<Action> _wizardAttacks=new Queue<Action>();
        private Action _wizardBoundaryResume;
        private Action _wizardAttackContinue;
        public WizardChoice PendingWizardChoice { get; private set; }

        public IReadOnlyList<SkillChoiceOption> PendingSkillOptions => PendingWizardChoice?.Options ?? PendingRequest?.Options ?? Array.Empty<SkillChoiceOption>();
        public bool ChooseSkillOption(int value)
        {
            if(Phase==BoardPhase.ChoosingWizardChoice && PendingWizardChoice!=null)
            {
                if(!PendingWizardChoice.ChooseOption(value)) return false;
                PendingWizardChoice=null;PumpWizardBoundary();return true;
            }
            var request=PendingRequest;
            if(!request.HasValue || !request.Value.ChooseOption(value)) return false;
            ApplyPreTurnChoiceAndContinue(null);return true;
        }

        private bool QueueWizardChoice(WizardChoice request)
        {
            if(request?.Owner==null || GetAi(request.Owner)!=null) return false;
            _wizardChoices.Enqueue(request); return true;
        }
        private bool QueueWizardAttack(AttackContext attack, Action resolve)
        {
            // 独立攻击要先收集防守方选择，不能从同步结算钩子直接询问人类。
            _wizardAttacks.Enqueue(()=>PrepareWizardAttack(attack,resolve)); return true;
        }
        private bool TryStartWizardBoundary(Action resume)
        {
            if(_wizardChoices.Count==0 && _wizardAttacks.Count==0) return false;
            _wizardBoundaryResume=resume; PumpWizardBoundary(); return true;
        }
        private void PumpWizardBoundary()
        {
            FlushSkillLog();
            if(_game.IsGameOver)
            {
                _wizardChoices.Clear();_wizardAttacks.Clear();PendingWizardChoice=null;
                _wizardBoundaryResume=null;Phase=BoardPhase.GameOver;Hint=WinMessage();return;
            }
            while(_wizardChoices.Count>0)
            {
                var next=_wizardChoices.Dequeue();
                if(!next.Owner.IsAlive) continue;
                PendingWizardChoice=next;Phase=BoardPhase.ChoosingWizardChoice;Hint=next.Prompt;
                _pendingChoiceCandidates.Clear();_pendingChoiceCandidates.AddRange(next.Candidates);return;
            }
            PendingWizardChoice=null;
            if(_wizardAttacks.Count>0) {_wizardAttacks.Dequeue()();return;}
            _pendingChoiceCandidates.Clear();
            var resume=_wizardBoundaryResume;_wizardBoundaryResume=null;resume?.Invoke();
        }
        public bool ChooseWizardHand(Hand hand)
        {
            if(Phase!=BoardPhase.ChoosingWizardChoice || PendingWizardChoice==null) return false;
            var choice=PendingWizardChoice;
            if(!choice.Choose(hand)) return false;
            PendingWizardChoice=null;PumpWizardBoundary();return true;
        }
        public string WizardHandLabel(Hand hand)
        {
            foreach(var player in _game.Players)
                if(player.Owns(hand)) return PendingWizardChoice?.TargetsPlayer==true ? player.Name
                    : player.Name+" · "+CombatText.HandName(player,hand)+" "+hand.Value;
            return "选择";
        }
        public bool ChooseResistanceType(AttackDamageType type)
        {
            if(PendingEffect!=HandEffect.SchoolOfAbjurationResistanceType) return false;
            bool valid=false;foreach(var t in ResistanceNames.Choices) if(t==type) valid=true;
            var abj=CharacterBehaviors.FirstImplementer<IAbjurationSkill>(ActivePlayer);
            if(!valid || abj==null) return false;
            abj.SelectedResistance=type;ApplyPreTurnChoiceAndContinue(null);return true;
        }

        // ---------- 战士·战斗大师：战斗招式（与防护抗性同一模式：答案存角色侧） ----------

        /// <summary>当前待答的战斗招式可选项（无则空数组；供 UI 渲染按钮）。</summary>
        public StatusEffect[] BattleMasterManeuverOptions
        {
            get
            {
                if(PendingEffect!=HandEffect.BattleMasterManeuver) return new StatusEffect[0];
                var master=CharacterBehaviors.FirstImplementer<IBattleMasterManeuverChoice>(
                    _pendingChoiceDecider ?? ActivePlayer);
                return master?.ManeuverOptions ?? new StatusEffect[0];
            }
        }

        /// <summary>玩家在战斗招式面板选定一种状态后由 UI 调用。</summary>
        public bool ChooseBattleMasterManeuver(StatusEffect status)
        {
            if(PendingEffect!=HandEffect.BattleMasterManeuver) return false;
            var master=CharacterBehaviors.FirstImplementer<IBattleMasterManeuverChoice>(
                _pendingChoiceDecider ?? ActivePlayer);
            if(master==null) return false;
            bool valid=false;
            foreach(var option in master.ManeuverOptions) if(option==status) valid=true;
            if(!valid) return false;
            master.SelectManeuver(status);
            ApplyPreTurnChoiceAndContinue(null);
            return true;
        }

        /// <summary>玩家在战斗招式面板点「不发动」（与"没问过"区分，结算不再走 AI 默认）。</summary>
        public bool DeclineBattleMasterManeuver()
        {
            if(PendingEffect!=HandEffect.BattleMasterManeuver) return false;
            var master=CharacterBehaviors.FirstImplementer<IBattleMasterManeuverChoice>(
                _pendingChoiceDecider ?? ActivePlayer);
            master?.DeclineManeuver();
            ApplyPreTurnChoiceAndContinue(null);
            return true;
        }
        private bool ConfirmStolenAttack()
        {
            var actor=ActivePlayer;var victim=TargetPlayer??OwnerOf(_selectedTarget);
            if(victim==null || !CanBeAttacked(victim)) return false;
            var attack=new AttackContext(actor,victim,_game.PendingStealAttackHand,_game.PendingStealAttackValue);
            _wizardBoundaryResume=BeginTurn;
            PrepareWizardAttack(attack,()=> {
                LastTurn=_game.ResolvePendingStealAttack(actor,victim,attack);
                LastTurnNumber=_game.TurnCount;_statusTurnEndFor=actor;
                foreach(var line in DescribeTurnLines(LastTurn)) _log.Add(line);
            });
            return true;
        }

        private void PrepareWizardAttack(AttackContext attack, Action resolve)
        {
            if(!attack.Attacker.IsAlive || !attack.Defender.IsAlive) {PumpWizardBoundary();return;}
            _pendingRequests.Clear();_pendingRequestIndex=0;_pendingChoiceCandidates.Clear();
            _preTargetChoiceActive=false;_returnToTargetAfterChoice=false;
            var context=new PreTurnChoiceContext(attack.Attacker,attack.Defender,attack.SourceHand,attack.Defender.LeftHand)
            { Manager=_game,IsResolvingAttack=true,ResultValue=attack.RawDamage };
            foreach(var p in _game.Players)
                if(p.IsAlive && !ReferenceEquals(p,attack.Attacker) && p.Character is IPreTurnChoiceProvider provider)
                    provider.CollectPreTurnChoices(context,_pendingRequests);
            CollectDevotionProtection(context);
            CollectTheArchfeyCancelChoices(attack.AttackValue,attack.Attacker);
            var shields=new List<Hand>();foreach(var hand in attack.Defender.Hands) if(hand.IsShield) shields.Add(hand);
            if(shields.Count>1) _pendingRequests.Add(new ChoiceRequest(HandEffect.ShieldBlock,
                "选择一面盾抵挡。",shields,allowDecline:false,decider:attack.Defender));
            _pendingRequests.RemoveAll(r=>(r.Candidates==null || r.Candidates.Count==0) && r.Options.Count==0);
            _wizardAttackContinue=()=>
            {
                attack.AttackerProvider=_game.GetChoiceProvider(attack.Attacker);
                attack.AnsweredLookupFor=_game.AnsweredLookupFor;
                attack.AskedCancelLookup=_game.AskedCancelLookup;
                resolve();
            };
            AdvanceCharacterChoice();
        }
    }
}
