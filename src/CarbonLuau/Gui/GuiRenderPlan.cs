using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class GuiBackendTarget : IEquatable<GuiBackendTarget>
        {
            internal readonly string ExactPlayerConnectionToken, ExactPlayerUserId, ClientRootId;
            internal GuiBackendTarget(string ExactPlayerConnectionToken, string ExactPlayerUserId, string ClientRootId)
            {
                Validate(ExactPlayerConnectionToken, "exact Player connection token"); Validate(ExactPlayerUserId, "exact Player user id");
                Validate(ClientRootId, "client root id"); this.ExactPlayerConnectionToken = ExactPlayerConnectionToken;
                this.ExactPlayerUserId = ExactPlayerUserId; this.ClientRootId = ClientRootId;
            }
            private static void Validate(string Value, string Label)
            { if (String.IsNullOrEmpty(Value) || Value.Length > 256 || Value.IndexOf('\0') >= 0) throw new InvalidOperationException(Label + " is invalid"); }
            public bool Equals(GuiBackendTarget Other)
            { return Other != null && ExactPlayerConnectionToken == Other.ExactPlayerConnectionToken && ExactPlayerUserId == Other.ExactPlayerUserId && ClientRootId == Other.ClientRootId; }
            public override bool Equals(object Value) { return Equals(Value as GuiBackendTarget); }
            public override int GetHashCode()
            { unchecked { return ((ExactPlayerConnectionToken.GetHashCode() * 397) ^ ExactPlayerUserId.GetHashCode()) * 31 ^ ClientRootId.GetHashCode(); } }
            internal string Key { get { return ExactPlayerConnectionToken.Length.ToString(CultureInfo.InvariantCulture) + ":" + ExactPlayerConnectionToken + ExactPlayerUserId.Length.ToString(CultureInfo.InvariantCulture) + ":" + ExactPlayerUserId + ClientRootId; } }
        }
    }
}
