using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;
namespace FingerDiceDuel.UI
{
    public sealed partial class CombatStatusMotion
    {
        private sealed class StatAccent
        {public PlayerState Player;public int Attack,Defense,Range;public Image Image;public float Until;public bool Up;}
        private readonly Dictionary<PlayerState,StatAccent> _stats=new Dictionary<PlayerState,StatAccent>();
        private void PrepareSecondaryAura(Aura aura,CombatStatusIcon icon)
        {
            aura.Key=icon.ResourceKey;aura.Value=icon.Value;aura.Hand=icon.TargetHand;
            int slots=aura.Key=="Status/knowledge_blessing"?6:aura.Key=="Status/confusion"?3:aura.Key=="Status/holy_light"||aura.Key=="Status/scorch"?4:aura.Key=="Effects/ki"?6:aura.Key=="Subclasses/bard_college_of_lore"?9:aura.Key=="Adventure/npc_mage_phantom"?3:aura.Key.StartsWith("Resistance/")?3:0;
            while(aura.Bits.Count<slots)
            {var go=new GameObject("ResourcePoint",typeof(Image));go.transform.SetParent(aura.Image.transform,false);var image=go.GetComponent<Image>();image.raycastTarget=false;image.preserveAspect=true;image.sprite=BattleArt.Get("Animation/fx_radiant");var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=Vector2.one*16;aura.Bits.Add(image);}
        }
        private void PoseSecondaryAura(Aura aura,float time,bool visible,float fade)
        {
            bool knowledge=aura.Key=="Status/knowledge_blessing",confused=aura.Key=="Status/confusion",holy=aura.Key=="Status/holy_light",scorch=aura.Key=="Status/scorch";
            if(knowledge||confused||holy||scorch)
            {
                aura.Image.rectTransform.localRotation=Quaternion.Euler(0,0,confused?Mathf.Sin(time*1.7f)*28:holy?time*12:0);
                var tint=knowledge?BattleArt.Gold:confused?new Color(.72f,.5f,1):holy?new Color(1,.85f,.35f):new Color(1,.34f,.12f);
                tint.a=aura.Image.color.a;aura.Image.color=tint;
            }
            for(int i=0;i<aura.Bits.Count;i++)
            {
                var image=aura.Bits[i];image.enabled=visible;bool filled=aura.Value<0||i<aura.Value;
                float angle=(i*360f/aura.Bits.Count-90+(aura.Key=="Effects/ki"?time*12:0))*Mathf.Deg2Rad;
                image.rectTransform.anchoredPosition=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*35;
                image.rectTransform.localScale=Vector3.one*(filled?1+.12f*Mathf.Sin(time*3+i):.55f);
                if(confused){image.rectTransform.anchoredPosition=new Vector2(Mathf.Cos(time*.7f+i*2.094f)*36,Mathf.Sin(time*1.4f+i*2.094f)*18);image.sprite=BattleArt.Get("Status/confusion");}
                if(holy||scorch){image.sprite=BattleArt.Get("Animation/fx_fire");image.rectTransform.anchoredPosition=new Vector2((i-1.5f)*15,holy?Mathf.Sin(time*1.2f+i)*8:Mathf.Repeat(time*13+i*12,45)-22);}
                var color=aura.Key.StartsWith("Resistance/")?new Color(.45f,.75f,1):BattleArt.Gold;if(confused)color=new Color(.72f,.5f,1);if(scorch)color=new Color(1,.34f,.12f);color.a=(filled?.5f:.1f)*fade;image.color=color;
            }
            if(aura.Key=="Effects/drunken_fist")
            {aura.Image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*2)*14);aura.Image.color=new Color(.95f,.7f,.35f,.2f*fade);}
            if(aura.Key=="Status/darkness_blind"||aura.Key=="Subclasses/cleric_life_domain"||aura.Key=="Effects/spore_life")
            {
                var tint=aura.Key=="Status/darkness_blind"?new Color(.55f,.45f,.9f):
                    aura.Key=="Effects/spore_life"?new Color(.52f,.84f,.42f):new Color(.91f,.89f,.53f);
                tint.a=aura.Image.color.a;aura.Image.color=tint;
            }
        }
        private void SyncStatAccents(CombatVisualFrame frame)
        {
            foreach(var state in frame.Players)
            {
                if(!_stats.TryGetValue(state.Player,out var accent)){accent=new StatAccent{Player=state.Player,Attack=state.Attack,Defense=state.Defense,Range=state.Range};_stats[state.Player]=accent;continue;}
                if(state.Health>0&&(accent.Attack!=state.Attack||accent.Defense!=state.Defense||accent.Range!=state.Range))
                {
                    accent.Up=state.Attack>accent.Attack||state.Defense>accent.Defense||state.Range>accent.Range;accent.Until=Time.unscaledTime+.42f;
                    if(accent.Image==null){var go=new GameObject("StatChangeAccent",typeof(Image));go.transform.SetParent(_root,false);accent.Image=go.GetComponent<Image>();accent.Image.raycastTarget=false;accent.Image.preserveAspect=true;accent.Image.sprite=BattleArt.Get("Overlay/attack");var rect=accent.Image.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=Vector2.one*34;}
                }
                accent.Attack=state.Attack;accent.Defense=state.Defense;accent.Range=state.Range;
            }
        }
        private void UpdateStatAccents(float time)
        {foreach(var a in _stats.Values)if(a.Image!=null){float fade=Mathf.Clamp01((a.Until-time)/.42f);a.Image.enabled=fade>0;a.Image.rectTransform.anchoredPosition=_anchor(a.Player)+new Vector2(38,18+(1-fade)*24);a.Image.color=a.Up?new Color(1,.8f,.4f,fade):new Color(.6f,.65f,.75f,fade);}}
        private void ClearStatAccents(){foreach(var a in _stats.Values)if(a.Image!=null){a.Image.gameObject.SetActive(false);Destroy(a.Image.gameObject);}_stats.Clear();}
    }
}
