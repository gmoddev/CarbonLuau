using System;
using System.Globalization;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal enum GuiImageSourceKind { None = 0, Sprite = 1, Png = 2, Item = 3, SteamAvatar = 4 }

        internal sealed class GuiImageSourceValue : IEquatable<GuiImageSourceValue>
        {
            internal readonly GuiImageSourceKind Kind;
            internal readonly string Primary, Secondary;
            internal readonly int ItemId;

            private GuiImageSourceValue(GuiImageSourceKind Kind, string Primary = "", string Secondary = "", int ItemId = 0)
            { this.Kind = Kind; this.Primary = Primary; this.Secondary = Secondary; this.ItemId = ItemId; }

            internal static GuiImageSourceValue Parse(string[] Fields, int KindIndex)
            {
                if (Fields == null || KindIndex >= Fields.Length || Fields[KindIndex] != "imagesource" || Fields.Length < KindIndex + 2)
                    throw new FacadeException("Image expects ImageSource");
                string SourceKind = Fields[KindIndex + 1];
                if (SourceKind == "None" && Fields.Length == KindIndex + 2)
                    return new GuiImageSourceValue(GuiImageSourceKind.None);
                if (SourceKind == "Sprite" && Fields.Length == KindIndex + 3) {
                    ValidateSprite(Fields[KindIndex + 2]);
                    return new GuiImageSourceValue(GuiImageSourceKind.Sprite, Fields[KindIndex + 2]);
                }
                if (SourceKind == "Png" && Fields.Length == KindIndex + 3) {
                    ValidateIdentifier(Fields[KindIndex + 2], "PNG identifier", false);
                    return new GuiImageSourceValue(GuiImageSourceKind.Png, Fields[KindIndex + 2]);
                }
                if (SourceKind == "Item" && Fields.Length == KindIndex + 4) {
                    int ItemId;
                    if (!Int32.TryParse(Fields[KindIndex + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out ItemId) ||
                        Fields[KindIndex + 2] != ItemId.ToString(CultureInfo.InvariantCulture))
                        throw new FacadeException("ImageSource.Item ItemId must be a canonical signed 32-bit integer");
                    string SkinId = Fields[KindIndex + 3];
                    if (SkinId.Length != 0) ValidateIdentifier(SkinId, "skin identifier", true);
                    return new GuiImageSourceValue(GuiImageSourceKind.Item, "", SkinId, ItemId);
                }
                if (SourceKind == "SteamAvatar" && Fields.Length == KindIndex + 3) {
                    ValidateIdentifier(Fields[KindIndex + 2], "Steam user identifier", true);
                    return new GuiImageSourceValue(GuiImageSourceKind.SteamAvatar, Fields[KindIndex + 2]);
                }
                throw new FacadeException("malformed ImageSource value");
            }

            internal string[] Encode()
            {
                switch (Kind) {
                    case GuiImageSourceKind.None: return new[] {"imagesource", "None"};
                    case GuiImageSourceKind.Sprite: return new[] {"imagesource", "Sprite", Primary};
                    case GuiImageSourceKind.Png: return new[] {"imagesource", "Png", Primary};
                    case GuiImageSourceKind.Item: return new[] {"imagesource", "Item", ItemId.ToString(CultureInfo.InvariantCulture), Secondary};
                    case GuiImageSourceKind.SteamAvatar: return new[] {"imagesource", "SteamAvatar", Primary};
                    default: throw new InvalidOperationException("unknown image source kind");
                }
            }

            internal string Describe()
            {
                string[] Values = Encode();
                return String.Join(":", Values);
            }

            public bool Equals(GuiImageSourceValue Other)
            { return Other != null && Kind == Other.Kind && Primary == Other.Primary && Secondary == Other.Secondary && ItemId == Other.ItemId; }
            public override bool Equals(object Value) { return Equals(Value as GuiImageSourceValue); }
            public override int GetHashCode()
            { unchecked { return (((int)Kind * 397 ^ Primary.GetHashCode()) * 397 ^ Secondary.GetHashCode()) * 397 ^ ItemId; } }

            private static void ValidateSprite(string Value)
            {
                if (String.IsNullOrEmpty(Value) || GuiRenderValue.Utf8Bytes(Value) > 256)
                    throw new FacadeException("ImageSource.Sprite name must contain 1..256 UTF-8 bytes");
                if (Value[0] == '/' || Value[Value.Length - 1] == '/' || Value.IndexOf("//", StringComparison.Ordinal) >= 0)
                    throw new FacadeException("ImageSource.Sprite name is not a canonical asset key");
                string[] Segments = Value.Split('/');
                foreach (string Segment in Segments) if (Segment == "." || Segment == "..")
                    throw new FacadeException("ImageSource.Sprite name is not a canonical asset key");
                foreach (char Character in Value) {
                    bool Allowed = Character >= 'a' && Character <= 'z' || Character >= 'A' && Character <= 'Z' ||
                        Character >= '0' && Character <= '9' || Character == '_' || Character == '-' || Character == '.' || Character == '/';
                    if (!Allowed) throw new FacadeException("ImageSource.Sprite name is not a canonical asset key");
                }
            }

            private static void ValidateIdentifier(string Value, string Label, bool Unsigned64)
            {
                if (String.IsNullOrEmpty(Value) || Value.Length > 20 || Value.Length > 1 && Value[0] == '0')
                    throw new FacadeException(Label + " must be a canonical decimal string of at most 20 digits");
                foreach (char Character in Value) if (Character < '0' || Character > '9')
                    throw new FacadeException(Label + " must be a canonical decimal string of at most 20 digits");
                ulong Parsed;
                if (Unsigned64 && !UInt64.TryParse(Value, NumberStyles.None, CultureInfo.InvariantCulture, out Parsed))
                    throw new FacadeException(Label + " exceeds the unsigned 64-bit range");
            }
        }
    }
}
