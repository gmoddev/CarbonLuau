using System;
using System.Collections.Generic;

// These host replacements are compiled only by this fixture project. The actual
// Main/Persistence adapters and all runtime/queue/worker code are linked unchanged.
namespace Carbon.Plugins
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InfoAttribute : Attribute
    { public InfoAttribute(string Name,string Author,string Version) { } }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DescriptionAttribute : Attribute
    { public DescriptionAttribute(string Text) { } }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ConsoleCommandAttribute : Attribute
    { public ConsoleCommandAttribute(string Name) { } }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AuthLevelAttribute : Attribute
    { public AuthLevelAttribute(int Level) { } }

    public class CarbonPlugin
    {
        public sealed class Configuration
        {
            public void WriteObject<T>(T Value,bool Pretty) { }
            public T ReadObject<T>() where T : new() { return new T(); }
        }
        public sealed class Timers
        {
            // Neither case schedules delayed work; reject it rather than silently
            // changing production scheduler behavior inside the fixture.
            public void Once(float Delay,Action Callback) { throw new InvalidOperationException("unexpected delayed timer"); }
        }
        protected readonly Configuration Config=new Configuration();
        protected readonly Timers timer=new Timers(); // Carbon API spelling.
        internal readonly List<string> TestLogs=new List<string>();
        internal bool ThrowNextFrame;
        internal int SchedulingFaults;
        internal Action PendingFrame;
        protected virtual void LoadDefaultConfig() { }
        protected void NextFrame(Action Callback)
        {
            if (ThrowNextFrame) { ++SchedulingFaults; throw new InvalidOperationException("fixture NextFrame failure"); }
            if (PendingFrame!=null) throw new InvalidOperationException("fixture frame capacity exceeded");
            PendingFrame=Callback;
        }
        protected void Puts(string Text) { Log(Text); }
        protected void PrintWarning(string Text) { Log(Text); }
        protected void PrintError(string Text) { Log(Text); }
        private void Log(string Text)
        {
            if (TestLogs.Count>=128 || Text.Length>8192) throw new InvalidOperationException("fixture diagnostic bound");
            TestLogs.Add(Text);
        }
        internal void RunFrame()
        {
            Action Callback=PendingFrame; PendingFrame=null;
            if (Callback==null) throw new InvalidOperationException("fixture expected a scheduled frame");
            Callback();
        }
    }

    public partial class CarbonLuau
    {
        private AddonRegistry Addons;
        private FacadeWorld Gameplay;
        private ICommandRegistrar CommandRegistrar;
        private bool RegisteringPermissions, AddonFrameScheduled;
        internal Action PermissionAction;
        private sealed class Registrar : ICommandRegistrar
        { public void Publish(FacadeSession Previous,FacadeSession Next) { } }
        private void InitializeGameplay()
        {
            CommandRegistrar=new Registrar();
            Gameplay=new FacadeWorld(new PlayerDirectory(Id=>null),CommandRegistrar);
        }
        private void SeedPlayers() { }
        private void RegisterActivePermissions() { if (PermissionAction!=null) PermissionAction(); }
        private void QueueAddonWork() { }

        internal void TestInitialize(string DataDirectory)
        {
            Oxide.Core.Interface.Oxide.DataDirectory=DataDirectory;
            Settings=new RuntimeConfig { MaxCallbackMilliseconds=100, FrameDrainBudgetMilliseconds=20 };
            Native=new NativeRuntime(DataDirectory);
            InitializeGameplay();
            Host=new ScriptHost(Native,Settings,()=>new ScriptSnapshot { EntryName="init.luau",EntrySource="return true" },Gameplay);
            Addons=new AddonRegistry(Host,Native.HostLifetimeId);
            InitializePersistence(); // Actual production worker construction/start.
        }
        internal NativeRuntime TestNative { get { return Native; } }
        internal ScriptHost TestHost { get { return Host; } }
        internal StorageSupervisor TestWorker { get { return Persistence; } }
        internal FacadeWorld TestWorld { get { return Gameplay; } }
        internal bool TestStopped { get { return Stopping; } }
        internal bool TestReleased {
            get { return Native==null && Host==null && Addons==null && Gameplay==null && CommandRegistrar==null &&
                Persistence==null && !DrainScheduled && !AddonFrameScheduled && !TeardownPending; }
        }
        internal void TestTick() { OnTick(); }
        internal void TestUnload() { Unload(); }
    }
}

public static class ConsoleSystem
{
    public sealed class Arg { public void ReplyWith(string Text) { } }
}
namespace Oxide.Core
{
    public static class Interface
    {
        public sealed class Environment { public string DataDirectory; }
        public static readonly Environment Oxide=new Environment();
    }
}
