using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class FacadeException : InvalidOperationException
        { public FacadeException(string Message) : base(Message) { } }

        internal static class GuiTextPolicy
        {
            internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
            public static void Text(string Value, int Bytes, string Label)
            {
                if (Value == null || Value.Length > Bytes || Value.IndexOf('\0') >= 0 || Utf8.GetByteCount(Value) > Bytes)
                    throw new FacadeException(Label + " exceeds limit or contains NUL");
            }
        }
    }
}
