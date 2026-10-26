using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    /// <summary>由独立留出实验生成，只采用多重比较校正后仍改善的角色／模式配置。</summary>
    public static class ValidatedAiTuning
    {
        public static void Apply(GreedyChoiceProvider ai,GameManager game,PlayerState actor)
        {
            ai.HandValueWeight=12;ai.DenyRatioPercent=43;ai.UseProjectedCounterplay=false;
            if(game==null||actor==null||actor.Character is ICharacterBehaviorContainer||actor.IsSummon)return;
            if(game.Players.Count==2)switch(actor.Definition.Id)
            {
                case "fighter_champion":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "monk_way_of_the_ninja":ai.HandValueWeight=8;ai.DenyRatioPercent=67;ai.UseProjectedCounterplay=true;break;
                case "warlock_the_archfey":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "fighter_arcane_archer":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "paladin_oath_of_the_crown":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "barbarian_path_of_the_giant":ai.HandValueWeight=12;ai.DenyRatioPercent=67;ai.UseProjectedCounterplay=true;break;
                case "rogue_thief":ai.HandValueWeight=12;ai.DenyRatioPercent=67;ai.UseProjectedCounterplay=true;break;
                case "rogue_magician":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "wizard_school_of_evocation":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "sorcerer_wild_magic":ai.HandValueWeight=12;ai.DenyRatioPercent=43;ai.UseProjectedCounterplay=true;break;
                case "paladin_oathbreaker":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "wizard_school_of_abjuration":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "cleric_light_domain":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "sorcerer_draconic_bloodline":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "monk_way_of_the_four_elements":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "druid_circle_of_the_moon":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "druid_circle_of_the_stars":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "paladin_oath_of_the_ancients":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "warlock_the_fiend":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "bard_college_of_lore":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "wizard_school_of_transmutation":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "fighter_eldritch_knight":ai.HandValueWeight=12;ai.DenyRatioPercent=100;ai.UseProjectedCounterplay=true;break;
                case "wizard_bladesinging":ai.HandValueWeight=12;ai.DenyRatioPercent=67;ai.UseProjectedCounterplay=true;break;
            }
        }
    }
}
