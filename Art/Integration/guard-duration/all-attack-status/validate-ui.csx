using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
// Run via execute_csharp_script in Play mode. Names, resources, raycast clicks and layout.
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<object,string,object[]> invoke=(o,n,a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
var output=System.IO.Path.GetFullPath("Art/Integration/guard-duration/all-attack-status");System.IO.Directory.CreateDirectory(output);
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,msg)=>{if(!ok)throw new System.Exception(msg);checks.Add("PASS "+msg);};
System.Action<UnityEngine.Canvas,string> shot=(canvas,name)=>{
 var go=new UnityEngine.GameObject("ProfessionReviewCamera",typeof(UnityEngine.Camera));var cam=go.GetComponent<UnityEngine.Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
 var rt=new UnityEngine.RenderTexture(1920,1080,24);var pixels=new UnityEngine.Texture2D(1920,1080,UnityEngine.TextureFormat.RGB24,false);var prior=UnityEngine.RenderTexture.active;var mode=canvas.renderMode;var oldCam=canvas.worldCamera;var distance=canvas.planeDistance;
 try{cam.targetTexture=rt;canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;UnityEngine.Canvas.ForceUpdateCanvases();cam.Render();if(name!=null){UnityEngine.RenderTexture.active=rt;pixels.ReadPixels(new UnityEngine.Rect(0,0,1920,1080),0,0);pixels.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,name+".png"),UnityEngine.ImageConversion.EncodeToPNG(pixels));}}
 finally{canvas.renderMode=mode;canvas.worldCamera=oldCam;canvas.planeDistance=distance;cam.targetTexture=null;UnityEngine.RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Canvas.ForceUpdateCanvases();}
};


System.Action<object,string,object> set=(o,n,v)=>o.GetType().GetField(n,flags).SetValue(o,v);
var front=((UnityEngine.RectTransform)field(boot,"_selectRoot")).GetComponentInParent<UnityEngine.Canvas>();
var board=(FingerDiceDuel.UI.GameBoardUI)field(boot,"_board");
var canvas=((UnityEngine.RectTransform)field(board,"_canvasRoot")).GetComponent<UnityEngine.Canvas>();
System.Action refresh=()=>{invoke(boot,"RefreshView",new object[0]);UnityEngine.Canvas.ForceUpdateCanvases();};
System.Action<UnityEngine.UI.Button,UnityEngine.Canvas> click=(button,c)=>{
 shot(c,null);var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};
 var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
 check(button.gameObject.activeInHierarchy&&button.interactable&&hits.Count>0&&UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"raycast "+button.name);
 UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);UnityEngine.Canvas.ForceUpdateCanvases();
};


float delay=boot.AiThinkSeconds;bool aiB=boot.PlayerBIsAI;
try{
 boot.AiThinkSeconds=1000;boot.PlayerBIsAI=false;boot.StartMatch(CharacterDefinition.Champion,CharacterDefinition.Champion);
 var g=(GameManager)field(boot,"_game");var b=g.PlayerB;
 System.Action shield=()=>{b.LeftHand.SetValue(5);b.LeftHand.SetShieldState(ShieldState.Formed);};
 System.Func<int,AttackContext> attack=hits=>{var c=new AttackContext(g.PlayerA,b,g.PlayerA.LeftHand,8){AttackType=AttackDamageType.Fire,AttackerStatusOverride=StatusEffect.Burn,IgnoreStatBonuses=true,InflictsBleed=true,HitCount=hits};c.Resolve(g.GetChoiceProvider(b),g);return c;};
 shield();var first=attack(1);refresh();
 check(first.ElementHits[0].Duration(2)==1&&b.StatusTurnsLeft(StatusEffect.Burn)==1&&b.StatusTurnsLeft(StatusEffect.Bleed)==1,"shielded elemental burn and skill bleed both apply one turn");
 var bar=field(board,"_statusIconsB");var icons=(System.Collections.Generic.List<UnityEngine.UI.Image>)field(bar,"_icons");var turns=(System.Collections.Generic.List<UnityEngine.UI.Text>)field(bar,"_turns");
 check(icons[0].sprite.name=="Status/burn"&&turns[0].text=="1","actual burn marker displays one turn");check(icons.Any(x=>x.gameObject.activeInHierarchy&&x.sprite!=null&&x.sprite.name=="Status/bleed"),"actual battle UI includes skill bleed marker");shot(canvas,"shield-one-turn");
 b.ClearStatuses();b.AddResistance(AttackDamageType.Fire);shield();var none=attack(1);refresh();
 check(none.ElementHits[0].Duration(2)==0&&!b.HasStatus(StatusEffect.Burn)&&!b.HasStatus(StatusEffect.Bleed),"shield and corresponding resistance prevent burn and skill bleed");shot(canvas,"shield-resistance-zero");
 b.ClearAllResistances();shield();var multi=attack(2);refresh();
 check(multi.ElementHits.Count==2&&multi.ElementHits[0].Duration(2)==1&&multi.ElementHits[1].Duration(2)==2,"segments independently record one then two turns");
 check(b.StatusTurnsLeft(StatusEffect.Burn)==2&&b.StatusTurnsLeft(StatusEffect.Bleed)==2&&turns[0].text=="2","later unguarded hit refreshes both burn and skill bleed to two turns");shot(canvas,"multi-refresh-two");
 var log=string.Join(" | ",g.TakeSkillLog());check(log.Contains("第1段")&&log.Contains("第2段"),"skill log identifies each segment");
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"validation.txt"),checks);return "PASS "+checks.Count+" guard-duration UI checks";
}finally{boot.AiThinkSeconds=delay;boot.PlayerBIsAI=aiB;}
