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
        private static readonly string Root="Tools/Statistics/2026-10-30";
        private static readonly CharacterDefinition[] Roster=CharacterDefinition.BuiltIn;
        private static void Row(StreamWriter w,int seed,string first,string second,int winner,int turns)
        {w.WriteLine(seed+","+first+","+second+","+winner+","+turns);}
        private static GreedyChoiceProvider Ai()=>new GreedyChoiceProvider{UseValidatedTuning=true,UseChainBreak=true};
        [Test] public void Revision_all_modes_win_rates()
        {
            string mode=Environment.GetEnvironmentVariable("FDD_REVISION_MODE")??"1v1";
            Directory.CreateDirectory(Root);int games=0;
            using var writer=new StreamWriter(Root+"/"+mode+".csv",false,new System.Text.UTF8Encoding(false));
            writer.WriteLine("seed,first,second,winner,turns");
            if(mode=="1v1")
                for(int i=0;i<Roster.Length;i++)for(int j=i+1;j<Roster.Length;j++)for(int n=0;n<20;n++)
                {
                    int seed=500000+i*20000+j*100+n;
                    var r=CharacterBalanceTests.RunOne(Roster[i],Roster[j],n%2==0,seed,out string error);
                    Assert.IsTrue(error==null,error);Assert.IsFalse(r.HitTurnLimit);
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
                    Assert.IsTrue(r.Error==null,r.Error);Assert.IsFalse(r.HitLimit);
                    Row(writer,seed,Roster[a[0]].Id+"+"+Roster[a[1]].Id,Roster[b[0]].Id+"+"+Roster[b[1]].Id,r.Seat14Won?0:r.Seat23Won?1:-1,actions);games++;
                }
            }
            else if(mode=="dual")
            {
                var pairs=DualVersusDualHarness.AllDuals();
                for(int i=0;i<pairs.Count;i++)for(int n=0;n<8;n++)
                {
                    var a=pairs[i];var b=pairs[(i+pairs.Count/2)%pairs.Count];int seed=4000000+i*100+n;
                    var r=DualVersusDualHarness.Play(a,b,n%2==0,seed);Assert.IsTrue(r.Error==null,r.Error);Assert.IsFalse(r.HitLimit);
                    Row(writer,seed,a.Primary.Id+"+"+a.Secondary.Id,b.Primary.Id+"+"+b.Secondary.Id,r.FirstWon.HasValue?(r.FirstWon.Value?0:1):-1,r.Turns);games++;
                }
            }
            else if(mode=="eight"||mode=="identity")
            {
                for(int n=0;n<600;n++)
                {
                    int seed=5000000+n;var rng=new Random(seed);var pool=new List<CharacterDefinition>(Roster);
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
                    for(int i=0;i<8;i++){players.Add(CharacterFactory.CreatePlayer("P"+i,pool[i]));sinks.Add(new SimpleChoiceProvider());ais.Add(Ai());}
                    var game=mode=="identity"?GameManager.CreateIdentityEight(players,sinks,roles):GameManager.CreateEight(players,sinks,teams);
                    foreach(var p in players)TwoVersusTwoHarness.ApplyClassicHands(p,seed+players.IndexOf(p)*31);
                    var result=TestSetup.RunSampling(game,ais,seed);Assert.IsFalse(result.HitTurnLimit);
                    string first="",second="";
                    for(int i=0;i<8;i++)
                    {
                        bool wins=mode=="identity"?WinningSide(game.WinningIdentity,roles[i]):game.WinningTeam==teams[i];
                        if(wins)first+=(first.Length==0?"":"+")+pool[i].Id;
                        else second+=(second.Length==0?"":"+")+pool[i].Id;
                    }
                    Row(writer,seed,first,second,first.Length==0?-1:0,result.Turns);games++;
                }
            }
            else throw new ArgumentException("Unknown sampling mode "+mode);
            writer.Flush();Assert.IsTrue(games>0);Console.WriteLine(mode+" completed "+games+" games; zero errors / forced aborts");
        }
        private static bool WinningSide(IdentityRole winner,IdentityRole role)
            =>winner==IdentityRole.Lord||winner==IdentityRole.Loyalist?role==IdentityRole.Lord||role==IdentityRole.Loyalist:winner!=IdentityRole.None&&winner==role;
    }
}
