// ---------------------------------------------------------------------------
// 无头测试运行器。
//
// 只依赖 System.Reflection：扫描 [TestFixture] 标注的类，逐个执行其中
// [Test] 标注的方法，并在每个方法前后调用 [SetUp] / [TearDown]。
//
// 同一套测试文件既能在 Unity/Tuanjie EditMode Test Runner 下运行（定义
// FDD_UNITY_TESTS 时映射到 NUnit），也能在这里以纯 .NET 进程运行。
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FingerDiceDuel.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--tower-probe")
        {MageTowerTests.RunFinaleProbe(args[1]);return 0;}
        if(args.Length==4&&args[0]=="--dual-shard")
        {DualVersusDualHarness.RunShard(int.Parse(args[1]),int.Parse(args[2]),args[3]);return 0;}
        bool trace = args.Any(a => a == "--trace");
        bool verbose = args.Any(a => a == "-v" || a == "--verbose");
        bool skipStatistics = args.Any(a => a == "--skip-statistics");
        var statistics = new HashSet<string> {
            "Revision_all_modes_win_rates", "All_characters_round_robin_win_rates", "All_dual_general_matchups_run_without_crashing",
            "Eight_player_dual_faction_battle_2000_games", "Three_focus_policies_per_role_600_games_two_seed_segments",
            "Identity_random_character_win_rates_2000_games", "Identity_biased_character_win_rates_2000_games",
            "Two_versus_two_round_robin_win_rates_and_seat_bias"
        };
        string filter = args.FirstOrDefault(a => !a.StartsWith("-"));

        Assembly assembly = typeof(Program).Assembly;

        List<Type> fixtures;
        try
        {
            fixtures = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && HasAttribute<TestFixtureAttribute>(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ToList();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Console.Error.WriteLine("加载测试类型失败：");
            foreach (Exception loader in ex.LoaderExceptions.Where(e => e != null))
                Console.Error.WriteLine("  " + loader.Message);
            return 2;
        }

        if (fixtures.Count == 0)
        {
            Console.Error.WriteLine("没有发现任何 [TestFixture]。是不是忘了把 Assets/Tests 加进编译？");
            return 2;
        }

        var failures = new List<string>();
        int passed = 0, failed = 0, skipped = 0, statisticsSkipped = 0;

        foreach (Type fixture in fixtures)
        {
            if (filter != null && fixture.FullName?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            List<MethodInfo> tests = fixture
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => HasAttribute<TestAttribute>(m) && m.GetParameters().Length == 0)
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ToList();

            if (tests.Count == 0) continue;

            MethodInfo setUp = FindSingle(fixture, typeof(SetUpAttribute));
            MethodInfo tearDown = FindSingle(fixture, typeof(TearDownAttribute));

            Console.WriteLine($"── {fixture.Name}  ({tests.Count} 项)");

            foreach (MethodInfo test in tests)
            {
                if(skipStatistics && statistics.Contains(test.Name))
                {statisticsSkipped++;Console.WriteLine($"   SKIP  {test.Name} (statistics)");continue;}
                string label = $"{fixture.Name}.{test.Name}";
                object instance = null;

                // 说明：测试内部的 Console 输出直接透传到控制台。
                // 需要只看某个测试时用过滤器参数（按类名匹配）即可。

                try
                {
                    instance = Activator.CreateInstance(fixture);
                    setUp?.Invoke(instance, null);
                    test.Invoke(instance, null);
                    passed++;
                    Console.WriteLine($"   PASS  {test.Name}");
                }
                catch (Exception raw)
                {
                    failed++;
                    Exception inner = Unwrap(raw);
                    string message = $"{label}: {inner.GetType().Name}: {inner.Message}";
                    failures.Add(message);
                    Console.WriteLine($"   FAIL  {test.Name}");
                    Console.WriteLine($"         {inner.GetType().Name}: {inner.Message}");
                    if (verbose && inner.StackTrace != null)
                    {
                        foreach (string line in inner.StackTrace.Split('\n').Take(12))
                            Console.WriteLine("         " + line.Trim());
                    }
                }
                finally
                {
                    if (instance != null && tearDown != null)
                    {
                        try { tearDown.Invoke(instance, null); }
                        catch (Exception td)
                        {
                            skipped++;
                            Console.WriteLine($"   WARN  {test.Name} 的 TearDown 抛异常：{Unwrap(td).Message}");
                        }
                    }
                }
            }

            Console.WriteLine();
        }

        Console.WriteLine(new string('─', 60));
        Console.WriteLine($"通过 {passed} / 失败 {failed}" + (skipped > 0 ? $" / TearDown 警告 {skipped}" : ""));
        if(statisticsSkipped>0)Console.WriteLine($"独立统计测试已跳过 {statisticsSkipped} 项。");

        if (failures.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("失败清单：");
            foreach (string f in failures) Console.WriteLine("  · " + f);
            return 1;
        }

        if (passed == 0)
        {
            Console.Error.WriteLine("没有执行任何测试（可能被 filter 全部过滤掉了）。");
            return 2;
        }

        return 0;
    }

    private static bool HasAttribute<T>(MemberInfo member) where T : Attribute
        => member.GetCustomAttributes(typeof(T), false).Length > 0;

    private static MethodInfo FindSingle(Type fixture, Type attributeType)
        => fixture.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttributes(attributeType, false).Length > 0
                                 && m.GetParameters().Length == 0);

    private static Exception Unwrap(Exception ex)
        => ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
}
