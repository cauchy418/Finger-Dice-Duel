using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public enum CampaignPhase { Guide, AwaitingHorn, FreeCombat, Finished }
    public enum CampaignActionKind { Operation, Observe, Castle, RaiseShield }
    public sealed class CampaignStep
    {
        public string Id,Text;
        public int Actor,Source,AddendOwner=1,Addend,Victim=1;
        public int? Result,Option;
        public DamageType? Element;
        public AttackDamageType? Resistance;
        public StatusEffect? Maneuver;
        public bool Abandon,StoreWard,DeclineDefense,DeclineNecromancy;
        public bool Charge,Redirect;
        public int ChainSeat=-1,ShockSeat=1,VowSeat=1;
        public int? Portent,IllusionTouch,IllusionDefense;
        public StatusEffect? Enchant;
        public int TargetSeat=-1,DefenseSeat=-1,DefenseHand,SwapOwn=-1,SwapDead=-1;
        public CampaignActionKind Kind;
        public Action<CampaignLesson,PlayerState> Prepare;
        public Func<CampaignLesson,bool> Verify;
    }
    public sealed partial class CampaignLesson:AdventureLesson,ITutorialInput
    {
        public readonly PlayerState[] Seats;
        public readonly List<CampaignStep> Steps;
        public CampaignPhase Stage {get;private set;}
        public readonly List<string> Observations=new List<string>();
        public int Cursor {get;private set;}
        public int HornCount {get;private set;}
        public int GuardianSeat {get;private set;}
        private readonly int _level;
        private int _preparedAt=-1,_preparedCursor=-1;
        private readonly HashSet<PlayerState> _preparedOwners=new HashSet<PlayerState>();
        private TurnResult _observedTurn;
        public CampaignStep Step=>Cursor<Steps.Count?Steps[Cursor]:null;
        public bool Guided=>Stage==CampaignPhase.Guide;
        public bool TeachingComplete=>Stage==CampaignPhase.FreeCombat||Stage==CampaignPhase.Finished;
        public override int LevelNumber=>_level;
        public override int StepNumber=>Guided?Cursor+1:0;
        public override int StepCount=>Steps.Count;
        public override bool IsFreeStage=>TeachingComplete;
        public override bool UsesClassicSetup=>_level==5||_level%10==0;
        public override bool AllowsTutorialSkip=>!UsesClassicSetup;
        public override string DisplayTitle=>(_level<=10?"第一章：边境哨站":_level<=20?"第二章：法师高塔":"第三章：残旗战场")+"·"+AdventureChapter.BattleName((_level-1)%10+1);
        public override string GuideName=>_level<=10?"老兵":_level<=20?"秘法典籍":"残旗军士长";
        public override string GuideAvatarKey=>_level<=10?AdventureLevel1.GuideAvatarKey:_level<=20?"Adventure/guide_arcane_codex":"Subclasses/"+CharacterDefinition.OathOfTheCrownId;
        public override string BattleBackgroundKey=>_level<=10?"Backgrounds/border_outpost_chapter_01":_level<=20?"Backgrounds/mage_tower_chapter_02":TatteredBannersChapter.Background;
        public override bool IsFinished=>_game.IsGameOver;
        public override bool IsComplete=>TeachingComplete&&_game.IsGameOver&&_game.Winner!=null&&(ReferenceEquals(_game.Winner,_player)||_game.IsTeammate(_game.Winner,_player));
        public override string ResultHeading=>_level%10==0?"第"+(_level/10)+"章通关":"试炼胜利";
        public override string SummaryText=>"你已在同一场战斗中完成教学并取得胜利。当前伤势、成长、手值和队伍关系一直保留，教学完成没有替你赢下比赛。";
        public override string FailureText=>TeachingComplete?"本次实战未获胜，未新增通关。可重试整场或返回章节；已有通关记录保留。":"教学尚未完成，本次不记通关。重试从本课开场开始，关内不会重置战场。";
        public override string OpeningText=>OpeningSummary();
        public override string Narration=>Stage==CampaignPhase.AwaitingHorn?"教学结束。等待当前技能和动画结算，随后在原战场进入自主战斗。":TeachingComplete?"自主战斗：选择任意合法行动。对手会正常进攻、合盾和回复，取得实际胜利才能通关。":Step?.Text;
        protected override int OpeningMoveCount=>0;
        protected override ScriptedMoveSelector CreateOpeningScript()=>new ScriptedMoveSelector();
        private CampaignLesson(int level,PlayerState[] seats,GameManager game,BoardController board,List<CampaignStep> steps,int guardian):base(game,board,seats[0])
        {
            _level=level;Seats=seats;Steps=steps;GuardianSeat=guardian;Stage=steps.Count==0?CampaignPhase.FreeCombat:CampaignPhase.Guide;
            foreach(var p in seats)if(p.Character is CampaignNpcLogic npc)npc.FreeCombat=TeachingComplete;
            _beats.Add(new Beat{FreeClicks=true});Bind(board);
            for(int i=0;i<seats.Length;i++)
            {
                var p=seats[i];p.TrainingProtected=Guided&&(i==0||game.IsTeammate(p,_player)||i==guardian||level==29&&i==5);
                p.TrainingProtectionTriggered=()=>game.NoteSkill(p,"操演护持","教学期间最低保留1生命",p);
            }
        }
        public override void BeginTutorial(){base.BeginTutorial();}
        public override void SkipTutorial(){base.BeginTutorial();EnterFree(false);}
        public override void Tick()
        {
            if(!Started)return;
            if(Stage==CampaignPhase.AwaitingHorn&&SafeBoundary)EnterFree(true);
            if(_game.IsGameOver&&TeachingComplete)Stage=CampaignPhase.Finished;
        }
        private bool SafeBoundary=>_board.PendingWizardChoice==null&&!_board.PendingRequest.HasValue&&!_board.CollegeOfValorChoicePending&&_board.Phase!=BoardPhase.ChoosingDefenseShield;
        public override void Bind(BoardController board)
        {
            board.TutorialInput=this;board.CampaignActionSettled+=OnSettled;
            board.BeforeAutomaticAction=PrepareNpc;
            board.TryAutomaticCampaignAction=p=>Guided&&Step?.Kind==CampaignActionKind.RaiseShield&&ReferenceEquals(p,Seats[Step.Actor])&&p.Character is CampaignNpcLogic trainer?trainer.RaiseShield(p,_game):TeachingComplete&&(TryPush(p)||p.Character is CampaignNpcLogic npc&&npc.TryAction(p,_game));
            board.CampaignActionLabel=ActionLabel;board.PerformCampaignAction=PerformAction;
            board.ScriptedRestAllowed=p=>Guided&&!ReferenceEquals(p,_player)&&(Step==null||!ReferenceEquals(p,Seats[Step.Actor])||Step.Kind==CampaignActionKind.Observe);
            for(int i=0;i<Seats.Length;i++)board.BindPlayer(Seats[i],i==0?null:TeachingComplete?(IMoveSelector)NewAi(_game):new CampaignMoves(this),_game.GetChoiceProvider(Seats[i]) as IPlayerChoiceSink??new SimpleChoiceProvider());
        }
        private void EnterFree(bool horn)
        {
            if(TeachingComplete)return;
            Stage=CampaignPhase.FreeCombat;
            if(horn)
            {
                HornCount++;var source=Seats[GuardianSeat];
                if(source.IsAlive&&!source.IsPolymorphed)CampaignNpcLogic.Heal(_game,source,source,12,"实战号角");
                foreach(var p in Seats)if(p.IsAlive&&(ReferenceEquals(p,_player)||_game.IsTeammate(p,_player)))CampaignNpcLogic.Heal(_game,source,p,6,"实战号角");
                if(_level==29&&Seats[5].IsAlive)CampaignNpcLogic.Heal(_game,Seats[5],Seats[5],12,"实战号角");
            }
            foreach(var p in Seats){p.TrainingProtected=false;if(p.Character is CampaignNpcLogic npc)npc.FreeCombat=true;}
            _board.ScriptedRestAllowed=null;
            for(int i=1;i<Seats.Length;i++)_board.BindPlayer(Seats[i],NewAi(_game),_game.GetChoiceProvider(Seats[i]) as IPlayerChoiceSink??new SimpleChoiceProvider());
        }
        private void OnSettled(PlayerState actor)
        {
            var turn=_board.LastTurn;bool fresh=turn!=null&&!ReferenceEquals(turn,_observedTurn);_observedTurn=turn;
            if(!Started||!Guided||Step==null||!ReferenceEquals(actor,Seats[Step.Actor]))return;
            if(Step.Kind==CampaignActionKind.Operation&&(!fresh||!ReferenceEquals(turn.Actor,actor)||!ReferenceEquals(turn.ActedHand,actor.GetHand(Step.Source==2?ReferenceEquals(turn.ActedHand,actor.LeftHand)?0:1:Step.Source))))return;
            if(Step.Verify!=null&&!Step.Verify(this))return;
            Observations.Add(Step.Id);Cursor++;_preparedOwners.Clear();
            if(Cursor>=Steps.Count)Stage=CampaignPhase.AwaitingHorn;
        }
        private void PrepareNpc(PlayerState actor)
        {
            if(!Started||!Guided||Step==null||actor.IsPolymorphed||!StatusRules.CanAct(actor))return;
            if(_preparedAt!=_board.CompletedActions||_preparedCursor!=Cursor){_preparedAt=_board.CompletedActions;_preparedCursor=Cursor;_preparedOwners.Clear();}
            if(!_preparedOwners.Add(actor))return;
            if(ReferenceEquals(actor,Seats[Step.Actor]))Step.Prepare?.Invoke(this,actor);
            if(Step.Result.HasValue&&Step.Kind==CampaignActionKind.Operation)
            {
                var source=Seats[Step.Actor].GetHand(Step.Source==2?0:Step.Source);
                if(Step.Actor==0&&ReferenceEquals(actor,Seats[Step.AddendOwner]))TuneAddend(actor,actor.GetHand(Step.Addend),source,Seats[0],Step.Result.Value);
                else if(Step.Actor!=0&&ReferenceEquals(actor,Seats[Step.Actor]))TuneSource(actor,source,Seats[Step.AddendOwner].GetHand(Step.Addend),Step.Result.Value);
            }
            if(ReferenceEquals(actor,Seats[Step.Actor])&&Cursor+1<Steps.Count)
            {
                var next=Steps[Cursor+1];
                if(next.Actor==0&&next.Kind==CampaignActionKind.Operation&&next.Result.HasValue&&ReferenceEquals(actor,Seats[next.AddendOwner])&&(Step.Kind!=CampaignActionKind.Operation||next.Addend!=Step.Source))
                    TuneAddend(actor,actor.GetHand(next.Addend),_player.GetHand(next.Source),_player,next.Result.Value);
            }
        }
        private static int ResultFor(PlayerState actor,int source,int addend)=>Math.Max(1,StatusRules.ApplyResultModifiers(actor,Hand.ComputeSum(source,addend)));
        private void TuneAddend(PlayerState owner,Hand hand,Hand source,PlayerState actor,int result)
        {for(int n=1;n<=9;n++)if(ResultFor(actor,source.Value,n)==result){Tune(owner,hand,n);return;}}
        private void TuneSource(PlayerState owner,Hand hand,Hand addend,int result)
        {for(int n=1;n<=9;n++)if(ResultFor(owner,n,addend.Value)==result){Tune(owner,hand,n);return;}}
        public bool Tune(PlayerState owner,Hand hand,int value)
        {
            if(!Guided||!ReferenceEquals(_board.ActivePlayer,owner)||owner.IsPolymorphed||!StatusRules.CanAct(owner)||!owner.Owns(hand)||!hand.CanBeOperated||hand.IsShield)return false;
            if(hand.Value==value)return true;int old=hand.Value;hand.SetValue(value);
            _game.NoteSkill(owner,"演练调律",(ReferenceEquals(hand,owner.LeftHand)?"左":"右")+"手 "+old+"→"+value,owner,new SkillVisualData(source:hand,value:value,previous:old));return true;
        }
        public void Equip(PlayerState owner,AttackDamageType type)
        {if(owner.Character is CampaignNpcLogic npc){npc.Weapon=type;_game.NoteSkill(owner,"符文换装",AttackDamageTypes.NameOf(type),owner,new SkillVisualData(element:type));}}
        public void Refit(PlayerState owner,int heal=16)
        {owner.ClearNegativeStatuses();_game.NoteSkill(owner,"训练整备","净化已观察负面",owner);CampaignNpcLogic.Heal(_game,owner,owner,heal,"训练整备");}
        public static GreedyChoiceProvider NewAi(GameManager game)
        {var ai=new GreedyChoiceProvider{Game=game,UseLearnedFocus=true};ai.ApplyDifficulty(AiDifficulty.Hard);return ai;}
        private bool ReadyOperation
        {
            get
            {
                var s=Step;if(s==null||s.Kind!=CampaignActionKind.Operation)return false;
                var actor=Seats[s.Actor];var hand=actor.GetHand(s.Source==2?(actor.LeftHand.CanBeOperated?0:1):s.Source);var addend=Seats[s.AddendOwner].GetHand(s.Addend);
                return hand.CanBeOperated&&(!s.Result.HasValue||ResultFor(actor,hand.Value,addend.Value)==s.Result.Value)&&StatusRules.CanUseHandAsAddend(actor,addend,_game);
            }
        }
        private string ActionLabel(PlayerState actor)
        {
            if(!ReferenceEquals(actor,_player))return null;
            if(Guided&&(Step?.Kind==CampaignActionKind.Observe&&Step.Actor==0||Step!=null&&(Step.Actor!=0||Step.Kind==CampaignActionKind.Operation&&!ReadyOperation)))return "观摩 · 保留手值";
            return TeachingComplete&&(_level==4||_level==7)&&!_obstacleMoved?"推开路障":null;
        }
        private bool PerformAction(PlayerState actor)
        {if(ActionLabel(actor)==null)return false;if(TeachingComplete)return TryPush(actor);return true;}
        private bool _obstacleMoved;
        private bool TryPush(PlayerState actor)
        {
            if((_level!=4&&_level!=7)||_obstacleMoved||!StatusRules.CanAct(actor))return false;
            if(!ReferenceEquals(actor,_player)&&_game.CanReach(actor,_player))return false;
            if(!_game.MoveSeatObstacle(0))return false;
            _obstacleMoved=true;_game.NoteSkill(actor,"推开路障","一侧石头移向场边，最短距离已缩短");return true;
        }
        public bool ObstacleMoved=>_obstacleMoved;
        private string OpeningSummary()
        {
            var lines=new List<string>{GuideName+"：先完成引导，再在同一战场自主获胜。中途不重置；退出重试从本课开场开始。","本场："+_player.Definition.DisplayName+"，生命"+_player.Health+"／"+_player.MaximumHealth+"。"};
            for(int i=1;i<Seats.Length;i++)lines.Add(Seats[i].Name+" "+Seats[i].Health+"／"+Seats[i].MaximumHealth+"，攻"+Seats[i].Character.AttackBonus+" 防"+Seats[i].Character.DefenseBonus+" 距"+(GameManager.BaseRangeOf(Seats[i])>=CharacterTemplate.InfiniteAttackRange?"∞":GameManager.BaseRangeOf(Seats[i]).ToString()));
            foreach(var p in Seats)if(p.Character is CampaignNpcLogic npc&&!string.IsNullOrEmpty(npc.AbilitySummary))lines.Add(p.Name+"："+npc.AbilitySummary);
            return string.Join("\n",lines);
        }
    }
}
