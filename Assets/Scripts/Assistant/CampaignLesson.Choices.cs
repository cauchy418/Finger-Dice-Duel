using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class CampaignLesson
    {
        private Hand Required(out bool handled,out bool person)
        {
            handled=true;person=false;var s=Step;if(!Guided||s==null){handled=false;return null;}
            var q=_board.PendingWizardChoice;
            if(q!=null&&q.Options.Count==0)
            {
                person=q.TargetsPlayer;
                if(person)
                {
                    if(q.Prompt.Contains("仇敌誓言"))return Seats[s.VowSeat].LeftHand;
                    if(q.Prompt.Contains("自然之怒"))return s.TargetSeat>=0?Seats[s.TargetSeat].LeftHand:null;
                    if(q.Prompt.Contains("不灭哨卫"))return Seats[s.TargetSeat>=0?s.TargetSeat:3].LeftHand;
                    if(q.Prompt.Contains("免费")||q.Prompt.Contains("惧怖收割"))return s.DeclineNecromancy?null:Seats[s.TargetSeat>=0?s.TargetSeat:GuardianSeat].LeftHand;
                    return s.TargetSeat>=0?Seats[s.TargetSeat].LeftHand:null;
                }
                if(q.Prompt.Contains("死者")||q.Prompt.Contains("自己的手"))
                {
                    Hand h=q.Prompt.Contains("死者")?(s.SwapDead>=0?Seats[1].GetHand(s.SwapDead):null):(s.SwapOwn>=0?_player.GetHand(s.SwapOwn):null);
                    if(h==null||!Contains(q.Candidates,h))return null;return h;
                }
                if(q.Prompt.Contains("复仇之魂"))return q.Prompt.Contains("加数")?Seats[3].LeftHand:_player.RightHand;
                handled=false;return null;
            }
            switch(_board.PendingEffect)
            {
                case HandEffect.SchoolOfAbjurationResistanceTarget:person=true;return Seats[s.TargetSeat>=0?s.TargetSeat:0].LeftHand;
                case HandEffect.SchoolOfNecromancyBlock:
                    if(s.DeclineDefense)return null;foreach(var h in _player.Hands)if(h.Value==2)return h;return null;
                case HandEffect.OathOfDevotionRedirect:person=true;return s.Redirect?_player.LeftHand:null;
                case HandEffect.OathOfDevotionShockTarget:person=true;return Seats[s.ShockSeat].LeftHand;
                case HandEffect.OathbreakerTransfer:person=true;return s.TargetSeat>=0?Seats[s.TargetSeat].LeftHand:null;
                case HandEffect.BreakerChainBreak:case HandEffect.BreakerRangeChainBreak:
                    if(s.ChainSeat<0)return null;foreach(var h in Seats[s.ChainSeat].Hands)if(h.IsShield)return h;return null;
                case HandEffect.BreakerRangeBreak:
                    if(!s.Charge)return null;foreach(var h in _player.Hands)if(h.IsShield&&!ReferenceEquals(h,_board.SelectedHand))return h;return null;
            }
            if(_board.Phase==BoardPhase.ChoosingSeatSwapTarget){person=true;return Seats[3].LeftHand;}
            if(_board.Phase==BoardPhase.ChoosingDefenseShield)
            {foreach(var h in (_board.Chooser??_player).Hands)if(h.IsShield)return h;return null;}
            // Optional extra strikes require an explicit teaching target; otherwise decline.
            if(_board.PendingRequest.HasValue)
            {
                if(_board.PendingEffect.ToString().Contains("Swordsman")){person=true;return s.TargetSeat>=0?Seats[s.TargetSeat].LeftHand:null;}
                return null;
            }
            handled=false;return null;
        }
        private static bool Contains(IReadOnlyList<Hand> hands,Hand value)
        {foreach(var h in hands)if(ReferenceEquals(h,value))return true;return false;}
        public int? RequiredOption
        {
            get
            {
                if(!Guided||Step==null||_board.PendingSkillOptions.Count==0)return null;
                var prompt=_board.PendingWizardChoice?.Prompt??"";
                if(prompt.Contains("幻术默发"))return Step.IllusionTouch??0;
                if(prompt.Contains("幻影实体"))return Step.IllusionDefense??0;
                if(prompt.Contains("预知未来"))return Step.Portent??-1;
                if(prompt.Contains("催眠凝视"))return Step.Enchant.HasValue?(int)Step.Enchant.Value:0;
                if(_board.PendingEffect==HandEffect.VengeanceRetarget)return Step.Option??0;
                if(_board.PendingEffect==HandEffect.SchoolOfIllusionDefense)return Step.IllusionDefense??0;
                return Step.Option??(_board.PendingSkillOptions[0].Value==0?0:DefaultOption());
            }
        }
        private int DefaultOption()
        {foreach(var o in _board.PendingSkillOptions)if(o.Value==0)return 0;foreach(var o in _board.PendingSkillOptions)if(o.Value==-1)return -1;return _board.PendingSkillOptions[0].Value;}
        public bool AllowsHand(Hand hand)
        {
            if(!Guided)return Stage!=CampaignPhase.AwaitingHorn||_board.PendingWizardChoice!=null||_board.PendingRequest.HasValue;
            var required=Required(out bool handled,out bool person);
            if(handled)return required!=null&&hand!=null&&(person?ReferenceEquals(_game.OwnerOfHand(required),_game.OwnerOfHand(hand)):ReferenceEquals(required,hand));
            if(Step==null||Step.Actor!=0||Step.Kind!=CampaignActionKind.Operation||!ReadyOperation)return false;
            if(_board.PendingSkillOptions.Count>0)return false;
            if(_board.PendingEffect==HandEffect.SchoolOfEvocationAbandonAttack||_board.PendingEffect==HandEffect.SchoolOfAbjurationBarrier)return AllowsAccept;
            switch(_board.Phase)
            {
                case BoardPhase.ChoosingAttackerHand:return ReferenceEquals(hand,_player.GetHand(Step.Source));
                case BoardPhase.ChoosingTargetHand:return ReferenceEquals(hand,Seats[Step.AddendOwner].GetHand(Step.Addend));
                case BoardPhase.ChoosingTargetPlayer:return ReferenceEquals(_game.OwnerOfHand(hand),Seats[Step.Victim]);
                default:return true;
            }
        }
        public bool AllowsOption(int value)=>!Guided||!RequiredOption.HasValue||value==RequiredOption.Value;
        public bool AllowsDamageType(DamageType type)=>!Guided||!Step.Element.HasValue||type==Step.Element.Value;
        public bool AllowsResistance(AttackDamageType type)=>!Guided||type==(Step.Resistance??AttackDamageType.Bludgeoning);
        public bool AllowsAccept
        {
            get
            {
                if(!Guided)return true;
                if(_board.PendingEffect==HandEffect.SchoolOfEvocationAbandonAttack)return Step?.Abandon==true;
                if(_board.PendingEffect==HandEffect.SchoolOfAbjurationBarrier)return Step?.StoreWard==true;
                var hand=Required(out bool handled,out _);return handled?hand!=null:true;
            }
        }
        public bool AllowsDecline
        {
            get
            {
                if(!Guided)return true;
                if(_board.Phase==BoardPhase.ChoosingSeatSwapTarget)return true;
                if(_board.PendingEffect==HandEffect.SchoolOfEvocationAbandonAttack)return Step?.Abandon!=true;
                if(_board.PendingEffect==HandEffect.SchoolOfAbjurationBarrier)return Step?.StoreWard!=true;
                var hand=Required(out bool handled,out _);return handled?hand==null:_board.PendingSkillOptions.Count==0;
            }
        }
        public bool AllowsSkip=>!Guided||!StatusRules.CanAct(_board.ActivePlayer);
        public bool AllowsCastling=>!Guided||Step?.Kind==CampaignActionKind.Castle;
        public override bool AllowAcceptChoice=>AllowsAccept;
        public override bool AllowDeclineChoice=>AllowsDecline;
        public override bool AllowClick(Hand hand)=>AllowsHand(hand);
        public override bool AllowManeuver(StatusEffect status)=>!Guided||status==(Step?.Maneuver??StatusEffect.None);
        public override Hand SuggestedHand
        {
            get
            {
                if(!Guided||!_board.IsHumanChooser)return null;
                var h=Required(out bool handled,out _);if(handled)return h;
                if(Step?.Actor!=0||!ReadyOperation)return null;
                if(_board.Phase==BoardPhase.ChoosingAttackerHand)return _player.GetHand(Step.Source);
                if(_board.Phase==BoardPhase.ChoosingTargetHand)return Seats[Step.AddendOwner].GetHand(Step.Addend);
                if(_board.Phase==BoardPhase.ChoosingTargetPlayer)return Seats[Step.Victim].LeftHand;
                return null;
            }
        }
        public override string RejectionHint=>"请完成本步高亮选择；需要等待时使用右侧【观摩】。";
        private sealed class CampaignMoves:AbstractChoiceProvider,IMoveSelector,IAttackTargetIntent,IAncientRoleChoicePolicy,IArcaneChoicePolicy
        {
            private readonly CampaignLesson _lesson;private readonly GreedyChoiceProvider _ai;
            public CampaignMoves(CampaignLesson lesson){_lesson=lesson;_ai=NewAi(lesson.Game);}
            public string Describe=>"操演";
            public PlayerState IntendedVictim=>_lesson.Step!=null&&_lesson.Step.Victim>=0?_lesson.Seats[_lesson.Step.Victim]:null;
            public LegalMove ChooseMove(GameManager game,PlayerState actor)
            {
                var s=_lesson.Step;if(!_lesson.Guided)return _ai.ChooseMove(game,actor);
                if(s==null||s.Kind!=CampaignActionKind.Operation||!ReferenceEquals(actor,_lesson.Seats[s.Actor]))return default;
                var hand=actor.GetHand(s.Source==2?(actor.LeftHand.CanBeOperated?0:1):s.Source);
                return new LegalMove(actor,hand,_lesson.Seats[s.AddendOwner].GetHand(s.Addend));
            }
            public override Hand ChooseHandFor(ChoiceRequest request,PlayerState owner)=>_ai.ChooseHandFor(request,owner);
            public Hand ChooseAncientTarget(string key,PlayerState owner,IReadOnlyList<Hand> candidates,GameManager game)=>_ai.ChooseAncientTarget(key,owner,candidates,game);
            public PlayerState ChooseArcaneTarget(string key,PlayerState owner,IReadOnlyList<PlayerState> candidates,GameManager game)=>_ai.ChooseArcaneTarget(key,owner,candidates,game);
        }
    }
}
