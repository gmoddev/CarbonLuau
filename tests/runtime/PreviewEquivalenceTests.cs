using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Runtime = Carbon.Plugins.CarbonLuau;

internal static class PreviewEquivalenceTests
{
    internal static void Run(string Root)
    {
        var Fixtures = JArray.Parse(File.ReadAllText(Path.Combine(Root, "tests/tooling/PreviewFixtures.json")));
        foreach (JObject Fixture in Fixtures) {
            var Limits = new Runtime.GuiConfig().Validate();
            var View = new Runtime.PlayerView { Identity = new object(), Connection = new object(), UserId = "76561198000000000", Name = "Test", Connected = true,
                Send = Value => { }, Permission = Value => true };
            var Players = new Runtime.PlayerDirectory(Id => Id == View.UserId ? View : null);
            var Player = Players.Connect(View);
            var Backend = new Runtime.InMemoryGuiBackend();
            var World = new Runtime.GuiRetainedWorld(Limits, Players, Backend);
            var Preview = new Runtime.PreviewGuiSession(60, Change => { });
            using (var Production = new Runtime.GuiRetainedRegistry(World, 30, 60)) {
                foreach (JArray Operation in (JArray)Fixture["Operations"]) {
                    string[] Fields = Operation.Select(Value => (string)Value).ToArray();
                    string[] Actual = Production.Mutate(Fields, () => "unused"), Expected = Preview.Call(21, Fields);
                    Check(Actual.SequenceEqual(Expected), "retained adapter results");
                }
                Production.Mutate(new[] { "show", "1", Player.Token, Player.UserId }, () => "unused");
                int Guard = 0;
                while (Production.HasWork && Guard++ < 64) Production.FlushOne(Limits.MaxSerializedBytesPerFlush);
                Check(!Production.HasWork, "production projection drained");
                var Call = Backend.Calls().Last(Value => Value.Plan != null);
                var Plan = Call.Plan;
                double Width = (double)Fixture["Viewport"]["Width"], Height = (double)Fixture["Viewport"]["Height"];
                var Pixels = Runtime.GuiPixelProjection.Resolve(Plan, Width, Height);
                var Tooling = Preview.Plan("1", Width, Height);
                Check(Plan.ProjectedElementCount == (int)Tooling["Accounting"]["ProjectedElements"], "production projected accounting");
                Check(World.LiveObjects == (int)Tooling["Accounting"]["RetainedObjects"], "production retained accounting");
                string Prefix = Call.Target.ClientRootId.Substring(0, Call.Target.ClientRootId.Length - 1);
                foreach (JObject Node in (JArray)Tooling["Nodes"]) {
                    if (Node["Projected"].Type == JTokenType.Null) continue;
                    string Identity = (string)Node["ClassName"] == "ScreenGui" ? Call.Target.ClientRootId :
                        Prefix + "o" + UInt64.Parse((string)Node["Id"], CultureInfo.InvariantCulture).ToString("x", CultureInfo.InvariantCulture);
                    var Pixel = Pixels[Identity]; var Projection = (JObject)Node["Projected"];
                    Check(JToken.DeepEquals(Rect(Pixel.Rect), Projection["RectPx"]), "production geometry: " + Fixture["Name"]);
                    Check(JToken.DeepEquals(Rect(Pixel.Clip), Projection["EffectiveClipRectPx"]), "production clipping");
                    Check(Pixel.Visible == (bool)Projection["EffectiveVisible"] && Pixel.PaintOrder == (int)Projection["PaintOrder"] &&
                        Pixel.ClipDepth == (int)Projection["ClipDepth"], "production visibility/order/depth");
                }
            }
            Check(World.LiveObjects == 0, "production fixture teardown");
        }
        Console.WriteLine("[CarbonLuau:PreviewEquivalence] PASS " + Fixtures.Count + " fixtures through production retained registry, Player Presentation and compiler versus preview adapter");
    }
    private static JObject Rect(Runtime.GuiPixelRect Value) { return new JObject { ["X"] = Value.X, ["Y"] = Value.Y, ["Width"] = Value.Width, ["Height"] = Value.Height }; }
    private static void Check(bool Value, string Message) { if (!Value) throw new Exception(Message); }
}
