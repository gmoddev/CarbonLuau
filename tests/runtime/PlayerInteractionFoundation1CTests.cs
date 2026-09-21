using System;
using System.Collections.Generic;
using System.Diagnostics;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class PlayerInteractionFoundation1CTests
{
    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception("Player-1C: " + Message); }
    private static Runtime.PhysicalInventoryContainer Container(object Identity, List<Runtime.PhysicalInventoryStack> Stacks)
    {
        return new Runtime.PhysicalInventoryContainer {Identity = Identity, StackCount = Stacks.Count, Read = Index => Stacks[Index]};
    }
    private static Runtime.PhysicalInventorySource Source(List<Runtime.PhysicalInventoryStack> Main,
        List<Runtime.PhysicalInventoryStack> Belt, List<Runtime.PhysicalInventoryStack> Wear,
        object MainIdentity, object BeltIdentity, object WearIdentity)
    {
        return new Runtime.PhysicalInventorySource {Main = Container(MainIdentity, Main), Belt = Container(BeltIdentity, Belt), Wear = Container(WearIdentity, Wear)};
    }
    private static bool Reject(Action Action)
    { try { Action(); return false; } catch (InvalidOperationException) { return true; } }

    public static void RunModel()
    {
        foreach (string Name in new[] {"wood", "scrap", "rifle.ak", "a", new string('a', 128)})
            Runtime.FacadePolicy.ItemShortName(Name);
        foreach (string Name in new[] {"", new string('a', 129), "Scrap", " scrap", "scrap ", "\tscrap", "scrap\n", "scr\0ap", "caf\u00e9"})
            Check(Reject(() => Runtime.FacadePolicy.ItemShortName(Name)), "invalid short name rejected");

        object Scrap = new object(), Wood = new object(), MainId = new object(), BeltId = new object(), WearId = new object(), ExternalId = new object();
        var Main = new List<Runtime.PhysicalInventoryStack>();
        var Belt = new List<Runtime.PhysicalInventoryStack>();
        var Wear = new List<Runtime.PhysicalInventoryStack>();
        var Empty = Source(Main, Belt, Wear, MainId, BeltId, WearId);
        Check(Runtime.PhysicalInventoryObservation.Count(Empty, Scrap) == 0 && !Runtime.PhysicalInventoryObservation.Has(Empty, Scrap, 1), "empty inventory");
        var EmptySource = Empty;
        Main.Add(new Runtime.PhysicalInventoryStack(MainId, Scrap, 10, true));
        Main.Add(new Runtime.PhysicalInventoryStack(MainId, Scrap, 5, true));
        Belt.Add(new Runtime.PhysicalInventoryStack(BeltId, Scrap, 20, true));
        Wear.Add(new Runtime.PhysicalInventoryStack(WearId, Scrap, 7, true));
        Wear.Add(new Runtime.PhysicalInventoryStack(WearId, Wood, 99, true));
        Wear.Add(new Runtime.PhysicalInventoryStack(WearId, Scrap, 0, true));
        Wear.Add(new Runtime.PhysicalInventoryStack(WearId, Scrap, -1, true));
        Wear.Add(new Runtime.PhysicalInventoryStack(WearId, Scrap, 100, false));
        Wear.Add(new Runtime.PhysicalInventoryStack(ExternalId, Scrap, 100, true));
        Empty = Source(Main, Belt, Wear, MainId, BeltId, WearId);
        Check(Runtime.PhysicalInventoryObservation.Count(Empty, Scrap) == 42, "main belt wear exact physical sum");
        Check(Runtime.PhysicalInventoryObservation.Count(Empty, Wood) == 99, "different definitions excluded");
        Check(Runtime.PhysicalInventoryObservation.Has(Empty, Scrap, 42) && !Runtime.PhysicalInventoryObservation.Has(Empty, Scrap, 43), "exact and above thresholds");
        Check(Runtime.PhysicalInventoryObservation.Has(Empty, Scrap, Runtime.FacadePolicy.MaxExactLuauInteger) == false, "large exact threshold");

        var Exact = new List<Runtime.PhysicalInventoryStack>();
        for (int Index = 0; Index < Runtime.FacadePolicy.InventoryStacks; ++Index)
            Exact.Add(new Runtime.PhysicalInventoryStack(MainId, Scrap, 1, true));
        var ExactSource = Source(Exact, new List<Runtime.PhysicalInventoryStack>(), new List<Runtime.PhysicalInventoryStack>(), MainId, BeltId, WearId);
        Check(Runtime.PhysicalInventoryObservation.Count(ExactSource, Scrap) == Runtime.FacadePolicy.InventoryStacks, "exact inspection bound");
        Exact.Add(new Runtime.PhysicalInventoryStack(MainId, Scrap, 1, true)); ExactSource.Main.StackCount = Exact.Count;
        Check(Reject(() => Runtime.PhysicalInventoryObservation.Count(ExactSource, Scrap)), "one-over Count fails closed");
        Check(Reject(() => Runtime.PhysicalInventoryObservation.Has(ExactSource, Scrap, 1)), "one-over Has cannot early-bypass bound");

        var Overflow = new List<Runtime.PhysicalInventoryStack> {
            new Runtime.PhysicalInventoryStack(MainId, Scrap, Runtime.FacadePolicy.MaxExactLuauInteger, true),
            new Runtime.PhysicalInventoryStack(MainId, Scrap, 1, true)};
        var OverflowSource = Source(Overflow, new List<Runtime.PhysicalInventoryStack>(), new List<Runtime.PhysicalInventoryStack>(), MainId, BeltId, WearId);
        Check(Reject(() => Runtime.PhysicalInventoryObservation.Count(OverflowSource, Scrap)), "checked exact-integer accumulation");

        int Reads = 0;
        var Early = new Runtime.PhysicalInventoryContainer {Identity = MainId, StackCount = 3, Read = Index => {
            Reads++; return new Runtime.PhysicalInventoryStack(MainId, Scrap, 1, true);
        }};
        var EarlySource = new Runtime.PhysicalInventorySource {Main = Early,
            Belt = Container(BeltId, new List<Runtime.PhysicalInventoryStack>()), Wear = Container(WearId, new List<Runtime.PhysicalInventoryStack>())};
        Check(Runtime.PhysicalInventoryObservation.Has(EarlySource, Scrap, 1) && Reads == 1, "Has early success after bound preflight");

        const int Iterations = 10000;
        var Watch = Stopwatch.StartNew();
        for (int Index = 0; Index < Iterations; ++Index) Runtime.PhysicalInventoryObservation.Count(EmptySource, Scrap);
        Watch.Stop(); double EmptyTime = Watch.Elapsed.TotalMilliseconds;
        Watch.Restart(); for (int Index = 0; Index < Iterations; ++Index) Runtime.PhysicalInventoryObservation.Count(Empty, Scrap);
        Watch.Stop(); double Normal = Watch.Elapsed.TotalMilliseconds;
        Reads = 0; Watch.Restart();
        for (int Index = 0; Index < Iterations; ++Index) Runtime.PhysicalInventoryObservation.Has(EarlySource, Scrap, 1);
        Watch.Stop(); double EarlyTime = Watch.Elapsed.TotalMilliseconds;
        Check(Reads == Iterations, "timed Has performs one read per early success");
        Exact.RemoveAt(Exact.Count - 1); ExactSource.Main.StackCount = Exact.Count;
        Watch.Restart(); for (int Index = 0; Index < Iterations; ++Index) Runtime.PhysicalInventoryObservation.Count(ExactSource, Scrap);
        Watch.Stop(); double Maximum = Watch.Elapsed.TotalMilliseconds;
        Console.WriteLine("[CarbonLuau:Player1CModel] PASS bound=128 empty10k=" + EmptyTime.ToString("F2") +
            "ms normal10k=" + Normal.ToString("F2") + "ms earlyHas10k=" + EarlyTime.ToString("F2") +
            "ms maximum10k=" + Maximum.ToString("F2") + "ms");
    }
}
