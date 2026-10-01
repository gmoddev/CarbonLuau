using System;
using System.IO;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static partial class PersistencePublicTests
{
    private static void QueryPublicVm(Runtime.NativeRuntime Native, string Worker, string Parent)
    {
        using (var F = new Fixture(Native, Worker, Path.Combine(Parent, "query-public"))) {
            foreach (var Entry in new[] { "'A',1", "'B',1", "'C',2" }) {
                F.Execute(Store + "S:SetAsync(" + Entry.Split(',')[0] + ",{Score=" + Entry.Split(',')[1] +
                    "},function(Ok,ErrorCode) assert(Ok==true and ErrorCode==nil); print('QUERY_SET_DONE') end)");
                F.Until("QUERY_SET_DONE");
                F.Logs.Clear();
            }
            F.Execute(Store + @"
local Ok, ErrorCode = pcall(function()
    S:Query({Field='Score', Limit=0}, function() end)
end)
assert(not Ok and string.find(ErrorCode, 'InvalidQuery'))
S:Query({Field='Score', Type='number', Limit=1}, function(First, FirstError)
    assert(FirstError==nil and #First.Items==1 and First.Items[1].Key=='A')
    assert(type(First.NextCursor)=='string')
    S:Query({Field='Score', Type='number', Limit=1, Cursor=First.NextCursor}, function(Second, SecondError)
        assert(SecondError==nil and #Second.Items==1 and Second.Items[1].Key=='B')
        print('QUERY_PUBLIC_DONE')
    end)
end)");
            F.Until("QUERY_PUBLIC_DONE");
            Check(F.Queue.PendingCount == 0, "public Query leaves no request reservations");
            F.Execute(Store + @"
S:SetAsync('D',{Score=false},function(Ok,ErrorCode)
    assert(Ok==true and ErrorCode==nil)
    print('QUERY_BOOLEAN_SET_DONE')
end)");
            F.Until("QUERY_BOOLEAN_SET_DONE");
            F.Execute(Store + @"
S:Query({Field='Score'},function(Result,ErrorCode)
    assert(Result==nil and ErrorCode=='AmbiguousFieldType')
    print('QUERY_AMBIGUOUS_DONE')
end)
S:Query({Field='Score',Equals=false},function(Result,ErrorCode)
    assert(ErrorCode==nil and #Result.Items==1 and Result.Items[1].Key=='D')
    print('QUERY_FALSE_DONE')
end)");
            F.Until("QUERY_AMBIGUOUS_DONE");
            F.Until("QUERY_FALSE_DONE");
            long SentBeforeCandidate = F.Worker.RequestsSent;
            F.Source = Store + "S:Query({Field='Score'},function() end); return true";
            var Candidate = F.Host.Reload();
            Check(Candidate.Status == Runtime.RuntimeStatus.RUNTIME_ERROR,
                "provisional Query must reject at the shared publication gate");
            for (int Index = 0; Index < 4; ++Index) F.Tick();
            Check(F.Worker.RequestsSent == SentBeforeCandidate,
                "provisional Query dispatches no storage request");
            foreach (string Example in new[] { "query-equals", "query-range", "query-top", "query-pages", "query-ambiguity" }) {
                long Before = F.Worker.RequestsSent;
                F.Source = File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                    "examples", "persistence", Example, "init.luau"));
                F.Reload();
                F.DrainChecked();
                WaitFor(() => F.Worker.RequestsSent > Before && F.Queue.PendingCount == 0,
                    () => F.Tick(), "public Query example completion: " + Example);
                F.DrainChecked();
            }
        }
        Console.WriteLine("[CarbonLuau:Persistence2C] real VM/public facade -> worker Query, preparation, cursor, ambiguity, publication and five examples PASS");
    }
}
