namespace FingerDiceDuel.Core
{
    public static class SkillVisualProfiles
    {
        public static string Key(string skill)
        {
            switch(skill)
            {
                case "王车易位":case "幻术默发":case "诡诈换位":case "不死奴仆":return "fx_psychic";
                case "预兆":case "预知未来":return "Effects/portent";
                case "奥术守御":case "法术抗性":case "不死适应":return "Effects/arcane_barrier";
                case "变形术":return "Status/polymorph";
                case "暗杀":case "伏击":case "阴影闪避":return "Overlay/concealed";
                case "影分身":case "渗透劲":return "Effects/hidden_force";
                case "荒野形态":case "解除荒野":return "fx_cold";
                case "星耀形态":case "解除星耀":case "巨龙座":case "圣杯座":return "fx_radiant";
                case "猎人印记":case "猎人标记":return "Effects/hunters_mark";
                case "咒剑诅咒":case "咒缚幽灵":return "Effects/hexblades_curse";
                case "剑歌":return "Effects/bladesong";
                case "战争祭司":return "Status/war_blessing";
                case "虚假生命":return "Effects/false_life";
                case "破阵强袭":return "Overlay/shield_broken";
                case "风暴之怒":return "fx_lightning";
                case "元素反应":return "fx_impact";
                case "燃烧":return "fx_fire";
                case "中毒":return "fx_poison";
                case "流血":return "fx_impact";
                case "感电":return "fx_lightning";
                case "圣光":return "fx_radiant";
                case "预言压制":return "Effects/portent";
                default:return null;
            }
        }
    }
    public partial class GameManager
    {
        public void RecordSkillVisual(PlayerState owner,string skill,PlayerState target=null,SkillVisualData data=null)
        {
            if(!RecordCombatVisuals||owner==null)return;var key=SkillVisualProfiles.Key(skill);if(key==null)return;
            var frame=CaptureVisualFrame();
            _combatVisuals.Add(new CombatVisualEvent(CombatVisualKind.Skill,CombatVisualOutcome.Hit,owner,target??owner,owner,data?.Source??owner.LeftHand,AttackDamageType.None,key,0,0,0,false,false,frame,frame,skill,null,data));
        }
    }
}
