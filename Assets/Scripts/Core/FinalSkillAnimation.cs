namespace FingerDiceDuel.Core
{
    public enum FinalSkillStyle { Portal, Number, Exchange, Ward, Sheep, Specter, Stealth, Clones, HiddenForce, BladeSong, Blessing, Curse, Hunt, Moon, Stars, Counter, Chain, Reaction, Status }
    public sealed class SkillVisualData
    {
        public Hand Source {get;}public Hand Destination {get;}public int Value {get;}public int Previous {get;}public string Variant {get;}public PlayerState DestinationOwner {get;}
        public SkillVisualData(Hand source=null,Hand destination=null,int value=-1,int previous=-1,string variant=null,PlayerState destinationOwner=null)
        {Source=source;Destination=destination;Value=value;Previous=previous;Variant=variant;DestinationOwner=destinationOwner;}
    }
    public static class FinalSkillAnimation
    {
        public static FinalSkillStyle Style(string skill)
        {
            switch(skill)
            {
                case "王车易位":return FinalSkillStyle.Portal;
                case "预兆":case "预知未来":return FinalSkillStyle.Number;
                case "幻术默发":case "诡诈换位":case "不死奴仆":return FinalSkillStyle.Exchange;
                case "奥术守御":case "不死适应":case "法术抗性":return FinalSkillStyle.Ward;
                case "变形术":return FinalSkillStyle.Sheep;
                case "咒缚幽灵":return FinalSkillStyle.Specter;
                case "暗杀":case "伏击":case "阴影闪避":return FinalSkillStyle.Stealth;
                case "影分身":return FinalSkillStyle.Clones;
                case "渗透劲":return FinalSkillStyle.HiddenForce;
                case "剑歌":return FinalSkillStyle.BladeSong;
                case "战争祭司":return FinalSkillStyle.Blessing;
                case "咒剑诅咒":return FinalSkillStyle.Curse;
                case "猎人印记":case "猎人标记":case "灭族者":return FinalSkillStyle.Hunt;
                case "荒野形态":case "解除荒野":return FinalSkillStyle.Moon;
                case "星耀形态":case "解除星耀":case "巨龙座":case "圣杯座":return FinalSkillStyle.Stars;
                case "风暴之怒":return FinalSkillStyle.Counter;
                case "破阵强袭":return FinalSkillStyle.Chain;
                case "元素反应":return FinalSkillStyle.Reaction;
                default:return FinalSkillStyle.Status;
            }
        }
    }
}
