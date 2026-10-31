using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public sealed class CombatStatusIcon
    {
        public string ResourceKey { get; }
        public string Description { get; }
        public string Counter { get; }
        public StatusEffect NegativeStatus { get; }public int Value {get;}public Hand TargetHand {get;}
        public CombatStatusIcon(string key,string description,string counter,StatusEffect negative=StatusEffect.None,int value=-1,Hand targetHand=null)
        { ResourceKey=key;Description=description;Counter=counter;NegativeStatus=negative;Value=value;TargetHand=targetHand; }
    }
    public static class CombatStatusIcons
    {
        public static List<CombatStatusIcon> For(PlayerState player, GameManager game=null)
        {
            var result=new List<CombatStatusIcon>();if(player==null) return result;
            if(CharacterBehaviors.FirstImplementer<FingerDiceDuel.Characters.SpecialLogics.AssassinLogic>(player)?.IsStealthedNow==true)
                result.Add(new CombatStatusIcon("Overlay/stealth","专业渗透 · 潜行：由暗杀技能维持；出手后按角色规则解除。",""));
            if(CharacterBehaviors.FirstImplementer<GloomStalkerLogic>(player)?.IsAmbushing==true)
                result.Add(new CombatStatusIcon("Overlay/concealed","恐惧伏击 · 暗置手不能运算或被借数；由技能条件解除。",""));
            foreach(var hand in player.Hands){string side=ReferenceEquals(hand,player.LeftHand)?"左手":"右手";
                if(hand.IsLocked)result.Add(new CombatStatusIcon("Overlay/locked","守望束缚 · "+side+"被锁，当前不能主动运算。","",targetHand:hand));
                if(hand.IsTargetRestricted)result.Add(new CombatStatusIcon("Status/restrained","大地行者 · "+side+"暂不能作为被加数。","",targetHand:hand));}
            foreach(var book in CharacterBehaviors.Implementers<ILoreRecord>(player))
            {
                var values=new List<string>();for(int n=1;n<=9;n++)if(book.HasRecord(n))values.Add(n.ToString());
                result.Add(new CombatStatusIcon("Subclasses/bard_college_of_lore","逸闻记录 · "+book.RecordCount+" / 9\n已记录："+(values.Count==0?"无":string.Join("、",values))+"\n成长"+book.LoreLevel+"次；消耗对应记录可使本次伤害减半。",book.RecordCount+"/9",value:book.RecordCount));
            }
            if(player.IsPolymorphed)result.Add(new CombatStatusIcon("Status/polymorph","变羊 · 剩余"+player.PolymorphTurns+"回合\n独立10生命，攻防−2/−2、距离1，原技能暂停；击破的溢伤扣原生命。",player.PolymorphTurns.ToString()));
            if(player.IsSummon)result.Add(new CombatStatusIcon("Units/hexbound_specter","咒缚幽灵 · 来源："+player.SummonOwner?.Name+"\n占位计距，AI行动；召唤者死亡或替换时消失，不计胜负存活人数。",""));
            foreach(var spell in CharacterBehaviors.Implementers<IElementEnchantment>(player))
                if(spell.Enchantment!=AttackDamageType.None)result.Add(new CombatStatusIcon("Damage/"+AttackDamageTypes.IconKeyOf(spell.Enchantment),"奥法武器 · 下次攻击："+AttackDamageTypes.NameOf(spell.Enchantment)+"\n攻击发起即消耗，闪避/盾挡也消耗。",""));
            foreach(var song in CharacterBehaviors.Implementers<IBladesong>(player))
                if(song.Singing)result.Add(new CombatStatusIcon("Effects/bladesong","剑歌 · 攻防+1，"+AttackDamageTypes.NameOf(song.SongElement)+"攻击\n后续自身回合须攻击、合盾或实际回复以维持。",""));
            if(game!=null)foreach(var source in game.Players)
                if(source.IsAlive)foreach(var curse in CharacterBehaviors.Implementers<IHexbladeCurse>(source))
                    if(ReferenceEquals(curse.CurseTarget,player)&&curse.CurseTurns>0)result.Add(new CombatStatusIcon("Effects/hexblades_curse","咒剑诅咒 · 来源："+source.Name+"\n该来源对你每段攻击伤害+2；本人击杀后召唤咒缚幽灵。",curse.CurseTurns.ToString()));
            if(game!=null)foreach(var source in game.Players)
                if(source.IsAlive)foreach(var vow in CharacterBehaviors.Implementers<IVowOfEnmity>(source))
                    if(ReferenceEquals(vow.VowTarget,player))result.Add(new CombatStatusIcon("Effects/vow_of_enmity",
                        "仇敌誓言 · 来源："+source.Name+"\n该来源对你伤害+1，对其他角色−1；你每打碎其一面盾，使其可额外纯运算一次。",""));
            if(player.FalseLife>0)result.Add(new CombatStatusIcon("Effects/false_life",
                "虚假生命 · "+player.FalseLife+" / 10\n治疗溢出转化，先于真实生命吸收伤害；直坠噩梦可消耗全部。角标是剩余点数。",player.FalseLife.ToString()));
            foreach(var status in player.Statuses)
                result.Add(new CombatStatusIcon("Status/"+StatusNames.IconKeyOf(status),
                    StatusNames.Of(status)+" · 剩余"+player.StatusTurnsLeft(status)+"回合\n"+StatusNames.Describe(status),
                    player.StatusTurnsLeft(status).ToString(),status));
            foreach(var type in ResistanceNames.All)
                if(player.HasResistance(type)) result.Add(new CombatStatusIcon(ResistanceNames.Key(type),
                    ResistanceNames.Name(type)+(player.HasSustainedResistance(type)?"：技能持续抗性；不因受击消耗，按对应技能条件结束。":player.HasPermanentResistance(type)?"：本场永久抗性，该类型伤害减半，向上取整；攻击附加的负面效果时长−1。":"：该类型伤害减半，向上取整；攻击附加的负面效果时长−1。一次攻击落地后，所有临时抗性、易伤、免疫一起清除。"),""));
            if(player.Character is INpcCircuitProgress phantom)
                result.Add(new CombatStatusIcon("Adventure/npc_mage_phantom","汲能回路 · "+phantom.CircuitProgress+" / 3\n每3次普通攻击实际伤人后回复3生命，腐朽可阻止回复。",phantom.CircuitProgress+"/3",value:phantom.CircuitProgress));
            var barrier=CharacterBehaviors.FirstImplementer<IAbjurationSkill>(player);
            foreach (var type in ResistanceNames.All)
            {
                if (player.HasVulnerability(type)) result.Add(new CombatStatusIcon(DamageAffinityNames.Key(DamageAffinity.Vulnerability, type),
                    DamageAffinityNames.Name(DamageAffinity.Vulnerability, type) + "：该类型伤害×1.5，向上取整。一次攻击落地后，全部临时伤害修正清除。", ""));
                if (player.HasImmunity(type)) result.Add(new CombatStatusIcon(DamageAffinityNames.Key(DamageAffinity.Immunity, type),
                    DamageAffinityNames.Name(DamageAffinity.Immunity, type) + (player.HasPermanentImmunity(type)?"：本场永久免疫，不受该类型伤害及其附加状态。"
                    :type==AttackDamageType.Thunder&&player.HasStatus(StatusEffect.Tinnitus)?"：耳鸣期间免疫，随耳鸣到期或净化解除，不因一次攻击消耗。"
                    :"：不受该类型伤害；魔法伤害对应的附加状态与元素反应也被阻止。一次攻击落地后消耗。"), ""));
            }
            if(barrier?.BarrierPoints>0) result.Add(new CombatStatusIcon("Effects/arcane_barrier",
                "奥术守御 · "+barrier.BarrierPoints+"点\n抗性后吸收伤害，使用即全部消耗。",barrier.BarrierPoints.ToString()));
            var resource=CharacterBehaviors.FirstImplementer<IKiResource>(player);
            if(resource!=null)
                result.Add(new CombatStatusIcon("Effects/ki","气 · "+resource.Ki+" / 6\n初始2、上限6；正常行动后双手相同获得1气，双将共用。",resource.Ki.ToString(),value:resource.Ki));
            if(MonkRules.DrunkenOf(player)!=null)result.Add(new CombatStatusIcon("Effects/drunken_fist","醉拳：所有数字攻击，每段基础1d10；受到攻击每段改为1d5。回合开始消耗1气维持；持续伤害不变。",""));
            if(player.HasResonance)result.Add(new CombatStatusIcon("Effects/hidden_force","暗劲 · 来源："+player.HiddenForceSource?.Name+"\n仅施加者后续首次命中引爆，造成应有伤害一半的真实伤害。无倒计时，不叠加。",""));
            var portentBook=CharacterBehaviors.FirstImplementer<IPortentBook>(player);
            if(portentBook!=null){var values=new List<int>(portentBook.Portents);values.Sort();result.Add(new CombatStatusIcon("Effects/portent","预兆："+(values.Count==0?"无":string.Join("、",values))+"\n持有7时可消耗记录替换角色运算结果。",values.Count.ToString()));}
            if(game!=null)
            {
                var sources=new List<string>();
                foreach(var marker in game.Players)
                    if(marker.IsAlive) foreach(var skill in CharacterBehaviors.Implementers<IHunterMarkSource>(marker))
                        if(skill.IsMarked(player)) {sources.Add(marker.Name);break;}
                if(sources.Count>0) result.Add(new CombatStatusIcon("Effects/hunters_mark",
                    "猎人标记 · 来源："+string.Join("、",sources)+"\n标记没有回合倒计时，由对应猎人的攻击消耗。",
                    sources.Count>1?sources.Count.ToString():""));
            }
            return result;
        }
    }
}
