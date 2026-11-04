using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
 [TestFixture] public class NewDruidFormsTests
 {
  [Test] public void All_six_wild_options_have_health_and_description(){Assert.AreEqual(6,DruidForms.Options("moon",false).Count);foreach(var option in DruidForms.Options("moon",false))Assert.IsTrue(option.Description.Length>0);}
  [Test] public void Dragon_no_longer_rewrites_normal_operations(){Assert.IsFalse(DruidForms.IsDragon(CharacterFactory.CreatePlayer("星",CharacterDefinition.CircleOfTheStars)));}
 }
}
