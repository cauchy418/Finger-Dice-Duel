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
            _skillOrigin=_target(e.Actor)+Vector2.down*150;_skillTarget=_target(e.Target)+Vector2.down*150;
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
                    Picture(0,"fx_psychic",target,220,pulse,180*t);
                    Picture(1,"Units/polymorphed_sheep",target,145,Mathf.Clamp01(t*2)*pulse,0,Vector2.one*(.5f+arrive));
                    Picture(2,"fx_cold",target+new Vector2(0,-40),160,pulse*.4f,-80*t);Digit(0,"变形",target+Vector2.down*95,pulse);break;
                case FinalSkillStyle.Specter:
                    Picture(0,"fx_necrotic",target+new Vector2(0,35-70*t),230,pulse*.7f,60*t);
                    Picture(1,"accursed_specter",target+Vector2.up*30*arrive,245,pulse,0,new Vector2(1,.35f+arrive));
                    Picture(2,"Effects/hexblades_curse",target+Vector2.down*70,65,pulse);break;
                case FinalSkillStyle.Stealth:
                    for(int i=0;i<3;i++)Picture(i,name=="伏击"?"fx_poison":"fx_necrotic",origin+new Vector2((i-1)*42,20*Mathf.Sin(t*6+i)),135,pulse*.45f,i*120+t*50);
                    Picture(3,name=="伏击"?"weapon_bow":"shadow_monk",origin,210,pulse*.45f);
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
                    Picture(0,mark,Arc(origin,target,arrive,60),100,pulse,-60*(1-arrive));
                    for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Picture(i+1,style==FinalSkillStyle.Hunt?"projectile_arrow":"fx_necrotic",target+d*(115-55*arrive),style==FinalSkillStyle.Hunt?95:80,pulse,a*Mathf.Rad2Deg+180);}
                    break;
                case FinalSkillStyle.Moon:case FinalSkillStyle.Stars:
                    string form=variant.Length>0?variant:style==FinalSkillStyle.Moon?"moon_bear":"stars_archer";
                    Picture(0,"Forms/"+form,origin,170,pulse,0,Vector2.one*(data?.Value==0?1.3f-arrive*.7f:.6f+arrive*.7f));
                    if(style==FinalSkillStyle.Moon)
                    {
                        string aura=form.EndsWith("snake")?"fx_poison":form.EndsWith("eagle")?"fx_slash":form.EndsWith("wolf")?"fx_necrotic":"fx_impact";
                        Picture(1,"fx_cold",origin,240,pulse*.4f,100*t);
                        for(int i=0;i<3;i++){float a=(i*120+90*t)*Mathf.Deg2Rad;Picture(i+2,aura,origin+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*85,95,pulse*.7f,a*Mathf.Rad2Deg);}
                    }
                    else
                    {
                        Vector2[] constellation=form.EndsWith("chalice")?new[]{new Vector2(-70,75),new Vector2(70,75),new Vector2(0,-10),new Vector2(0,-80),new Vector2(55,-80)}
                            :form.EndsWith("dragon")?new[]{new Vector2(-80,80),new Vector2(50,40),new Vector2(-55,0),new Vector2(65,-45),new Vector2(-70,-90)}
                            :new[]{new Vector2(-70,80),new Vector2(0,40),new Vector2(85,0),new Vector2(0,-40),new Vector2(-70,-80)};
                        for(int i=0;i<constellation.Length;i++){Vector2 star=origin+constellation[i];Picture(i+1,"fx_radiant",star,42,Mathf.Clamp01(t*5-i*.3f)*pulse);if(i>0)Line(i+5,origin+constellation[i-1],star,pulse);}
                    }
                    break;
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
        private void ClearFinalNumbers(){foreach(var text in _finalNumbers)if(text!=null)Destroy(text.gameObject);_finalNumbers.Clear();}
        private void Line(int slot,Vector2 a,Vector2 b,float alpha)
        {Vector2 d=b-a;Picture(slot,"UI/divider",(a+b)*.5f,d.magnitude,alpha,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,new Vector2(1,.07f),new Color(.8f,.65f,1));}
        private IEnumerator Skill(CombatVisualEvent e,Vector2 from,Vector2 to)
        {
            StartFinalPose(e);yield return Tween(.72f,t=>DrawFinalPose(e,t));
            _pose(null,Vector2.zero,1);
            foreach(var label in _finalNumbers)if(label!=null)Destroy(label.gameObject);_finalNumbers.Clear();_impact(e);
        }
        // Native review uses the exact production pose renderer, without advancing game rules.
        public void PreviewFinalSkill(CombatVisualEvent e,float progress)
        {StopPlayback();StartFinalPose(e);DrawFinalPose(e,Mathf.Clamp01(progress));}
    }
}
