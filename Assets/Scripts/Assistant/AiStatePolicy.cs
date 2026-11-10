using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;

namespace FingerDiceDuel.Assistant
{
    /// <summary>Read-only decision snapshot. Modifiers never run combat hooks or spend resources.</summary>
    public sealed class AiStateContext
    {
        public PlayerState Actor {get;} public PlayerState Target {get;} public GameManager Game {get;}
        public LegalMove Move {get;} public int Result {get;} public HandEffectType Effect {get;}
        public int ExpectedDamage {get;} public int ExpectedHealing {get;} public bool CanAttack {get;}
        public AiStateContext(PlayerState actor,PlayerState target,GameManager game,LegalMove move,int result,
            HandEffectType effect,int damage,int healing,bool canAttack)
        {Actor=actor;Target=target;Game=game;Move=move;Result=result;Effect=effect;ExpectedDamage=damage;ExpectedHealing=healing;CanAttack=canAttack;}
        public bool IsAttack=>Effect==HandEffectType.Attack4||Effect==HandEffectType.Attack6||Effect==HandEffectType.Attack8;
    }
    /// <summary>One named rule: live predicates plus additive preferences, with an optional move-specific extension.</summary>
    public sealed class AiStateRule
    {
        public string Key {get;} public StatusEffect Status {get;}
        public int Attack {get;} public int Heal {get;} public int Shield {get;} public int Setup {get;} public int Exposure {get;}
        private readonly Func<PlayerState,GameManager,bool> _active;
        private readonly Func<AiStateContext,int> _move;
        public AiStateRule(string key,Func<PlayerState,GameManager,bool> active,int attack=0,int heal=0,int shield=0,
            int setup=0,int exposure=0,Func<AiStateContext,int> move=null,StatusEffect status=StatusEffect.None)
        {Key=key;_active=active;Attack=attack;Heal=heal;Shield=shield;Setup=setup;Exposure=exposure;_move=move;Status=status;}
        public bool Applies(PlayerState player,GameManager game)=>player!=null&&_active(player,game);
        public int MoveAdjustment(AiStateContext c)=>_move?.Invoke(c)??0;
    }
    public static class AiStatePolicy
    {
        private static readonly Dictionary<string,AiStateRule> Registry=new Dictionary<string,AiStateRule>();
        public static IReadOnlyDictionary<string,AiStateRule> Rules=>Registry;
        /// <summary>Register once during startup. Replacing a key updates all roles and modes.</summary>
        public static void Register(AiStateRule rule){if(rule==null||string.IsNullOrEmpty(rule.Key))throw new ArgumentException("A state rule needs a key.");Registry[rule.Key]=rule;}
        public static string KeyOf(StatusEffect state)=>"status:"+(int)state;
        private static void State(StatusEffect state,int attack=0,int heal=0,int shield=0,int setup=0,int exposure=0)
            =>Register(new AiStateRule(KeyOf(state),(p,g)=>p.HasStatus(state),attack,heal,shield,setup,exposure,status:state));
        static AiStatePolicy()
        {
            State(StatusEffect.Resonance,4,8,12,8);
            State(StatusEffect.Acid,0,16,20,0,18);
            State(StatusEffect.Poison,12,18,0,0,8);
            State(StatusEffect.Blind,2,6,8,12);
            State(StatusEffect.Decay,18,-120,18,4);
            State(StatusEffect.Burn,8,10,28,0,8);
            State(StatusEffect.Frost,4,4,12,12);
            State(StatusEffect.Shock,8,14,18,0,8);
            State(StatusEffect.Panic,-6,12,18,8);
            State(StatusEffect.Bleed,4,16,16,0,16);
            State(StatusEffect.Wet,4,4,8,10);
            State(StatusEffect.Frozen,0,0,0,0,24);
            State(StatusEffect.Stunned,-120,16,24,12,8);
            State(StatusEffect.Limp,0,8,12,10,8);
            State(StatusEffect.KnockedDown,4,18,30,0,26);
            State(StatusEffect.Charm,0,0,0,0,10);
            State(StatusEffect.Disabled,-90,16,22,12,8);
            State(StatusEffect.Dizzy,0,0,0,16,12);
            State(StatusEffect.Restrained,4,6,10,12,12);
            State(StatusEffect.Greased,8,10,20,0,12);
            State(StatusEffect.Tinnitus,18,-160,-160,8,30);
            State(StatusEffect.DarknessBlind,-12,12,16,10,12);
            State(StatusEffect.PhysicalVulnerability,4,16,28,0,26);
            State(StatusEffect.Scorch,10,-12,14,0,8);
            State(StatusEffect.Confusion,4,4,8,12);
            State(StatusEffect.HolyLight,10,14,8,4);
            State(StatusEffect.WarBlessing,16,0,6,0,-6);
            State(StatusEffect.BlessedHealing,12,0,6,0,-6);
            State(StatusEffect.KnowledgeBlessing,18,0,4,0,-4);
            Effect("crowd_gambling",(p,g)=>g?.HasCrowdGambling(p)==true);
            Effect("bladesong",(p,g)=>CharacterBehaviors.FirstImplementer<IBladesong>(p)?.Singing==true,12,8,10);
            Effect("drunken",(p,g)=>MonkRules.DrunkenOf(p)!=null,16,0,0,6);
            Effect("polymorph",(p,g)=>p.IsPolymorphed,0,16,18,0,18);
            Effect("wild_shape",(p,g)=>p.IsWildShaped,8,12,8);
            for(int star=1;star<=3;star++){int value=star;Effect("star:"+star,(p,g)=>p.IsWildShaped&&p.WildStar==value,star==1?12:4,star==2?14:0,0,star==3?10:0);}
            Effect("ki",(p,g)=>CharacterBehaviors.FirstImplementer<IMonkResource>(p)!=null,4,0,4,8);
            Effect("wild_charge",(p,g)=>DruidWildShape.Has<IDruidCaster>(p.DruidOriginal)&&p.WildCharges>0,0,0,4,8);
            Effect("beast_soul",(p,g)=>CharacterBehaviors.FirstImplementer<PathOfTheBeastLogic>(p)?.BeastSouls>0,0,4,4,8);
            Effect("lore_records",(p,g)=>CharacterBehaviors.FirstImplementer<ILoreRecord>(p)!=null,0,0,4,8);
            Effect("portents",(p,g)=>CharacterBehaviors.FirstImplementer<IPortentBook>(p)?.Portents.Count>0,4,0,4,8);
            Effect("element_enchantment",(p,g)=>CharacterBehaviors.FirstImplementer<IElementEnchantment>(p)?.Enchantment>AttackDamageType.None,14,0,0,4);
            Effect("arcane_barrier",(p,g)=>CharacterBehaviors.FirstImplementer<IAbjurationSkill>(p)?.BarrierPoints>0,8,4,-6,0,-12);
            Effect("false_life",(p,g)=>p.FalseLife>0,8,0,-4,0,-8);
            Effect("spore_life",(p,g)=>p.SporeLife>0,10,0,-4,0,-10);
            Effect("hidden_force",(p,g)=>p.HasResonance,4,12,18,0,20);
            Effect("stealth",(p,g)=>CharacterBehaviors.FirstImplementer<AssassinLogic>(p)?.IsStealthedNow==true,18,4,0,4,-14);
            Effect("ambush",(p,g)=>CharacterBehaviors.FirstImplementer<GloomStalkerLogic>(p)?.IsAmbushing==true,14,0,4,0,-10);
            Effect("locked_hand",(p,g)=>p.LeftHand.IsLocked||p.RightHand.IsLocked,2,6,8,10,8);
            Effect("protected_addend",(p,g)=>p.LeftHand.IsTargetRestricted||p.RightHand.IsTargetRestricted,0,0,4,6,-4);
            Effect("low_barbarian",(p,g)=>p.Health<=15&&CharacterBehaviors.FirstImplementer<IBarbarian>(p)!=null,12,8,6);
            Effect("shield",(p,g)=>p.HasAnyShield,4,2,-8,4,-15);
            Effect("broken_shield",(p,g)=>p.LeftHand.State==ShieldState.Broken||p.RightHand.State==ShieldState.Broken,0,0,12,2,4);
            Effect("taichi",(p,g)=>p.LeftHand.State==ShieldState.Taichi||p.RightHand.State==ShieldState.Taichi,2,0,4,8,-10);
            Effect("grave_strength",(p,g)=>CharacterBehaviors.FirstImplementer<IGraveStrength>(p) is IGraveStrength grave&&!grave.GraveSpent,4,0,2,0,-8);
            Effect("training_protection",(p,g)=>p.TrainingProtected,6,-6,-4,0,-40);
            Effect("npc_circuit",(p,g)=>p.Character is INpcCircuitProgress circuit&&circuit.CircuitProgress>0,8,0,0,0,6);
            Effect("summon",(p,g)=>p.IsSummon,12,0,0,0,-6);
            Effect("hunter_mark",(p,g)=>HasMark(p,g,"hunter"),4,8,12,0,14);
            Effect("hexblade_curse",(p,g)=>HasMark(p,g,"hex"),4,8,12,0,14);
            Effect("vow",(p,g)=>HasMark(p,g,"vow"),2,6,10,0,10);
            Effect("resistance",(p,g)=>HasAffinity(p,0),8,0,-4,0,-8);
            Effect("vulnerability",(p,g)=>HasAffinity(p,1),0,12,18,0,16);
            Effect("immunity",(p,g)=>HasAffinity(p,2),8,0,-4,0,-10);
        }
        private static void Effect(string key,Func<PlayerState,GameManager,bool> active,int attack=0,int heal=0,int shield=0,int setup=0,int exposure=0)
            =>Register(new AiStateRule("effect:"+key,active,attack,heal,shield,setup,exposure));
        private static bool HasAffinity(PlayerState p,int mode)
        {foreach(var type in ResistanceNames.All)if(mode==0?p.HasResistance(type):mode==1?p.HasVulnerability(type):p.HasImmunity(type))return true;return false;}
        private static bool HasMark(PlayerState p,GameManager g,string kind)
        {
            if(g==null)return false;
            foreach(var source in g.Players)if(source.IsAlive&&!g.IsKnownAlly(p,source))
            {
                if(kind=="hunter"&&CharacterBehaviors.FirstImplementer<IHunterMarkSource>(source)?.IsMarked(p)==true)return true;
                var hex=CharacterBehaviors.FirstImplementer<IHexbladeCurse>(source);
                if(kind=="hex"&&hex?.CurseTarget==p&&hex.CurseTurns>0)return true;
                if(kind=="vow"&&CharacterBehaviors.FirstImplementer<IVowOfEnmity>(source)?.VowTarget==p)return true;
            }
            return false;
        }
        public static int PreviewResult(PlayerState actor,int source,int add,bool subtract=false)
        {
            int value=subtract?Hand.ComputeDifference(source,add):actor.HasStatus(StatusEffect.Confusion)&&source+add>=10?1:Hand.ComputeSum(source,add);
            return GameManager.ModifiedOperationValue(actor,value);
        }
        public static HandValueProfile ApplyHandWeights(HandValueProfile basis,PlayerState owner,GameManager game=null)
        {
            if(owner==null)return basis;
            var copy=basis.Clone();
            foreach(var rule in Registry.Values)if(rule.Applies(owner,game)&&!(rule.Status==StatusEffect.Disabled&&CharacterBehaviors.FirstImplementer<IFixedNormalAttackDamage>(owner)!=null))
            for(int digit=1;digit<=9;digit++)
            {
                var effect=CharacterBehaviors.DisplayedEffectOf(owner,digit);
                bool attack=DivineChoices.IsAttack(effect);
                int delta=attack?rule.Attack:effect==HandEffectType.Shield?rule.Shield:effect==HandEffectType.Heal?rule.Heal:rule.Setup;
                copy.Adjust(digit,delta/3);
            }
            return copy;
        }
        public static int ScoreMove(AiStateContext c)
        {
            if(c.Actor==null)return 0;
            if(!StatusRules.CanAct(c.Actor))return -600;
            if(c.Actor.HasStatus(StatusEffect.Charm))return 0; // Forced random action is not rationalized.
            int score=0;
            foreach(var rule in Registry.Values)if(rule.Applies(c.Actor,c.Game))
            {
                if(rule.Status==StatusEffect.Disabled&&c.IsAttack&&c.ExpectedDamage>0)continue;
                int healWeight=rule.Heal;
                bool selfCleanse=c.Effect==HandEffectType.Heal&&ReferenceEquals(c.Target,c.Actor)&&CharacterBehaviors.FirstImplementer<LifeDomainLogic>(c.Actor)!=null;
                if(c.Effect==HandEffectType.Heal&&healWeight<0&&(selfCleanse||!ReferenceEquals(c.Target,c.Actor)||rule.Status==StatusEffect.Decay&&c.ExpectedHealing>0))healWeight=0;
                score+=c.IsAttack?rule.Attack:c.Effect==HandEffectType.Shield?rule.Shield:c.Effect==HandEffectType.Heal?healWeight:rule.Setup;
                score+=rule.MoveAdjustment(c);
            }
            if(c.IsAttack&&!c.CanAttack)score-=400;
            if(c.Effect==HandEffectType.Heal&&c.ExpectedHealing<=0)score-=240;
            if(c.Effect==HandEffectType.Shield&&c.Actor.HasStatus(StatusEffect.Tinnitus))score-=240;
            if(c.IsAttack&&c.Actor.HasStatus(StatusEffect.Disabled)&&c.ExpectedDamage==0)score-=140;
            if(c.Effect==HandEffectType.Heal&&ReferenceEquals(c.Target,c.Actor)&&CharacterBehaviors.FirstImplementer<LifeDomainLogic>(c.Actor)==null&&c.Actor.HasStatus(StatusEffect.Scorch)&&c.ExpectedHealing>0)
                score-=(c.ExpectedHealing+1)/2*6;
            return Math.Max(-600,Math.Min(300,score));
        }
        public static int ScoreTarget(PlayerState owner,PlayerState target,GameManager game)
        {
            if(owner==null||target==null||ReferenceEquals(owner,target)||game?.IsKnownAlly(owner,target)==true)return 0;
            int score=0;foreach(var rule in Registry.Values)if(rule.Applies(target,game))score+=rule.Exposure;
            if(owner.HasStatus(StatusEffect.DarknessBlind)&&!target.HasStatus(StatusEffect.DarknessBlind))score-=35;
            if(ReferenceEquals(target.HiddenForceSource,owner))score+=30;
            return Math.Max(-160,Math.Min(160,score));
        }
    }
}
