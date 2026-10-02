using System;
using System.Reflection;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class EntityPublicationWitnessTests
{
    private sealed class Registrar : Runtime.ICommandRegistrar
    { public void Publish(Runtime.FacadeSession Previous, Runtime.FacadeSession Next) { } }

    private static void Check(bool Condition, string Message)
    { if (!Condition) throw new Exception(Message); }

    private static void Publication(Runtime.FacadeSession Session, uint Code)
    {
        // Exercise the same private dispatch path used by native publication
        // callbacks without introducing a production-only test entrypoint.
        typeof(Runtime.FacadeSession).GetMethod("Operation", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(Session, new object[] {Code, new string[0]});
    }

    internal static void Run()
    {
        var World = new Runtime.FacadeWorld(new Runtime.PlayerDirectory(Id => null), new Registrar());
        var Session = new Runtime.FacadeSession(World, 7, 8, 32);
        var Root = Session.CapturePublicationWitness();
        Check(Session.IsPublicationWitnessCurrent(Root), "root witness current");
        Publication(Session, 10);
        var Outer = Session.CapturePublicationWitness();
        Publication(Session, 10);
        var Inner = Session.CapturePublicationWitness();
        Check(Outer.Token != Inner.Token && Session.IsPublicationWitnessCurrent(Inner), "nested witness identity");
        Publication(Session, 11);
        Check(Session.IsPublicationWitnessCurrent(Inner), "committed inner witness current");
        Publication(Session, 12);
        Check(!Session.IsPublicationWitnessCurrent(Outer) && !Session.IsPublicationWitnessCurrent(Inner),
            "outer rollback retires committed nested witnesses");
        Check(Session.IsPublicationWitnessCurrent(Root), "rollback does not retire root");
        Publication(Session, 10);
        var Committed = Session.CapturePublicationWitness();
        Publication(Session, 11);
        Check(Session.IsPublicationWitnessCurrent(Committed), "committed witness current");
        World.Retire(Session);
        Check(!Session.IsPublicationWitnessCurrent(Root) && !Session.IsPublicationWitnessCurrent(Committed),
            "session retirement stales every witness");
        var Replacement = new Runtime.FacadeSession(World, 7, 8, 32);
        Check(!Replacement.IsPublicationWitnessCurrent(Root),
            "same numeric VM/domain identity must not retarget a witness");
        World.Retire(Replacement);
        var AddonOld = new Runtime.FacadeSession(World, 9, 10, 32);
        World.CommitAddon(null, AddonOld);
        var AddonWitness = AddonOld.CapturePublicationWitness();
        var AddonNew = new Runtime.FacadeSession(World, 9, 11, 32);
        World.CommitAddon(AddonOld, AddonNew);
        Check(!AddonOld.IsPublicationWitnessCurrent(AddonWitness) &&
            !AddonNew.IsPublicationWitnessCurrent(AddonWitness),
            "addon replacement must retire old publication authority without retargeting");
        World.Retire(AddonNew);
        Console.WriteLine("[CarbonLuau:EntityPublication] PASS");
    }
}
