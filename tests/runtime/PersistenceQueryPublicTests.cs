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
            // Complete the real worker request without draining the Luau
            // callback, then retire its root domain. A queued result belongs
            // to the old domain and must not enter its replacement.
            long CompletedBeforeRetirement = F.Queue.QueryCompleted;
            F.Execute(Store + @"
S:Query({Field='Score',Equals=1,Limit=1},function()
    print('STALE_QUERY_CALLBACK')
end)");
            Check(F.Queue.PendingCount == 1, "accepted Query retains one original reservation");
            var QueuedRequest = OnlyRequest(F);
            WaitFor(() => System.Threading.Volatile.Read(ref QueuedRequest.State) == 2,
                () => F.Worker.Tick(), "worker completed Query before owner callback admission");
            var QueuedPage = QueuedRequest.Envelope;
            Check(QueuedPage != null && QueuedPage.Length >= 16 &&
                BitConverter.ToUInt32(QueuedPage, 12) > 0,
                "worker created a cursor before undelivered callback retirement");
            WaitFor(() => F.Queue.QueryCompleted > CompletedBeforeRetirement,
                () => F.Tick(false), "completed Query handed off before Luau callback drain");
            Check(QueuedRequest.HandedOff &&
                F.Queue.PendingCount == 1 && QueuedRequest.Envelope == null,
                "owner handed off cursor page and released managed bytes before Luau callback");
            F.Source = "return true";
            F.Reload();
            F.DrainChecked();
            for (int Index = 0; Index < 4; ++Index) F.Tick();
            Check(!F.Logs.ToString().Contains("STALE_QUERY_CALLBACK") && F.Queue.WaiterCount == 0,
                "retired Query callback must not enter replacement domain");
        }
        using (var F = new Fixture(Native, Worker, Path.Combine(Parent, "query-combined"))) {
            F.Execute(@"
local Q=game:GetService('DataStoreService'):GetDataStore('QueryCombined')
local function Step(I)
    if I<=20 then
        Q:SetAsync('K'..tostring(I),{Score=I},function(Ok,ErrorCode)
            assert(Ok==true and ErrorCode==nil)
            task.delay(0.3,function() Step(I+1) end)
        end)
        return
    end
    Q:Query({Field='Score',Type='number',Limit=5},function(First,FirstError)
        assert(FirstError==nil and #First.Items==5 and First.Items[1].Key=='K1' and First.Items[5].Key=='K5')
        assert(type(First.NextCursor)=='string')
        Q:RemoveAsync('K8',function(Removed,RemoveError)
            assert(Removed==true and RemoveError==nil)
            Q:SetAsync('K12',{Score=-1},function(Updated,UpdateError)
                assert(Updated==true and UpdateError==nil)
                Q:GetAsync('K12',function(Value,GetError)
                    assert(GetError==nil and Value.Score==-1)
                    Q:Query({Field='Score',Type='number',Limit=20,Cursor=First.NextCursor},function(Next,NextError)
                        assert(NextError==nil and #Next.Items==13 and Next.Items[1].Key=='K6' and Next.Items[13].Key=='K20')
                        for _,Item in Next.Items do
                            assert(Item.Key~='K8' and Item.Key~='K12')
                        end
                        print('QUERY_COMBINED_DONE')
                    end)
                end)
            end)
        end)
    end)
end
Step(1)
");
            // Twenty separately committed writes are intentionally paced at
            // 300 ms before Query starts. Busy hosted Windows machines can
            // take longer overall without any individual D21 request timing out.
            F.Until("QUERY_COMBINED_DONE", 45000);
            Check(F.Queue.PendingCount == 0 && F.Queue.WaiterCount == 0,
                "combined public Query/write/read/remove leaves no reservations");
        }
        Console.WriteLine("[CarbonLuau:Persistence2D] real VM/public facade -> worker Query, preparation, pagination-under-writes, queued-result retirement, callback chains and five examples PASS");
    }
}
