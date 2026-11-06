using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public static partial class CampaignContent
    {
        private static CampaignSpec ChapterOne(int n)
        {
            var s=new CampaignSpec(n,AdventureChapter.PlayerDefinition(n),new[]{1,1,3,1});
            switch(n)
            {
                case 1:
                    s.Npcs[1].Name="老兵";s.Npcs[1].Maximum=24;s.Npcs[1].Health=12;
                    s.Op(0,0,1,0,"左1＋老兵左3＝4，先看数字变化，再选择老兵确认攻击。");
                    s.Op(1,0,0,1,"观察老兵3＋你的1＝4，玩家20→16。",0);
                    s.Op(0,1,1,0,"右1＋老兵左4＝5，形成完整盾，护盾会替你挡下下一次攻击。");
                    s.Op(1,1,0,1,"老兵1＋你的盾5＝6；盾挡一次后破碎。",0);
                    s.Op(0,1,1,0,"碎盾5＋老兵左4＝9，回复；超过上限的部分不保留。");
                    s.Op(1,1,0,0,"老兵6＋你的4＝10→1，无效果。接下来，试着抓住老兵的破绽。",0);break;
                case 2:
                    s.Hands=new[]{2,1,2,3};s.Npcs[1].Maximum=40;
                    s.Op(0,0,1,0,"2＋2＝4，普通攻击造成5。");
                    s.Op(1,0,0,1,"教官2＋1＝3，保留右手3。",0);
                    s.Op(0,1,1,1,"1＋3＝4，重击造成7，附物理易伤两回合。");
                    s.Wait(1,"教官休整，左手调整为4。").Prepare=(l,p)=>l.Tune(p,p.LeftHand,4);
                    s.Op(0,0,1,0,"4＋4＝8，攻防结算后9，易伤令伤害变为14。");
                    s.Wait(1,"教官经过第二个自身回合，物理易伤到期。");
                    s.Op(0,1,1,0,"再算8，易伤到期后恢复为9伤。");break;
                case 3:
                    s.Hands=new[]{2,1,3,5};s.Opening=l=>Shield(l.Seats[1],1);s.Npcs[1].Shield=true;
                    s.Op(0,0,1,0,"2＋3＝5，先合盾。");
                    s.Op(1,0,0,1,"教官3＋1＝4，自己盾碎时发动破阵强袭，选择教官完整盾。",0).ChainSeat=1;
                    s.Op(0,1,1,0,"1＋4＝5；合盾并修复另一只碎盾。");
                    s.Op(1,1,0,0,"教官盾5＋己5＝1，教官主动使用自己的盾。",0);
                    s.Op(0,0,1,1,"完整5＋1＝6，主动碎盾命中附流血；保留另一面盾，不用无畏冲锋。");
                    s.Op(1,0,0,0,"流血扣1；教官4＋6＝1，观察攻防减值。",0);break;
                case 4:
                    s.Hands=new[]{1,1,3,4};s.Opening=l=>l.Game.ConfigureSeatObstacles(1,1);
                    s.Op(0,0,1,0,"1＋3＝4，但双方距离2、射程1；高亮【放弃攻击】保留结果。");
                    s.Op(1,0,0,0,"教官3＋4＝7；石头只占位，不行动。",0);
                    s.Op(0,1,1,1,"1＋4＝5，保留完整盾作为无畏冲锋资源。");
                    s.Op(1,0,0,0,"教官7＋4＝1；距离仍为2。",0);
                    s.Op(0,0,1,1,"4＋4＝8，选择无畏冲锋，先碎另一面盾，本次距离∞再攻击。距离仍不够时，可以推开路障。").Charge=true;break;
                case 5:s.Npcs[1].Maximum=26;break;
                case 6:
                    s.Hands=new[]{2,3,8,5};s.Npcs[1].Maximum=32;s.Npcs[1].FreeAttack=1;s.Npcs[1].Shield=true;s.Opening=l=>Shield(l.Seats[1],1);
                    s.Op(0,1,1,1,"3＋盾5＝8，穿透箭造成8，完整盾仍在。");
                    s.Op(1,1,0,1,"教官完整5＋8＝3，主动破盾。",0);
                    s.Op(0,1,1,0,"8＋8＝6，强力射击基础+2，造成8。");
                    s.Op(1,1,0,0,"教官3＋2＝5，重新合成完整盾。",0);
                    s.Op(0,1,1,0,"6＋8＝4，齐射第一段破盾、第二段造成4；再找机会追击教官。");break;
                case 7:
                    s.Hands=new[]{1,4,2,3};s.Npcs[1].Maximum=36;s.Npcs[1].Range=2;s.Npcs[1].Shield=true;s.Opening=l=>l.Game.ConfigureSeatObstacles(1,1);
                    s.Op(0,0,1,1,"1＋3＝4，卓越战技选择流血，观察回合开始扣血与攻防。 ").Maneuver=StatusEffect.Bleed;
                    s.Op(1,1,0,0,"教官3＋4＝7，推进流血时间。",0);
                    s.Op(0,0,1,0,"4＋2＝6，选择跛脚，教官距离2减到1。").Maneuver=StatusEffect.Limp;
                    s.Op(1,1,0,1,"教官7＋4＝1；跛脚不禁止运算。",0);
                    s.Op(0,1,1,0,"4＋2＝6，选择震慑；仍能运算，但不能攻击。").Maneuver=StatusEffect.Stunned;
                    s.Op(1,0,0,0,"教官2＋6＝8，震慑使其放弃攻击，保留8。",0);
                    s.Op(0,0,1,0,"6＋8＝4，选择倒伏；不放大施加它的这一击。").Maneuver=StatusEffect.KnockedDown;
                    s.Wait(1,"教官休整，倒伏仍在。");
                    s.Op(0,1,1,0,"6＋8＝4，选择不附加，观察倒伏增伤，接下来试着自行运用这些招式。").Maneuver=StatusEffect.None;break;
                case 8:
                    s.Hands=new[]{2,4,2,3};s.Npcs[1].Maximum=30;s.Npcs[1].FreeAttack=1;
                    s.Op(0,0,1,0,"2＋2＝4得到双4；发动双刃连斩，先追加4及流血，再主攻击7，总11。").TargetSeat=1;break;
                case 9:
                    s.Hands=new[]{1,4,3,6};s.Npcs[1].Maximum=32;s.Npcs[1].FreeAttack=1;
                    s.Opening=l=>l.Player.SetHealthExact(16);
                    s.Op(0,0,1,0,"选择加法，1＋3＝4。").Option=0;
                    s.Op(1,0,0,0,"教官3＋4＝7，右手仍是6。",0);
                    s.Op(0,1,1,1,"选择减法，4−6＝−2，记为8。").Option=1;
                    s.Wait(1,"教官把左手调整为4。").Prepare=(l,p)=>l.Tune(p,p.LeftHand,4);
                    s.Op(0,0,1,0,"选择减法，4−4＝0，记为1。").Option=1;
                    s.Wait(1,"教官把左手调整为2。").Prepare=(l,p)=>l.Tune(p,p.LeftHand,2);
                    s.Op(0,0,1,0,"选择减法，1−2＝−1，记为9并回复。").Option=1;break;
                case 10:
                    s.Role=CharacterDefinition.Champion;s.Npcs[1]=new CampaignNpcSpec("哨站骑士",AdventureChapter.KnightId,40){Attack=2,Defense=1,Range=2,Shield=true,Rally=true};break;
            }
            return s;
        }
    }
}
