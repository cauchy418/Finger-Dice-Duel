using System.Collections.Generic;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private readonly Dictionary<RectTransform,RectTransform> _druidHints=new Dictionary<RectTransform,RectTransform>();
        private readonly Dictionary<RectTransform,string> _druidHintKeys=new Dictionary<RectTransform,string>();
        private void RefreshDruidForms(RectTransform parent,PlayerState player,bool compact=false)
        {
            RectTransform root;
            if(!_druidHints.TryGetValue(parent,out root)){root=FrontEndArt.Rect(parent,"DruidFormHints",Vector2.zero,Vector2.one);_druidHints[parent]=root;}
            bool caster=DruidWildShape.Has<IDruidCaster>(player.DruidOriginal);
            string key=caster+"|"+player.WildForm+"|"+player.WildStar+"|"+player.WildCharges+"|"+player.IsPolymorphed;
            if(_druidHintKeys.TryGetValue(parent,out var oldKey)&&oldKey==key)return;
            _druidHintKeys[parent]=key;
            foreach(Transform child in root){child.gameObject.SetActive(false);Object.Destroy(child.gameObject);}
            var forms=new List<IDruidForm>();if(caster){forms.Add(new DruidFormView("moon",player.WildForm));if(DruidWildShape.Has<FingerDiceDuel.Characters.SpecialLogics.CircleOfTheStarsLogic>(player.DruidOriginal))forms.Add(new DruidFormView("stars",player.WildStar));}root.gameObject.SetActive(forms.Count>0);
            if(forms.Count==0)return;
            float size=compact?26:36;float x=compact?parent.sizeDelta.x/2-(forms.Count==2?38:22):340;float startY=compact?25:30;
            root.anchoredPosition=new Vector2(x,startY);
            int row=0;
            foreach(var form in forms)
            {
                var options=DruidForms.Options(form.CircleKey,false);
                if(form.CircleKey=="moon"&&!DruidWildShape.Has<FingerDiceDuel.Characters.SpecialLogics.CircleOfTheMoonLogic>(player.DruidOriginal))options.RemoveAll(o=>o.Value>=5);
                for(int i=0;i<options.Count;i++)
                {
                    var option=options[i];var icon=ArtImage(root,"Form_"+form.CircleKey+"_"+option.Value,option.IconKey,new Vector2(forms.Count==2?(form.CircleKey=="moon"?-size/2:size/2):0,-i*(size+4)),Vector2.one*size);
                    icon.color=form.FormValue==option.Value?Color.white:new Color(.6f,.6f,.6f,.8f);
                    if(form.FormValue==option.Value){var motion=icon.gameObject.AddComponent<CombatStatusIconMotion>();motion.Bind(new CombatStatusIcon(option.IconKey,option.Description,""));}
                    BattleTooltip.Set(icon,(form.FormValue==option.Value?"当前：":"可选：")+option.Label+"\n"+option.Description);
                }
                if(!compact){var label=CreateText(root,"Current_"+form.CircleKey,new Vector2(105,row* -55),new Vector2(90,52),18,TextAnchor.MiddleLeft,BattleArt.Gold);label.text=(form.CircleKey=="moon"?"荒野":"星耀")+"\n"+DruidForms.Name(form.CircleKey,form.FormValue);}
                row++;
            }
        }
    }
}
