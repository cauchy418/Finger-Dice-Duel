using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;
namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private CombatAnimationPlayer _combatAnimations;private CanvasGroup _combatInput;
        private CombatStatusMotion _statusMotion;
        public bool CombatAnimationBusy=>_combatAnimations!=null&&_combatAnimations.Busy;
        private void EnsureCombatAnimations()
        {
            if(_combatAnimations!=null)return;
            _combatInput=_canvasRoot.GetComponent<CanvasGroup>();if(_combatInput==null)_combatInput=_canvasRoot.gameObject.AddComponent<CanvasGroup>();
            _combatAnimations=_canvasRoot.gameObject.AddComponent<CombatAnimationPlayer>();
            _combatAnimations.Initialize(_canvasRoot,_font,CombatHandAnchor,CombatTargetAnchor,ApplyCombatFrame,CombatImpact,PoseCombatEvasion);
            var ambient=new GameObject("PersistentStatusEffects",typeof(RectTransform));ambient.transform.SetParent(_canvasRoot,false);var rect=(RectTransform)ambient.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            rect.SetSiblingIndex(((RectTransform)fieldAnimationRoot()).GetSiblingIndex());_statusMotion=ambient.AddComponent<CombatStatusMotion>();_statusMotion.Initialize(rect,CombatStatusAnchor);
            // Return and the history remain available while gameplay controls are locked.
            foreach(var button in new[]{_exitButton})if(button!=null)
            {var g=button.GetComponent<CanvasGroup>();if(g==null)g=button.gameObject.AddComponent<CanvasGroup>();g.ignoreParentGroups=true;}
        }
        public void ShowCombatWindup(PlayerState actor,Hand hand,string weapon,AttackDamageType type)
        {EnsureCombatAnimations();_combatAnimations.SetWindup(actor,hand,weapon,type);}
        public void HideCombatWindup(){_combatAnimations?.HideWindup();}
        private Transform fieldAnimationRoot()=>_canvasRoot.Find("CombatAnimationLayer");
        public void SyncCombatStatus(CombatVisualFrame frame){EnsureCombatAnimations();_statusMotion.Sync(frame);}
        public void PlayCombatEvents(IReadOnlyList<CombatVisualEvent> events,CombatVisualFrame final,Action complete)
        {
            EnsureCombatAnimations();_combatInput.interactable=false;
            _combatAnimations.Play(events,final,()=>{_combatInput.interactable=true;complete?.Invoke();});
        }
        private Vector2 CombatPosition(RectTransform rect)=>rect==null?Vector2.zero:(Vector2)_canvasRoot.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
        private Vector2 CombatStatusAnchor(PlayerState player)
        {
            foreach(var panel in _ringPanels)if(panel.Root.gameObject.activeInHierarchy&&ReferenceEquals(panel.Owner,player))return CombatPosition(panel.Emblem.rectTransform);
            return CombatPosition(ReferenceEquals(_cardsA[0].Owner,player)?_badgeA.rectTransform:_badgeB.rectTransform);
        }
        private readonly Dictionary<RectTransform,Vector2> _evadeOrigins=new Dictionary<RectTransform,Vector2>();
        private void PoseCombatEvasion(PlayerState player,Vector2 offset,float alpha)
        {
            if(player==null){foreach(var pair in _evadeOrigins){pair.Key.anchoredPosition=pair.Value;var cg=pair.Key.GetComponent<CanvasGroup>();if(cg!=null)cg.alpha=1;}_evadeOrigins.Clear();return;}
            RectTransform root=null;
            foreach(var panel in _ringPanels)if(panel.Root.gameObject.activeInHierarchy&&ReferenceEquals(panel.Owner,player)){root=panel.Root;break;}
            if(root==null)root=ReferenceEquals(_cardsA[0].Owner,player)?_areaA:_areaB;
            if(!_evadeOrigins.ContainsKey(root))_evadeOrigins[root]=root.anchoredPosition;
            root.anchoredPosition=_evadeOrigins[root]+offset;var group=root.GetComponent<CanvasGroup>();if(group==null)group=root.gameObject.AddComponent<CanvasGroup>();group.alpha=alpha;
        }
        private IEnumerable<HandCard> CombatCards()
        {
            foreach(var card in _cardsA)yield return card;foreach(var card in _cardsB)yield return card;
            foreach(var panel in _ringPanels)if(panel.Root.gameObject.activeInHierarchy)foreach(var card in panel.Cards)yield return card;
        }
        private Vector2 CombatHandAnchor(PlayerState player,Hand hand)
        {
            foreach(var panel in _ringPanels)if(panel.Root.gameObject.activeInHierarchy&&ReferenceEquals(panel.Owner,player))
                foreach(var card in panel.Cards)if(ReferenceEquals(card.Hand,hand))return CombatPosition(card.Button.GetComponent<RectTransform>());
            foreach(var card in CombatCards())if(ReferenceEquals(card.Owner,player)&&ReferenceEquals(card.Hand,hand))return CombatPosition(card.Button.GetComponent<RectTransform>());
            return CombatTargetAnchor(player);
        }
        private Vector2 CombatTargetAnchor(PlayerState player)
        {
            foreach(var panel in _ringPanels)if(panel.Root.gameObject.activeInHierarchy&&ReferenceEquals(panel.Owner,player))return CombatPosition(panel.HpFill.rectTransform);
            if(_cardsA.Length>0&&ReferenceEquals(_cardsA[0].Owner,player))return CombatPosition(_hpBarFillA.rectTransform);
            if(_cardsB.Length>0&&ReferenceEquals(_cardsB[0].Owner,player))return CombatPosition(_hpBarFillB.rectTransform);
            return Vector2.zero;
        }
        private void ApplyCombatFrame(CombatVisualFrame frame)
        {
            if(frame==null)return;
            _statusMotion?.Sync(frame);
            foreach(var state in frame.Players)
            {
                var p=state.Player;
                foreach(var card in CombatCards())if(ReferenceEquals(card.Owner,p)&&card.Button.gameObject.activeInHierarchy)
                {
                    var hand=ReferenceEquals(card.Hand,p.LeftHand)?state.Left:state.Right;
                    card.ValueText.text=hand.Value.ToString();card.StatusText.text=HandVisual.StatusText(hand,p);
                    RefreshPaintedHand(card,hand,HandVisual.OutlineState.Hidden);
                    card.Gesture.color=state.Health>0?Color.white:new Color(.45f,.45f,.45f);
                }
                foreach(var panel in _ringPanels)if(ReferenceEquals(panel.Owner,p))
                {
                    panel.HpText.text=state.Health+" / "+state.MaximumHealth;panel.HpFill.fillAmount=state.MaximumHealth==0?0:(float)state.Health/state.MaximumHealth;
                    panel.StatusText.text=state.Stats;panel.StatusIcons.RefreshSnapshot(p,state.Icons);panel.Emblem.sprite=BattleArt.Get(state.Emblem);
                    panel.Emblem.color=state.Health>0?Color.white:Color.gray;panel.Root.GetComponent<Image>().color=state.Health>0?Color.white:new Color(.58f,.58f,.58f);
                }
                bool a=_cardsA.Length>0&&ReferenceEquals(_cardsA[0].Owner,p),b=_cardsB.Length>0&&ReferenceEquals(_cardsB[0].Owner,p);
                if(a||b)
                {
                    (a?_healthTextA:_healthTextB).text=state.Health+" / "+state.MaximumHealth;
                    (a?_hpBarFillA:_hpBarFillB).fillAmount=state.MaximumHealth==0?0:(float)state.Health/state.MaximumHealth;
                    (a?_statusTextA:_statusTextB).text=state.Stats;
                    (a?_statusIconsA:_statusIconsB)?.RefreshSnapshot(p,state.Icons);
                    (a?_badgeA:_badgeB).sprite=BattleArt.Get(state.Emblem);
                }
            }
        }
        private void CombatImpact(CombatVisualEvent e)
        {
            _statusMotion?.Flash(e.Target,e.Kind==CombatVisualKind.Skill?.6f:.35f);
            if(e.Kind==CombatVisualKind.Skill){_presentation?.PlaySfx(SfxKind.ShieldUp);return;}
            if(e.Kind!=CombatVisualKind.Attack){_presentation?.PlaySfx(SfxKind.ShieldUp);return;}
            bool hit=e.Outcome==CombatVisualOutcome.Hit,blocked=e.Outcome==CombatVisualOutcome.Blocked;
            string text=hit?"−"+e.Damage:blocked?"格挡":e.Outcome==CombatVisualOutcome.Dodged?"闪避":e.EvasionActor!=null?"妖精步伐 · 取消":"攻击中止";
            int color=e.Outcome==CombatVisualOutcome.Cancelled?0xb97ee8:AttackDamageTypes.ColorOf(e.Type);
            var floats=new[]{new FloatTextSpec(e.Target,text,hit?FloatKind.Damage:blocked?FloatKind.Blocked:FloatKind.Dodged,0,color)};
            HitTier tier=hit&&e.Damage>0?e.Damage>=6?HitTier.Heavy:HitTier.Light:HitTier.None;
            var impacts=tier==HitTier.None?null:new[]{new HitImpactSpec(e.Target,tier,tier==HitTier.Heavy?5:2,0)};
            _presentation?.Play(new AnimationPlan(floats,impacts),null,null);
            _presentation?.PlaySfx(blocked?SfxKind.Blocked:!hit?SfxKind.Dodged:e.Damage>=6?SfxKind.HitHeavy:SfxKind.HitLight);
        }
        private void StopCombatPlayback()
        {_combatAnimations?.StopPlayback();_statusMotion?.Clear();if(_combatInput!=null)_combatInput.interactable=true;}
    }
}
