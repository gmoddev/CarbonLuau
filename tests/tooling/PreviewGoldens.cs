using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Gui = Carbon.Plugins.CarbonLuau.PreviewGuiSession;

internal static class PreviewGoldens
{
    internal static void Run(string Root, bool Update)
    {
        JArray Fixtures = JArray.Parse(File.ReadAllText(Path.Combine(Root, "tests/tooling/PreviewFixtures.json")));
        var Results = new JArray();
        foreach (JObject Fixture in Fixtures) {
            var Session = new Gui(1, Change => { });
            foreach (JArray Fields in (JArray)Fixture["Operations"]!) Session.Call(21, Fields.Select(Value => (string)Value!).ToArray());
            JObject Plan = Session.Plan("1", (double)Fixture["Viewport"]!["Width"]!, (double)Fixture["Viewport"]!["Height"]!);
            Plan["AvailableScreens"] = Session.Screens;
            // Validate across the JSON boundary, including null and numeric kinds.
            Gui.ValidatePlan(JObject.Parse(Plan.ToString()));
            Results.Add(new JObject { ["Name"] = Fixture["Name"]!.DeepClone(), ["Plan"] = Plan });
        }
        string FileName = Path.Combine(Root, "tests/tooling/PreviewGoldens.json");
        if (Update) File.WriteAllText(FileName, Results.ToString(Newtonsoft.Json.Formatting.None) + "\n");
        else if (!JToken.DeepEquals(Results, JArray.Parse(File.ReadAllText(FileName)))) throw new Exception("preview semantic golden drift");
        Console.WriteLine("[CarbonLuau:PreviewGoldens] PASS " + Results.Count + " canonical fixture plans, retained reconstruction and exact semantic comparison");
    }
}
