using System;
using FingerDiceDuel.Core;
using UnityEngine;
namespace FingerDiceDuel.UI
{
    public sealed partial class CombatAnimationPlayer
    {
        private Vector2 SkillBodyAnchor(PlayerState player)
        {Vector2 hands=(_hand(player,player.LeftHand)+_hand(player,player.RightHand))*.5f;return Vector2.Lerp(_target(player),hands,.72f);}
        private static Color ElementColor(AttackDamageType type)
        {int rgb=AttackDamageTypes.ColorOf(type);return new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f);}
        private void Orbit(int slot,Vector2 center,string sprite,float radius,float size,int count,float turn,float alpha,Color? tint=null)
        {for(int i=0;i<count;i++){float angle=(360f*i/count+turn)*Mathf.Deg2Rad;Picture(slot+i,sprite,center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,size,alpha,angle*Mathf.Rad2Deg, null,tint);}}
        private void DrawSecondaryPose(SecondarySkillProfile p,CombatVisualEvent e,float t)
        {
            var d=e.SkillData;string variant=d?.Variant??"";
            Vector2 origin=_skillOrigin,target=_skillTarget,from=_skillFrom,to=_skillTo;
            Vector2 badge=_target(e.Actor)+new Vector2(-205,-10);
            badge.x=Mathf.Clamp(badge.x,-_root.rect.width*.5f+85,_root.rect.width*.5f-85);badge.y=Mathf.Clamp(badge.y,-_root.rect.height*.5f+85,_root.rect.height*.5f-85);
            float pulse=Mathf.Sin(t*Mathf.PI),move=Mathf.SmoothStep(0,1,t),outward=variant=="consume"||variant=="expire"?1-move:move;
            Color gold=BattleArt.Gold,green=new Color(.45f,1,.65f),purple=new Color(.76f,.42f,1),red=new Color(1,.3f,.22f),blue=new Color(.35f,.8f,1);
            var type=d?.Element??AttackDamageType.None;Color element=type==AttackDamageType.None?blue:ElementColor(type);
            string fx=type==AttackDamageType.None?"fx_force":AttackDamageTypes.IsMagic(type)?"fx_"+AttackDamageTypes.IconKeyOf(type):type==AttackDamageType.Slashing?"fx_slash":type==AttackDamageType.Piercing?"projectile_arrow":"fx_impact";
            string value=d!=null&&d.Value>=0?d.Value.ToString():"",old=d!=null&&d.Previous>=0?d.Previous.ToString():"";
            if(p.Style==SecondarySkillStyle.Heal&&d?.Value==0)green=new Color(.6f,.65f,.65f);
            switch(p.Style)
            {
                case SecondarySkillStyle.Strike:
                    Picture(0,p.Sprite,from+Vector2.up*30,175+(p.Flavor=="giant"?80:30)*pulse,pulse,-65+90*move);
                    Orbit(1,from,"fx_slash",45+30*move,95,3,150*t,pulse*.7f,p.Flavor=="shadow"?purple:gold);break;
                case SecondarySkillStyle.Arrows:
                    int arrows=p.Flavor=="volley"?2:1;
                    for(int i=0;i<arrows;i++){Vector2 at=from+new Vector2(65*move,(i-(arrows-1)*.5f)*40);Picture(i,"projectile_arrow",at,p.Flavor=="power"?180:140,pulse,0,null, p.Flavor=="piercing"?blue:gold);}
                    Picture(3,"weapon_bow",from+Vector2.left*45,140,pulse,-15+15*move);
                    if(p.Flavor=="piercing")Picture(4,"fx_force",from+Vector2.right*65,100,pulse,90,new Vector2(.3f,1));break;
                case SecondarySkillStyle.Heal:
                    for(int i=0;i<4;i++)Picture(i,"fx_radiant",Arc(origin,target,Mathf.Clamp01(move-i*.06f),65),65,pulse*.55f,i*90,null,green);
                    Orbit(4,target,"fx_radiant",45+20*(1-move),65,4,-100*t,pulse*.5f,green);
                    if(value.Length>0)Digit(0,"+"+value,target+Vector2.up*65,pulse,green);break;
                case SecondarySkillStyle.Cleanse:
                    for(int i=0;i<4;i++){float a=(i*90+30)*Mathf.Deg2Rad;Picture(i,"fx_psychic",target+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(25+100*move),65,pulse*(1-move),i*90,null,Color.gray);}
                    Picture(4,"fx_radiant",target+new Vector2(0,90-180*move),150,pulse,0,new Vector2(1.4f,.35f),green);break;
                case SecondarySkillStyle.Eye:
                    Line(0,origin,target,pulse*.65f*(variant=="expire"?1-move:1));Picture(1,p.Sprite,Arc(origin,target,move,40),100,pulse*(variant=="expire"?1-move:1));
                    Orbit(2,target,"fx_psychic",60,70,3,90*t,pulse*.5f);break;
                case SecondarySkillStyle.Enchant:
                    Vector2 enchanted=p.Flavor=="echo"?Arc(target,from,move,45):from;
                    Picture(0,p.Flavor=="orb"?fx:"weapon_sword",enchanted,p.Flavor=="orb"?125:205,pulse,-65+50*t,null,p.Flavor=="orb"?element:Color.white);
                    Picture(1,fx,enchanted+new Vector2(45,40),115,pulse,120*t,null,element);
                    Orbit(2,enchanted,fx,65*(1-outward),45,4,180*t,pulse*.65f,element);
                    if(variant=="consume")Picture(6,fx,Arc(from,target,move,30),90,pulse,0,null,element);break;
                case SecondarySkillStyle.Surge:
                    Picture(0,fx,from,150,pulse,45*t,null,element);
                    Orbit(1,from,fx,35+50*move,60,4,-160*t,pulse*.5f,element);break;
                case SecondarySkillStyle.Music:
                    Picture(0,p.Sprite,origin,80,pulse);
                    for(int i=0;i<4;i++){Vector2 note=Arc(origin,target,Mathf.Clamp01(move-i*.08f),50+(i%2)*30);Digit(i,"♪",note,pulse,gold);}
                    Picture(1,"fx_radiant",target,140,pulse*.45f,70*t,null,gold);if(value.Length>0)Digit(4,(variant.StartsWith("Weaken")?"−":"+")+value,target+Vector2.down*65,pulse,variant.StartsWith("Weaken")?purple:gold);break;
                case SecondarySkillStyle.Steal:
                    Picture(0,"Hands/hand_right_2",Arc(to,from,move,55),130,pulse,-45*move,null,new Color(1,.85f,.55f));
                    Digit(0,value,Arc(to,from,move,65),pulse);Digit(1,old,Arc(from,to,move,65),pulse,blue);Line(1,to,from,pulse*.4f);break;
                case SecondarySkillStyle.Hand:
                    Picture(0,p.Sprite,from+Vector2.up*45*move,155,pulse,-20+40*move);Orbit(1,from,"fx_force",65,45,4,120*t,pulse*.5f);break;
                case SecondarySkillStyle.Cards:
                    for(int i=0;i<3;i++){Vector2 card=from+new Vector2((i-1)*45,Mathf.Sin(t*Mathf.PI)*30);float flip=Mathf.Max(.12f,Mathf.Abs(Mathf.Cos(t*Mathf.PI)));float angle=(i-1)*25+50*move;Picture(i,"UI/panel_tooltip",card,130,pulse,angle,new Vector2(flip*.65f,1));Picture(i+3,p.Sprite,card,75,pulse,angle,new Vector2(flip,1));}break;
                case SecondarySkillStyle.TaichiBoost:
                    Picture(0,"Overlay/taichi",from,155,pulse,180*t);
                    if(variant=="guard")Picture(1,"fx_force",origin,165,pulse);
                    else {Line(1,origin,target,pulse*.6f);Picture(2,"fx_force",Arc(origin,target,move,65),90,pulse);}
                    if(value.Length>0)Digit(0,value,origin+Vector2.up*65,pulse);break;
                case SecondarySkillStyle.Tether:
                    Vector2 end=d?.Destination==null?target:to;
                    if(p.Flavor=="chain"){float angle=Mathf.Atan2((end-origin).y,(end-origin).x)*Mathf.Rad2Deg;for(int i=0;i<12;i++)Picture(i,"chain_link",Vector2.Lerp(origin,end,i/11f),Mathf.Max(60,(end-origin).magnitude/9f),Mathf.Clamp01(move*14-i)*pulse,angle);}
                    else for(int i=0;i<4;i++)Picture(i,"vine_tendril",Arc(origin,end,Mathf.Clamp01(move*1.5f-i*.12f),25*(i%2==0?1:-1)),180,pulse,-45+90*t);
                    Picture(12,p.Sprite,end,95,pulse,0,Vector2.one*(.7f+move*.4f));break;
                case SecondarySkillStyle.Rage:
                    Orbit(0,origin,"fx_slash",60,90,4,160*t,pulse*.65f,red);Picture(4,p.Sprite,origin,130,pulse*.6f,90*t,null,red);
                    if(value.Length>0)Digit(0,p.Flavor=="frenzy"?"狂乱 "+value:value+"→攻击",origin+Vector2.up*65,pulse,red);break;
                case SecondarySkillStyle.Drain:
                    for(int i=0;i<5;i++)Picture(i,"fx_necrotic",Arc(target,origin,Mathf.Clamp01(move-i*.05f),35),70,pulse*.65f,0,null,Color.Lerp(red,green,move));
                    if(value.Length>0)Digit(0,"+"+value,origin+Vector2.up*60,pulse,green);break;
                case SecondarySkillStyle.Dice:
                    float spin=p.Flavor=="forced"?0:180*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.48f)));
                    Picture(0,p.Sprite,from,155,pulse,spin,new Vector2(.7f+.3f*move,1));
                    if(p.Flavor=="forced"){Orbit(1,from,"fx_radiant",75,45,3,90*t,pulse*.7f);Digit(0,"必赌",from+Vector2.up*75,pulse,gold);}
                    else if(t>.35f){Vector2 face=new Vector2(-2*(.7f+.3f*move),-9);float radians=spin*Mathf.Deg2Rad;face=new Vector2(face.x*Mathf.Cos(radians)-face.y*Mathf.Sin(radians),face.x*Mathf.Sin(radians)+face.y*Mathf.Cos(radians));Digit(0,value,from+face,pulse,gold);_finalNumbers[0].fontSize=34;_finalNumbers[0].rectTransform.localRotation=Quaternion.Euler(0,0,spin);}break;
                case SecondarySkillStyle.Growth:
                    Picture(0,p.Sprite,badge,105,pulse,0,Vector2.one*(.8f+.35f*pulse));
                    Orbit(1,badge,p.Flavor=="claw"?"fx_slash":"fx_radiant",45+15*move,45,3,0,pulse*.65f,p.Flavor=="conquest"?red:gold);
                    Digit(0,variant=="decay"?"−1":value.Length>0?"+"+value:"+1",badge+Vector2.up*(35+30*move),pulse,p.Flavor=="conquest"?red:gold);break;
                case SecondarySkillStyle.Measure:
                    Line(0,from+new Vector2(-65,-60),from+new Vector2(-65,40),pulse);Line(1,from+new Vector2(65,-60),from+new Vector2(65,90),pulse);
                    for(int i=0;i<4;i++)Line(i+2,from+new Vector2(-55,i*25-45),from+new Vector2(55,i*25-45),pulse*.35f);
                    Digit(0,value.Length>0?"差 "+value:"战术优势",from,pulse,gold);break;
                case SecondarySkillStyle.Sun:
                    Picture(0,"fx_radiant",p.Flavor=="brand"?target:from,p.Flavor=="brand"?150:210,pulse,90*t);
                    Orbit(1,p.Flavor=="brand"?target:from,"fx_radiant",55+30*move,65,6,-90*t,pulse*.5f);break;
                case SecondarySkillStyle.Maneuver:
                    string status=d!=null&&d.Status!=StatusEffect.None?StatusNames.IconKeyOf(d.Status):"bleed";
                    Picture(0,"Status/"+status,target,100,pulse,0,Vector2.one*(.6f+move*.6f));
                    if(status=="bleed")for(int i=0;i<3;i++)Picture(i+1,"fx_slash",target+new Vector2((i-1)*25,0),110,pulse,-40,null,red);
                    else if(status=="limp")for(int i=0;i<3;i++)Line(i+1,target+new Vector2(-50+i*35,-45),target+new Vector2(-30+i*35,-20),pulse);
                    else Orbit(1,target,"fx_impact",45+30*move,80,4,90*t,pulse*.5f,status=="knocked_down"?new Color(.7f,.6f,.5f):gold);break;
                case SecondarySkillStyle.Dragon:
                    Picture(0,p.Sprite,origin+Vector2.up*25,200,pulse*.65f,0,new Vector2(1,1+.2f*move));Orbit(1,origin,fx,85,60,5,90*t,pulse*.65f,element);break;
                case SecondarySkillStyle.ElementFist:
                    Picture(0,"weapon_fist",from,175,pulse,-30+60*move);Picture(1,fx,from+Vector2.right*45,130,pulse,60*t,null,element);break;
                case SecondarySkillStyle.ElementGuard:
                    Orbit(0,origin,fx,55+25*(1-move),70,6,120*t,pulse*.6f,element);Picture(6,"UI/badge_ring",origin,145,pulse,0,null,element);break;
                case SecondarySkillStyle.Repair:
                    Picture(0,"Overlay/shield_broken",from,130,pulse*(1-move));Picture(1,"Overlay/shield",from,130,pulse*move);
                    Picture(2,"shield_fragment_1",from+new Vector2(-60,40)*(1-move),100,pulse);Picture(3,"shield_fragment_2",from+new Vector2(60,-40)*(1-move),100,pulse);break;
                case SecondarySkillStyle.FalseLife:
                    Picture(0,"Effects/false_life",origin,140,pulse,0,Vector2.one*(.7f+.4f*move));Orbit(1,origin,"fx_poison",65,60,4,100*t,pulse*.5f,green);
                    if(value.Length>0)Digit(0,value,origin+Vector2.up*75,pulse,green);break;
                case SecondarySkillStyle.Nightmare:
                    Picture(0,variant!="prepare"&&d!=null&&d.Status!=StatusEffect.None?"Status/"+StatusNames.IconKeyOf(d.Status):p.Sprite,variant=="prepare"?origin:target,135,pulse,-40+80*move,null,purple);
                    for(int i=0;i<4;i++)Picture(i+1,"fx_necrotic",Arc(origin,target,Mathf.Clamp01(move-i*.05f),35),90,pulse*.5f,i*90,null,purple);break;
                case SecondarySkillStyle.Book:
                    Picture(0,p.Sprite,origin,120,pulse,p.Flavor=="consume"?80*move:-20+30*move);
                    Digit(0,p.Flavor=="growth"?"层 "+value:value,Arc(p.Flavor=="consume"?origin:from,p.Flavor=="consume"?target:origin,move,65),pulse,gold);
                    if(p.Flavor=="growth")Orbit(1,origin,"fx_radiant",70,65,6,120*t,pulse*.6f);break;
                case SecondarySkillStyle.Wind:
                    for(int i=0;i<4;i++)Picture(i,"fx_slash",from+new Vector2(50*move,(i-1.5f)*22),90,pulse*.4f,-25+35*t,null,blue);
                    if(value.Length>0)Digit(0,old.Length>0?old+"→"+value:value,from+Vector2.up*65,pulse,blue);break;
                case SecondarySkillStyle.Charm:
                    Line(0,origin,target,pulse*.6f);Picture(1,d?.Status==StatusEffect.Disabled?"Status/disabled":"Status/charm",target,125,pulse,0,Vector2.one*(.6f+.4f*move));Orbit(2,target,"fx_psychic",55,50,3,120*t,pulse*.4f);break;
                case SecondarySkillStyle.Drunk:
                    Picture(0,"Effects/drunken_fist",origin,135,pulse,Mathf.Sin(t*12)*12,Vector2.one*(variant=="expire"?1.2f-.7f*move:.7f+.4f*move));Orbit(1,origin,"fx_impact",50+10*Mathf.Sin(t*10),55,4,100*t,pulse*.45f,new Color(.9f,.65f,.3f));
                    if(value.Length>0)Digit(0,value,origin+Vector2.up*70,pulse,gold);break;
                case SecondarySkillStyle.Ki:
                    for(int i=0;i<6;i++){float a=(i*60-90)*Mathf.Deg2Rad;Vector2 point=badge+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*55;
                        Picture(i,"fx_radiant",point,i<(d?.Value??2)?36:19,pulse*(i<(d?.Value??2)?.8f:.2f),0,null,gold);}
                    Digit(0,value.Length>0?value+"/6":"气",badge,pulse,gold);break;
                case SecondarySkillStyle.Convert:
                    Picture(0,"Overlay/heal",from,140,pulse*(1-move),0,null,green);Picture(1,"Overlay/attack",from,140,pulse*move,0,null,red);Digit(0,"9",from+Vector2.up*65,pulse,red);break;
                case SecondarySkillStyle.Circuit:
                    Orbit(0,origin,"fx_force",65,55,3,120*t,pulse*.5f,blue);Picture(3,p.Sprite,origin,95,pulse);
                    if(value.Length>0)Digit(0,value+"/3",origin+Vector2.up*70,pulse,blue);
                    if(variant=="recover"&&(d?.Previous??0)>0){for(int i=0;i<3;i++)Picture(i+4,"fx_radiant",Vector2.Lerp(origin+new Vector2((i-1)*60,70),origin,move),65,pulse,0,null,green);Digit(1,"+"+d.Previous,origin+Vector2.down*60,pulse,green);}
                    else if(variant=="recover"){Line(4,origin+new Vector2(-35,-30),origin+new Vector2(35,30),pulse);Line(5,origin+new Vector2(-35,30),origin+new Vector2(35,-30),pulse);}break;
            }
        }
    }
}
