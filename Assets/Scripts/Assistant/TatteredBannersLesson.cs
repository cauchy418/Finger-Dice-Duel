using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>运算加数与真正攻击对象各有一个意图；同样用于确定性的多人演练。</summary>
    public interface IAttackTargetIntent { PlayerState IntendedVictim { get; } }

    public sealed class BannersTurn
    {
        public int Actor, Source, AddendOwner, Addend, Victim;
        public string Text;
        public BannersTurn(int actor,int source,int owner,int addend,int victim,string text)
        {Actor=actor;Source=source;AddendOwner=owner;Addend=addend;Victim=victim;Text=text;}
    }

    public sealed class BannersSample
    {
        public string Key,Title,Introduction;
        public CharacterDefinition Role;
        public int[] Hands;
        public int[] Ranges;
        public CharacterDefinition AllyRole;
        public int First;
        public Action<GameManager> Prepare;
        public Func<TatteredBannersLesson,bool> Verify;
        public readonly List<BannersTurn> Turns=new List<BannersTurn>();
        public BannersSample(string key,string title,CharacterDefinition role,int[] hands,string intro)
        {Key=key;Title=title;Role=role;Hands=hands;Introduction=intro;}
        public BannersSample Turn(int actor,int source,int owner,int addend,int victim,string text)
        {Turns.Add(new BannersTurn(actor,source,owner,addend,victim,text));return this;}
        public BannersSample Rest(int actor)=>Turn(actor,-1,0,0,-1,"教学休整：观察轮序与状态计时，同袍和教官等待下一次演练。");
        public BannersSample RestOthers(){for(int i=1;i<Hands.Length/2;i++)Rest(i);return this;}
    }

    /// <summary>第三章独立多人样本。导演只提供合法输入，不代替技能或伤害结算。</summary>
    public sealed class TatteredBannersLesson : AdventureLesson, ITutorialInput
    {
        public readonly BannersSample Sample;
        public readonly PlayerState[] Seats;
        public readonly int SampleIndex,SampleCount;
        private readonly int _level;
        private bool _passed;
        public bool SamplePassed=>_passed;
        public bool LastSample=>SampleIndex+1==SampleCount;
        public bool IsFinale=>_level==30;
        public BannersTurn CurrentTurn=>_board.CompletedActions<Sample.Turns.Count?Sample.Turns[_board.CompletedActions]:null;
        public override int LevelNumber=>_level;
        public override string DisplayTitle=>"第三章：残旗战场·"+AdventureChapter.BattleName(_level-20);
        public override string GuideName=>"残旗军士长";
        public override string GuideAvatarKey=>"Subclasses/"+CharacterDefinition.OathOfTheCrownId;
        public override string BattleBackgroundKey=>TatteredBannersChapter.Background;
        public override bool UsesClassicSetup=>IsFinale;
        public override bool AllowsTutorialSkip=>false;
        public override string ResultHeading=>IsFinale?"第三章通关":LastSample?"试炼完成":"演练完成";
        public override string OpeningText=>IsFinale
            ?"这一次，我不替你指出该碰哪只手。把你选的誓言，带到真正的交锋中。你的子职："+Player.Definition.DisplayName+"。同袍为古贤之誓（20生命，攻−1／防+1，距1）；对手为残旗指挥官（24生命，攻+1／防0，距1）和阵前弩手（20生命，攻防0，距2）。四人真实1d20，按经典2v2分配手值与席位；正常AI，允许失败。"
            :(_level==21?"高塔教你看懂法术，我教你看懂身旁的人。先认清你的同袍，再认清你的敌人。\n":"")
                +Sample.Title+"（"+(SampleIndex+1)+"／"+SampleCount+"）\n"+Sample.Introduction+"\n独立演练已重新布置手值、生命与状态。普通5不自动合盾；完整盾另有盾牌标记。";
        public override string SummaryText=>IsFinale?"残旗未倒，因为总有人愿意替身旁的人挡下一击。你已学会与同袍并肩。":Sample.Introduction;
        public override string FailureText=>IsFinale?"本次未获胜，未记为通关。可同子职重投四人先攻，或重新选择圣武士。":"演练未达成，请重试本样本；已完成的样本仍保留。";
        private bool AtBoundary=>_board.PendingWizardChoice==null&&!_board.PendingRequest.HasValue
            &&_board.Phase!=BoardPhase.ChoosingDefenseShield&&!_board.CollegeOfValorChoicePending;
        public override bool IsComplete=>IsFinale?_game.IsGameOver&&_game.Winner!=null&&_game.IsTeammate(_player,_game.Winner):_passed;
        public override bool IsFinished=>IsFinale?_game.IsGameOver:_passed||_game.IsGameOver||_board.CompletedActions>=Sample.Turns.Count&&AtBoundary;
        public override string Narration=>IsFinale?"自由2v2：借数与攻击目标分开选择，留意同袍的生命和自己的技能范围。"
            :ChoiceInstruction+(CurrentTurn?.Text??"观察最后结果，再点击右侧【试炼结束】。");

        private TatteredBannersLesson(int level,int index,BannersSample sample,PlayerState[] seats,GameManager game,BoardController board,PlayerState human=null)
            :base(game,board,human)
        {
            _level=level;SampleIndex=index;SampleCount=TatteredBannersChapter.Samples(level).Count;Sample=sample;Seats=seats;
            if(IsFinale)_beats.Add(new Beat{FreeClicks=true,Narration="自由2v2"});
            for(int i=0;i<sample.Turns.Count;i++)
            {int n=i;_beats.Add(new Beat{Narration=sample.Turns[i].Text,Complete=l=>board.CompletedActions>n});}
        }
        public override void Tick()
        {
            if(!Started)return;
            while(CurrentBeat?.Complete?.Invoke(this)==true)base.Tick();
            if(!IsFinale&&_board.CompletedActions>=Sample.Turns.Count&&AtBoundary)_passed=Sample.Verify?.Invoke(this)??true;
        }
        public override void Bind(BoardController board)
        {
            board.TutorialInput=IsFinale?null:this;
            foreach(var p in Seats)
            {
                var sink=_game.GetChoiceProvider(p) as IPlayerChoiceSink;
                IMoveSelector ai=ReferenceEquals(p,_player)?null:IsFinale?(IMoveSelector)NewAi(_game):new BannerMoves(this);
                board.BindPlayer(p,ai,sink);
            }
            board.ScriptedRestAllowed=actor=>!IsFinale&&!ReferenceEquals(actor,_player)
                &&CurrentTurn!=null&&CurrentTurn.Source<0&&ReferenceEquals(Seats[CurrentTurn.Actor],actor);
        }
        public static GreedyChoiceProvider NewAi(GameManager game)
        {var ai=new GreedyChoiceProvider{Game=game,UseLearnedFocus=true};ai.ApplyDifficulty(AiDifficulty.Hard);return ai;}
        private sealed class BannerMoves:AbstractChoiceProvider,IMoveSelector,IAttackTargetIntent,IAncientRoleChoicePolicy,IArcaneChoicePolicy
        {
            private readonly TatteredBannersLesson _lesson;
            private readonly GreedyChoiceProvider _policy;
            public BannerMoves(TatteredBannersLesson l){_lesson=l;_policy=NewAi(l.Game);}
            public string Describe=>"残旗战场演练";
            public PlayerState IntendedVictim {get;private set;}
            public LegalMove ChooseMove(GameManager game,PlayerState actor)
            {
                var t=_lesson.CurrentTurn;IntendedVictim=t?.Victim>=0?_lesson.Seats[t.Victim]:null;
                if(t==null||t.Source<0||!ReferenceEquals(_lesson.Seats[t.Actor],actor))return default;
                int source=t.Source==2?(actor.LeftHand.CanBeOperated?0:1):t.Source;
                return new LegalMove(actor,actor.GetHand(source),_lesson.Seats[t.AddendOwner].GetHand(t.Addend));
            }
            public override Hand ChooseHandFor(ChoiceRequest q,PlayerState decider)=>_policy.ChooseHandFor(q,decider);
            public Hand ChooseAncientTarget(string key,PlayerState owner,IReadOnlyList<Hand> candidates,GameManager game)
                =>((IAncientRoleChoicePolicy)_policy).ChooseAncientTarget(key,owner,candidates,game);
            public PlayerState ChooseArcaneTarget(string key,PlayerState owner,IReadOnlyList<PlayerState> candidates,GameManager game)
                =>((IArcaneChoicePolicy)_policy).ChooseArcaneTarget(key,owner,candidates,game);
        }

        public static TatteredBannersLesson Create(int level,int index=0,CharacterDefinition finaleRole=null)
        {
            var samples=TatteredBannersChapter.Samples(level);
            if(index<0||index>=samples.Count)throw new ArgumentOutOfRangeException(nameof(index));
            var sample=samples[index];var role=level==30?finaleRole??CharacterDefinition.OathOfDevotion:sample.Role;
            if(level==30&&role.Profession?.Id!="paladin")throw new ArgumentException("终战仅限圣武士子职");
            var seats=new PlayerState[sample.Hands.Length/2];var sinks=new IChoiceProvider[seats.Length];
            for(int i=0;i<seats.Length;i++)
            {
                sinks[i]=new SimpleChoiceProvider();
                if(i==0)seats[i]=CharacterFactory.CreatePlayer(role.DisplayName,role);
                else if(i==3&&(level==30||sample.AllyRole!=null))seats[i]=CharacterFactory.CreatePlayer("同袍",sample.AllyRole??CharacterDefinition.OathOfTheAncients);
                else
                {
                    var def=level==30?(i==1?TatteredBannersChapter.Commander:TatteredBannersChapter.Arbalest):TatteredBannersChapter.Guard;
                    seats[i]=new PlayerState(seats.Length==8?TatteredBannersChapter.EightNames[i]:level==30?def.DisplayName:i==3?"同袍":"残旗教官"+(i==1?"甲":"乙"),
                        new BannersGuardLogic(sample.Ranges?[i]??(level==30&&i==2?2:1),level==30&&i==1?1:0,level==30?(i==1?AttackDamageType.Slashing:AttackDamageType.Piercing):AttackDamageType.Bludgeoning),def.MaxHealth);
                    seats[i].BindDefinition(def);
                }
                if(i>0)seats[i].DisplayNameOverride=seats[i].Name;
            }
            var game=seats.Length==8?GameManager.CreateEight(seats,sinks,TatteredBannersChapter.EightTeams):GameManager.Create2v2(seats,sinks);
            for(int i=0;i<seats.Length;i++){seats[i].LeftHand.SetValue(sample.Hands[2*i]);seats[i].RightHand.SetValue(sample.Hands[2*i+1]);}
            sample.Prepare?.Invoke(game);
            game.ForceActivePlayerForTest(seats[sample.First]);
            game.TakeSkillLog();game.TakeShockLog();
            var board=new BoardController(game,(IPlayerChoiceSink)sinks[0],(IPlayerChoiceSink)sinks[1]);
            var lesson=new TatteredBannersLesson(level,index,sample,seats,game,board);lesson.Bind(board);return lesson;
        }
        public static TatteredBannersLesson AttachFinale(PlayerState[] seats,PlayerState human)
        {
            if(human?.Definition?.Profession?.Id!="paladin")throw new ArgumentException("终战仅限圣武士子职");
            var sinks=new IChoiceProvider[4];for(int i=0;i<4;i++)sinks[i]=new SimpleChoiceProvider();
            var game=GameManager.Create2v2(seats,sinks);
            var board=new BoardController(game,(IPlayerChoiceSink)sinks[0],(IPlayerChoiceSink)sinks[1]);
            var lesson=new TatteredBannersLesson(30,0,TatteredBannersChapter.Samples(30)[0],seats,game,board,human);
            lesson.Bind(board);board.SetFirstPlayer(seats[0]);return lesson;
        }
        protected override int OpeningMoveCount=>0;
        protected override ScriptedMoveSelector CreateOpeningScript()=>new ScriptedMoveSelector();

        // 角色请求与正常行动分开白名单。纯运算仍逐手点战场；人物目标只比所属角色。
        private Hand RequiredHand
        {
            get
            {
                var q=_board.PendingWizardChoice;
                if(q!=null)
                {
                    if(q.Prompt.Contains("自然之怒"))return Seats[1].LeftHand;
                    if(q.Prompt.Contains("不灭哨卫"))return Seats[3].LeftHand;
                    if(q.Prompt.Contains("仇敌誓言"))
                        return Seats[Sample.Key=="unmarked"||Sample.Key=="retarget"?2:Sample.Key=="death"&&!Seats[1].IsAlive?2:1].LeftHand;
                    if(q.Prompt.Contains("复仇之魂"))return q.Prompt.Contains("加数")?Seats[3].LeftHand:_player.LeftHand;
                }
                if(_board.PendingEffect==HandEffect.OathOfDevotionRedirect)return _player.LeftHand;
                if(_board.PendingEffect==HandEffect.OathOfDevotionShockTarget)return Seats[1].LeftHand;
                if(_board.PendingEffect==HandEffect.OathbreakerTransfer)return Seats[2].LeftHand;
                if(_board.Phase==BoardPhase.ChoosingDefenseShield)return _player.LeftHand;
                var t=CurrentTurn;if(t==null||t.Actor!=0)return null;
                if(_board.Phase==BoardPhase.ChoosingAttackerHand)return _player.GetHand(t.Source);
                if(_board.Phase==BoardPhase.ChoosingTargetHand)return Seats[t.AddendOwner].GetHand(t.Addend);
                if(_board.Phase==BoardPhase.ChoosingTargetPlayer&&t.Victim>=0)return Seats[t.Victim].LeftHand;
                return null;
            }
        }
        private bool IsPlayerTarget=>_board.PendingWizardChoice?.TargetsPlayer==true
            ||_board.PendingEffect==HandEffect.OathOfDevotionShockTarget||_board.PendingEffect==HandEffect.OathOfDevotionRedirect
            ||_board.PendingEffect==HandEffect.OathbreakerTransfer||_board.Phase==BoardPhase.ChoosingTargetPlayer;
        public bool AllowsHand(Hand hand)
        {
            if(IsFinale)return true;
            var required=RequiredHand;
            return required!=null&&hand!=null&&(IsPlayerTarget?ReferenceEquals(_game.OwnerOfHand(hand),_game.OwnerOfHand(required)):ReferenceEquals(hand,required));
        }
        public bool AllowsOption(int value)=>IsFinale||value==1;
        public bool AllowsDamageType(DamageType type)=>IsFinale;
        public bool AllowsResistance(AttackDamageType type)=>IsFinale;
        public bool AllowsAccept=>IsFinale||RequiredHand!=null||_board.PendingSkillOptions.Count>0;
        public bool AllowsDecline=>IsFinale;
        public bool AllowsSkip=>IsFinale||!_board.IsHumanChooser;
        public bool AllowsCastling=>IsFinale;
        public override bool AllowAcceptChoice=>AllowsAccept;
        public override bool AllowDeclineChoice=>AllowsDecline;
        public override bool AllowClick(Hand hand)=>AllowsHand(hand);
        public override Hand SuggestedHand=>_board.IsHumanChooser&&_board.PendingSkillOptions.Count==0?RequiredHand:null;
        public override string RejectionHint=>"本步请点击高亮的手牌或角色；借谁的数字与攻击谁分开选择。";
        private string ChoiceInstruction=>_board.PendingSkillOptions.Count>0?"本步选择【转移仇敌标记】。":_board.PendingWizardChoice!=null||_board.PendingRequest.HasValue
            ?"本步选择高亮的"+(IsPlayerTarget?"角色":"手牌")+"，其余正常选项保留显示但不可点。":"";
    }

    public sealed class BannersGuardLogic:CharacterTemplate
    {
        private readonly int _range,_attack;private readonly AttackDamageType _type;
        public BannersGuardLogic(int range=1,int attack=0,AttackDamageType type=AttackDamageType.Bludgeoning){_range=range;_attack=attack;_type=type;}
        public override int DefaultAttackBonus=>_attack;
        public override int BaseAttackRange=>_range;
        public override AttackDamageType AttackTypeOf(AttackContext context)=>_type;
    }
}
