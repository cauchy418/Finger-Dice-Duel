using System;
using System.Collections;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>Visual-only playback. The rule engine has already resolved each event exactly once.</summary>
    public sealed partial class CombatAnimationPlayer : MonoBehaviour
    {
        public bool Busy {get;private set;}
        public float Speed {get;private set;}=1;
        public int PlayedEvents {get;private set;}
        public CombatVisualEvent Current {get;private set;}
        public bool WindupVisible=>_ready!=null&&_ready.enabled;
        private RectTransform _root;private Func<PlayerState,Hand,Vector2> _hand;private Func<PlayerState,Vector2> _target;
        private Action<CombatVisualFrame> _apply;private Action<CombatVisualEvent> _impact;private Action _complete;
        private Action<PlayerState,Vector2,float> _pose;
        private readonly List<Image> _pool=new List<Image>();private readonly List<Image> _used=new List<Image>();
        private Image _ready,_readyElement;private PlayerState _readyOwner;private Hand _readyHand;
        private GameObject _controls;private Text _speedText;private bool _skip;private Coroutine _routine;
        public void Initialize(RectTransform parent,Font font,Func<PlayerState,Hand,Vector2> hand,Func<PlayerState,Vector2> target,
            Action<CombatVisualFrame> apply,Action<CombatVisualEvent> impact,Action<PlayerState,Vector2,float> pose)
        {
            _hand=hand;_target=target;_apply=apply;_impact=impact;_pose=pose;
            var go=new GameObject("CombatAnimationLayer",typeof(RectTransform));go.transform.SetParent(parent,false);_root=(RectTransform)go.transform;
            _root.anchorMin=Vector2.zero;_root.anchorMax=Vector2.one;_root.offsetMin=_root.offsetMax=Vector2.zero;
            _controls=new GameObject("PlaybackControls",typeof(RectTransform));_controls.AddComponent<CanvasGroup>();_controls.transform.SetParent(_root,false);
            var group=_controls.GetComponent<CanvasGroup>();group.ignoreParentGroups=true;
            _speedText=Control("Speed",new Vector2(640,-510),"动画 ×1",font,()=>{Speed=Speed==1?2:Speed==2?4:1;_speedText.text="动画 ×"+Speed;});
            Control("Skip",new Vector2(810,-510),"跳过动画",font,()=>_skip=true);_controls.SetActive(false);
        }
        private Text Control(string name,Vector2 pos,string label,Font font,Action click)
        {
            var go=new GameObject(name,typeof(Image),typeof(Button));go.transform.SetParent(_controls.transform,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=pos;rect.sizeDelta=new Vector2(140,38);
            BattleArt.Apply(go.GetComponent<Image>(),"UI/button_normal",true);go.GetComponent<Button>().onClick.AddListener(()=>click());
            var textGo=new GameObject("Label",typeof(Text));textGo.transform.SetParent(rect,false);var t=textGo.GetComponent<Text>();
            var tr=t.rectTransform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
            t.font=font;t.fontSize=20;t.alignment=TextAnchor.MiddleCenter;t.color=BattleArt.Gold;t.text=label;t.raycastTarget=false;return t;
        }
        private Image Take(string key,float size)
        {
            Image result=null;foreach(var candidate in _pool)if(!candidate.gameObject.activeSelf){result=candidate;break;}
            if(result==null){var go=new GameObject("CombatSprite",typeof(Image));go.transform.SetParent(_root,false);result=go.GetComponent<Image>();result.raycastTarget=false;result.preserveAspect=true;_pool.Add(result);}
            result.gameObject.SetActive(true);result.enabled=true;result.sprite=BattleArt.Get(key.Contains("/")?key:"Animation/"+key);
            var r=result.rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=Vector2.one*size;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;result.color=Color.white;
            _used.Add(result);return result;
        }
        private void ReleaseUsed(){foreach(var img in _used)img.gameObject.SetActive(false);_used.Clear();}
        public void SetWindup(PlayerState owner,Hand hand,string weapon,AttackDamageType type)
        {
            if(Busy||owner==null||hand==null){HideWindup();return;}
            if(_ready!=null&&ReferenceEquals(owner,_readyOwner)&&ReferenceEquals(hand,_readyHand)&&_ready.sprite==BattleArt.Get("Animation/"+weapon))return;
            HideWindup();_readyOwner=owner;_readyHand=hand;
            _ready=Take(weapon,210);_used.Remove(_ready);
            if(AttackDamageTypes.IsMagic(type)&&weapon.StartsWith("weapon_")){_readyElement=Take("fx_"+AttackDamageTypes.IconKeyOf(type),70);_used.Remove(_readyElement);}
            Update();
        }
        public void HideWindup(){if(_ready!=null)_ready.gameObject.SetActive(false);if(_readyElement!=null)_readyElement.gameObject.SetActive(false);_ready=_readyElement=null;_readyOwner=null;_readyHand=null;}
        private void Update()
        {
            if(_ready==null)return;Vector2 pos=_hand(_readyOwner,_readyHand)+new Vector2(0,68);
            _ready.rectTransform.anchoredPosition=pos;_ready.rectTransform.localRotation=Quaternion.Euler(0,0,-18+Mathf.Sin(Time.unscaledTime*3)*5);
            _ready.color=new Color(1,1,1,.65f+Mathf.Sin(Time.unscaledTime*4)*.15f);
            if(_readyElement!=null){_readyElement.rectTransform.anchoredPosition=pos+new Vector2(42,0);_readyElement.color=_ready.color;}
        }
        public void Play(IReadOnlyList<CombatVisualEvent> events,CombatVisualFrame final,Action complete)
        {if(events==null||events.Count==0){complete?.Invoke();return;}HideWindup();Busy=true;_skip=false;_complete=complete;_controls.SetActive(true);_routine=StartCoroutine(Run(events,final));}
        private IEnumerator Run(IReadOnlyList<CombatVisualEvent> events,CombatVisualFrame final)
        {
            if(events[0].Before!=null)_apply(events[0].Before);
            foreach(var ev in events)
            {
                Current=ev;
                if(!_skip)yield return PlayOne(ev);else _apply(ev.After);
                PlayedEvents++;ReleaseUsed();
            }
            _pose(null,Vector2.zero,1);_apply(final);Busy=false;Current=null;_controls.SetActive(false);_routine=null;var complete=_complete;_complete=null;complete?.Invoke();
        }
        private IEnumerator PlayOne(CombatVisualEvent e)
        {
            Vector2 from=_hand(e.Actor,e.SourceHand),to=_target(e.Target);
            if(e.Kind==CombatVisualKind.Skill){yield return Skill(e,from,to);yield break;}
            if(e.Kind==CombatVisualKind.VowTransfer)
            {
                var vow=Take("Effects/vow_of_enmity",88);var origin=e.IntendedTarget==null?_target(e.Actor):_target(e.IntendedTarget);
                yield return Tween(.35f,t=>{vow.rectTransform.anchoredPosition=Vector2.Lerp(origin,to,t);vow.rectTransform.localScale=Vector3.one*(.8f+Mathf.Sin(t*Mathf.PI)*.3f);});_apply(e.After);yield break;
            }
            if(e.Kind!=CombatVisualKind.Attack){yield return Shield(e,from);yield break;}
            bool cancel=e.Outcome==CombatVisualOutcome.Cancelled;
            bool dodge=e.Outcome==CombatVisualOutcome.Dodged;
            Vector2 delta=to-from;float angle=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
            if(!ReferenceEquals(e.Target,e.IntendedTarget))
            {var guard=Take(e.RedirectKind=="protection"?"fx_radiant":"fx_force",90);yield return Tween(.16f,t=>{guard.rectTransform.anchoredPosition=Vector2.Lerp(_target(e.IntendedTarget),to,t);guard.color=e.RedirectKind=="protection"?new Color(1,.85f,.4f,1-t*.4f):new Color(1,.35f,.5f,1-t*.4f);});guard.gameObject.SetActive(false);}
            bool swing=e.Weapon=="weapon_sword"||e.Weapon=="weapon_hammer"||e.Weapon=="weapon_staff";
            var weapon=Take(e.Weapon??"fx_impact",e.Weapon=="weapon_fist"?175:swing?300:e.Weapon=="weapon_rapier"?255:210);
            if(swing)weapon.rectTransform.pivot=new Vector2(.18f,.5f);
            Image slash=swing?Take("fx_slash",230):null;
            Image bow=e.Weapon=="projectile_arrow"?Take("weapon_bow",160):null;
            Image element=AttackDamageTypes.IsMagic(e.Type)&&e.Weapon!=null&&e.Weapon.StartsWith("weapon_")?Take("fx_"+AttackDamageTypes.IconKeyOf(e.Type),100):null;
            Image evade=dodge?Take("fx_slash",120):cancel?Take("fx_psychic",130):null;
            var evader=e.EvasionActor??e.Target;
            Image ghost=dodge?Take(e.Target.BattleEmblemKey,100):null;
            Vector2 evadeOrigin=_target(evader);
            Vector2 end=dodge?to+new Vector2(100,45):to;
            yield return Tween(cancel?.30f:.50f,t=>{
                float travel=Mathf.Clamp01((t-.18f)/.82f);Vector2 pos=Vector2.Lerp(from,end,travel);
                // Hold at the source for windup, then sweep around a grip near the target.
                // The weapon rotates about its handle rather than translating like a missile.
                if(swing){pos=travel<.23f?from:end-delta.normalized*145;float sweep=Mathf.SmoothStep(0,1,Mathf.Clamp01((travel-.23f)/.77f));weapon.rectTransform.localRotation=Quaternion.Euler(0,0,angle+Mathf.Lerp(-100,38,sweep));
                    slash.rectTransform.anchoredPosition=end;slash.rectTransform.localRotation=Quaternion.Euler(0,0,angle-30+sweep*55);slash.color=new Color(1,1,1,cancel?0:Mathf.Sin(sweep*Mathf.PI)*.85f);}
                weapon.rectTransform.anchoredPosition=cancel?from:pos;
                if(!swing)weapon.rectTransform.localRotation=Quaternion.Euler(0,0,angle);
                weapon.color=new Color(1,1,1,cancel?1-t:swing&&travel<.3f?Mathf.Abs(travel-.23f)/.23f:1);
                if(bow!=null){bow.rectTransform.anchoredPosition=from;bow.rectTransform.localRotation=Quaternion.Euler(0,0,angle);bow.color=new Color(1,1,1,1-t);}
                if(element!=null){element.rectTransform.anchoredPosition=pos+delta.normalized*52;element.color=new Color(1,1,1,.85f);}
                if(evade!=null){evade.rectTransform.anchoredPosition=evadeOrigin+(dodge?new Vector2(Mathf.Sin(t*Mathf.PI)*70,20*t):Vector2.zero);evade.rectTransform.localScale=Vector3.one*(.6f+t);evade.color=cancel?new Color(.9f,.4f,1,1-t*.4f):new Color(.3f,1,1,1-t*.4f);}
                if(ghost!=null){ghost.rectTransform.anchoredPosition=to;ghost.color=new Color(.3f,1,1,.35f*(1-t));}
                if(dodge)_pose(e.Target,new Vector2(Mathf.Sin(t*Mathf.PI)*34,0),1);
                else if(cancel&&e.EvasionActor!=null)_pose(evader,Vector2.zero,1-Mathf.Sin(t*Mathf.PI)*.75f);
            });
            _pose(null,Vector2.zero,1);_apply(e.After);_impact(e);
            weapon.gameObject.SetActive(false);if(element!=null)element.gameObject.SetActive(false);
            if(!cancel&&!dodge)
            {
                var impact=Take(e.Mitigated?"Overlay/taichi":e.Outcome==CombatVisualOutcome.Blocked?"Overlay/shield":AttackDamageTypes.IsMagic(e.Type)?"fx_"+AttackDamageTypes.IconKeyOf(e.Type):e.Type==AttackDamageType.Slashing?"fx_slash":"fx_impact",120);
                impact.rectTransform.anchoredPosition=to;
                yield return Tween(.22f,t=>{impact.rectTransform.localScale=Vector3.one*(.7f+t*.7f);impact.rectTransform.localRotation=Quaternion.Euler(0,0,e.Mitigated?t*180:0);impact.color=new Color(1,1,1,1-t);});
            }
            else yield return Tween(.12f,t=>{if(evade!=null)evade.color=new Color(dodge?.3f:.9f,dodge?1:.4f,1,1-t);});
        }
        private IEnumerator Shield(CombatVisualEvent e,Vector2 pos)
        {
            if(e.Kind==CombatVisualKind.Sync){_apply(e.After);yield break;}
            bool broken=e.Kind==CombatVisualKind.ShieldBreak;
            var icon=Take(e.Kind==CombatVisualKind.Taichi?"Overlay/taichi":broken?"Overlay/shield_broken":"Overlay/shield",125);icon.rectTransform.anchoredPosition=pos;
            Image first=broken?Take("shield_fragment_1",125):null,second=broken?Take("shield_fragment_2",125):null;
            yield return Tween(.32f,t=>{
                icon.rectTransform.localScale=Vector3.one*(.55f+Mathf.Sin(t*Mathf.PI)*.55f);icon.color=new Color(1,1,1,1-t);
                icon.rectTransform.localRotation=Quaternion.Euler(0,0,e.Kind==CombatVisualKind.Taichi?t*180:0);
                if(first!=null){first.rectTransform.anchoredPosition=pos+new Vector2(-50,50)*t;second.rectTransform.anchoredPosition=pos+new Vector2(50,-50)*t;first.color=second.color=new Color(1,1,1,1-t);}
                if(t>=.35f)_apply(e.After);
            });_apply(e.After);_impact(e);
        }
        private IEnumerator Tween(float duration,Action<float> update)
        {for(float t=0;t<duration&&!_skip;t+=Time.unscaledDeltaTime*Speed){update(Mathf.Clamp01(t/duration));yield return null;}update(1);}
        public void StopPlayback()
        {if(_routine!=null)StopCoroutine(_routine);ClearFinalNumbers();_pose?.Invoke(null,Vector2.zero,1);_routine=null;_complete=null;Busy=false;Current=null;_skip=false;HideWindup();ReleaseUsed();if(_controls!=null)_controls.SetActive(false);}
        private void OnDisable()=>StopPlayback();
    }
}
