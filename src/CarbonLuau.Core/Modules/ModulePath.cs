using System;
using System.IO;

namespace CarbonLuau.Core
{
    public static class ModulePath
    {
    public static void Validate(string Value, bool File)
    {
        if (String.IsNullOrEmpty(Value) || Value.Length > 127 || Path.IsPathRooted(Value)) throw new InvalidOperationException("source path: expected bounded relative path");
        string Name = Value;
        if (File) {
            if (!Name.EndsWith(".luau", StringComparison.Ordinal)) throw new InvalidOperationException("source path: expected .luau extension");
            Name = Name.Substring(0, Name.Length - 5);
        }
        foreach (string Part in Name.Split('/')) {
            if (Part.Length == 0) throw new InvalidOperationException("source path: empty segment");
            foreach (char C in Part)
                if (!(C >= 'a' && C <= 'z') && !(C >= '0' && C <= '9') && C != '_' && C != '-')
                    throw new InvalidOperationException("source path: use lowercase letters, digits, '_' or '-' and single '/' separators");
            string Upper = Part.ToUpperInvariant();
            if (Upper == "CON" || Upper == "PRN" || Upper == "AUX" || Upper == "NUL" ||
                (Upper.Length == 4 && (Upper.StartsWith("COM") || Upper.StartsWith("LPT")) && Upper[3] >= '0' && Upper[3] <= '9'))
                throw new InvalidOperationException("source path: reserved device name");
        }
    }
    }
}
