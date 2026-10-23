using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public sealed class CombatStatusIcon
    {
        public string ResourceKey { get; }
        public string Description { get; }
        public string Counter { get; }
        public StatusEffect NegativeStatus { get; }
        public CombatStatusIcon(string key,string description,string counter,StatusEffect negative=StatusEffect.None)
        { ResourceKey=key;Description=description;Counter=counter;NegativeStatus=negative; }
    }
    public static class CombatStatusIcons
    {
        public static List<CombatStatusIcon> For(PlayerState player, GameManager game=null)
        {
            var result=new List<CombatStatusIcon>();if(player==null) return result;
            foreach(var status in player.Statuses)
                result.Add(new CombatStatusIcon("Status/"+StatusNames.IconKeyOf(status),
                    StatusNames.Of(status)+" · 剩余"+player.StatusTurnsLeft(status)+"回合\n"+StatusNames.Describe(status),
                    player.StatusTurnsLeft(status).ToString(),status));
            foreach(var type in ResistanceNames.All)
                if(player.HasResistance(type)) result.Add(new CombatStatusIcon(ResistanceNames.Key(type),
                    ResistanceNames.Name(type)+"：该类型伤害减半，向上取整。一次攻击落地后，所有抗性一起清除。",""));
            var barrier=CharacterBehaviors.FirstImplementer<IAbjurationSkill>(player);
            if(barrier?.BarrierPoints>0) result.Add(new CombatStatusIcon("Effects/arcane_barrier",
                "奥术屏障 · "+barrier.BarrierPoints+"点\n抗性后吸收伤害，使用即全部消耗。",barrier.BarrierPoints.ToString()));
            foreach(var resource in CharacterBehaviors.Implementers<IKiResource>(player))
                result.Add(new CombatStatusIcon("Effects/ki","气 · "+resource.Ki+" / 6\n武僧技能资源；数字为当前气点，不是剩余回合。",resource.Ki.ToString()));
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
