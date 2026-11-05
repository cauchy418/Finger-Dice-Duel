using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;
namespace FingerDiceDuel.Assistant
{
    public static partial class CampaignContent
    {
        private static CampaignSpec GalleryBase(int n,CharacterDefinition role,int health=28)
        {
            var s=new CampaignSpec(n,role,new[]{1,2,3,2});
            s.Npcs[1]=new CampaignNpcSpec(n==18||n==19?"元素回响":"元素精魂",n==18||n==19?"campaign_elemental_echo":"campaign_elemental_spirit",health){Range=CharacterTemplate.InfiniteAttackRange,Type=AttackDamageType.Bludgeoning};
            return s;
        }
        private static CampaignSpec GalleryCourse(int n)
        {
            if(n==18)return ReactionCourse();
            if(n==19)return ThreeAspectsCourse();
            if(n==20)
            {
                var finale=GalleryBase(n,CharacterDefinition.StormSorcery,44);
                finale.Npcs[1].Name="万象回响";finale.Npcs[1].Id="campaign_elemental_echo";
                finale.Npcs[1].Attack=1;finale.Npcs[1].Cycle=true;return finale;
            }
            if(n==17)return ShadowCourse();
            if(n<=12)
            {
                var s=GalleryBase(n,CharacterDefinition.StormSorcery,26);
                int roll=n==11?40:0;
                s.Npcs[1].Type=n==11?AttackDamageType.Thunder:AttackDamageType.Lightning;
                if(n==11){s.Npcs[1].Range=3;s.Opening=l=>l.Game.ConfigureSeatObstacles(1,1);}
                var hit=s.Op(0,0,1,0,"暴风狂怒");hit.Option=(int)(n==11?AttackDamageType.Thunder:AttackDamageType.Lightning);hit.ForcedRoll=roll;
                if(n==11){hit.PureSource=0;hit.PureOwner=1;hit.PureAddend=1;}
                s.Op(1,1,0,0,"观察元素限制",0,8).ForcedRoll=roll;
                s.Wait(0,"观察状态").ForcedRoll=roll;
                return s;
            }
            var dragon=GalleryBase(n,CharacterDefinition.DraconicBloodline);
            var type=n==13?AttackDamageType.Fire:n==14?AttackDamageType.Cold:n==15?AttackDamageType.Poison:AttackDamageType.Acid;
            dragon.Npcs[1].Type=type;
            dragon.Opening=l=>l.Seats[1].AddImmunity(type);
            var immune=dragon.Op(0,0,1,0,"先祖与免疫");immune.Option=(int)type;
            dragon.Op(1,1,0,0,"反击",0,6).Prepare=(l,p)=>{l.Equip(p,AttackDamageType.Bludgeoning);l.Tune(p,p.LeftHand,2);};
            dragon.Op(0,0,1,0,"龙族体魄");
            dragon.Op(1,1,0,1,"观察对应负面",0,n==14?4:8).Prepare=(l,p)=>l.Tune(p,p.LeftHand,n==16?8:7);
            if(n==16)
            {
                // Vulnerability is visible before the second acid hit, after the first
                // one has already demonstrated the persistent defense penalty.
                var hit=dragon.Op(0,0,1,0,"酸蚀易伤");
                var previous=dragon.Steps[dragon.Steps.Count-2];var prepare=previous.Prepare;
                previous.Prepare=(l,p)=>{prepare(l,p);p.AddVulnerability(AttackDamageType.Acid);l.Game.NoteSkill(p,"试验铭刻","一次强酸易伤",p);};
                dragon.Op(1,1,0,0,"保留回复加数",0).Prepare=(l,p)=>l.Tune(p,p.LeftHand,7);
            }
            dragon.Op(0,1,1,0,"回复");return dragon;
        }
        private static CampaignSpec ShadowCourse()
        {
            var s=GalleryBase(17,CharacterDefinition.ShadowMagic);s.Hands=new[]{1,6,3,2};s.First=1;s.Npcs[1].Type=AttackDamageType.Necrotic;
            s.Opening=l=>l.Player.SetHealthExact(5);
            s.Op(1,0,0,0,"坟冢之力",0).Prepare=(l,p)=>{
                if(!(l.Player.Character is ShadowMagicLogic shadow)||shadow.GraveSpent||l.Player.Health!=5)throw new InvalidOperationException("坟冢之力演示起点不符");
                l.Player.TrainingProtected=false;l.Equip(p,AttackDamageType.Bludgeoning);l.Tune(p,p.RightHand,3);
            };
            var dark=s.Op(0,0,1,1,"黑暗术");dark.Option=1;
            s.Op(1,0,0,0,"安魂与失明",0).Prepare=(l,p)=>{l.Player.TrainingProtected=true;CampaignNpcLogic.Heal(l.Game,p,l.Player,8,"安魂");l.Tune(p,p.RightHand,2);};
            s.Op(0,0,1,1,"幽暗之瞳");
            s.Op(1,0,0,0,"腐朽阻止回复",0,9).Prepare=(l,p)=>l.Tune(p,p.RightHand,3);
            s.Op(0,1,1,1,"正常回复");return s;
        }
        private static CampaignSpec ReactionCourse()
        {
            var s=GalleryBase(18,CharacterDefinition.WildMagic,32);
            s.Npcs[1].EveryThird=AttackDamageType.Fire;
            var types=new[]{DamageType.Thunder,DamageType.Lightning,DamageType.Thunder,DamageType.Cold,DamageType.Cold,DamageType.Cold,DamageType.Cold,DamageType.Fire,DamageType.Fire,DamageType.Fire,DamageType.Lightning,DamageType.Acid,DamageType.Poison};
            var precursors=new[]{StatusEffect.Resonance,StatusEffect.Resonance,StatusEffect.Shock,StatusEffect.Frost,StatusEffect.Wet,StatusEffect.Frozen,StatusEffect.Burn,StatusEffect.Frost,StatusEffect.Wet,StatusEffect.Greased,StatusEffect.Wet,StatusEffect.Poison,StatusEffect.Acid};
            s.First=1;
            for(int i=0;i<types.Length;i++)
            {
                int index=i,roll=Array.IndexOf(WildMagicLogic.DamageTypesForRoll,types[i])*8;
                bool prepared=false;
                var setup=s.Wait(1,"刻印");
                setup.Prepare=(l,p)=>{
                    if(p.HasStatus(StatusEffect.Dizzy))return;
                    if(index%2==0)l.Refit(p,16);else p.ClearNegativeStatuses();
                    // The trainer only tunes its own addend, during its own action.
                    int desired=index%2==0?4:6;
                    int source=l.Player.LeftHand.Value;
                    for(int value=1;value<=9;value++)if(Hand.ComputeSum(source,value)==desired){l.Tune(p,p.LeftHand,value);break;}
                    p.ApplyStatus(precursors[index],2,p);
                    l.Game.NoteSkill(p,"刻印",StatusNames.Of(precursors[index])+"2回合",p);prepared=true;
                };
                int page=index==0?2:index<=2?3:index<=4?4:index<=6?5:index<=9?6:index==10?7:8;
                setup.Verify=l=>prepared;setup.ForcedRoll=roll;setup.GuideIndex=index==0?0:page;
                var hit=s.Op(0,0,1,0,"观察反应",1,index%2==0?4:6);hit.ForcedRoll=roll;
                hit.Verify=l=>l.Board.LastTurn.AttackType==AttackDamageTypes.FromDamageType(types[index])&&l.Board.LastTurn.DamageDealt>0;
                hit.GuideIndex=page;
                // Let the victim really complete (or lose) an action under its status.
                if(index!=6&&index!=7){var observe=s.Op(1,1,0,1,"观察状态行动",0,index==1?9:1);observe.ForcedRoll=roll;observe.GuideIndex=page;}
            }
            return s;
        }
        private static CampaignSpec ThreeAspectsCourse()
        {
            var s=GalleryBase(19,CharacterDefinition.SchoolOfEvocation,30);
            var types=new[]{DamageType.Radiant,DamageType.Force,DamageType.Psychic};
            for(int i=0;i<types.Length;i++)
            {
                int index=i;
                Cast(s,0,4,"法术塑形",true,types[i]);
                var prep=s.Op(1,1,0,1,"换相",0,index==0?4:1);
                prep.Prepare=(l,p)=>{
                    l.Equip(p,index==0?AttackDamageType.Radiant:AttackDamageType.Bludgeoning);
                    if(index==1){((CampaignNpcLogic)p.Character).Defense=3;l.Game.NoteSkill(p,"架盾试验","防+3",p);}
                    if(index==2)l.Refit(p);
                };
                if(index==1)prep.Kind=CampaignActionKind.RaiseShield;
                Cast(s,0,6,"释放类型");
                s.Wait(1,"观察命中状态");
            }
            s.TrialHands=new[]{1,2,3,5};s.Npcs[1].Type=AttackDamageType.Radiant;s.Npcs[1].Shield=true;
            s.TrialSetup=l=>((CampaignNpcLogic)l.Seats[1].Character).Defense=1;
            return s;
        }
    }
}
