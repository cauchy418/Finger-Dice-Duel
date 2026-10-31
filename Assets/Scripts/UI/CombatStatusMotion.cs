using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;
namespace FingerDiceDuel.UI
{
    // The icon itself always animates; large ambient accents are capped per player.
    public sealed class CombatStatusIconMotion:MonoBehaviour
    {
        private Image _image;private string _signature;private float _burst;
        public void Bind(CombatStatusIcon entry)
        {if(_image==null)_image=GetComponent<Image>();string signature=entry.ResourceKey+"|"+entry.Counter+"|"+entry.Description;if(_signature!=null&&signature!=_signature)_burst=Time.unscaledTime+.42f;_signature=signature;}
        private void Update(){if(_image==null)return;float pulse=(Mathf.Sin(Time.unscaledTime*2.4f+transform.GetSiblingIndex())+1)*.5f;
            bool burst=Time.unscaledTime<_burst;transform.localScale=Vector3.one*(1+pulse*(burst?.12f:.035f));_image.color=Color.Lerp(new Color(.76f,.78f,.84f),Color.white,burst?1:.55f+pulse*.45f);}
        private void OnDisable(){transform.localScale=Vector3.one;_signature=null;}
    }
    public sealed class CombatStatusMotion:MonoBehaviour
    {
        private sealed class Aura{public Image Image;public PlayerState Player;public int Index,Groups=1;public string Signature;public float Flash,Remove;}
        private RectTransform _root;private Func<PlayerState,Vector2> _anchor;
        private readonly Dictionary<string,Aura> _auras=new Dictionary<string,Aura>();private readonly HashSet<string> _wanted=new HashSet<string>();
        public int Count=>_auras.Count;
        public void Initialize(RectTransform root,Func<PlayerState,Vector2> anchor){_root=root;_anchor=anchor;}
        public static string Effect(string key)
        {
            if(key.Contains("cold")||key.Contains("frost")||key.Contains("frozen")||key.Contains("freeze"))return "fx_cold";
            if(key.Contains("burn")||key.Contains("fire"))return "fx_fire";
            if(key.Contains("poison")||key.Contains("false_life"))return "fx_poison";
            if(key.Contains("acid")||key.Contains("restrain"))return "fx_acid";
            if(key.Contains("shock")||key.Contains("lightning"))return "fx_lightning";
            if(key.Contains("radiant")||key.Contains("holy")||key.Contains("blessing"))return "fx_radiant";
            if(key.Contains("decay")||key.Contains("necrotic")||key.Contains("curse")||key.Contains("conceal"))return "fx_necrotic";
            if(key.Contains("thunder")||key.Contains("resonance")||key.Contains("tinnitus"))return "fx_thunder";
            if(key.Contains("bladesong"))return "fx_slash";
            if(key.Contains("barrier")||key.Contains("immunity"))return "fx_force";
            return "fx_psychic";
        }
        public void Sync(CombatVisualFrame frame)
        {
            if(frame==null)return;_wanted.Clear();
            foreach(var state in frame.Players)
            {
                if(state.Health<=0)continue;int index=0;
                foreach(var icon in state.Icons)
                {
                    if(icon.Counter=="0"||icon.Counter=="0/9")continue;
                    string id=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(state.Player)+"|"+icon.ResourceKey;_wanted.Add(id);
                    if(!_auras.TryGetValue(id,out var aura))
                    {var go=new GameObject("StatusAura",typeof(Image));go.transform.SetParent(_root,false);var image=go.GetComponent<Image>();image.raycastTarget=false;image.preserveAspect=true;
                     image.rectTransform.anchorMin=image.rectTransform.anchorMax=new Vector2(.5f,.5f);image.rectTransform.sizeDelta=Vector2.one*72;
                     aura=new Aura{Image=image,Player=state.Player,Flash=Time.unscaledTime+.25f};_auras[id]=aura;}
                    string signature=icon.Counter+icon.Description;if(aura.Signature!=null&&aura.Signature!=signature)aura.Flash=Time.unscaledTime+.4f;
                    aura.Signature=signature;aura.Index=index++;aura.Remove=0;aura.Image.sprite=BattleArt.Get("Animation/"+Effect(icon.ResourceKey));
                }
                if(state.Left.IsConcealed||state.Right.IsConcealed)Flash(state.Player,.12f);
                foreach(var a in _auras.Values)if(ReferenceEquals(a.Player,state.Player))a.Groups=Mathf.Max(1,(index+2)/3);
            }
            foreach(var pair in _auras)if(!_wanted.Contains(pair.Key)&&pair.Value.Remove==0)pair.Value.Remove=Time.unscaledTime+.3f;
            Update();
        }
        public void Flash(PlayerState player,float duration=.5f){foreach(var a in _auras.Values)if(ReferenceEquals(a.Player,player))a.Flash=Mathf.Max(a.Flash,Time.unscaledTime+duration);}
        private void Update()
        {
            var dead=new List<string>();float time=Time.unscaledTime;
            foreach(var pair in _auras)
            {
                var a=pair.Value;if(a.Remove>0&&time>=a.Remove){Destroy(a.Image.gameObject);dead.Add(pair.Key);continue;}
                bool visible=a.Index/3==Mathf.FloorToInt(time/2)%a.Groups;bool burst=time<a.Flash;
                a.Image.enabled=visible;float angle=time*.35f+a.Index*2.399f;var r=a.Image.rectTransform;
                r.anchoredPosition=_anchor(a.Player)+new Vector2(Mathf.Cos(angle)*18,Mathf.Sin(angle)*18);
                r.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time+a.Index)*18);r.localScale=Vector3.one*(burst?1.4f:1+.08f*Mathf.Sin(time*2+a.Index));
                float fade=a.Remove==0?1:Mathf.Clamp01((a.Remove-time)/.3f);a.Image.color=new Color(1,1,1,(burst?.7f:.12f+.06f*Mathf.Sin(time*2))*fade);
            }
            foreach(var key in dead)_auras.Remove(key);
        }
        public void Clear(){foreach(var a in _auras.Values)Destroy(a.Image.gameObject);_auras.Clear();}
        private void OnDisable()=>Clear();
    }
}
