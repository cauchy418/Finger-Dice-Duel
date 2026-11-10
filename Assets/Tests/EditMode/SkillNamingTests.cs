using System.Collections.Generic;
using System.Reflection;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests {
[TestFixture] public class SkillNamingTests {
[Test] public void Current_skill_names_have_animation_routes() {
foreach(var name in new[]{"阴阳相生","魔术之手","魔术扑克"}) Assert.IsTrue(SkillVisualProfiles.Key(name)!=null,name);
}
[Test] public void Tai_chi_buttons_distinguish_yin_defense_and_yang_attack() {
var type=typeof(CharacterDefinition).Assembly.GetType("FingerDiceDuel.Characters.SpecialLogics.WayOfTheTaiChiLogic")??typeof(CharacterDefinition).Assembly.GetType("FingerDiceDuel.Core.WayOfTheTaiChiLogic");
var options=(List<SkillChoiceOption>)type.GetMethod("BoostOptions",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
Assert.AreEqual("阴阳相生·阴，防",options[1].Label);Assert.AreEqual("阴阳相生·阳，攻",options[2].Label);
}
}}
