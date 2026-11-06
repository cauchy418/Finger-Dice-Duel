using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class RevisionBalanceTests
    {
        private static readonly string Root=Environment.GetEnvironmentVariable("FDD_REVISION_ROOT")??"Tools/HeadlessTests/bin/revision-sample-data";
        private static readonly CharacterDefinition[] Roster=CharacterDefinition.BuiltIn;
        private static void Row(StreamWriter w,int seed,string first,string second,int winner,int turns)
        {w.WriteLine(seed+","+first+","+second+","+winner+","+turns);if(++_written%1000==0){w.Flush();Console.WriteLine("progress "+_written+" games");}}
        private static int _written;
        private static GreedyChoiceProvider Ai()=>new GreedyChoiceProvider{UseValidatedTuning=true,UseChainBreak=true};
        [Test] public void Revision_all_modes_win_rates()
        {
            string mode=Environment.GetEnvironmentVariable("FDD_REVISION_MODE")??"1v1";
            Directory.CreateDirectory(Root);int games=0;_written=0;
            if (mode == "audit")
            {
                RunAiAudit();
                return;
            }
            if (mode == "tune-1v1" || mode == "tune-2v2")
            {
                RunTuning(mode);
                return;
            }
            if(mode=="1v1")File.WriteAllLines(Root+"/roster.tsv",new[]{"id\tname\taxis"}.Concat(Roster.Select(d=>d.Id+"\t"+d.DisplayName+"\t"+Alignments.LawAxisOf(CharacterCodex.For(d).Alignment))),new System.Text.UTF8Encoding(false));
            using var writer=new StreamWriter(Root+"/"+mode+".csv",false,new System.Text.UTF8Encoding(false));
            writer.WriteLine("seed,first,second,winner,turns");
            if(mode=="1v1")
                for(int i=0;i<Roster.Length;i++)for(int j=i+1;j<Roster.Length;j++)for(int n=0;n<20;n++)
                {
                    int seed=500000+i*20000+j*100+n;
                    var r=CharacterBalanceTests.RunOne(Roster[i],Roster[j],n%2==0,seed,out string error);
                    Assert.IsTrue(error==null,"seed="+seed+" "+error);Assert.IsFalse(r.HitTurnLimit);
                    int winner=r.Winner==null?-1:r.Winner.Definition.Id==Roster[i].Id?0:1;
                    Row(writer,seed,Roster[i].Id,Roster[j].Id,winner,r.Turns);games++;
                }
            else if(mode=="2v2")
            {
                var teams=new List<int[]>();for(int i=0;i<Roster.Length;i++)for(int j=i+1;j<Roster.Length;j++)teams.Add(new[]{i,j});
                for(int i=0;i<teams.Count;i++)for(int n=0;n<16;n++)
                {
                    var a=teams[i];var b=teams[(i+teams.Count/2)%teams.Count];int seed=3000000+i*100+n;
                    var r=TwoVersusTwoHarness.Play(Roster[a[0]],Roster[b[0]],Roster[b[1]],Roster[a[1]],seed,50,out int actions);
                    Assert.IsTrue(r.Error==null,"seed="+seed+" "+r.Error);Assert.IsFalse(r.HitLimit);
                    Row(writer,seed,Roster[a[0]].Id+"+"+Roster[a[1]].Id,Roster[b[0]].Id+"+"+Roster[b[1]].Id,r.Seat14Won?0:r.Seat23Won?1:-1,actions);games++;
                }
            }
            else if(mode=="dual")
            {
                var pairs=DualVersusDualHarness.AllDuals();
                for(int i=0;i<pairs.Count;i++)for(int n=0;n<8;n++)
                {
                    var a=pairs[i];var b=pairs[(i+pairs.Count/2)%pairs.Count];int seed=4000000+i*100+n;
                    var r=DualVersusDualHarness.Play(a,b,n%2==0,seed);Assert.IsTrue(r.Error==null,"seed="+seed+" "+r.Error);Assert.IsFalse(r.HitLimit);
                    Row(writer,seed,a.Primary.Id+"+"+a.Secondary.Id,b.Primary.Id+"+"+b.Secondary.Id,r.FirstWon.HasValue?(r.FirstWon.Value?0:1):-1,r.Turns);games++;
                }
            }
            else if(mode=="eight"||mode=="identity"||mode=="identity-preferred")
            {
                var identityRates = new Dictionary<(IdentityRole Role, string Id), (int Wins, int Games)>();
                var preference = mode == "identity-preferred"
                    ? new IdentityDraftPreference(File.ReadAllText("Assets/Resources/Art/Battle/v1/Adventure/identity-role-rates.txt"))
                    : new IdentityDraftPreference(null);
                int sampleSize = mode == "identity" ? 2000 : 600;
                for(int n=0;n<sampleSize;n++)
                {
                    int seed=(mode=="identity-preferred" ? 5100000 : 5000000)+n;var rng=new Random(seed);var pool=new List<CharacterDefinition>(Roster);
                    for(int i=pool.Count-1;i>0;i--){int j=rng.Next(i+1);var d=pool[i];pool[i]=pool[j];pool[j]=d;}
                    var players=new List<PlayerState>();var sinks=new List<IChoiceProvider>();var ais=new List<IMoveSelector>();
                    var teams=new List<int>{0,0,0,1,1,1,2,2};int weak=n%3;for(int i=0;i<teams.Count;i++)teams[i]=(teams[i]+weak)%3;
                    if(mode=="eight")
                    {
                        var axes=new[]{"守序","中立","混乱"};
                        var factionPools=new List<CharacterDefinition>[3];
                        for(int t=0;t<3;t++)factionPools[t]=new List<CharacterDefinition>(Roster.Where(d=>Alignments.LawAxisOf(CharacterCodex.For(d).Alignment)==axes[t]));
                        for(int i=0;i<8;i++){var available=factionPools[teams[i]];int pick=rng.Next(available.Count);pool[i]=available[pick];available.RemoveAt(pick);}
                    }
                    var roles=new[]{IdentityRole.Lord,IdentityRole.Loyalist,IdentityRole.Loyalist,IdentityRole.Rebel,IdentityRole.Rebel,IdentityRole.Rebel,IdentityRole.Rebel,IdentityRole.Traitor};
                    for(int i=7;i>1;i--){int j=1+rng.Next(i);var r=roles[i];roles[i]=roles[j];roles[j]=r;}
                    var picks = new CharacterDefinition[8];
                    int offset = 0;
                    for (int i=0;i<8;i++)
                    {
                        int count = i == 0 ? 8 : 6;
                        picks[i] = mode == "eight" ? pool[i] : preference.Choose(roles[i], pool.GetRange(offset, count), rng);
                        offset += count;
                        players.Add(CharacterFactory.CreatePlayer("P"+i,picks[i]));
                        sinks.Add(new SimpleChoiceProvider());ais.Add(Ai());
                    }
                    var game=mode!="eight"?GameManager.CreateIdentityEight(players,sinks,roles):GameManager.CreateEight(players,sinks,teams);
                    foreach(var p in players)TwoVersusTwoHarness.ApplyClassicHands(p,seed+players.IndexOf(p)*31);
                    var result=TestSetup.RunSampling(game,ais,seed);Assert.IsFalse(result.HitTurnLimit);
                    string first="",second="";
                    for(int i=0;i<8;i++)
                    {
                        bool wins=mode!="eight"?WinningSide(game.WinningIdentity,roles[i]):game.WinningTeam==teams[i];
                        if(wins)first+=(first.Length==0?"":"+")+picks[i].Id;
                        else second+=(second.Length==0?"":"+")+picks[i].Id;
                        if (mode != "eight")
                        {
                            var key = (roles[i], picks[i].Id);
                            identityRates.TryGetValue(key, out var prior);
                            identityRates[key] = (prior.Wins + (wins ? 1 : 0), prior.Games + 1);
                        }
                    }
                    Row(writer,seed,first,second,first.Length==0?-1:0,result.Turns);games++;
                }
                if (mode != "eight")
                {
                    var rates = identityRates.OrderBy(x => x.Key.Role).ThenBy(x => x.Key.Id, StringComparer.Ordinal)
                        .Select(x => $"{x.Key.Role}\t{x.Key.Id}\t{x.Value.Wins}\t{x.Value.Games}");
                    File.WriteAllLines(Path.Combine(Root, mode + "-roles.tsv"),
                        new[] { "role\tid\twins\tgames" }.Concat(rates), new System.Text.UTF8Encoding(false));
                }
            }
            else throw new ArgumentException("Unknown sampling mode "+mode);
            writer.Flush();Assert.IsTrue(games>0);Console.WriteLine(mode+" completed "+games+" games; zero errors / forced aborts");
        }
        private static bool WinningSide(IdentityRole winner,IdentityRole role)
            =>winner==IdentityRole.Lord||winner==IdentityRole.Loyalist?role==IdentityRole.Lord||role==IdentityRole.Loyalist:winner!=IdentityRole.None&&winner==role;

        private static void RunAiAudit()
        {
            using var writer = new StreamWriter(Path.Combine(Root, "ai-audit.tsv"), false, new System.Text.UTF8Encoding(false));
            writer.WriteLine("id\tname\tattack\tdefense\tweights_0_to_9\tcombos\t1v1\t2v2\topening_counter_adjustments");
            var split = new DiceSplitAdvisor();
            int checks = 0;
            foreach (var role in Roster)
            {
                Assert.True(HandValueProfiles.HasExplicitProfile(role), role.Id);
                var ai = new GreedyChoiceProvider();
                var actor = CharacterFactory.CreatePlayer("A", role);
                var blank = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
                var providers = new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()};
                var duel = new GameManager(actor, blank, providers[0], providers[1]);
                string solo = TuningOf(ai, duel, actor);
                var team = GameManager.Create2v2(new[]{actor,blank,CharacterFactory.CreatePlayer("C", CharacterDefinition.Blank),
                    CharacterFactory.CreatePlayer("D", CharacterDefinition.Blank)},providers);
                string pair = TuningOf(ai, team, actor);
                var own = HandValueProfiles.For(role);
                var counters = new List<string>();
                foreach (var opponent in Roster)
                {
                    var against = HandValueProfiles.For(role, opponent);
                    var differences = Enumerable.Range(1, 9).Where(i => against.WeightOf(i) != own.WeightOf(i))
                        .Select(i => $"{i}:{against.WeightOf(i)-own.WeightOf(i):+0;-0}");
                    string delta = string.Join(",", differences);
                    if (delta.Length > 0) counters.Add(opponent.Id + "=" + delta);
                    foreach (int roll in new[]{4,10,17})
                    {
                        var legal = DiceRollSetup.DistinctSplits(roll);
                        var picked = split.Choose(role, opponent, legal);
                        Assert.True(legal.Any(s => s.Left == picked.Left && s.Right == picked.Right), role.Id + " vs " + opponent.Id);
                        checks++;
                    }
                }
                writer.WriteLine(string.Join("\t",role.Id,role.DisplayName,role.AttackBonus,role.DefenseBonus,
                    string.Join(",",own.Weights),string.Join(" / ",own.Combos),solo,pair,string.Join(" / ",counters)));
            }
            Console.WriteLine($"{Roster.Length} roles, {checks} legal opening decisions, runtime profiles exported");
        }

        private static string TuningOf(GreedyChoiceProvider ai, GameManager game, PlayerState actor)
        {
            ValidatedAiTuning.Apply(ai,game,actor);
            return $"{(ai.UseProjectedCounterplay ? "P" : "L")}/{ai.HandValueWeight}/{ai.DenyRatioPercent}";
        }

        private static void RunTuning(string mode)
        {
            bool teams = mode == "tune-2v2";
            var roles = new[]{CharacterDefinition.CircleOfTheMoon,CharacterDefinition.CircleOfTheStars,
                CharacterDefinition.CircleOfTheLand,CharacterDefinition.CircleOfSpores,CharacterDefinition.ShadowMagic};
            string[] variants = {"current","L67","P67","L100"};
            using var writer = new StreamWriter(Path.Combine(Root, mode + ".csv"), false, new System.Text.UTF8Encoding(false));
            writer.WriteLine("role,phase,trial,variant,seed,won");
            int games = 0;
            for (int roleIndex = 0; roleIndex < roles.Length; roleIndex++)
            {
                var role = roles[roleIndex];
                var pool = Roster.Where(r => r.Id != role.Id).ToArray();
                for (int phase = 0; phase <= 1; phase++)
                {
                    int trials = (teams ? 320 : pool.Length * 4) * (phase + 1);
                    for (int trial = 0; trial < trials; trial++)
                    {
                        int seed = 8100000 + (teams ? 100000 : 0) + roleIndex * 10000 + phase * 2000 + trial;
                        int seat = trial % 4;
                        var roster = new CharacterDefinition[4];
                        if (teams)
                        {
                            var rng = new Random(seed);
                            int[] picks = Enumerable.Range(0, pool.Length).ToArray();
                            for (int index = 0; index < 3; index++)
                            {
                                int swap = rng.Next(index, picks.Length);
                                int pick = picks[swap]; picks[swap] = picks[index]; picks[index] = pick;
                            }
                            for (int index = 0, next = 0; index < roster.Length; index++)
                                roster[index] = index == seat ? role : pool[picks[next++]];
                        }
                        foreach (var variant in variants)
                        {
                            bool won;
                            if (teams)
                            {
                                var providers = new GreedyChoiceProvider[4];
                                for (int index = 0; index < providers.Length; index++)
                                {
                                    providers[index] = Ai();
                                    providers[index].UseLearnedFocus = true;
                                }
                                ConfigureTrial(providers[seat], variant);
                                var result = TwoVersusTwoHarness.PlayWith(roster[0], roster[1], roster[2], roster[3], seed,
                                    50, providers, null, out _);
                                Assert.IsTrue(result.Error == null && !result.HitLimit, $"seed={seed} {result.Error}");
                                won = seat == 0 || seat == 3 ? result.Seat14Won : result.Seat23Won;
                            }
                            else
                            {
                                var result = CharacterBalanceTests.RunOne(role, pool[trial % pool.Length],
                                    (trial / pool.Length) % 2 == 0, seed, out string error, ai => ConfigureTrial(ai, variant));
                                Assert.IsTrue(error == null && !result.HitTurnLimit, $"seed={seed} {error}");
                                won = result.Winner?.Definition.Id == role.Id;
                            }
                            writer.WriteLine($"{role.Id},{phase},{trial},{variant},{seed},{(won ? 1 : 0)}");
                            if (++games % 1000 == 0) { writer.Flush(); Console.WriteLine("progress " + games + " games"); }
                        }
                    }
                }
            }
            Console.WriteLine(mode + " completed " + games + " games; zero errors / forced aborts");
        }

        private static void ConfigureTrial(GreedyChoiceProvider ai, string variant)
        {
            if (variant == "current") return;
            ai.UseValidatedTuning = false;
            ai.HandValueWeight = 12;
            ai.DenyRatioPercent = variant == "L100" ? 100 : 67;
            ai.UseProjectedCounterplay = variant == "P67";
        }
    }
}
