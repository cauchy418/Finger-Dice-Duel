using System;
using System.Collections;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;
namespace FingerDiceDuel.UI
{
    public sealed partial class CombatAnimationPlayer
    {
        private readonly List<Image> _finalImages=new List<Image>();
        private readonly List<Text> _finalNumbers=new List<Text>();
        private Vector2 _skillOrigin,_skillTarget,_skillFrom,_skillTo;
        private void StartFinalPose(CombatVisualEvent e)
        {
            _finalImages.Clear();ClearFinalNumbers();var data=e.SkillData;
            // Capture once: moving panels must never feed their changed anchors back into the path.
            bool adaptive=SecondarySkillAnimation.Find(e.RedirectKind)!=null;
            _skillOrigin=adaptive?SkillBodyAnchor(e.Actor):_target(e.Actor)+Vector2.down*150;_skillTarget=adaptive?SkillBodyAnchor(e.Target):_target(e.Target)+Vector2.down*150;
            _skillFrom=_hand(e.Actor,data?.Source??e.SourceHand);
            _skillTo=data?.Destination!=null?_hand(data.DestinationOwner??e.Target,data.Destination):_skillTarget;
        }
        private void Picture(int slot,string key,Vector2 at,float size,float alpha,float rotation=0,Vector2? stretch=null,Color? tint=null)
        {
            while(_finalImages.Count<=slot)_finalImages.Add(Take("fx_impact",1));
            var image=_finalImages[slot];image.enabled=true;image.sprite=BattleArt.Get(key.Contains("/")?key:"Animation/"+key);
            image.rectTransform.anchoredPosition=at;image.rectTransform.sizeDelta=Vector2.one*size;image.rectTransform.localScale=stretch.HasValue?new Vector3(stretch.Value.x,stretch.Value.y,1):Vector3.one;
            image.rectTransform.localRotation=Quaternion.Euler(0,0,rotation);var color=tint??Color.white;color.a=Mathf.Clamp01(alpha);image.color=color;
        }
        private void Digit(int slot,string number,Vector2 at,float alpha,Color? tint=null)
        {
            while(_finalNumbers.Count<=slot){var go=new GameObject("SkillNumber",typeof(Text));go.transform.SetParent(_root,false);var text=go.GetComponent<Text>();text.font=_speedText.font;text.fontSize=38;text.fontStyle=FontStyle.Bold;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.rectTransform.sizeDelta=new Vector2(170,58);_finalNumbers.Add(text);}
            var label=_finalNumbers[slot];label.text=number;label.rectTransform.anchoredPosition=at;var color=tint??BattleArt.Gold;color.a=Mathf.Clamp01(alpha);label.color=color;
        }
        private string SkillEmblem(CombatVisualEvent e,PlayerState player)
        {if(e.Before!=null)foreach(var state in e.Before.Players)if(ReferenceEquals(state.Player,player))return state.Emblem;return player.BattleEmblemKey;}
        private Vector2 Arc(Vector2 a,Vector2 b,float t,float height)=>Vector2.Lerp(a,b,t)+new Vector2(-(b-a).normalized.y,(b-a).normalized.x)*Mathf.Sin(t*Mathf.PI)*height;
        private void DrawFinalPose(CombatVisualEvent e,float t)
        {
            foreach(var img in _finalImages)img.enabled=false;foreach(var label in _finalNumbers)label.text="";
            var data=e.SkillData;string name=e.RedirectKind??"",variant=data?.Variant??"";var style=FinalSkillAnimation.Style(name);
            var secondary=SecondarySkillAnimation.Find(name);if(secondary!=null){DrawSecondaryPose(secondary,e,t);return;}
            Vector2 origin=_skillOrigin,target=_skillTarget,from=_skillFrom,to=_skillTo;
            float pulse=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t));float arrive=Mathf.SmoothStep(0,1,t);float last=Mathf.Clamp01((t-.45f)/.55f);
            string value=data!=null&&data.Value>=0?data.Value.ToString():"",previous=data!=null&&data.Previous>=0?data.Previous.ToString():"";
            switch(style)
            {
                case FinalSkillStyle.Portal:
                    Picture(0,"portal_frame",origin,235,pulse,0,new Vector2(.75f,1.2f));Picture(1,"portal_frame",target,235,pulse,0,new Vector2(.75f,1.2f));
                    Picture(2,SkillEmblem(e,e.Actor),Arc(origin,target,arrive,85),90,pulse);Picture(3,SkillEmblem(e,e.Target),Arc(target,origin,arrive,85),90,pulse);
                    _pose(e.Actor,Arc(origin,target,arrive,240)-origin,1-pulse*.35f);_pose(e.Target,Arc(target,origin,arrive,240)-target,1-pulse*.35f);break;
                case FinalSkillStyle.Number:
                    Picture(0,"Effects/portent",name=="预兆"?target:origin,110,pulse,-20+40*t);
                    Digit(0,value,Arc(name=="预兆"?from:origin,to,arrive,65),pulse);
                    if(name!="预兆")Digit(1,previous,to+new Vector2(30,-20),pulse*(1-arrive),Color.gray);
                    Picture(1,"fx_psychic",to,105,last*(1-last)*3);break;
                case FinalSkillStyle.Exchange:
                    Picture(0,"portal_frame",from,130,pulse,15,new Vector2(.55f,1));Picture(1,"portal_frame",to,130,pulse,-15,new Vector2(.55f,1));
                    if(name=="幻术默发")
                    {Digit(0,value,from,pulse*(1-arrive),Color.gray);Digit(1,previous,to,pulse,new Color(.6f,.85f,1));Line(4,Arc(from,to,arrive,40),to,pulse);}
                    else {Digit(0,value,Arc(from,to,arrive,65),pulse);Digit(1,previous,Arc(to,from,arrive,65),pulse,new Color(.6f,.85f,1));}
                    Picture(2,name=="不死奴仆"?"fx_necrotic":"fx_psychic",from,85,pulse*.5f);Picture(3,"fx_psychic",to,85,pulse*.5f);break;
                case FinalSkillStyle.Ward:
                    Picture(0,name=="不死适应"?"fx_necrotic":"Effects/arcane_barrier",target,190,pulse,15*Mathf.Sin(t*6));
                    int cracks=data!=null&&data.Value>=0?Mathf.Clamp(data.Previous/2,2,7):0;
                    for(int i=0;i<cracks;i++){float angle=i*360f/cracks;Vector2 d=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));Picture(i+1,"fx_lightning",target+d*(35+last*75),75,Mathf.Clamp01((t-.35f)*4)*(1-last),angle,new Vector2(.3f,1));}
                    Digit(0,value.Length>0?"吸收 "+value:name,target+new Vector2(0,-65),pulse);break;
                case FinalSkillStyle.Sheep:
                    bool sheepExit=variant=="break"||variant=="expire";
                    Picture(0,variant=="break"?"fx_impact":"fx_psychic",target,220,pulse,180*t);
                    Picture(1,"Units/polymorphed_sheep",target,145,pulse,0,Vector2.one*(sheepExit?1.3f-arrive*.8f:.5f+arrive));
                    Picture(2,"fx_cold",target+new Vector2(0,-40),160,pulse*.4f,-80*t);
                    Digit(0,variant=="break"?"羊形击破":sheepExit?"恢复原形":value.Length>0?"变形 "+value+"/"+value:"变形",
                        target+Vector2.down*95,pulse);break;
                case FinalSkillStyle.Specter:
                    bool dismiss=variant=="death"||variant=="owner-death"||variant=="replace";
                    float rise=dismiss?1-arrive:arrive;
                    Picture(0,"fx_necrotic",target+new Vector2(0,35-70*rise),230,pulse*.7f,60*t);
                    Picture(1,"accursed_specter",target+Vector2.up*30*rise,245,pulse,0,new Vector2(1,.35f+rise));
                    Picture(2,"Effects/hexblades_curse",target+Vector2.down*70,65,pulse);break;
                case FinalSkillStyle.Stealth:
                    for(int i=0;i<3;i++)Picture(i,name=="伏击"?"fx_poison":"fx_necrotic",origin+new Vector2((i-1)*42,20*Mathf.Sin(t*6+i)),135,pulse*.45f,i*120+t*50);
                    Picture(3,name=="伏击"?"weapon_bow":"shadow_monk",origin,210,pulse*.45f);
                    if(variant=="conceal"||variant=="reveal")
                    {
                        Picture(4,"Overlay/concealed",from,105,pulse,0,Vector2.one*(variant=="reveal"?1.2f-arrive*.4f:.8f+arrive*.4f));
                        Digit(0,variant=="conceal"?"攻击 +"+value:"伏击解除",from+Vector2.up*62,pulse);
                    }
                    _pose(e.Actor,name=="阴影闪避"?Vector2.right*65*pulse:Vector2.zero,1-pulse*(name=="暗杀"?.75f:.45f));break;
                case FinalSkillStyle.Clones:
                    for(int i=0;i<3;i++){Vector2 p=Arc(from,target,arrive,(i-1)*95);Picture(i,"shadow_monk",p,190,pulse*(i==1?.85f:.4f));Picture(i+3,"weapon_fist",p+Vector2.right*40,75,pulse*.75f);}
                    break;
                case FinalSkillStyle.HiddenForce:
                    Picture(0,"Effects/hidden_force",Arc(from,target,arrive,20),110,pulse,-180*t);
                    for(int i=0;i<3;i++)Picture(i+1,"fx_impact",target,(variant=="detonate"?130:50)+(variant=="detonate"?200:70)*t+i*20,pulse*(1-last)*.5f,-i*35);
                    if(variant=="detonate")Digit(0,"暗劲 "+value,target+Vector2.up*70,pulse);break;
                case FinalSkillStyle.BladeSong:
                    Picture(0,"weapon_sword",origin,245,pulse,Mathf.Lerp(-100,40,arrive),Vector2.one*(data?.Value==0?1-arrive*.5f:1));
                    for(int i=0;i<4;i++){float a=(t*180+i*90)*Mathf.Deg2Rad;Picture(i+1,"fx_slash",origin+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*55,110,pulse*.65f,a*Mathf.Rad2Deg);Digit(i,"♪",origin+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*95,pulse);}
                    break;
                case FinalSkillStyle.Blessing:
                    Picture(0,"Status/war_blessing",Arc(origin,target,arrive,70),105,pulse);
                    Picture(1,"fx_radiant",target,190,pulse,0,new Vector2(1+t,.5f));Picture(2,"fx_radiant",target,190,pulse,180,new Vector2(1+t,.5f));break;
                case FinalSkillStyle.Curse:case FinalSkillStyle.Hunt:
                    string mark=style==FinalSkillStyle.Curse?"Effects/hexblades_curse":"Effects/hunters_mark";
                    if(name=="巨像屠夫")
                    {
                        bool guard=variant=="guard";
                        Picture(0,mark,target,125,pulse,guard?0:60*arrive,Vector2.one*(guard?.7f+arrive:1.35f-.8f*arrive));
                        Picture(1,guard?"fx_impact":"fx_slash",target,guard?160:100,pulse*.7f,guard?45:-20);
                        Digit(0,(guard?"附伤 −":"加伤 +")+value,target+Vector2.up*68,pulse,guard?new Color(1,.62f,.36f):BattleArt.Gold);
                        break;
                    }
                    Picture(0,mark,Arc(origin,target,arrive,60),100,pulse,-60*(1-arrive));
                    for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Picture(i+1,style==FinalSkillStyle.Hunt?"projectile_arrow":"fx_necrotic",target+d*(115-55*arrive),style==FinalSkillStyle.Hunt?95:80,pulse,a*Mathf.Rad2Deg+180);}
                    break;
                case FinalSkillStyle.Moon:case FinalSkillStyle.Stars:
                    bool modern=variant=="enter"||variant=="exit"||variant=="break";
                    bool leaving=variant=="exit"||variant=="break"||data?.Value==0;
                    if(variant=="operation")
                    {
                        Picture(0,"Forms/stars_dragon",origin,140,pulse);
                        Picture(1,"fx_radiant",Arc(to,from,arrive,45),95,pulse,90*t);
                        Digit(0,previous+" + "+data.Amount+" → "+value,from+Vector2.up*62,pulse);
                        break;
                    }
                    if(variant=="heal")
                    {
                        Picture(0,"Forms/stars_chalice",origin,120,pulse);
                        Picture(1,"fx_radiant",Arc(from,target,arrive,75),130,pulse,0,null,new Color(.54f,1,.63f));
                        Digit(0,"+"+value,target+Vector2.up*60,pulse,new Color(.54f,1,.63f));
                        break;
                    }
                    string form=style==FinalSkillStyle.Moon&&modern&&data.Value>0&&data.Value<DruidForms.MoonKeys.Length
                        ?DruidForms.MoonKeys[data.Value]:variant.StartsWith("moon_")||variant.StartsWith("stars_")?variant
                        :style==FinalSkillStyle.Moon?"moon_bear":"stars_archer";
                    Picture(0,"Forms/"+form,origin,170,pulse,0,Vector2.one*(leaving?1.3f-arrive*.7f:.6f+arrive*.7f));
                    if(style==FinalSkillStyle.Moon)
                    {
                        bool panther=form.EndsWith("panther"),owlbear=form.EndsWith("owlbear");
                        string aura=form.EndsWith("snake")?"fx_poison":form.EndsWith("eagle")||panther?"fx_slash":form.EndsWith("wolf")?"fx_necrotic":"fx_impact";
                        Picture(1,"fx_cold",origin,240,pulse*.4f,100*t);
                        for(int i=0;i<3;i++)
                        {
                            float a=(i*120+90*t)*Mathf.Deg2Rad;
                            Vector2 offset=panther?new Vector2((i-1)*41,10):new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(variant=="break"?50+85*arrive:85);
                            Picture(i+2,aura,origin+offset,panther?155:95,pulse*.7f,panther?-35:a*Mathf.Rad2Deg);
                        }
                        if(owlbear)Picture(5,"fx_impact",origin+Vector2.down*67,170+95*arrive,pulse,0,new Vector2(1.5f,.4f));
                        if(modern&&data.Previous>0&&data.Previous<DruidForms.StarKeys.Length)
                            DrawConstellation(DruidForms.StarKeys[data.Previous],origin+Vector2.right*120,t,leaving,6);
                        if(variant=="enter"&&data.Amount>0)Digit(0,"充能 −"+data.Amount,origin+Vector2.down*93,pulse);
                        if(variant=="break")Digit(0,"形态击破",origin+Vector2.down*93,pulse);
                    }
                    else DrawConstellation(form,origin,t,leaving,1);
                    break;
                case FinalSkillStyle.Spore:
                    DrawSporePose(e,t,arrive,pulse,origin,target,value);break;
                case FinalSkillStyle.Shadow:
                    bool grave=name=="坟冢之力";
                    Picture(0,"fx_necrotic",name=="黑暗术"?Arc(origin,target,arrive,45):target,190,pulse*.85f,75*t);
                    Picture(1,grave?"fx_radiant":"Status/darkness_blind",target,grave?110:92,pulse*(grave?arrive:1),0,null,
                        grave?new Color(.65f,.44f,1):new Color(.65f,.62f,.95f));
                    if(grave||name=="幽暗之瞳")Digit(0,grave?"1 生命":"暗蚀 +"+value,target+Vector2.up*66,pulse);
                    break;
                case FinalSkillStyle.Reroll:
                    bool rerolled=t>.58f&&variant!="fatal";
                    Vector2 dice=origin+Vector2.up*35;
                    Picture(0,"fate_die",dice,155,pulse,rerolled?0:40*Mathf.Sin(t*12));
                    Digit(0,rerolled?value:previous,dice+new Vector2(0,-9),pulse);
                    Picture(1,"fx_slash",origin+Vector2.down*48,105,pulse*(1-last),-40, tint:new Color(.85f,.22f,.19f));
                    Digit(1,"−"+data.Amount+" 生命",origin+Vector2.down*64,pulse,new Color(1,.48f,.36f));
                    break;
                case FinalSkillStyle.Overflow:
                    Picture(0,"fx_impact",target,150,pulse,25);
                    Digit(0,"溢伤 −"+value,target+Vector2.up*64,pulse,new Color(1,.48f,.36f));break;
                case FinalSkillStyle.IllusionGuard:
                    Picture(0,"fx_psychic",from,145,pulse,65*t);
                    Picture(1,"Effects/arcane_barrier",target,170,pulse*.7f,-30*t);
                    Digit(0,previous+" → "+value,from+Vector2.up*58,pulse);
                    Digit(1,"伤害减半",target+Vector2.up*60,pulse,new Color(.64f,.84f,1));break;
                case FinalSkillStyle.Counter:
                    Picture(0,"fx_thunder",origin,180,pulse,180*t);Picture(1,variant=="thunder"?"fx_thunder":"fx_lightning",Arc(origin,target,arrive,15),160,pulse);
                    Picture(2,"weapon_hammer",origin,160,pulse,-80+120*arrive);break;
                case FinalSkillStyle.Chain:
                    Picture(0,"Overlay/shield_broken",from,140,pulse);Line(1,from,to,pulse);
                    Picture(2,"shield_fragment_1",to+new Vector2(-55,35)*last,125,pulse);Picture(3,"shield_fragment_2",to+new Vector2(55,-35)*last,125,pulse);break;
                case FinalSkillStyle.Reaction:
                    string left=variant.StartsWith("fire")?"fx_fire":variant.StartsWith("acid")?"fx_acid":variant.StartsWith("poison")?"fx_poison":variant.StartsWith("lightning")?"fx_lightning":variant.StartsWith("thunder")?"fx_thunder":"fx_cold";
                    string right=variant.EndsWith("poison")?"fx_poison":variant.EndsWith("acid")?"fx_acid":variant.EndsWith("resonance")?"fx_thunder":variant.EndsWith("shock")?"fx_lightning":variant.Contains("burn")?"fx_fire":variant.Contains("greased")?"Status/greased":variant.Contains("wet")?"Status/wet":"fx_cold";
                    Picture(0,left,target+Vector2.left*90*(1-arrive),155,pulse,-40*t);Picture(1,right,target+Vector2.right*90*(1-arrive),155,pulse,40*t);
                    Picture(2,variant.Contains("frozen")?"fx_cold":"fx_impact",target,80+190*last,last*(1-last)*3,80*t);
                    string result=variant=="cold_frost"||variant=="cold_wet"?"frozen":variant=="thunder_resonance"?"dizzy":variant=="lightning_resonance"||variant=="thunder_shock"?"tinnitus":null;
                    if(result!=null)Picture(3,"Status/"+result,target+Vector2.down*65,65,last*pulse);break;
                case FinalSkillStyle.Status:
                    Picture(0,e.Weapon,target,120,pulse,0,Vector2.one*(.7f+arrive*.6f));break;
            }
        }
        private void DrawConstellation(string form,Vector2 anchor,float t,bool leaving,int slot)
        {
            float pulse=Mathf.Sin(Mathf.PI*t);
            float gather=leaving?1-t:t;
            Vector2[] points=form.EndsWith("chalice")?new[]{new Vector2(-70,75),new Vector2(70,75),new Vector2(0,-10),new Vector2(0,-80),new Vector2(55,-80)}
                :form.EndsWith("dragon")?new[]{new Vector2(-80,80),new Vector2(50,40),new Vector2(-55,0),new Vector2(65,-45),new Vector2(-70,-90)}
                :new[]{new Vector2(-70,80),new Vector2(0,40),new Vector2(85,0),new Vector2(0,-40),new Vector2(-70,-80)};
            float scale=.55f+gather*.45f;
            for(int i=0;i<points.Length;i++)
            {
                Vector2 point=anchor+points[i]*scale;
                Picture(slot+i,"fx_radiant",point,36,Mathf.Clamp01(gather*5-i*.3f)*pulse);
                if(i>0)Line(slot+points.Length+i-1,anchor+points[i-1]*scale,point,pulse);
            }
        }
        private void DrawSporePose(CombatVisualEvent e,float t,float arrive,float pulse,Vector2 origin,Vector2 target,string value)
        {
            string skill=e.RedirectKind;var data=e.SkillData;
            var green=new Color(.62f,.96f,.55f);
            if(skill=="孢子环绕"||skill=="真菌感染")
            {
                Vector2 source=skill=="真菌感染"&&data?.DestinationOwner!=null?_target(data.DestinationOwner):origin;
                for(int i=0;i<5;i++)
                {
                    Vector2 mote=Arc(source,target,Mathf.Clamp01(arrive-i*.06f),20+i*12);
                    Picture(i,"fx_poison",mote,50+i*3,pulse*.55f,i*57+t*80,tint:green);
                }
                Picture(5,e.Weapon,target,82,pulse*arrive);
                if(skill=="真菌感染")Digit(0,"充能 +1",target+Vector2.up*65,pulse,green);
                return;
            }
            bool gain=skill=="一体共生",broken=data?.Variant=="break";
            Picture(0,"Effects/spore_life",target,112,pulse,0,Vector2.one*(broken?1.25f-arrive*.65f:.7f+arrive*.55f));
            for(int i=0;i<6;i++)
            {
                float angle=(i*60+t*72)*Mathf.Deg2Rad;
                float radius=broken?35+arrive*82:105-arrive*40;
                Picture(i+1,"fx_poison",target+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,58,pulse*.5f,i*60,tint:green);
            }
            Digit(0,(gain?"孢子生命 +":"孢子吸收 ")+value,target+Vector2.up*70,pulse,green);
        }
        private void ClearFinalNumbers(){foreach(var text in _finalNumbers)if(text!=null){text.gameObject.SetActive(false);Destroy(text.gameObject);}_finalNumbers.Clear();}
        private void Line(int slot,Vector2 a,Vector2 b,float alpha)
        {Vector2 d=b-a;Picture(slot,"UI/divider",(a+b)*.5f,d.magnitude,alpha,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,new Vector2(1,.07f),new Color(.8f,.65f,1));}
        private IEnumerator Skill(CombatVisualEvent e,Vector2 from,Vector2 to)
        {
            StartFinalPose(e);
            var profile=SecondarySkillAnimation.Find(e.RedirectKind);
            bool synchronized=false;
            yield return Tween(profile?.Priority==3?.38f:profile!=null?.56f:.72f,t=>{
                DrawFinalPose(e,t);
                if(e.SynchronizeFrame&&!synchronized&&t>=.6f){_apply(e.After);synchronized=true;}
            });
            if(e.SynchronizeFrame&&!synchronized)_apply(e.After);
            _pose(null,Vector2.zero,1);
            ClearFinalNumbers();_impact(e);
        }
        // Native review uses the exact production pose renderer, without advancing game rules.
        public void PreviewFinalSkill(CombatVisualEvent e,float progress)
        {StopPlayback();StartFinalPose(e);DrawFinalPose(e,Mathf.Clamp01(progress));}
    }
}
