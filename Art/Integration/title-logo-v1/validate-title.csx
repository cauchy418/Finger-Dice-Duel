using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using FingerDiceDuel.UI;
var boot=UnityEngine.Object.FindObjectOfType<GameBootstrap>();if(boot==null)throw new Exception("No game bootstrap");
boot.ShowScreen(GameScreen.MainMenu);
var flags=BindingFlags.NonPublic|BindingFlags.Instance;
var root=(RectTransform)boot.GetType().GetField("_selectRoot",flags).GetValue(boot);var canvas=root.GetComponent<Canvas>();
var logo=root.Find("MainTitle").GetComponent<Image>();
if(logo.sprite==null||logo.sprite.name!="UI/title_logo_zh"||!logo.preserveAspect||logo.raycastTarget)throw new Exception("Title image settings invalid");
if(root.Find("SubTitle")!=null||root.Find("MainDivider")!=null)throw new Exception("Duplicate title decoration");
string output="C:/Users/29391/Finger Dice Duel/Art/Integration/title-logo-v1";
Action<int,int> render=(w,h)=>{
 var go=new GameObject("TitlePreviewCamera",typeof(Camera));var cam=go.GetComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;
 var rt=new RenderTexture(w,h,24);var tex=new Texture2D(w,h,TextureFormat.RGB24,false);var old=RenderTexture.active;var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var distance=canvas.planeDistance;var scaler=canvas.GetComponent<CanvasScaler>();
 try{cam.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;Canvas.ForceUpdateCanvases();scaler.GetType().GetMethod("Handle",flags).Invoke(scaler,new object[0]);Canvas.ForceUpdateCanvases();
  var titleBox=new Vector3[4];var buttonBox=new Vector3[4];logo.rectTransform.GetWorldCorners(titleBox);((RectTransform)root.Find("ModeAdventure")).GetWorldCorners(buttonBox);
  if(titleBox[0].y<=buttonBox[2].y)throw new Exception("Title overlaps adventure button");
  cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();System.IO.File.WriteAllBytes(output+"/main-menu-"+w+"x"+h+".png",ImageConversion.EncodeToPNG(tex));
 }finally{canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;cam.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);scaler.GetType().GetMethod("Handle",flags).Invoke(scaler,new object[0]);Canvas.ForceUpdateCanvases();}
};
render(1920,1080);render(1280,720);
return "PASS: new Chinese/English logo loaded, no duplicated titles, no button overlap at 1920x1080 and 1280x720";
