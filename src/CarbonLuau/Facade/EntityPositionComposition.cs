namespace Carbon.Plugins
{
    // Arithmetic only: callers own capture, lifetime validation and exclusive
    // scratch access. No engine object, pointer or observation authority lives here.
    internal static class EntityPositionComposition
    {
        // An implementation ceiling, not a selected discovery depth policy.
        // This counts ALL records, including the leaf (at most 64 parents).
        internal const int HardMaximumDepth = 65;

        internal readonly struct Position
        {
            internal readonly float X, Y, Z;

            internal Position(float X, float Y, float Z)
            {
                this.X = X;
                this.Y = Y;
                this.Z = Z;
            }
        }

        internal readonly struct LocalTRS
        {
            internal readonly float PX, PY, PZ;
            internal readonly float QX, QY, QZ, QW;
            internal readonly float SX, SY, SZ;

            internal LocalTRS(float PX, float PY, float PZ,
                float QX, float QY, float QZ, float QW, float SX, float SY, float SZ)
            {
                this.PX = PX;
                this.PY = PY;
                this.PZ = PZ;
                this.QX = QX;
                this.QY = QY;
                this.QZ = QZ;
                this.QW = QW;
                this.SX = SX;
                this.SY = SY;
                this.SZ = SZ;
            }
        }

        // Scratch[0] is the captured leaf LOCAL TRS; [1..Count) contains
        // successive parents through the root. Count and Maximum include the leaf.
        // The caller must certify the complete chain; this kernel cannot do so.
        // Only the prefix is inspected. Failure never publishes a partial result.
        internal static bool TryCompose(LocalTRS[] Scratch, int Count, int Maximum,
            out Position WorldPosition)
        {
            WorldPosition = default(Position);
            if (Maximum < 1 || Maximum > HardMaximumDepth || Count < 1 ||
                Count > Maximum || Scratch == null || Scratch.Length > HardMaximumDepth ||
                Count > Scratch.Length) return false;

            LocalTRS Leaf = Scratch[0];
            if (!IsFinite(Leaf)) return false;
            float X = Leaf.PX, Y = Leaf.PY, Z = Leaf.PZ;
            for (int Index = 1; Index < Count; Index++)
            {
                LocalTRS Parent = Scratch[Index];
                if (!IsFinite(Parent)) return false;

                float ScaledX = Parent.SX * X;
                float ScaledY = Parent.SY * Y;
                float ScaledZ = Parent.SZ * Z;
                if (!IsFinite(ScaledX) || !IsFinite(ScaledY) || !IsFinite(ScaledZ)) return false;

                // Unity's quaternion-vector polynomial, using float operations.
                // Preserve captured components: do not normalize or clamp them.
                float TwiceX = Parent.QX * 2f;
                float TwiceY = Parent.QY * 2f;
                float TwiceZ = Parent.QZ * 2f;
                float XX = Parent.QX * TwiceX, YY = Parent.QY * TwiceY, ZZ = Parent.QZ * TwiceZ;
                float XY = Parent.QX * TwiceY, XZ = Parent.QX * TwiceZ, YZ = Parent.QY * TwiceZ;
                float WX = Parent.QW * TwiceX, WY = Parent.QW * TwiceY, WZ = Parent.QW * TwiceZ;
                float RotatedX = (1f - (YY + ZZ)) * ScaledX + (XY - WZ) * ScaledY + (XZ + WY) * ScaledZ;
                float RotatedY = (XY + WZ) * ScaledX + (1f - (XX + ZZ)) * ScaledY + (YZ - WX) * ScaledZ;
                float RotatedZ = (XZ - WY) * ScaledX + (YZ + WX) * ScaledY + (1f - (XX + YY)) * ScaledZ;
                if (!IsFinite(RotatedX) || !IsFinite(RotatedY) || !IsFinite(RotatedZ)) return false;

                X = RotatedX + Parent.PX;
                Y = RotatedY + Parent.PY;
                Z = RotatedZ + Parent.PZ;
                if (!IsFinite(X) || !IsFinite(Y) || !IsFinite(Z)) return false;
            }

            WorldPosition = new Position(X, Y, Z);
            return true;
        }

        private static bool IsFinite(LocalTRS Value)
        {
            return IsFinite(Value.PX) && IsFinite(Value.PY) && IsFinite(Value.PZ) &&
                IsFinite(Value.QX) && IsFinite(Value.QY) && IsFinite(Value.QZ) && IsFinite(Value.QW) &&
                IsFinite(Value.SX) && IsFinite(Value.SY) && IsFinite(Value.SZ);
        }

        private static bool IsFinite(float Value)
        {
            return !float.IsNaN(Value) && !float.IsInfinity(Value);
        }
    }
}
