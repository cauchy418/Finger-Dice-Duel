using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.UI
{
    public sealed partial class GameBoardUI
    {
        private static string CharacterNotes(PlayerState player,string display)
        {
            if(player.PersistentCharacter is CampaignNpcLogic npc)
            {
                string notes=display+"\n"+AttackDamageTypes.NameOf(npc.Weapon)+"伤害，距离"+(npc.Range==int.MaxValue?"∞":npc.Range.ToString())+"。\n"+npc.AbilitySummary;
                if(npc.CanRaiseShield)notes+="\n架盾距可用："+npc.ShieldCooldown+"个自身回合。";
                if(npc.CanAid)notes+="\n急救剩余："+npc.AidCharges+"次；冷却："+npc.AidCooldown+"。";
                if(npc.CanRally)notes+="\n整军："+(npc.RallySpent?"已使用":"尚未使用")+"。";
                if(npc.CanMuster)notes+="\n集结令："+(npc.MusterSpent?"已使用":"尚未使用")+"。";
                return notes;
            }
            string text;
            return display+"\n"+(player.Definition!=null&&CharacterSkillText.TryGet(player.Definition.Id,out text)?text:player.Definition?.Description??"");
        }
    }
}
