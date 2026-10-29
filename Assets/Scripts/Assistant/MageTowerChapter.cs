using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>全局11～20关的数据与独立试验；NPC不进入竞技角色名单。</summary>
    public static class MageTowerChapter
    {
        public const string CompletionKey="FingerDiceDuel.Adventure.Chapter2.Completed";
        public const string GuardId="mage_tower_guard", PhantomId="mage_tower_phantom";
        public static readonly CharacterDefinition Guard=new CharacterDefinition(GuardId,"奥法护卫",SpecialLogicType.None);
        public static readonly CharacterDefinition Phantom=new CharacterDefinition(PhantomId,"法师幻影",SpecialLogicType.None,0,0,32,
            "强酸攻击，距离∞。永久火焰、寒冷抗性，毒素免疫。每3次普通攻击实际伤人后回复3生命。","Mage Phantom");
        public static IReadOnlyList<CharacterDefinition> FinaleRoster
        {
            get { var roles=new List<CharacterDefinition>();foreach(var r in CharacterDefinition.BuiltIn)
                if(r.Profession?.Id=="wizard"&&CharacterFactory.IsRegistered(r.Logic))roles.Add(r);return roles; }
        }
        public static CharacterDefinition PlayerRole(int level)
        {
            switch(level)
            {
                case 11:return CharacterDefinition.EldritchKnight;
                case 12:return CharacterDefinition.SchoolOfEvocation;
                case 13:return CharacterDefinition.SchoolOfConjuration;
                case 14:return CharacterDefinition.SchoolOfAbjuration;
                case 15:return CharacterDefinition.SchoolOfEnchantment;
                case 16:return CharacterDefinition.SchoolOfNecromancy;
                case 17:return CharacterDefinition.SchoolOfDivination;
                case 18:return CharacterDefinition.SchoolOfIllusion;
                case 19:return CharacterDefinition.SchoolOfTransmutation;
                default:throw new ArgumentOutOfRangeException(nameof(level));
            }
        }
        public static string Hint(int level)=>new[]{"奥法骑士 · 碎盾附魔 / 法术回响","塑能 · 十种法术 / 元素反应",
            "咒法 · 力场 / 王车易位","防护 · 持续抗性 / 屏障 / 易伤与免疫","惑控 · 失能 / 魅惑 / 受伤解除",
            "死灵 · 抵挡 / 免费暗蚀 / 不死奴仆","预言 · 预兆记录 / 预知未来","幻术 · 幻术默发 / 伤害减半",
            "变化 · 无序匹配 / 变羊 / 溢伤","法师选角 · 经典先攻 / 幻影试炼"}[level-11];
        public static string SampleKey(int level,string key)=>"FingerDiceDuel.Adventure.Chapter2.Level"+level+".Sample."+key+".Completed";

        public static IReadOnlyList<MageTowerSample> Samples(int level)
        {
            var list=new List<MageTowerSample>();
            switch(level)
            {
                case 11: Knight(list);break;
                case 12: Evocation(list);break;
                case 13: Conjuration(list);break;
                case 14: Abjuration(list);break;
                case 15: Enchantment(list);break;
                case 16: Necromancy(list);break;
                case 17: Divination(list);break;
                case 18: Illusion(list);break;
                case 19: Transmutation(list);break;
                case 20:list.Add(new MageTowerSample("finale","高塔的回声",new[]{1,1,1,1}));break;
                default:throw new ArgumentOutOfRangeException(nameof(level));
            }
            return list;
        }
        private static void Knight(List<MageTowerSample> list)
        {
            var a=new MageTowerSample("shield","碎盾附魔",new[]{5,2,1,3},"让一面完整盾成为运算源；附魔先于本次攻击生效。")
                .Turn(0,0,1,0,"己左盾5＋护卫左1＝6。点【火焰】，再确认护卫为攻击目标；本击立即附魔，造成6火焰并附燃烧。")
                .Turn(1,0,0,1,"护卫左1＋己右2＝3，无效果。其回合结束燃烧3：生命14→11。盾已碎，附魔已消耗。");
            a.Prepare=g=>g.PlayerA.LeftHand.SetShieldState(ShieldState.Formed);
            a.Verify=l=>l.Soldier.Health==11&&l.Board.LastTurn?.Actor==l.Soldier;
            a.Conclusion="完整盾作运算源，也会先触发附魔。本击6火焰＋燃烧3，共9伤；普通物理伤害不会自动附燃烧。";list.Add(a);
            var b=new MageTowerSample("echo","法术回响",new[]{1,2,3,5},"受到实际魔法伤害后，可记录其类型；记录覆盖旧附魔，下次发起攻击消耗。")
                .Turn(1,0,0,0,"护卫左3＋己左1＝4，光耀使你20→16并致盲。点【记忆·光耀】，保留下一击的类型。")
                .Turn(0,1,1,0,"己右2＋护卫左4＝6。确认攻击：6光耀，护卫20→14并致盲。光耀不是圣光状态。");
            b.FirstSeat=1;b.GuardType=AttackDamageType.Radiant;
            b.Verify=l=>l.Board.LastTurn?.AttackType==AttackDamageType.Radiant&&l.Soldier.Health==14;list.Add(b);
        }
        private static void Evocation(List<MageTowerSample> list)
        {
            var types=new[]{DamageType.Fire,DamageType.Cold,DamageType.Lightning,DamageType.Thunder,DamageType.Acid,
                DamageType.Poison,DamageType.Radiant,DamageType.Necrotic,DamageType.Psychic,DamageType.Force};
            var explains=new[]{"燃烧：目标回合结束失去3生命。","寒冷先附覆霜；其运算结果减半取整，至少为1，不直接冻结。",
                "感电：目标回合开始，自身及相邻角色各受1点闪电。","残响：限制攻击距离，不限制借用加数。",
                "强酸命中后防−2，首次命中不提前享受这次降防。","中毒：目标回合开始失去2生命。",
                "光耀附目盲，只能借用距离1的角色手牌；与圣光不同。","腐朽：无法回复生命。",
                "心灵随机附恐慌或震慑。恐慌降攻且不能攻击来源；震慑不能发起攻击。","力场无附加负面状态；下一课展示其穿盾能力。"};
            for(int i=0;i<types.Length;i++)
            {
                var type=types[i];string name=type.ToString();
                var s=new MageTowerSample(name,"塑能 · "+AttackDamageTypes.NameOf(AttackDamageTypes.FromDamageType(type)),new[]{1,2,3,2},explains[i])
                    .Turn(0,0,1,0,"己左1＋护卫左3＝4。选择【放弃本次攻击】，再选【"+AttackDamageTypes.NameOf(AttackDamageTypes.FromDamageType(type))+"】。此步只存类型，不伤人。")
                    .Turn(1,0,0,0,"护卫左3＋己左4＝7，无效果；下一击使用刚选择的类型。")
                    .Turn(0,0,1,1,"己左4＋护卫右2＝6。选择继续攻击，确认护卫。"+explains[i]);
                s.CastingType=type;
                if(type==DamageType.Fire)s.Turn(1,0,0,0,"护卫左7＋己左6＝3。回合结束燃烧3，护卫14→11。");
                if(type==DamageType.Cold)s.Turn(1,1,0,0,"护卫右2＋己左6＝8→覆霜减半4；实际攻击造成5伤，你20→15。");
                if(type==DamageType.Necrotic)s.Turn(1,0,0,1,"护卫左7＋己右2＝9，本应回复；腐朽使实际回复0，仍为14生命。");
                if(type!=DamageType.Fire&&type!=DamageType.Cold&&type!=DamageType.Necrotic)
                    s.Turn(1,0,0,0,"护卫再行动一次：左7＋己左6＝3，无攻击／回复／合盾。"+
                        (type==DamageType.Radiant?"目盲只允许借距离1的角色手牌；本样本双方相邻，仍可借左6。":
                         type==DamageType.Lightning?"回合开始已感电：护卫和相邻的你各失去1生命。":
                         type==DamageType.Poison?"回合开始中毒失去2生命，护卫14→12。":
                         type==DamageType.Thunder?"残响仍限制攻击距离为1，状态剩余回合递减。":
                         type==DamageType.Acid?"强酸使防御−2；查看名字旁的状态与防御值。":
                         type==DamageType.Psychic?"恐慌会降攻且不能攻击来源，震慑会禁止攻击；均可进行这次无效果运算。":"力场没有附加负面状态；与前面的元素对照。"));
                s.Verify=l=>HasTypeInLog(l,type);list.Add(s);
            }
            var reserve=new MageTowerSample("reserve_ward","待用类型与持续抗性",new[]{1,2,3,2},"塑能距离∞。存储的法术类型在消耗前赋予对应抗性；受击不会消耗待用类型。")
                .Turn(0,0,1,0,"己左1＋护卫左3＝4，放弃攻击并选择火焰；获得待用火焰类型和火焰抗性。")
                .Turn(1,1,0,0,"护卫右2＋己左4＝6火焰：原伤7减半为4，并附1回合燃烧。")
                .Turn(0,0,1,0,"己左4＋护卫左3＝7，无攻击；回合结束燃烧3经火焰抗性减为2。待用类型和抗性仍在。")
                .Turn(1,1,0,1,"护卫右6＋己右2＝8火焰：原伤9减半为5。第二次受击后抗性仍在；之后发起待用类型的攻击才消耗它。");
            reserve.CastingType=DamageType.Fire;reserve.GuardType=AttackDamageType.Fire;
            reserve.Verify=l=>l.Player.Health==9&&l.Player.HasResistance(AttackDamageType.Fire)&&((FingerDiceDuel.Characters.SpecialLogics.SchoolOfEvocationLogic)l.Player.Character).HasPendingType;list.Add(reserve);
            Reaction(list,"freeze",DamageType.Cold,StatusEffect.Frost,"寒冷＋覆霜","清除覆霜／潮湿，附冻结2回合。冻结使双手无法行动；点击跳过回合继续，不会判为败北。",
                l=>l.Soldier.HasStatus(StatusEffect.Frozen));
            Reaction(list,"steam",DamageType.Fire,StatusEffect.Wet,"火焰＋潮湿","本击6×1.5＝9；清除潮湿与覆霜，不附燃烧。",
                l=>l.Soldier.Health==11&&!l.Soldier.HasStatus(StatusEffect.Burn));
            Reaction(list,"conduct",DamageType.Lightning,StatusEffect.Wet,"闪电＋潮湿","本击6×1.5＝9；保留潮湿并附感电。感电在护卫回合开始立即跳电。",
                l=>l.Soldier.HasStatus(StatusEffect.Shock));
        }
        private static bool HasTypeInLog(MageTowerLesson l,DamageType type)
        {
            // LastTurn在观察后续状态时可能已是护卫行动；战报仍保留玩家真实类型。
            foreach(var line in l.Board.Log)if(line.Contains(AttackDamageTypes.NameOf(AttackDamageTypes.FromDamageType(type))))return true;return false;
        }
        private static void Reaction(List<MageTowerSample> list,string key,DamageType type,StatusEffect initial,string title,string text,Func<MageTowerLesson,bool> verify)
        {
            var s=new MageTowerSample(key,title,new[]{1,2,3,2},"训练配置：护卫预置"+StatusNames.Of(initial)+"2回合。"+text)
                .Turn(0,0,1,0,"己左1＋护卫左3＝4，放弃攻击，选【"+AttackDamageTypes.NameOf(AttackDamageTypes.FromDamageType(type))+"】。")
                .Rest(1,"本试验护卫休整一次，保留预置状态；不会在覆霜或潮湿下强行演算错误的7。")
                .Turn(0,0,1,1,"己左4＋护卫右2＝6，继续攻击护卫。"+text);
            s.CastingType=type;
            s.Turn(1,key=="freeze"?0:1,0,0,
                key=="freeze"?"护卫冻结，两只手都不能行动，本回合按普通规则跳过；查看冻结剩余1回合。":
                key=="conduct"?"护卫回合开始感电：自身与相邻角色各受1点闪电。右2＋己左6＝8，潮湿−1变7，无效果；原潮湿随后自然到期。":
                "护卫右2＋己左6＝8，正常攻击；潮湿已清除，也没有燃烧，所以回合结束不受燃烧伤害。");
            s.Prepare=g=>g.PlayerB.ApplyStatus(initial,2);
            s.Introduction="训练配置：护卫预置"+StatusNames.Of(initial)+"2回合，准备期休整后剩1回合。"+text;
            s.Verify=verify;list.Add(s);
        }
        private static void Conjuration(List<MageTowerSample> list)
        {
            var a=new MageTowerSample("force","次级咒术",new[]{3,2,1,5},"护卫防+3且有完整盾。攻0的普通4只能造成1；咒法攻−1的力场4仍造成3，且盾保留。")
                .Turn(0,0,1,0,"己左3＋护卫左1＝4。确认攻击；无视正防御、盾牌与闪避，护卫20→17，盾不破。");
            a.GuardDefense=3;a.Prepare=g=>g.PlayerB.RightHand.SetShieldState(ShieldState.Formed);
            a.Verify=l=>l.Soldier.Health==17&&l.Soldier.RightHand.IsShield;list.Add(a);
            var b=new MageTowerSample("castle","王车易位",new[]{4,6,1,3,2,8,6,4},"座位1你、2护卫甲、3护卫乙、4协作护卫；1与4同队。你的4/6与队友6/4无序匹配。")
                .Rest(0,"点击【王车易位】，可先取消查看不消耗行动；再选择协作护卫交换。对甲距离1→2，对乙2→1，队友关系不变。")
                .Rest(1,"本轮沿用换位前顺序：先护卫甲休整。看高亮行动者，而非物理座位编号。")
                .Rest(2,"接着护卫乙休整。距离已经更新，行动顺序要等下一轮。")
                .Rest(3,"最后协作护卫休整。本轮结束。")
                .Rest(3,"新一轮从新座位1的协作护卫开始；后续为甲→乙→你。")
                .Rest(1,"新顺序：护卫甲休整。")
                .Rest(2,"新顺序：护卫乙休整，随后轮到新座位4的你。");
            b.Verify=l=>l.Game.SeatOf(l.Player)==4&&l.Game.DistanceBetween(l.Player,l.Seats[1])==2
                &&l.Game.DistanceBetween(l.Player,l.Seats[2])==1&&l.Game.IsTeammate(l.Player,l.Seats[3]);list.Add(b);
        }
        private static void Abjuration(List<MageTowerSample> list)
        {
            foreach(bool fire in new[]{false,true})
            {
                var s=new MageTowerSample(fire?"fire_resist":"physical_resist",fire?"抗性仍附状态":"物理整组抗性",new[]{1,3,1,3},"算出2后先选自己，再选"+(fire?"火焰":"物理整组")+"；防−1，6原伤7减半向上取整为4。")
                    .Turn(0,0,1,0,"己左1＋护卫左1＝2。目标选自己；类型选"+(fire?"火焰":"物理整组")+"。物理包含挥砍、钝击、穿刺。")
                    .Turn(1,1,0,1,"护卫右3＋己右3＝6，你20→16；法术抗性仍保留，直到你死亡或再次施法。"+(fire?"抗性令燃烧从2回合减为1回合。":"三种物理抗性均保留。"));
                s.GuardType=fire?AttackDamageType.Fire:AttackDamageType.Slashing;s.ResistanceType=fire?AttackDamageType.Fire:AttackDamageType.Bludgeoning;
                s.Verify=l=>l.Player.Health==16&&l.Player.HasResistance(fire?AttackDamageType.Fire:AttackDamageType.Slashing);list.Add(s);
            }
            var sustained=new MageTowerSample("sustained","抗性持续与重新赋予",new[]{1,1,1,3},"法术抗性不因受击消耗；再次发动会先撤销上次赋予，再设定本次目标与类型。")
                .Turn(0,0,1,0,"己左1＋护卫左1＝2，选择自己与物理整组。")
                .Turn(1,1,0,0,"护卫右3＋己左2＝5，形成护盾；你的物理抗性仍保留。")
                .Turn(0,1,1,0,"己右1＋护卫左1＝2，再次施法：目标改为护卫，类型改为火焰。你的物理抗性立即撤销，护卫获得持续火焰抗性。");
            sustained.ResistanceTargetSeat=0;
            sustained.Verify=l=>!l.Player.HasResistance(AttackDamageType.Slashing)&&l.Soldier.HasResistance(AttackDamageType.Fire);list.Add(sustained);
            var b=new MageTowerSample("barrier","存储奥术守御",new[]{3,2,1,2},"放弃4/6/8攻击可存对应屏障；抗性之后吸收，使用后整份清空。")
                .Turn(0,0,1,0,"己左3＋护卫左1＝4，点【存储奥术守御】。本击不攻击，标记显示4。")
                .Turn(1,1,0,0,"护卫右2＋己左4＝6。待承伤7−屏障4＝3，你20→17，屏障归0。");
            b.Verify=l=>l.Player.Health==17&&Barrier(l)==0;list.Add(b);
            var c=new MageTowerSample("combined","先抗性，后屏障",new[]{1,3,1,2},"分别取得物理抗性与4点屏障，观察屏障消耗、法术抗性仍保留。")
                .Turn(0,0,1,0,"己左1＋护卫左1＝2，给自己物理整组抗性。")
                .Turn(1,0,0,0,"护卫左1＋己左2＝3，无效果。")
                .Turn(0,0,1,1,"己左2＋护卫右2＝4，存4点屏障。")
                .Turn(1,1,0,0,"护卫右2＋己左4＝6，原伤7→抗性4→屏障0。生命仍20，屏障清空、物理抗性保留。");
            c.Verify=l=>l.Player.Health==20&&Barrier(l)==0&&l.Player.HasResistance(AttackDamageType.Slashing);list.Add(c);
            var d=new MageTowerSample("surplus","屏障余量不保留",new[]{5,2,1,2},"开场5只是数字，不是盾。屏障超过来伤时，也会整份消耗。")
                .Turn(0,0,1,0,"己左5＋护卫左1＝6，存6点屏障。")
                .Turn(1,1,0,1,"护卫右2＋己右2＝4，原伤5全部吸收；6点屏障归0，不剩1。");
            d.Verify=l=>l.Player.Health==20&&Barrier(l)==0;list.Add(d);
            for(int mode=0;mode<4;mode++)
            {
                int m=mode;var labels=new[]{"易伤","抗性","易伤与抗性","免疫"};
                var s=new MageTowerSample("affinity_"+m,labels[m],new[]{1,2,3,1},"开场训练属性为火焰"+labels[m]+"，并非法师的新技能。护卫先攻4，防−1使待承伤为5。")
                    .Turn(1,0,0,0,"护卫左3＋己左1＝4，火焰待承伤5。"+new[]{"易伤5→8，生命12。","抗性5→3，生命17。","先易伤5→8，再抗性8→4，生命16。","免疫归0，也不附燃烧；生命20。"}[m]);
                s.FirstSeat=1;s.GuardType=AttackDamageType.Fire;s.Prepare=g=>{if(m==0||m==2)g.PlayerA.AddVulnerability(AttackDamageType.Fire);if(m==1||m==2)g.PlayerA.AddResistance(AttackDamageType.Fire);if(m==3)g.PlayerA.AddImmunity(AttackDamageType.Fire);};
                s.Verify=l=>l.Player.Health==new[]{12,17,16,20}[m];list.Add(s);
            }
        }
        private static int Barrier(MageTowerLesson l)=>CharacterBehaviors.FirstImplementer<IAbjurationSkill>(l.Player)?.BarrierPoints??0;
        private static void Enchantment(List<MageTowerSample> list)
        {
            var s=new MageTowerSample("control","失能、受伤解除与魅惑",new[]{1,2,3,2},"护卫攻+2。失能使全部攻击伤害归0；之后实际受伤才清除新控制。魅惑强制随机合法行动。")
                .Turn(0,0,1,0,"己左1＋护卫左3＝4，护卫20→16。惑控选【失能】。")
                .Turn(1,1,0,0,"护卫右2＋己左4＝6。即使攻+2、你防−1，失能仍令伤害0。")
                .Turn(0,1,1,1,"己右2＋护卫右6＝8，护卫16→8，旧失能被实际掉血解除；本次选【不发动】。")
                .Turn(1,1,0,1,"护卫右6＋己右8＝4，造成7伤，你20→13；受到伤害后选【魅惑】给来源护卫。")
                .Turn(0,0,1,0,"己左4＋护卫左3＝7，无效果，保留护卫魅惑。")
                .Rest(1,"护卫受魅惑，随机执行合法行动。观察随机源与加数；此后暂停，不继续追打。");
            s.GuardAttack=2;s.Verify=l=>l.Player.IsAlive&&string.Join("|",l.Board.Log).Contains("失能")&&string.Join("|",l.Board.Log).Contains("魅惑");
            // 魅惑由内核重写AI选择，最后一拍必须真的行动，不能以教学休整绕开。
            s.Turns[s.Turns.Count-1]=new MageTowerTurn(1,0,0,0,"护卫受魅惑，随机执行一次合法行动，随后试验结束。若再次出现惑控选择，选不发动。");list.Add(s);
        }
        private static void Necromancy(List<MageTowerSample> list)
        {
            var a=new MageTowerSample("block","不死适应与免费暗蚀",new[]{2,3,3,8},"你预置14/20生命，护卫先攻。2可变1挡一次非力场；回合开始有1自动询问攻击目标。")
                .Turn(1,0,0,1,"护卫左3＋己右3＝6。直接点自己的左2，变1挡住攻击。你仍14生命。随后免费暗蚀选护卫，使其20→19并腐朽。")
                .Turn(0,1,1,0,"己右3＋护卫左6＝9，回复6，你14→20。免费暗蚀没有消耗正常行动。")
                .Turn(1,1,0,0,"护卫右8＋己左1＝9，腐朽阻止回复，仍19生命。下一次免费暗蚀可以选【放弃攻击】。");
            a.FirstSeat=1;a.Prepare=g=>g.PlayerA.SetHealthExact(14);
            a.Verify=l=>l.Player.Health==20&&l.Soldier.Health==19;list.Add(a);
            var b=new MageTowerSample("exchange","不死奴仆",new[]{1,3,6,8,8,2,1,1},"公开2v2。护卫甲仅1生命，乙仍存活，所以死亡后不结束比赛；两次交换均可放弃。")
                .Turn(0,1,2,0,"回合开始免费暗蚀选1生命的护卫甲。不死奴仆：点己右3，再点死者右8，交换为1/8；结束选择。随后己右8＋护卫乙左8＝6，确认攻击乙。正常行动仍可用。");
            b.Prepare=g=>g.PlayerB.SetHealthExact(1);
            b.Verify=l=>!l.Seats[1].IsAlive&&l.Player.RightHand.Value==6&&l.Seats[1].RightHand.Value==3&&!l.Game.IsGameOver;list.Add(b);
            var c=new MageTowerSample("force","力场不可不死适应",new[]{2,3,3,2},"面对力场，不显示无效的2→1抵挡选项。")
                .Turn(1,0,0,1,"护卫左3＋己右3＝6，力场造成7伤，你20→13；左2不消耗。");
            c.FirstSeat=1;c.GuardType=AttackDamageType.Force;c.Verify=l=>l.Player.Health==13&&l.Player.LeftHand.Value==2;list.Add(c);
        }
        private static void Divination(List<MageTowerSample> list)
        {
            foreach(int recorded in new[]{4,5})
            {
                int value=recorded;
                var a=new MageTowerSample(value==4?"portent_attack":"portent_shield",value==4?"预兆与改写攻击":"预知未来：改写为盾",new[]{3,value,1,4},"打出7自动记录另一只手；每个数字各保留一份，没有回合时限。持有7时可消耗记录改写结果。")
                    .Turn(0,0,1,1,"己左3＋护卫右4＝7，预兆记录另一手"+value+"。名字旁可查看已存数字。")
                    .Turn(1,0,0,0,"护卫左1＋己左7＝8。数字先显示8，再选择【替换为"+value+"】，消耗该记录。"+(value==4?"护卫改为打出4，造成5伤，你20→15。":"护卫改为打出5，形成盾牌，不再攻击。"));
                a.Verify=l=>l.Soldier.LeftHand.Value==value&&l.Player.Health==(value==4?15:20)&&CharacterBehaviors.FirstImplementer<IPortentBook>(l.Player).Portents.Count==0&&(value!=5||l.Soldier.LeftHand.IsShield);
                list.Add(a);
            }
            var b=new MageTowerSample("portent_heal","保留记录与改写自身",new[]{3,9,1,4},"持有7时可改写任何角色的运算结果，包括自己；也可不发动而保留预兆。替换发生在数字效果结算前。")
                .Turn(0,0,1,1,"己左3＋护卫右4＝7，记录另一手9。")
                .Turn(1,0,0,0,"护卫左1＋己左7＝8，选择【不发动】保留9；护卫正常攻击，你20→11。")
                .Turn(0,1,1,0,"己右9＋护卫左8＝7；你仍持有左7。选择【替换为9】，右手改9并回复6，体力11→20。预兆9被消耗，不触发原结果7的记录。");
            b.Verify=l=>l.Player.Health==20&&l.Player.RightHand.Value==9&&CharacterBehaviors.FirstImplementer<IPortentBook>(l.Player).Portents.Count==0;list.Add(b);
        }
        private static void Illusion(List<MageTowerSample> list)
        {
            for(int variant=0;variant<2;variant++)
            {
                int v=variant;var s=new MageTowerSample("redirect_"+v,v==0?"幻术默发 · 改算":"幻术默发 · 保留加数",new[]{2,4,2,5},"持有2时，看到对方准备用哪只手触碰你的哪只手，再选发动改用另一只合法手，或不发动保留原加数。")
                    .Turn(1,0,0,1,v==0?"护卫左2准备触碰己右4。点【发动·改用左手2】：2＋2＝4，造成5伤。若询问幻影实体，选不发动。":"护卫左2准备触碰己右4。点【不发动·保留右手4】：2＋4＝6，造成7伤。若询问幻影实体，选不发动。");
                s.FirstSeat=1;
                s.Verify=l=>l.Player.Health==(v==0?15:13);list.Add(s);
            }
            var b=new MageTowerSample("defense","幻影实体",new[]{3,5,3,1},"3/5差为2，没有2故不触发错位。被攻击时可把较小3改2，伤害减半。")
                .Turn(1,0,0,1,"护卫左3＋己右5＝8，原伤9。点【小手变2 · 减半】；自动较小3→2，9减半向上取整5，你20→15。不必再选手。");
            b.FirstSeat=1;b.Verify=l=>l.Player.Health==15&&l.Player.LeftHand.Value==2;list.Add(b);
        }
        private static void Transmutation(List<MageTowerSample> list)
        {
            var a=new MageTowerSample("sheep","变形与自然恢复",new[]{8,6,6,4},"护卫原为火焰攻击。4/6与6/4无序匹配；变羊后10生命、攻防−2/−2、距离1、无原技能。")
                .Turn(0,0,1,0,"己左8＋护卫左6＝4，护卫20→16。回合结束【变形术】选择护卫，独立羊生命10/10，保存原生命16。")
                .Turn(1,0,0,0,"羊左6＋己左4＝1，无效果；第一次自身回合结束，剩1回合。")
                .Turn(0,1,1,0,"己右6＋羊左1＝7，无效果；本次变形术选不发动，别刷新持续时间。")
                .Turn(1,1,0,0,"羊右4＋己左4＝8，攻−2、防−1后7钝击，你20→13；不附燃烧。第二次自身回合结束解除，恢复原16生命，手值保留1/8。");
            a.GuardType=AttackDamageType.Fire;a.Verify=l=>!l.Soldier.IsPolymorphed&&l.Soldier.Health==16&&l.Player.Health==13;list.Add(a);
            var b=new MageTowerSample("overflow","击破羊身与溢伤",new[]{6,3,1,2},"开场样本：护卫原生命16/20，已变羊且羊身受伤至4/10。此为明确训练配置。")
                .Turn(0,0,1,1,"己左6＋羊右2＝8，羊防−2，造成10。羊身抵4，溢出6扣原生命16→10，不再叠一次攻防。回合结束变形术选不发动。");
            b.Prepare=g=>{g.PlayerB.SetHealthExact(16);g.PlayerB.TryPolymorph();g.PlayerB.SetHealthExact(4);};
            b.Verify=l=>!l.Soldier.IsPolymorphed&&l.Soldier.Health==10;list.Add(b);
        }
    }
}
