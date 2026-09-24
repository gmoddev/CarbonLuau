using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Gui = Carbon.Plugins.CarbonLuau;

namespace CarbonLuau.Core
{
    // Build-time export of explicit, implementation-owned declarations. Never workspace input.
    public static class ApiCatalog
    {
        // Legacy declarations and GUI descriptors were introduced by the 0.4
        // candidate (or their explicit earlier SinceApi), not by today's release.
        private const string LegacyApiVersion = "0.4.0-experimental";
        public static JObject Build(string Bootstrap, JObject Release, string NativeBindings)
        {
            var Types = new SortedDictionary<string, JObject>(StringComparer.Ordinal);
            var Members = new SortedDictionary<string, JObject>(StringComparer.Ordinal);
            var Templates = new Dictionary<string, JObject>(StringComparer.Ordinal);
            string ApiVersion = (string)Release["apiVersion"];
            var Lines = (Bootstrap + "\n" + NativeBindings.Replace("// @carbonluau-api ", "-- @carbonluau-api ")).Replace("\r\n", "\n").Split('\n');
            const string Marker = "-- @carbonluau-api ";
            var Bindings = new HashSet<string>(StringComparer.Ordinal);
            foreach (string Line in Lines) {
                if (!Line.StartsWith(Marker, StringComparison.Ordinal)) continue;
                JObject Declaration = JObject.Parse(Line.Substring(Marker.Length));
                string Binding = (string)Declaration["Binding"];
                if (Binding != null) {
                    if (!Lines.Contains(Binding)) throw new InvalidOperationException("API declaration differs from runtime binding: " + Binding);
                    Bindings.Add(Binding);
                    if (Binding.TrimStart().StartsWith("function ", StringComparison.Ordinal) && Declaration["Args"] is JArray Arguments) {
                        int Start = Binding.IndexOf('('), End = Binding.IndexOf(')', Start);
                        var Parameters = Binding.Substring(Start + 1, End - Start - 1).Split(',').Select(Value => Value.Trim()).Where(Value => Value.Length > 0);
                        if ((string)Declaration["Kind"] == "Method") Parameters = Parameters.Skip(1);
                        if (!Parameters.SequenceEqual(Arguments.Cast<JArray>().Select(Argument => Argument.Count > 2 && (bool)Argument[2] ? "..." : (string)Argument[0])))
                            throw new InvalidOperationException("API parameter names differ from binding: " + Binding);
                    }
                }
                string Type = (string)Declaration["Type"];
                if (Type != null) {
                    if (Types.ContainsKey(Type)) throw new InvalidOperationException("duplicate API type: " + Type);
                    Types.Add(Type, TypeRecord(Type, (string)Declaration["Kind"], (string)Declaration["Summary"], (string)Declaration["SinceApi"] ?? LegacyApiVersion));
                    Types[Type]["Availability"]["Qualification"] = (string)Declaration["Qualification"] ?? "Experimental";
                    Types[Type]["Preview"] = (string)Declaration["Preview"] ?? "Unavailable";
                    if (Declaration["Representation"] != null) Types[Type]["Representation"] = Declaration["Representation"].DeepClone();
                    if (Declaration["TypeExpression"] != null) Types[Type]["TypeExpression"] = Declaration["TypeExpression"].DeepClone();
                    continue;
                }
                if ((string)Declaration["Owner"] == "$Gui") Templates.Add((string)Declaration["Name"], Declaration);
                else Add(Members, MemberRecord(Declaration, LegacyApiVersion));
            }
            // Audit the explicit native persistence registration contract in both
            // directions. Signatures still come only from owned annotations.
            var StorageMethods = Members.Values.Where(Value => (string)Value["OwnerId"] == "DataStore" ||
                (string)Value["OwnerId"] == "DataStoreService").ToArray();
            var StorageRegistrations = new HashSet<string>(StringComparer.Ordinal);
            const string RegistrationPattern = "lua_pushcfunction\\(State, Controlled<([A-Za-z_][A-Za-z0-9_]*)>, \"([^\"]+)\"\\); lua_setfield\\(State, -2, \"([^\"]+)\"\\);";
            foreach (System.Text.RegularExpressions.Match Registration in System.Text.RegularExpressions.Regex.Matches(NativeBindings, RegistrationPattern)) {
                string Name = Registration.Groups[1].Value;
                if (Name != Registration.Groups[2].Value || Name != Registration.Groups[3].Value ||
                    !StorageRegistrations.Add(Name) || StorageMethods.Count(Value => (string)Value["Name"] == Name) != 1)
                    throw new InvalidOperationException("native persistence registration differs from API declaration: " + Name);
            }
            if (!StorageRegistrations.SetEquals(StorageMethods.Select(Value => (string)Value["Name"])))
                throw new InvalidOperationException("native persistence API declaration lacks registration");
            // Declared public function tables must not acquire an unannotated function.
            string[] Tables = { "UDim", "UDim2", "Vector2", "Vector3", "Color3", "ImageSource", "PlayerMethods", "Players", "Commands", "GuiObjectMethods", "Gui", "Items", "Game" };
            foreach (string Line in Lines) {
                if (Tables.Any(Table => Line.StartsWith("function " + Table + ".", StringComparison.Ordinal)) && !Bindings.Contains(Line))
                    throw new InvalidOperationException("runtime function lacks API declaration: " + Line);
                if ((Line.Contains(" = Font(") || Line.StartsWith("            if Key == \"", StringComparison.Ordinal) ||
                    Line.TrimStart().StartsWith("if Name == \"", StringComparison.Ordinal)) && !Line.StartsWith(Marker, StringComparison.Ordinal) && !Bindings.Contains(Line))
                    throw new InvalidOperationException("runtime member lacks API declaration: " + Line);
            }
            Gui.GuiSchema.Validate();
            if (!Templates.Keys.OrderBy(Name => Name, StringComparer.Ordinal).SequenceEqual(Gui.GuiSchema.Classes.SelectMany(Class => Class.Methods)
                .Select(Id => Gui.GuiSchema.GetMethod(Id).Name).Distinct().OrderBy(Name => Name, StringComparer.Ordinal)))
                throw new InvalidOperationException("GUI method declarations differ from descriptor membership");
            foreach (var Class in Gui.GuiSchema.Classes) {
                Types.Add(Class.Name, TypeRecord(Class.Name, "Class", "Retained " + Class.Name + ".", LegacyApiVersion));
                if (Class.BaseClass.HasValue) Types[Class.Name]["BaseTypeId"] = Gui.GuiSchema.GetClass(Class.BaseClass.Value).Name;
                foreach (var Use in Class.Properties) {
                    string ValueType = ValueName(Use.Descriptor.ValueKind);
                    if (Use.Descriptor.AllowedValues.Length > 0)
                        ValueType = String.Join(" | ", Use.Descriptor.AllowedValues.Select(Value => "\"" + Value + "\""));
                    Add(Members, MemberRecord(new JObject { ["Owner"] = Class.Name, ["Name"] = Use.Descriptor.Name,
                        ["Kind"] = "Property", ["ValueType"] = ValueType, ["Writable"] = Use.Writable,
                        ["Summary"] = "Retained " + Use.Descriptor.Name + ".", ["Qualification"] = "ClientUnqualified" }, LegacyApiVersion));
                }
                foreach (var Id in Class.Methods) {
                    string Name = Gui.GuiSchema.GetMethod(Id).Name;
                    if (!Templates.ContainsKey(Name)) throw new InvalidOperationException("GUI method has no signature: " + Name);
                    JObject Value = (JObject)Templates[Name].DeepClone(); Value["Owner"] = Class.Name;
                    if (Name == "Clone") Value["Returns"] = new JArray(Class.Name);
                    Add(Members, MemberRecord(Value, LegacyApiVersion));
                }
                foreach (var Id in Class.Events) {
                    var Event = Gui.GuiSchema.GetEvent(Id);
                    Add(Members, MemberRecord(new JObject { ["Owner"] = Class.Name, ["Name"] = Event.Name,
                        ["Kind"] = "Signal", ["Args"] = new JArray { new JArray("Player", "Player") },
                        ["Returns"] = new JArray(), ["Summary"] = "Admitted activation with the exact Player connection.",
                        ["Qualification"] = "ClientUnqualified" }, LegacyApiVersion));
                }
            }
            foreach (var Value in Gui.GuiSchema.ValueTypes) {
                Types.Add(Value.Name, TypeRecord(Value.Name, Value.Name == "GuiFont" ? "Enum" : "Value", "Immutable " + Value.Name + ".", LegacyApiVersion));
                foreach (var Field in Value.Fields) Add(Members, MemberRecord(new JObject {
                    ["Owner"] = Value.Name, ["Name"] = Field.Name, ["Kind"] = "Property", ["Writable"] = false,
                    ["ValueType"] = ValueName(Field.Kind), ["Summary"] = "Immutable " + Field.Name + "." }, LegacyApiVersion));
                foreach (var Constructor in Value.Constructors) {
                    JObject Member;
                    if (!Members.TryGetValue(Value.Name + "." + Constructor.Name, out Member))
                        throw new InvalidOperationException("GUI constructor missing: " + Value.Name + "." + Constructor.Name);
                    var Parameters = (JArray)Member["Signatures"][0]["Parameters"];
                    if (!Parameters.Select(Parameter => (string)Parameter["Name"] + ((bool)Parameter["Optional"] ? "?" : "")).SequenceEqual(Constructor.Arguments))
                        throw new InvalidOperationException("GUI constructor arguments differ: " + Member["Id"]);
                }
            }
            foreach (var Member in Members.Values)
                if (!Types.ContainsKey((string)Member["OwnerId"])) throw new InvalidOperationException("unknown member owner");
            Members["Game.GetService"]["Signatures"] = new JArray(Types.Values.Where(Value => (string)Value["Kind"] == "Service")
                .Select(Value => NamedSignature("Name", "\"" + (string)Value["Name"] + "\"", (string)Value["Name"])));
            foreach (var Member in Members.Values.Where(Value => (string)Value["Name"] == "Create")) {
                bool Service = (string)Member["OwnerId"] == "Gui";
                Member["Signatures"] = new JArray(Gui.GuiSchema.Classes.Where(Class => Class.Constructible &&
                    (Class.CreationScope & (Service ? Gui.GuiCreationScope.GuiService : Gui.GuiCreationScope.GuiObject)) != 0)
                    .Select(Class => NamedSignature("ClassName", "\"" + Class.Name + "\"", Class.Name)));
            }
            var Catalog = new JObject { ["SchemaVersion"] = 1,
                ["Api"] = new JObject { ["Name"] = Release["apiName"].DeepClone(), ["Version"] = ApiVersion, ["Status"] = Release["apiStatus"].DeepClone() },
                ["Types"] = new JArray(Types.Values), ["Members"] = new JArray(Members.Values),
                ["LimitKeys"] = new JArray(Limits().Properties().Select(Property => Property.Name)) };
            foreach (string Name in new[] { "ApiName", "ApiVersion", "ApiStatus" }) {
                string Field = Char.ToLowerInvariant(Name[0]) + Name.Substring(1);
                if ((string)Members["Game." + Name]["ValueType"] != "\"" + (string)Release[Field] + "\"") throw new InvalidOperationException("Game identity differs from release");
            }
            Validate(Catalog);
            return Catalog;
        }
        public static void Validate(JObject Catalog)
        {
            var Types = ((JArray)Catalog["Types"]).Cast<JObject>().ToDictionary(Value => (string)Value["Id"], StringComparer.Ordinal);
            var Members = ((JArray)Catalog["Members"]).Cast<JObject>().ToDictionary(Value => (string)Value["Id"], StringComparer.Ordinal);
            if (Types.Count == 0 || Members.Count == 0) throw new InvalidOperationException("shipping catalog cannot be empty");
            foreach (var Type in Types.Values) {
                bool Alias = (string)Type["Representation"] == "Alias";
                if (Alias != (Type["TypeExpression"] != null)) throw new InvalidOperationException("invalid API alias representation");
                if (Alias) {
                    if ((string)Type["Kind"] != "Value" || Type["BaseTypeId"] != null ||
                        String.IsNullOrWhiteSpace((string)Type["TypeExpression"]) ||
                        Members.Values.Any(Member => (string)Member["OwnerId"] == (string)Type["Id"]))
                        throw new InvalidOperationException("invalid API value alias");
                    CheckType((string)Type["TypeExpression"], Types);
                }
                var Seen = new HashSet<string>(StringComparer.Ordinal); string Parent = (string)Type["BaseTypeId"];
                while (Parent != null) {
                    if (!Types.ContainsKey(Parent) || !Seen.Add(Parent) || Parent == (string)Type["Id"]) throw new InvalidOperationException("invalid API inheritance");
                    Parent = (string)Types[Parent]["BaseTypeId"];
                }
            }
            foreach (var Member in Members.Values) {
                if (!Types.ContainsKey((string)Member["OwnerId"]) || (string)Member["Id"] != (string)Member["OwnerId"] + "." + (string)Member["Name"])
                    throw new InvalidOperationException("invalid API owner");
                if (Member["ValueType"] != null) CheckType((string)Member["ValueType"], Types);
                if (Member["Signatures"] is JArray Signatures) foreach (JObject Signature in Signatures) {
                    bool Optional = false, Variadic = false; var Names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (JObject Parameter in (JArray)Signature["Parameters"]) {
                        if (!Names.Add((string)Parameter["Name"]) || Variadic || (Optional && !(bool)Parameter["Optional"] && !(bool)Parameter["Variadic"]))
                            throw new InvalidOperationException("invalid API parameter ordering");
                        Optional |= (bool)Parameter["Optional"]; Variadic = (bool)Parameter["Variadic"];
                        CheckType((string)Parameter["Type"], Types);
                    }
                    foreach (string Return in ((JArray)Signature["Returns"]).Values<string>()) CheckType(Return, Types);
                }
            }
            Version Current = Version.Parse(((string)Catalog["Api"]["Version"]).Split('-')[0]);
            foreach (var Value in Types.Values.Concat(Members.Values)) {
                JObject Availability = (JObject)Value["Availability"];
                if (!new[] { "Supported", "Experimental", "ClientUnqualified", "WorkInProgress", "Deferred", "Unavailable" }
                    .Contains((string)Availability["Qualification"])) throw new InvalidOperationException("unknown API qualification");
                if (!(bool)Availability["Implemented"] || Availability["RemovedSince"] != null ||
                    Version.Parse(((string)Availability["SinceApi"]).Split('-')[0]) > Current || String.IsNullOrWhiteSpace((string)Value["Summary"]))
                    throw new InvalidOperationException("inconsistent API availability");
            }
        }
        private static void CheckType(string Expression, IDictionary<string, JObject> Types)
        {
            string WithoutLiterals = System.Text.RegularExpressions.Regex.Replace(Expression, "\"[^\"]*\"", "");
            foreach (System.Text.RegularExpressions.Match Word in System.Text.RegularExpressions.Regex.Matches(WithoutLiterals, "[A-Za-z_][A-Za-z0-9_]*"))
                if (!Types.ContainsKey(Word.Value) && !new[] { "string", "number", "boolean", "any", "nil", "unknown", "never", "thread" }.Contains(Word.Value))
                    throw new InvalidOperationException("unknown API type: " + Word.Value);
        }
        private static JObject TypeRecord(string Name, string Kind, string Summary, string ApiVersion)
        { return new JObject { ["Id"] = Name, ["Name"] = Name, ["Kind"] = Kind, ["Summary"] = Summary,
            ["Availability"] = Availability(ApiVersion, "Experimental"), ["Preview"] = "Unavailable" }; }
        private static JObject Availability(string ApiVersion, string Qualification)
        { return new JObject { ["SinceApi"] = ApiVersion, ["Implemented"] = true, ["Qualification"] = Qualification }; }
        private static void Add(IDictionary<string, JObject> Members, JObject Value)
        { Members.Add((string)Value["Id"], Value); }
        private static JObject MemberRecord(JObject Value, string ApiVersion)
        {
            string Owner = (string)Value["Owner"], Name = (string)Value["Name"], Kind = (string)Value["Kind"];
            var Result = new JObject { ["Id"] = Owner + "." + Name, ["Name"] = Name, ["OwnerId"] = Owner,
                ["Kind"] = Kind, ["Summary"] = (string)Value["Summary"], ["Preview"] = (string)Value["Preview"] ?? "Unavailable",
                ["Availability"] = Availability((string)Value["SinceApi"] ?? ApiVersion, (string)Value["Qualification"] ?? "Experimental") };
            if (Kind == "Property" || Kind == "Singleton") {
                Result["ValueType"] = Value["ValueType"].DeepClone(); Result["Writable"] = (bool?)Value["Writable"] ?? false;
            } else {
                var Parameters = new JArray();
                foreach (JArray Arg in (JArray)Value["Args"]) Parameters.Add(new JObject {
                    ["Name"] = (string)Arg[0], ["Type"] = (string)Arg[1], ["Optional"] = ((string)Arg[1]).EndsWith("?", StringComparison.Ordinal),
                    ["Variadic"] = Arg.Count > 2 && (bool)Arg[2] });
                Result["Signatures"] = new JArray(new JObject { ["Parameters"] = Parameters, ["Returns"] = Value["Returns"].DeepClone() });
                if (Value["Overloads"] is JArray Overloads) {
                    Result["Signatures"] = new JArray(Overloads.Cast<JArray>().Select(Args => new JObject {
                        ["Parameters"] = new JArray(Args.Cast<JArray>().Select(Arg => new JObject { ["Name"] = Arg[0].DeepClone(), ["Type"] = Arg[1].DeepClone(), ["Optional"] = false, ["Variadic"] = false })),
                        ["Returns"] = Value["Returns"].DeepClone() }));
                }
            }
            return Result;
        }
        private static string ValueName(Gui.GuiValueKind Kind)
        {
            switch (Kind) {
                case Gui.GuiValueKind.String: return "string";
                case Gui.GuiValueKind.Boolean: return "boolean";
                case Gui.GuiValueKind.Integer: case Gui.GuiValueKind.Number: return "number";
                case Gui.GuiValueKind.GuiNodeReference: return "GuiNode?";
                default: return Kind.ToString();
            }
        }
        private static JObject NamedSignature(string Name, string Type, string Result)
        { return new JObject { ["Parameters"] = new JArray(new JObject { ["Name"] = Name, ["Type"] = Type, ["Optional"] = false, ["Variadic"] = false }), ["Returns"] = new JArray(Result) }; }
        public static JObject Limits()
        {
            var GuiLimits = new Gui.GuiConfig().Validate();
            return new JObject { ["Addon.ArchiveBytes"] = AddonPolicy.MaxArchiveBytes, ["Addon.ExpandedBytes"] = AddonPolicy.MaxExpandedBytes,
                ["Addon.ManifestBytes"] = AddonPolicy.MaxManifestBytes, ["Addon.SourceBytes"] = AddonPolicy.MaxSourceBytes,
                ["Addon.AggregateSourceBytes"] = AddonPolicy.MaxAggregateSourceBytes, ["Addon.Modules"] = AddonPolicy.MaxSourceModules,
                ["Addon.Dependencies"] = AddonPolicy.MaxDependencies, ["Gui.ObjectsPerScreen"] = GuiLimits.MaxObjectsPerScreen,
                ["Gui.ProjectedElementsPerScreen"] = GuiLimits.MaxProjectedElementsPerScreen, ["Gui.ClipDepth"] = GuiLimits.MaxEffectiveClipDepth,
                ["Gui.TextBytesPerScreen"] = GuiLimits.MaxTextUtf8BytesPerScreen };
        }
    }
}
