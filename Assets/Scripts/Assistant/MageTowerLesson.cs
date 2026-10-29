using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>高塔教学的独立试验。重新建局才切换样本，不在连续战斗中改手值。</summary>
    public sealed class MageTowerSample
    {
        public string Key, Title, Introduction, Conclusion;
        public DamageType CastingType;
        public AttackDamageType ResistanceType=AttackDamageType.Bludgeoning;
        public int ResistanceTargetSeat;
        public int[] Hands;
        public int FirstSeat;
        public int GuardAttack, GuardDefense;
        public AttackDamageType GuardType = AttackDamageType.Slashing;
        public Action<GameManager> Prepare;
        public Func<MageTowerLesson, bool> Verify;
        public readonly List<MageTowerTurn> Turns = new List<MageTowerTurn>();
        public MageTowerSample(string key, string title, int[] hands, string introduction = "")
        { Key=key;Title=title;Hands=hands;Introduction=introduction; }
        public MageTowerSample Turn(int actor, int source, int target, int addend, string narration)
        { Turns.Add(new MageTowerTurn(actor,source,target,addend,narration));return this; }
        public MageTowerSample Rest(int actor, string narration) => Turn(actor,-1,0,0,narration);
    }

    public sealed class MageTowerTurn
    {
        public readonly int Actor, Source, Target, Addend;
        public readonly string Narration;
        public MageTowerTurn(int actor,int source,int target,int addend,string narration)
        {Actor=actor;Source=source;Target=target;Addend=addend;Narration=narration;}
    }

    public sealed partial class MageTowerLesson : AdventureLesson
    {
        public readonly MageTowerSample Sample;
        public readonly PlayerState[] Seats;
        public readonly int SampleIndex, SampleCount;
        private readonly int _level;
        private bool _passed;
        public bool SamplePassed => _passed;
        public bool LastSample => SampleIndex+1==SampleCount;
        public override string GuideName => "秘法典籍";
        public override string GuideAvatarKey => "Adventure/guide_arcane_codex";
        public override string BattleBackgroundKey => "Backgrounds/mage_tower_chapter_02";
        public override int LevelNumber => _level;
        public override string DisplayTitle => "第二章：法师高塔·"+AdventureChapter.BattleName(_level-10);
        public override bool UsesClassicSetup => _level==20;
        public override bool AllowsTutorialSkip => false;
        public override string ResultHeading => _level==20?"第二章通关":LastSample?"试炼完成":"试验完成";
        public override string OpeningText => _level==20
            ? "典籍：塔顶留下的是一位法师的回声。用你选择的道路，回答它。\n本场子职："+_player.Definition.DisplayName+"。\n法师幻影：32生命，攻防0/0，距离∞，强酸攻击。\n本场永久火焰、寒冷抗性，毒素免疫。每3次普通攻击实际伤人后回复3生命。\n真实投骰，按经典规则自由对战，允许失败。"
            : Sample.Title+"（"+(SampleIndex+1)+" / "+SampleCount+"）\n"+Sample.Introduction+"\n独立试验：双方手值、生命与状态已重置。本课以完成指定观察为目标。";
        public override string SummaryText => _level==20
            ? "你已分清力量的形状，也学会了让它失去方向。典籍合上最后一页，高塔仍向你敞开。"
            : Sample.Conclusion??Sample.Introduction;
        public override string FailureText => _level==20?"本场未获胜，未记为通关。可同子职重投，或返回选角更换法师。"
            : "本试验的目标尚未达成。请重试本样本，按典籍提示选择技能；此前完成的试验仍保留。";
        public override bool IsComplete => _level==20?base.IsComplete:_passed;
        public override bool IsFinished => _level==20?base.IsFinished:_passed||_game.IsGameOver||FinishedActions;
        private bool FinishedActions => _game.TurnCount>=Sample.Turns.Count && AtBoundary;
        private bool AtBoundary => _board.PendingWizardChoice==null && !_board.PendingRequest.HasValue
            && _board.Phase!=BoardPhase.ChoosingDefenseShield && !_board.CollegeOfValorChoicePending;
        public override string Narration => _level==20
            ? "自由试炼：留意幻影的永久修正与汲能回路。强酸会降防；腐朽、失能、预言和变化仍可正常生效。"
            : ChoiceInstruction+(CurrentBeat?.Narration??"完成当前技能选择后查看试验结果。");

        private MageTowerLesson(int level,int index,int count,MageTowerSample sample,PlayerState[] seats,GameManager game,BoardController board)
            : base(game,board)
        {
            _level=level;SampleIndex=index;SampleCount=count;Sample=sample;Seats=seats;
            if(level==20)_beats.Add(new Beat{Narration="自由试炼：运用所选法师的技能挑战法师幻影。",FreeClicks=true});
            for(int i=0;i<sample.Turns.Count;i++)
            {
                int number=i;var turn=sample.Turns[i];
                _beats.Add(new Beat { Narration=turn.Narration,
                    ActedHand=turn.Actor==0&&turn.Source>=0?seats[0].GetHand(turn.Source):null,
                    AddendHand=turn.Actor==0&&turn.Source>=0?seats[turn.Target].GetHand(turn.Addend):null,
                    Complete=l=>game.TurnCount>number });
            }
        }
        public override void Tick()
        {
            if(!Started)return;
            while(CurrentBeat?.Complete?.Invoke(this)==true)base.Tick();
            if(_level!=20 && FinishedActions && (Sample.Verify?.Invoke(this)??true))_passed=true;
        }
        public override bool AllowClick(Hand hand)=>AllowsHand(hand);
        public override bool AllowAcceptChoice=>AllowsAccept;
        public override bool AllowDeclineChoice=>AllowsDecline;
        public override void Bind(BoardController board)
        {
            board.TutorialInput=_level<20?this:null;
            foreach(var p in Seats)if(!ReferenceEquals(p,_player))
                board.BindPlayer(p,_level==20?(IMoveSelector)new GreedyChoiceProvider {UseLearnedFocus=true}:new TowerMoves(this),
                    _game.GetChoiceProvider(p) as IPlayerChoiceSink??new SimpleChoiceProvider());
            board.ScriptedRestAllowed = actor => _level!=20 && !ReferenceEquals(actor,_player)
                && (NextTurn==null || NextTurn.Source<0);
        }
        private MageTowerTurn NextTurn => _game.TurnCount<Sample.Turns.Count?Sample.Turns[_game.TurnCount]:null;
        private sealed class TowerMoves:IMoveSelector
        {
            private readonly MageTowerLesson _lesson;
            public TowerMoves(MageTowerLesson lesson){_lesson=lesson;}
            public string Describe=>"高塔试验剧本";
            public LegalMove ChooseMove(GameManager game,PlayerState actor)
            {
                var t=_lesson.NextTurn;
                if(t==null||t.Source<0||!ReferenceEquals(_lesson.Seats[t.Actor],actor))return default;
                return new LegalMove(actor,actor.GetHand(t.Source),_lesson.Seats[t.Target].GetHand(t.Addend));
            }
        }
        protected override int OpeningMoveCount=>0;
        protected override ScriptedMoveSelector CreateOpeningScript()=>new ScriptedMoveSelector();

        public static MageTowerLesson Create(int level,int sampleIndex=0,CharacterDefinition finaleRole=null)
        {
            var samples=MageTowerChapter.Samples(level);
            if(sampleIndex<0||sampleIndex>=samples.Count)throw new ArgumentOutOfRangeException(nameof(sampleIndex));
            var sample=samples[sampleIndex];
            var role=level==20?finaleRole??CharacterDefinition.SchoolOfEvocation:MageTowerChapter.PlayerRole(level);
            if(level==20 && role.Profession?.Id!="wizard")throw new ArgumentException("终战仅限法师子职");
            var seats=new PlayerState[sample.Hands.Length/2];var providers=new IChoiceProvider[seats.Length];
            for(int i=0;i<seats.Length;i++)
            {
                providers[i]=new SimpleChoiceProvider();
                seats[i]=i==0?CharacterFactory.CreatePlayer(role.DisplayName,role)
                    :new PlayerState(level==20?"法师幻影":i==3?"协作护卫":"奥法护卫"+(seats.Length==4?(i==1?"甲":"乙"):""),
                        level==20?(ICharacterBehavior)new MagePhantomLogic():new TowerGuardLogic(sample.GuardType,sample.GuardAttack,sample.GuardDefense),level==20?32:20);
                if(i>0){seats[i].BindDefinition(level==20?MageTowerChapter.Phantom:MageTowerChapter.Guard);seats[i].DisplayNameOverride=seats[i].Name;}
            }
            var game=seats.Length==4?GameManager.Create2v2(seats,providers):new GameManager(seats[0],seats[1],providers[0],providers[1]);
            for(int i=0;i<seats.Length;i++){seats[i].LeftHand.SetValue(sample.Hands[2*i]);seats[i].RightHand.SetValue(sample.Hands[2*i+1]);}
            game.ForceActivePlayerForTest(seats[sample.FirstSeat]);
            sample.Prepare?.Invoke(game);
            var board=new BoardController(game,(IPlayerChoiceSink)providers[0],(IPlayerChoiceSink)providers[1]);
            var lesson=new MageTowerLesson(level,sampleIndex,samples.Count,sample,seats,game,board);
            lesson.Bind(board);return lesson;
        }
    }

    public class TowerGuardLogic:CharacterTemplate,IStatusApplyingAttack
    {
        private readonly AttackDamageType _type;
        private readonly int _attack,_defense;
        public TowerGuardLogic(AttackDamageType type=AttackDamageType.Slashing,int attack=0,int defense=0)
        {_type=type;_attack=attack;_defense=defense;}
        public override int DefaultAttackBonus=>_attack;
        public override int DefaultDefenseBonus=>_defense;
        public override AttackDamageType AttackTypeOf(AttackContext context)=>_type;
        public StatusEffect StatusToApplyOnHit(AttackContext context)=>ArcaneChoices.ElementStatus(context.AttackType);
    }

    public sealed class MagePhantomLogic:TowerGuardLogic,ICompletedDamageDealt,INpcCircuitProgress
    {
        public int CircuitProgress{get;private set;}
        public MagePhantomLogic():base(AttackDamageType.Acid){}
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override void OnMatchReset(PlayerState owner,GameManager game)
        {
            CircuitProgress=0;owner.AddPermanentResistance(AttackDamageType.Fire);
            owner.AddPermanentResistance(AttackDamageType.Cold);owner.AddPermanentImmunity(AttackDamageType.Poison);
        }
        public void OnCompletedDamage(AttackContext attack,GameManager game)
        {
            if(attack.IsStatusDamage||attack.IsReflected||attack.IsSpecialAttack||attack.IsNecromancyFreeAttack
                ||attack.ActualHealthDamage<=0||!attack.Attacker.IsAlive)return;
            if(++CircuitProgress<3)return;CircuitProgress=0;
            int hp=attack.Attacker.Health;attack.Attacker.Heal(3);
            game?.NoteSkill(attack.Attacker,"汲能回路","回复"+(attack.Attacker.Health-hp)+"生命，进度归0");
        }
    }
}
