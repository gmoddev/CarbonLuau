using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Opaque projection identities. Only the production adapter supplies client names.
        internal class GuiProjectionNames
        {
            internal readonly string ClientPrefix;
            internal GuiProjectionNames(string Prefix) { ClientPrefix = Prefix; }
            internal string RootClientId { get { return ClientPrefix + "r"; } }
            internal string ObjectClientId(ulong ObjectId) { return ClientPrefix + "o" + ObjectId.ToString("x", CultureInfo.InvariantCulture); }
            internal string TextClientId(ulong ObjectId) { return ClientPrefix + "t" + ObjectId.ToString("x", CultureInfo.InvariantCulture); }
            internal string ImageClientId(ulong ObjectId) { return ClientPrefix + "i" + ObjectId.ToString("x", CultureInfo.InvariantCulture); }
            internal string ActionClientId(ulong ObjectId) { return ClientPrefix + "a" + ObjectId.ToString("x", CultureInfo.InvariantCulture); }
            internal string ClipClientId(ulong ObjectId) { return ClientPrefix + "c" + ObjectId.ToString("x", CultureInfo.InvariantCulture); }
            internal string ScrollContentClientId(ulong ObjectId) { return ObjectClientId(ObjectId) + "___Content"; }
        }
    }
}
