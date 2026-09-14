// EXCLUDED from production packages. Fixed fixtures only; no arbitrary eval.
using System;
namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        [ConsoleCommand("carbonluau.fixture"), AuthLevel(2)]
        private void Phase1Fixture(ConsoleSystem.Arg Arg)
        {
            try
            {
                if (Host == null) throw new InvalidOperationException("runtime unavailable");
                string Fixture = Arg.Args != null && Arg.Args.Length == 1 ? Arg.Args[0].ToString() : "";
                ExecutionResult Result;
                RuntimeStatus Expected;
                if (Fixture == "timeout") { Expected = RuntimeStatus.TIMEOUT; Result = Host.Execute("timeout", "while true do end"); }
                else if (Fixture == "memory")
                {
                    Expected = RuntimeStatus.MEMORY_LIMIT;
                    Result = Host.Execute("memory", "return buffer.create(" + ((long)Settings.MaxVmMemoryMiB * 1048576 + 1) + ")");
                }
                else if (Fixture == "valid") { Expected = RuntimeStatus.OK; Result = Host.Execute("valid", "print('post-failure execution OK'); return 3"); }
                else if (Fixture == "failed-reload")
                {
                    long Before = Host.Generation;
                    Result = Host.Reload("local =");
                    Expected = RuntimeStatus.COMPILE_ERROR;
                    if (Host.Generation != Before || Host.Execute("recovery", "return 3").Status != RuntimeStatus.OK)
                        throw new InvalidOperationException("failed replacement disturbed healthy generation");
                }
                else throw new InvalidOperationException("choose timeout, memory, valid or failed-reload");
                Report(Fixture, Result);
                if (Result.Status != Expected) throw new InvalidOperationException("expected " + Expected + ", got " + Result.Status);
                if (Native.LiveVmCount != 1) throw new InvalidOperationException("unexpected live VM ownership count");
                Arg.ReplyWith("[CarbonLuau:LiveTest] PASS " + Fixture + "; " + Result.Status + "; generation=" + Host.Generation);
            }
            catch (Exception Error) { Arg.ReplyWith("[CarbonLuau:LiveTest] FAIL: " + Error.Message); }
        }
    }
}
