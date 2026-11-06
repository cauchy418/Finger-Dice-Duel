using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class CampaignLesson
    {
        public bool Version3 {get;}
        public CampaignChapter Chapter=>CampaignChapter.ForLevel(_level);
        public string StableId=>CampaignChapter.LevelId(_level);
        public override string CompletionKey=>Version3?CampaignChapter.LevelKey(_level):base.CompletionKey;
        public int CourseLevel=>Version3?CampaignChapter.LegacyLevel(_level):_level;
        public int TrialStartRound {get;private set;}
        public bool TrialConfirmationPending=>Version3&&Started&&Stage==CampaignPhase.AwaitingHorn&&SafeBoundary;
        public bool CanRetryTrial=>_trialBaseline!=null;
        private CampaignSpec _spec;
        private CampaignLesson _trialBaseline;
        private string OpeningCopy=>_level==1?"边境哨站，身负旧伤的老兵拦住了去路。\n「新兵，先学会指尖骰术，再谈闯荡。」":Chapter==CampaignChapter.Outpost?"老兵将兵器摆在你面前。\n「这一课，试试"+Player.Definition.DisplayName+"的本领。」":Chapter==CampaignChapter.Gallery?"风、火与影子沿着长廊流转，石壁上的铭文逐渐亮起。\n「看清力量经过之后，留下了什么。」":Chapter==CampaignChapter.Tower?"高塔深处，秘法典籍缓缓展开。\n「你带着回廊的痕迹而来，翻到下一页，看看法术还能做些什么。」":"残旗在风中猎猎作响，军士长示意你归队。\n「这一战，留意同袍，也留意敌人的动向。」";
        private string VictoryCopy=>Chapter==CampaignChapter.Outpost?"老兵点了点头：能把招式用在合适的时候，才算真正学会。":Chapter==CampaignChapter.Gallery?"铭文渐渐收敛光芒：你已听懂这道力量的回声。":Chapter==CampaignChapter.Tower?"典籍翻过一页：带着这份领悟，继续向塔顶前进。":"军士长收起残旗：记住身旁的同袍，下一场还要并肩作战。";
        public bool ConfirmTrial()
        {
            if(!TrialConfirmationPending)return false;
            EnterFree(true);
            CaptureTrial();return true;
        }
        public void CaptureTrial()
        {
            if(!Version3||!Started||!TeachingComplete||!SafeBoundary||_trialBaseline!=null)return;
            _trialBaseline=CampaignCheckpoint.Copy(this);
        }
        public CampaignLesson RetryTrial()
        {
            if(_trialBaseline==null)throw new InvalidOperationException("尚无实战试炼起点");
            var restored=CampaignCheckpoint.Copy(_trialBaseline);
            restored._trialBaseline=_trialBaseline;
            restored.Game.ClearCombatVisuals();
            return restored;
        }
        internal void SetGuidedRoll()
        {
            if(!Version3||!Guided||Step==null)return;
            if(Player.Character is StormSorceryLogic storm)storm.ForcedRoll=Step.ForcedRoll??-1;
            if(Player.Character is DraconicBloodlineLogic dragon)dragon.ForcedRoll=0;
            if(Player.Character is WildMagicLogic wild)wild.ForcedRoll=Step.ForcedRoll??-1;
        }
        private void ReleaseGuidedRolls()
        {
            if(Player.Character is StormSorceryLogic storm)storm.ForcedRoll=-1;
            if(Player.Character is DraconicBloodlineLogic dragon)dragon.ForcedRoll=-1;
            if(Player.Character is WildMagicLogic wild)wild.ForcedRoll=-1;
        }
        public void LeaveLesson(){ReleaseGuidedRolls();}
        private void RestoreTrialOpposition()
        {
            if(_spec==null)return;
            for(int i=1;i<Seats.Length;i++)
            {
                var p=Seats[i];
                // A corpse used by necromancy or the defeated front soldier remains dead.
                if(!p.IsAlive||_game.IsTeammate(p,Player))continue;
                var n=_spec.Npcs[i];
                p.EndPolymorph();p.ClearNegativeStatuses();p.ClearTemporaryDamageAffinities();
                p.SetHealthExact(p.MaximumHealth);
                if(p.Character is CampaignNpcLogic npc)
                {npc.Weapon=n.Type;npc.OnMatchReset(p,_game);}
                if(!UsesClassicSetup)
                {
                    var hands=_spec.TrialHands;
                    p.LeftHand.SetValue(hands!=null?hands[i*2]:3);
                    p.RightHand.SetValue(hands!=null?hands[i*2+1]:2);
                    p.LeftHand.SetShieldState(ShieldState.None);p.RightHand.SetShieldState(ShieldState.None);
                    if(n.Shield&&p.RightHand.Value==5)CampaignContent.Shield(p,1);
                }
                _game.NoteSkill(p,"整装迎战","恢复至最佳状态",p);
            }
            _spec.TrialSetup?.Invoke(this);
            if(_level==17&&Player.Character is ShadowMagicLogic shadow)
            {shadow.OnMatchReset(Player,_game);_game.NoteSkill(Seats[GuardianSeat],"回廊重赋","坟冢之力已恢复",Player);}
        }
    }
}
