using System;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class MageTowerLesson : ITutorialInput
    {
        private bool Guided => _level<20;
        public int? RequiredOption
        {
            get
            {
                if(!Guided||_board.PendingSkillOptions.Count==0)return null;
                if(_level==11)return SampleIndex==0?(int)AttackDamageType.Fire:1;
                if(_level==15)return _game.TurnCount<=1?(int)StatusEffect.Disabled:_game.TurnCount==4?(int)StatusEffect.Charm:0;
                if(_level==18)return _board.PendingWizardChoice?.Prompt.Contains("镜像错位")==true?(SampleIndex==0?1:0):SampleIndex==2?1:0;
                return null;
            }
        }
        private bool RequiredHand(out Hand hand,out bool playerTarget)
        {
            hand=null;playerTarget=false;if(!Guided)return false;
            var q=_board.PendingWizardChoice;
            if(q!=null&&q.Options.Count==0)
            {
                playerTarget=q.TargetsPlayer;
                if(_level==16)
                {
                    if(q.TargetsPlayer)hand=_game.TurnCount<=1?_soldier.LeftHand:null;
                    else if(q.Prompt.Contains("选自己的手"))hand=_player.RightHand.Value==3?_player.RightHand:null;
                    else if(q.Prompt.Contains("选择死者"))hand=_soldier.RightHand;
                    return true;
                }
                if(_level==19){hand=SampleIndex==0&&_game.TurnCount==1?_soldier.LeftHand:null;return true;}
            }
            if(_board.PendingEffect==HandEffect.SchoolOfAbjurationResistanceTarget)
            {hand=_player.LeftHand;playerTarget=true;return true;}
            if(_board.PendingEffect==HandEffect.SchoolOfNecromancyBlock)
            {hand=_player.LeftHand;return true;}
            if(_board.Phase==BoardPhase.ChoosingSeatSwapTarget)
            {hand=Seats[3].LeftHand;playerTarget=true;return true;}
            return false;
        }
        public bool AllowsHand(Hand hand)
        {
            if(!Guided)return true;
            if(RequiredHand(out var required,out bool target))
                return required==null?hand==null:hand!=null&&(target?_game.OwnerOfHand(hand)==_game.OwnerOfHand(required):ReferenceEquals(hand,required));
            if(_board.PendingEffect==HandEffect.SchoolOfEvocationAbandonAttack||_board.PendingEffect==HandEffect.SchoolOfAbjurationBarrier)
                return AllowsAccept;
            if(_board.PendingSkillOptions.Count>0||_board.PendingEffect==HandEffect.SchoolOfEvocationDamageType
                ||_board.PendingEffect==HandEffect.SchoolOfAbjurationResistanceType)return false;
            if(_board.Phase==BoardPhase.ChoosingTargetPlayer)
                return _game.OwnerOfHand(hand)==Seats[NextTurn?.Target??1];
            return base.AllowClick(hand);
        }
        public bool AllowsOption(int value)=>!RequiredOption.HasValue||RequiredOption.Value==value;
        public bool AllowsDamageType(DamageType type)=>!Guided||Sample.CastingType==DamageType.None||type==Sample.CastingType;
        public bool AllowsResistance(AttackDamageType type)=>!Guided||type==(SampleIndex==1?AttackDamageType.Fire:AttackDamageType.Bludgeoning);
        public bool AllowsAccept
        {
            get
            {
                if(!Guided)return true;
                if(RequiredHand(out var hand,out _))return hand!=null;
                if(_board.PendingEffect==HandEffect.SchoolOfEvocationAbandonAttack)return _game.TurnCount==0;
                return true;
            }
        }
        public bool AllowsDecline
        {
            get
            {
                if(!Guided)return true;
                if(_board.Phase==BoardPhase.ChoosingSeatSwapTarget)return true; // 取消预览不消耗回合。
                if(RequiredHand(out var hand,out _))return hand==null;
                if(_board.PendingEffect==HandEffect.SchoolOfAbjurationBarrier)return false;
                if(_board.PendingEffect==HandEffect.SchoolOfEvocationAbandonAttack)return _game.TurnCount!=0;
                return true;
            }
        }
        public bool AllowsSkip=>!Guided||!ReferenceEquals(_board.ActivePlayer,_player);
        public bool AllowsCastling=>!Guided||_level==13&&SampleIndex==1&&_game.TurnCount==0;
        public override Hand SuggestedHand
        {
            get
            {
                if(!_board.IsHumanChooser)return null;
                if(RequiredHand(out var hand,out _))return hand;
                if(_board.PendingSkillOptions.Count>0||_board.PendingRequest.HasValue)return null;
                if(_board.Phase==BoardPhase.ChoosingTargetPlayer)return Seats[NextTurn?.Target??1].LeftHand;
                return base.SuggestedHand;
            }
        }
        public string ChoiceInstruction
        {
            get
            {
                if(RequiredOption.HasValue)foreach(var o in _board.PendingSkillOptions)
                    if(o.Value==RequiredOption.Value)return "本步选择【"+o.Label+"】。";
                if(_board.PendingEffect==HandEffect.SchoolOfEvocationDamageType)
                    return "本步选择【"+StatusNames.Of(Sample.CastingType)+"】，其余类型暂不可选。";
                if(_board.PendingEffect==HandEffect.SchoolOfAbjurationResistanceType)
                    return "本步选择【"+(SampleIndex==1?"火焰抗性":"物理整组")+"】。";
                if(RequiredHand(out var h,out bool target))return h==null?"本步选择【"+(_board.PendingWizardChoice?.DeclineLabel??"不发动")+"】。"
                    :"本步选择【"+_game.OwnerOfHand(h).Name+(target?"":"·"+CombatText.HandName(_game.OwnerOfHand(h),h)+h.Value)+"】。";
                if(_board.PendingEffect==HandEffect.SchoolOfEvocationAbandonAttack)return _game.TurnCount==0?"本步点击【放弃本次攻击】，随后指定类型。":"本步点击【继续攻击】，使用已存储的元素。";
                if(_board.PendingEffect==HandEffect.SchoolOfAbjurationBarrier)return "本步点击【存储奥术屏障】。";
                return "";
            }
        }
    }
}
