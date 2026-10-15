using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CarbonLuau.Core
{
    public static class ApiArtifacts
    {
        public static string Definitions(JObject Catalog)
        {
            ApiCatalog.Validate(Catalog);
            var Result = new StringBuilder("-- Generated from implementation-owned CarbonLuau API declarations. Do not edit.\n");
            var Types = ((JArray)Catalog["Types"]).Cast<JObject>().ToDictionary(Value => (string)Value["Id"], StringComparer.Ordinal);
            var Members = ((JArray)Catalog["Members"]).Cast<JObject>().ToList();
            if (Members.Where(Value => (string)Value["Kind"] == "Signal").Any(Value => SignalType(Value) != "Signal")) {
                if (Types.ContainsKey("SignalWith")) throw new InvalidOperationException("generated Signal callback alias conflicts with API type");
                // A static structural specialization of the same runtime Signal.
                // Preserve the existing nominal Signal and its legacy callback.
                Result.Append("export type SignalWith<Callback> = {\n")
                    .Append("    read Connect: (self: SignalWith<Callback>, Callback: Callback) -> (Connection),\n")
                    .Append("}\n\n");
            }
            var Written = new HashSet<string>(StringComparer.Ordinal);
            foreach (string Name in Types.Keys.OrderBy(Value => Value, StringComparer.Ordinal))
                WriteType(Name, Types, Members, Written, Result);
            foreach (string Name in Types.Keys.OrderBy(Value => Value, StringComparer.Ordinal)) {
                // A Global's readonly enum property is a namespace table, not an
                // enum userdata. Its singletons stay typed as the nominal enum;
                // a nested enum does not also acquire a standalone global.
                if (Members.Any(Value => NamespaceEnum(Value, Types, Members) == Name)) continue;
                var Static = Members.Where(Value => (string)Value["OwnerId"] == Name &&
                    ((string)Value["Kind"] == "Constructor" || (string)Value["Kind"] == "Singleton" ||
                        NamespaceEnum(Value, Types, Members) != null || NamespaceProperty(Value, Types))).ToArray();
                if (Static.Length == 0) continue;
                string Global = Name == "Task" ? "task" : Name;
                Result.Append("declare ").Append(Global).Append(": {\n");
                foreach (var Member in Static) {
                    Result.Append("    read ").Append((string)Member["Name"]).Append(": ");
                    string Enum = NamespaceEnum(Member, Types, Members);
                    if (Enum != null) Result.Append(NamespaceType(Enum, Members));
                    else if ((string)Member["Kind"] == "Singleton" || NamespaceProperty(Member, Types)) Result.Append((string)Member["ValueType"]);
                    else Result.Append(FunctionType((JObject)Member["Signatures"][0]));
                    Result.Append(",\n");
                }
                Result.Append("}\n\n");
            }
            Result.Append("declare game: Game\ndeclare addon: Addon\n");
            return Result.ToString();
        }
        private static void WriteType(string Name, IDictionary<string, JObject> Types, IList<JObject> Members,
            ISet<string> Written, StringBuilder Result)
        {
            if (!Written.Add(Name)) return;
            if ((string)Types[Name]["Kind"] == "Global" && Members.Where(Value => (string)Value["OwnerId"] == Name)
                .All(Value => (string)Value["Kind"] == "Singleton" || NamespaceProperty(Value, Types))) return;
            if ((string)Types[Name]["Availability"]["Qualification"] == "WorkInProgress")
                Result.Append("-- WORK IN PROGRESS / UNQUALIFIED; introduction version is not qualification.\n");
            if ((string)Types[Name]["Representation"] == "Alias") {
                Result.Append("export type ").Append(Name).Append(" = ").Append((string)Types[Name]["TypeExpression"]).Append("\n\n");
                return;
            }
            string Base = (string)Types[Name]["BaseTypeId"];
            if (Base != null) WriteType(Base, Types, Members, Written, Result);
            if ((string)Types[Name]["Representation"] == "Record") {
                Result.Append("export type ").Append(Name).Append(" = {\n");
                foreach (var Member in Members.Where(Value => (string)Value["OwnerId"] == Name))
                    Result.Append("    read ").Append((string)Member["Name"]).Append(": ").Append((string)Member["ValueType"]).Append(",\n");
                Result.Append("}\n\n");
                return;
            }
            Result.Append("declare extern type ").Append(Name);
            if (Base != null) Result.Append(" extends ").Append(Base);
            Result.Append(" with\n");
            foreach (var Member in Members.Where(Value => (string)Value["OwnerId"] == Name)) {
                string Kind = (string)Member["Kind"], MemberName = (string)Member["Name"];
                if (Kind == "Constructor" || Kind == "Singleton") continue;
                if (Kind == "Operator") {
                    Result.Append("    ").Append(MemberName).Append(": ")
                        .Append(String.Join(" & ", ((JArray)Member["Signatures"]).Cast<JObject>().Select(Signature => "(" + FunctionType(Signature) + ")"))).Append('\n');
                    continue;
                }
                if (Base != null && Kind != "Method" && Members.Any(Value => (string)Value["OwnerId"] == Base && (string)Value["Name"] == MemberName)) continue;
                if (Kind == "Property" || Kind == "Signal") {
                    Result.Append("    ");
                    if (Kind == "Signal" || !(bool)Member["Writable"]) Result.Append("read ");
                    string Enum = NamespaceEnum(Member, Types, Members);
                    Result.Append(MemberName).Append(": ").Append(Enum != null ? NamespaceType(Enum, Members) :
                        Kind == "Signal" ? SignalType(Member) : (string)Member["ValueType"]).Append('\n');
                    continue;
                }
                foreach (JObject Signature in (JArray)Member["Signatures"])
                    Result.Append("    function ").Append(MemberName).Append(Kind == "Operator" ? "(" : "(self")
                        .Append(Parameters(Signature, Kind != "Operator")).Append("): ").Append(Returns(Signature)).Append('\n');
            }
            Result.Append("end\n\n");
        }
        private static string Parameters(JObject Signature, bool Receiver)
        {
            var Values = new List<string>();
            foreach (JObject Parameter in (JArray)Signature["Parameters"])
                Values.Add((bool)Parameter["Variadic"] ? (Receiver ? "...: " : "...") + (string)Parameter["Type"] : (string)Parameter["Name"] + ": " + (string)Parameter["Type"]);
            return (Receiver && Values.Count > 0 ? ", " : "") + String.Join(", ", Values);
        }
        private static string Returns(JObject Signature)
        { return "(" + String.Join(", ", ((JArray)Signature["Returns"]).Select(Value => (string)Value)) + ")"; }
        private static string FunctionType(JObject Signature)
        { return "(" + Parameters(Signature, false) + ") -> " + Returns(Signature); }
        private static string SignalType(JObject Member)
        {
            var Signatures = Member["Signatures"] as JArray;
            if (Signatures == null || Signatures.Count == 0 || Signatures.Any(Value =>
                !(Value is JObject) || !(Value["Parameters"] is JArray) || !(Value["Returns"] is JArray) ||
                ((JArray)Value["Returns"]).Count != 0))
                throw new InvalidOperationException("invalid observation Signal signature: " + (string)Member["Id"]);
            if (Signatures.Count == 1) {
                var Parameters = (JArray)Signatures[0]["Parameters"];
                if (Parameters.Count == 1 && (string)Parameters[0]["Type"] == "Player" &&
                    !(bool)Parameters[0]["Optional"] && !(bool)Parameters[0]["Variadic"]) return "Signal";
            }
            var Callbacks = Signatures.Cast<JObject>().Select(FunctionType).Distinct(StringComparer.Ordinal).ToArray();
            return "SignalWith<" + (Callbacks.Length == 1 ? Callbacks[0] :
                String.Join(" & ", Callbacks.Select(Value => "(" + Value + ")"))) + ">";
        }
        private static string NamespaceEnum(JObject Member, IDictionary<string, JObject> Types, IList<JObject> Members)
        {
            string Owner = (string)Member["OwnerId"], ValueType = (string)Member["ValueType"];
            if ((string)Member["Kind"] != "Property" || (bool?)Member["Writable"] != false ||
                (string)Types[Owner]["Kind"] != "Global" || ValueType == null || !Types.ContainsKey(ValueType) ||
                (string)Types[ValueType]["Kind"] != "Enum" ||
                !Members.Any(Value => (string)Value["OwnerId"] == ValueType && (string)Value["Kind"] == "Singleton")) return null;
            return ValueType;
        }
        private static bool NamespaceProperty(JObject Member, IDictionary<string, JObject> Types)
        {
            string Target = (string)Member["ValueType"];
            return (string)Member["Kind"] == "Property" && (bool?)Member["Writable"] == false &&
                (string)Types[(string)Member["OwnerId"]]["Kind"] == "Global" && Target != null &&
                Types.ContainsKey(Target) && (string)Types[Target]["Kind"] == "Value" &&
                (string)Types[Target]["Representation"] == "Record";
        }
        private static string NamespaceType(string Enum, IList<JObject> Members)
        {
            return "{" + String.Join(", ", Members.Where(Value => (string)Value["OwnerId"] == Enum &&
                (string)Value["Kind"] == "Singleton").OrderBy(Value => (string)Value["Name"], StringComparer.Ordinal)
                .Select(Value => "read " + (string)Value["Name"] + ": " + (string)Value["ValueType"])) + "}";
        }
        public static JObject Documentation(JObject Catalog)
        {
            var Result = new JObject();
            var Types = ((JArray)Catalog["Types"]).Cast<JObject>().ToDictionary(Value => (string)Value["Id"], StringComparer.Ordinal);
            var Members = ((JArray)Catalog["Members"]).Cast<JObject>().ToList();
            foreach (JObject Member in Members) {
                bool Static = (string)Member["Kind"] == "Constructor" || (string)Member["Kind"] == "Singleton";
                var Namespaces = Members.Where(Value => NamespaceEnum(Value, Types, Members) == (string)Member["OwnerId"]).ToArray();
                if (Static && Namespaces.Length > 0) {
                    foreach (var Namespace in Namespaces)
                        Result["@carbonluau/global/" + (string)Namespace["Id"] + "." + (string)Member["Name"]] =
                            new JObject { ["documentation"] = (string)Member["Summary"] };
                    continue;
                }
                Result["@carbonluau/" + (Static ? "global/" : "globaltype/") +
                    ((string)Member["OwnerId"] == "Task" ? "task." + (string)Member["Name"] : (string)Member["Id"])] = new JObject { ["documentation"] = (string)Member["Summary"] };
                if (NamespaceEnum(Member, Types, Members) != null || NamespaceProperty(Member, Types))
                    Result["@carbonluau/global/" + (string)Member["Id"]] = new JObject { ["documentation"] = (string)Member["Summary"] };
            }
            return Result;
        }
    }
}
