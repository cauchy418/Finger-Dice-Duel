using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;

internal static class AiTuning
{
    private sealed class Config
    {public string Name;public int Own,Deny;public bool Projected;public Config(string name,int own,int deny,bool projected){Name=name;Own=own;Deny=deny;Projected=projected;}}
    private static readonly Config[] Candidates={new Config("legacy12_43",12,43,false),new Config("projected12_0",12,0,true),new Config("projected12_25",12,25,true),new Config("projected12_43",12,43,true),new Config("projected12_67",12,67,true),new Config("projected12_100",12,100,true),new Config("projected8_43",8,43,true),new Config("projected16_43",16,43,true),new Config("projected8_67",8,67,true),new Config("projected16_25",16,25,true)};
    private static readonly CharacterDefinition[] Roles=CharacterDefinition.BuiltIn;
    private static double Play(int role,int count,int sample,int seedBase,Config config)
    {
        int seed=seedBase+sample*7919+role*104729+count*997;var rng=new Random(seed);
        var ps=new PlayerState[count];var sinks=new SimpleChoiceProvider[count];var ais=new GreedyChoiceProvider[count];
        int subject=sample%count;
        for(int i=0;i<count;i++)
        {
            int index=i==subject?role:(role+1+(sample*11+i*17)%(Roles.Length-1))%Roles.Length;
            ps[i]=CharacterFactory.CreatePlayer("P"+i,Roles[index]);sinks[i]=new SimpleChoiceProvider();
            ais[i]=new GreedyChoiceProvider{UseLearnedFocus=true,UseChainBreak=true};
            if(i==subject){ais[i].HandValueWeight=config.Own;ais[i].DenyRatioPercent=config.Deny;ais[i].UseProjectedCounterplay=config.Projected;}
            foreach(var part in CharacterBehaviors.Implementers<ICharacterBehavior>(ps[i]))
                foreach(var p in part.GetType().GetProperties())if(p.CanWrite&&p.PropertyType==typeof(Random))p.SetValue(part,new Random(seed+i*37));
        }
        var g=count==2?new GameManager(ps[0],ps[1],sinks[0],sinks[1]):GameManager.Create2v2(ps,sinks);
        g.StatusRandom=new Random(seed+71);StatusRules.ResetInteractionRandomForTest(seed+73);
        var advisor=new DiceSplitAdvisor();
        for(int i=0;i<count;i++)
        {
            int roll=rng.Next(1,21);var split=advisor.Choose(ps[i].Definition,ps[(i+1)%count].Definition,DiceRollSetup.LegalSplits(roll));
            ps[i].LeftHand.SetValue(split.Left);ps[i].RightHand.SetValue(split.Right);
        }
        var b=new BoardController(g,sinks[0],sinks[1],ais[0],ais[1]){GambleRng=new Random(seed+79)};
        for(int i=0;i<count;i++)b.BindPlayer(ps[i],ais[i],sinks[i]);
        b.BeginTurn();int guard=0;
        while(!g.IsGameOver&&guard++<2400)
        {
            b.AutoResolveAiCollegeOfValorChoice();
            bool progress=b.Phase==BoardPhase.ChoosingAttackerHand?b.MakeAutomaticMove():b.ResolvePendingAiChoice();
            if(!progress&&!g.IsGameOver)throw new Exception(Roles[role].Id+" stalled "+b.DebugPhaseInfo());
        }
        if(!g.IsGameOver)throw new Exception("guard exceeded");
        return g.Winner==null?.5:g.OriginalTeamOf(g.Winner)==g.OriginalTeamOf(ps[subject])?1:0;
    }
    private static void Main(string[] args)
    {
        int train=args.Length>0?int.Parse(args[0]):128,hold=args.Length>1?int.Parse(args[1]):320;
        string path="Tools/AiTuning/results.tsv";Directory.CreateDirectory("Tools/AiTuning");
        using(var output=new StreamWriter(path,false,new System.Text.UTF8Encoding(false)))
        {
            output.WriteLine("mode\tid\tname\tcandidate\ttrain_baseline\ttrain_candidate\thold_baseline\thold_candidate\tdelta\tpaired_se\tz\tfirst_half_delta\tsecond_half_delta\taccepted\tgames");
            foreach(int count in new[]{2,4})for(int r=0;r<Roles.Length;r++)
            {
                var totals=new double[Candidates.Length];
                for(int c=0;c<Candidates.Length;c++)for(int j=0;j<train;j++)totals[c]+=Play(r,count,j,1000003,Candidates[c]);
                int best=0;for(int c=1;c<Candidates.Length;c++)if(totals[c]>totals[best])best=c;
                double baseline=0,challenger=0,sum=0,sq=0,half=0;
                for(int j=0;j<hold;j++)
                {
                    double a=Play(r,count,j,7000009,Candidates[0]),b=best==0?a:Play(r,count,j,7000009,Candidates[best]);
                    baseline+=a;challenger+=b;double d=b-a;sum+=d;sq+=d*d;if(j<hold/2)half+=d;
                }
                double delta=sum/hold,se=Math.Sqrt(Math.Max(0,sq/hold-delta*delta)/hold),z=se>0?delta/se:0;
                // 一百项筛选采用保守 Bonferroni：单侧 z>=3.30，且留出集前后半段均改善。
                bool accept=best!=0&&delta>0&&half>0&&sum-half>0&&z>=3.30;
                string row=string.Join("\t",count==2?"1v1":"2v2",Roles[r].Id,Roles[r].DisplayName,Candidates[best].Name,totals[0]/train,totals[best]/train,baseline/hold,challenger/hold,delta,se,z,half/(hold/2),(sum-half)/(hold-hold/2),accept,train*Candidates.Length+hold*(best==0?1:2));
                output.WriteLine(row);output.Flush();Console.WriteLine(row);
            }
        }
    }
}
