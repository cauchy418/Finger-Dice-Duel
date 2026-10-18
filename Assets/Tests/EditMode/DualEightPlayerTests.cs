using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>8 人阵营混战的双将形态：8 名玩家各带两名同阵营角色。</summary>
    [TestFixture]
    public class DualEightPlayerTests
    {
        private static int FactionOf(CharacterDefinition def)
        {
            CodexEntry entry = CharacterCodex.For(def);
            string axis = entry == null ? "中立" : Alignments.LawAxisOf(entry.Alignment);
            if (axis == "守序") return 0;
            if (axis == "混乱") return 2;
            return 1;
        }

        [Test]
        public void Eight_dual_players_use_sixteen_unique_characters()
        {
            var byAxis = new List<CharacterDefinition>[3];
            for (int i = 0; i < 3; i++) byAxis[i] = new List<CharacterDefinition>();
            foreach (CharacterDefinition def in CharacterDefinition.BuiltIn)
                byAxis[FactionOf(def)].Add(def);

            var players = new List<PlayerState>(8);
            var teams = new List<int>(8);
            var names = new HashSet<string>();

            int[] counts = { 3, 3, 2 };
            for (int f = 0; f < 3; f++)
            {
                for (int i = 0; i < counts[f]; i++)
                {
                    CharacterDefinition primary = byAxis[f][i * 2];
                    CharacterDefinition secondary = byAxis[f][i * 2 + 1];
                    PlayerState p = DualGeneralFactory.Create("P" + (players.Count + 1),
                                                              primary, secondary);
                    players.Add(p);
                    teams.Add(f);
                    names.Add(primary.DisplayName);
                    names.Add(secondary.DisplayName);
                }
            }

            var providers = new List<IChoiceProvider>(8);
            for (int i = 0; i < 8; i++) providers.Add(new SimpleChoiceProvider());

            GameManager game = GameManager.CreateEight(players, providers, teams);

            Assert.AreEqual(8, game.Players.Count);
            Assert.IsTrue(game.IsEightPlayerFactionMode);
            Assert.AreEqual(16, names.Count, "8 名双将应尽量使用 16 名不重复角色。");
            foreach (PlayerState p in players)
            {
                Assert.IsTrue(p.Character is DualGeneralBehavior);
                Assert.True(!string.IsNullOrEmpty(p.DisplayNameOverride));
            }
        }
    }
}
