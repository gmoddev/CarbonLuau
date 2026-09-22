using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public sealed partial class PreviewGuiSession
        {
            // Reconstruct only bounded retained data, never source or VM state. The
            // coordinator compares against the same canonical compiler used by the worker.
            public static void ValidatePlan(JObject Plan) { PreviewTree.ValidatePlan(Plan); }

            private sealed partial class PreviewTree
            {
                internal static void ValidatePlan(JObject Plan)
                {
                    var Tree = new PreviewTree(1, Change => { });
                    var Nodes = Plan["Nodes"] as JArray;
                    if (Nodes == null || Nodes.Count < 1 || Nodes.Count > Tree.Limits.MaxObjectsPerScreen)
                        throw new InvalidOperationException("invalid preview object count");
                    foreach (JToken Value in Nodes) {
                        var Item = Value as JObject;
                        if (Item == null) throw new InvalidOperationException("invalid preview node");
                        string Identity = Text(Item, "Id");
                        ulong ObjectId;
                        if (!UInt64.TryParse(Identity, NumberStyles.None, CultureInfo.InvariantCulture, out ObjectId) || ObjectId == 0 ||
                            ObjectId.ToString(CultureInfo.InvariantCulture) != Identity || Tree.State.Nodes.ContainsKey(ObjectId))
                            throw new InvalidOperationException("invalid preview object identity");
                        bool First = Tree.State.Nodes.Count == 0;
                        string Class = Text(Item, "ClassName");
                        if ((Class == "ScreenGui") != First || (First && Item["ParentId"]?.Type != JTokenType.Null))
                            throw new InvalidOperationException("invalid preview root: " + Class + ", first=" + First + ", parent=" + Item["ParentId"]?.Type);
                        ulong Parent = First ? 0 : Id(Text(Item, "ParentId"));
                        Tree.NextObjectId = ObjectId;
                        GuiRetainedNode Node = Tree.Create(Parent, Class);
                        var Retained = Item["Retained"] as JObject;
                        if (Retained == null || Retained.Count != Node.Properties.Count)
                            throw new InvalidOperationException("invalid retained property count");
                        foreach (JProperty Property in Retained.Properties()) {
                            GuiPropertyUse Use;
                            if (!GuiSchema.TryGetProperty(Node.ClassId, Property.Name, out Use) || !Node.Properties.ContainsKey(Use.Descriptor.Id))
                                throw new InvalidOperationException("unknown retained property");
                            var Stored = Property.Value as JObject;
                            var Encoded = Stored?["Encoded"] as JArray;
                            if (Stored == null || Stored.Count != 2 || Encoded == null || Encoded.Count > 6 ||
                                Encoded.Any(Field => Field.Type != JTokenType.String)) throw new InvalidOperationException("invalid retained value");
                            Tree.SetProperty(Node, Property.Name, Encoded.Select(Field => (string)Field).ToArray(), 0);
                        }
                    }
                    var Viewport = Plan["Viewport"] as JObject;
                    if (Viewport == null || Viewport.Count != 2 || !Number(Viewport["Width"]) || !Number(Viewport["Height"]))
                        throw new InvalidOperationException("invalid preview viewport");
                    JObject Expected = Tree.Plan(Text((JObject)Plan["Screen"], "Id"), (double)Viewport["Width"], (double)Viewport["Height"]);
                    foreach (JProperty Field in Expected.Properties())
                        if (!JToken.DeepEquals(Field.Value, Plan[Field.Name])) throw new InvalidOperationException("preview canonical mismatch: " + Field.Name);
                    var Screens = Plan["AvailableScreens"] as JArray;
                    if (Screens == null || Screens.Count < 1 || Screens.Count > Tree.Limits.MaxScreensPerDomain ||
                        !Screens.Any(Screen => JToken.DeepEquals(Screen, Plan["Screen"]))) throw new InvalidOperationException("invalid preview screen choices");
                    var Seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                    foreach (JToken Screen in Screens) {
                        var Choice = Screen as JObject;
                        if (Choice == null || Choice.Count != 2 || !Seen.Add(Text(Choice, "Id")) || Id(Text(Choice, "Id")) == 0)
                            throw new InvalidOperationException("invalid preview screen choice");
                        ValidateText(Text(Choice, "Name"), Tree.Limits.MaxNameUtf8Bytes, "screen name");
                    }
                }
                private static bool Number(JToken Value) { return Value != null && (Value.Type == JTokenType.Integer || Value.Type == JTokenType.Float); }
                private static string Text(JObject Value, string Name)
                {
                    if (Value == null || Value[Name]?.Type != JTokenType.String) throw new InvalidOperationException("invalid preview " + Name);
                    return (string)Value[Name];
                }
            }
        }
    }
}
