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
            int slots=aura.Key=="Effects/ki"?6:aura.Key=="Subclasses/bard_college_of_lore"?9:aura.Key=="Adventure/npc_mage_phantom"?3:aura.Key.StartsWith("Resistance/")?3:0;
            while(aura.Bits.Count<slots)
            {var go=new GameObject("ResourcePoint",typeof(Image));go.transform.SetParent(aura.Image.transform,false);var image=go.GetComponent<Image>();image.raycastTarget=false;image.preserveAspect=true;image.sprite=BattleArt.Get("Animation/fx_radiant");var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=Vector2.one*16;aura.Bits.Add(image);}
        }
        private void PoseSecondaryAura(Aura aura,float time,bool visible,float fade)
        {
            for(int i=0;i<aura.Bits.Count;i++)
            {
                var image=aura.Bits[i];image.enabled=visible;bool filled=aura.Value<0||i<aura.Value;
                float angle=(i*360f/aura.Bits.Count-90+(aura.Key=="Effects/ki"?time*12:0))*Mathf.Deg2Rad;
                image.rectTransform.anchoredPosition=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*35;
                image.rectTransform.localScale=Vector3.one*(filled?1+.12f*Mathf.Sin(time*3+i):.55f);
                var color=aura.Key.StartsWith("Resistance/")?new Color(.45f,.75f,1):BattleArt.Gold;color.a=(filled?.5f:.1f)*fade;image.color=color;
            }
            if(aura.Key=="Effects/drunken_fist")
            {aura.Image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*2)*14);aura.Image.color=new Color(.95f,.7f,.35f,.2f*fade);}
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
