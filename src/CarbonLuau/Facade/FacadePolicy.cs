using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public static class FacadePolicy
        {
            public const string ApiName = "CarbonLuau", ApiVersion = "0.5.0-experimental";
            public const int Players = 1024, ListenersPerSignal = 128, Listeners = 256, Commands = 64, PendingEvents = 256;
            public const int CommandBytes = 32, PermissionBytes = 128, DescriptionBytes = 256;
            public const int MessageBytes = 1024, Arguments = 16, ArgumentBytes = 512, TotalArgumentBytes = 4096;
            public const int ItemShortNameBytes = 128, InventoryStacks = 128;
            public const long MaxExactLuauInteger = 9007199254740991L;
            public static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
            public static void Text(string Value, int Bytes, string Label)
            { GuiTextPolicy.Text(Value, Bytes, Label); }
            public static void Identifier(string Value, bool Permission)
            {
                Text(Value, Permission ? PermissionBytes : CommandBytes, Permission ? "permission name" : "command name");
                if (Value.Length == 0 || Value[0] < 'a' || Value[0] > 'z') throw new FacadeException("name is invalid");
                foreach (char C in Value)
                    if (!(C >= 'a' && C <= 'z') && !(C >= '0' && C <= '9') && C != '_' && C != '-' && !(Permission && C == '.'))
                        throw new FacadeException("name is invalid");
                if (Permission && (Value.EndsWith(".", StringComparison.Ordinal) || Value.Contains("..") || !Value.Contains(".")))
                    throw new FacadeException("permission requires nonempty dotted namespace");
                if (!Permission && (Value.StartsWith("carbon", StringComparison.Ordinal) || Value.StartsWith("oxide", StringComparison.Ordinal) ||
                    Value.StartsWith("rcon", StringComparison.Ordinal) || Value == "c" || Value == "quit" || Value == "restart" || Value == "server"))
                    throw new FacadeException("protected command name");
            }
            public static void UserId(string Value)
            {
                if (String.IsNullOrEmpty(Value) || Value.Length > 20) throw new FacadeException("invalid user ID string");
                foreach (char C in Value) if (C < '0' || C > '9') throw new FacadeException("invalid user ID string");
            }
            public static void ItemShortName(string Value)
            {
                if (String.IsNullOrEmpty(Value) || Value.Length > ItemShortNameBytes ||
                    Char.IsWhiteSpace(Value[0]) || Char.IsWhiteSpace(Value[Value.Length - 1]))
                    throw new FacadeException("item short name is invalid");
                foreach (char C in Value)
                    if (C == '\0' || C > 127 || (C >= 'A' && C <= 'Z'))
                        throw new FacadeException("item short name must be canonical lowercase ASCII");
            }
            public static long ExactPositiveInteger(string Value, string Label)
            {
                long Result;
                if (!Int64.TryParse(Value, NumberStyles.None, CultureInfo.InvariantCulture, out Result) ||
                    Result < 1 || Result > MaxExactLuauInteger)
                    throw new FacadeException(Label + " must be an exact positive integer");
                return Result;
            }
            public static byte[] Pack(params string[] Fields)
            { return Utf8.GetBytes(String.Join("\0", Fields) + (Fields.Length == 0 ? "" : "\0")); }
            public static string[] Unpack(byte[] Bytes)
            {
                if (Bytes.Length == 0) return new string[0];
                if (Bytes[Bytes.Length - 1] != 0) throw new FacadeException("invalid host payload");
                return Utf8.GetString(Bytes, 0, Bytes.Length - 1).Split('\0');
            }
        }

        // These managed-only views are never serialized to Lua. Fresh views must
    }
}
