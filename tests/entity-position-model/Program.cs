using System;
using Model = Carbon.Plugins.EntityPositionComposition;
using Position = Carbon.Plugins.EntityPositionComposition.Position;
using LocalTRS = Carbon.Plugins.EntityPositionComposition.LocalTRS;

internal static class Program
{
    private static int Checks;

    private static void Check(bool Condition, string Label)
    {
        Checks++;
        if (!Condition) throw new Exception(Label);
    }

    private static LocalTRS TRS(float PX = 0f, float PY = 0f, float PZ = 0f,
        float QX = 0f, float QY = 0f, float QZ = 0f, float QW = 1f,
        float SX = 1f, float SY = 1f, float SZ = 1f)
    {
        return new LocalTRS(PX, PY, PZ, QX, QY, QZ, QW, SX, SY, SZ);
    }

    private static void Near(float Actual, double Expected, string Label)
    {
        Check(!float.IsNaN(Actual) && !float.IsInfinity(Actual) &&
            Math.Abs(Actual - Expected) <= 0.00002 * (1.0 + Math.Abs(Expected)), Label);
    }

    private static void Expected(LocalTRS[] Scratch, int Count, int Maximum,
        double X, double Y, double Z, string Label)
    {
        Position Result;
        Check(Model.TryCompose(Scratch, Count, Maximum, out Result), Label + " accepted");
        Near(Result.X, X, Label + " X");
        Near(Result.Y, Y, Label + " Y");
        Near(Result.Z, Z, Label + " Z");
    }

    private static void Rejected(LocalTRS[] Scratch, int Count, int Maximum, string Label)
    {
        Position Result = new Position(17f, 19f, 23f);
        Check(!Model.TryCompose(Scratch, Count, Maximum, out Result), Label + " rejected");
        Check(Result.X == 0f && Result.Y == 0f && Result.Z == 0f, Label + " no partial result");
    }

    // Independent q*v reference via cross products in double precision. It does
    // not reuse the production rotation matrix or coefficient grouping.
    private static void ReferenceStep(LocalTRS Parent, ref double X, ref double Y, ref double Z)
    {
        double VX = Parent.SX * X, VY = Parent.SY * Y, VZ = Parent.SZ * Z;
        double TX = 2.0 * (Parent.QY * VZ - Parent.QZ * VY);
        double TY = 2.0 * (Parent.QZ * VX - Parent.QX * VZ);
        double TZ = 2.0 * (Parent.QX * VY - Parent.QY * VX);
        X = VX + Parent.QW * TX + (Parent.QY * TZ - Parent.QZ * TY) + Parent.PX;
        Y = VY + Parent.QW * TY + (Parent.QZ * TX - Parent.QX * TZ) + Parent.PY;
        Z = VZ + Parent.QW * TZ + (Parent.QX * TY - Parent.QY * TX) + Parent.PZ;
    }

    private static void ReferenceCase(LocalTRS[] Scratch, int Count, string Label)
    {
        double X = Scratch[0].PX, Y = Scratch[0].PY, Z = Scratch[0].PZ;
        for (int Index = 1; Index < Count; Index++) ReferenceStep(Scratch[Index], ref X, ref Y, ref Z);
        Expected(Scratch, Count, Model.HardMaximumDepth, X, Y, Z, Label);
    }

    private static void Geometry()
    {
        LocalTRS Leaf = TRS(1f, 2f, 3f);
        Expected(new[] { Leaf }, 1, 1, 1, 2, 3, "unparented");
        Expected(new[] { Leaf, TRS() }, 2, 2, 1, 2, 3, "identity");
        Expected(new[] { Leaf, TRS(PX: 4f, PY: -5f, PZ: 6f) }, 2, 2, 5, -3, 9, "translation");
        Expected(new[] { Leaf, TRS(SX: 2f, SY: -3f, SZ: 4f) }, 2, 2, 2, -6, 12, "signed nonuniform scale");
        Expected(new[] { Leaf, TRS(PX: 7f, PY: 8f, PZ: 9f, SX: 0f, SY: 0f, SZ: 0f) },
            2, 2, 7, 8, 9, "zero scale");
        Expected(new[] { Leaf, TRS(QX: 1f, QW: 0f) }, 2, 2, 1, -2, -3, "X half turn");
        Expected(new[] { Leaf, TRS(QY: 1f, QW: 0f) }, 2, 2, -1, 2, -3, "Y half turn");
        Expected(new[] { Leaf, TRS(QZ: 1f, QW: 0f) }, 2, 2, -1, -2, 3, "Z half turn");
        Expected(new[] { TRS(1f, 2f, 3f, QX: 1f, QW: 0f, SX: 7f, SY: 8f, SZ: 9f) },
            1, 1, 1, 2, 3, "leaf rotation and scale do not move its origin");

        float HalfRoot = (float)Math.Sqrt(0.5);
        Expected(new[] { Leaf, TRS(QZ: HalfRoot, QW: HalfRoot) }, 2, 2, -2, 1, 3, "Z quarter turn");
        Expected(new[] { Leaf, TRS(QZ: -HalfRoot, QW: -HalfRoot) }, 2, 2, -2, 1, 3, "quaternion sign");
        Expected(new[] { Leaf, TRS(PX: 10f, PY: 20f, PZ: 30f, QZ: HalfRoot, QW: HalfRoot,
            SX: 2f, SY: 3f, SZ: 4f) }, 2, 2, 4, 22, 42, "scale then rotate then translate");

        Expected(new[] {
            Leaf,
            TRS(PX: 1f, PY: 2f, PZ: 3f, QZ: 1f, QW: 0f, SX: 2f, SY: 3f, SZ: 4f),
            TRS(PX: 10f, PY: 20f, PZ: 30f, QX: 1f, QW: 0f, SX: 5f, SY: 6f, SZ: 7f)
        }, 3, 3, 5, 44, -75, "bottom-up two parents");
        ReferenceCase(new[] {
            Leaf,
            TRS(QX: HalfRoot, QW: HalfRoot, SX: 2f, SY: 3f, SZ: 4f),
            TRS(PX: -4f, QY: HalfRoot, QW: HalfRoot, SX: -1f, SY: 2f, SZ: 0.5f),
            TRS(PY: 8f, QZ: HalfRoot, QW: HalfRoot, SX: 3f, SY: 0.25f, SZ: 2f)
        }, 4, "rotated nonuniform multilevel");
        ReferenceCase(new[] { Leaf, TRS(QX: 0.2f, QY: -0.3f, QZ: 0.4f, QW: 0.5f) },
            2, "finite quaternion components preserved without normalization");
    }

    private static void DepthAndPrefix()
    {
        LocalTRS[] Scratch = new LocalTRS[Model.HardMaximumDepth];
        Scratch[0] = TRS(1f, 2f, 3f);
        for (int Index = 1; Index < Scratch.Length; Index++) Scratch[Index] = TRS(PX: 1f);
        for (int Count = 1; Count <= Model.HardMaximumDepth; Count++)
        {
            Expected(Scratch, Count, Count, Count, 2, 3, "record count " + Count);
            Rejected(Scratch, Count, Count - 1, "injected cap " + Count);
        }
        Rejected(Scratch, 0, 65, "empty prefix");
        Rejected(Scratch, -1, 65, "negative count");
        Rejected(Scratch, 66, 65, "count above ceiling");
        Rejected(Scratch, int.MaxValue, 65, "huge count");
        Rejected(Scratch, 1, -1, "negative cap");
        Rejected(Scratch, 1, 0, "zero cap");
        Rejected(Scratch, 1, 66, "cap above ceiling");
        Rejected(Scratch, 1, int.MaxValue, "huge cap");
        Rejected(new LocalTRS[0], 1, 1, "missing leaf");
        Rejected(new[] { TRS() }, 2, 2, "short scratch");
        Rejected(null, 0, 1, "null empty scratch");
        Rejected(null, 1, 1, "null used scratch");
        Rejected(new LocalTRS[66], 1, 65, "oversized scratch with short prefix");
        Scratch[1] = TRS(PX: float.NaN);
        Expected(Scratch, 1, 65, 1, 2, 3, "unused poisoned tail ignored");
        Rejected(Scratch, 2, 65, "poisoned value inside prefix");
    }

    private static LocalTRS WithComponent(int Index, float Value)
    {
        float[] Components = { 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f };
        Components[Index] = Value;
        return new LocalTRS(Components[0], Components[1], Components[2], Components[3],
            Components[4], Components[5], Components[6], Components[7], Components[8], Components[9]);
    }

    private static void NonfiniteAndOverflow()
    {
        LocalTRS Leaf = TRS(1f, 2f, 3f);
        float[] InvalidValues = { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
        foreach (float Value in InvalidValues)
        {
            for (int Component = 0; Component < 10; Component++)
            {
                Rejected(new[] { WithComponent(Component, Value) }, 1, 1,
                    "nonfinite leaf component " + Component);
                Rejected(new[] { Leaf, TRS(PX: 1f), WithComponent(Component, Value) }, 3, 3,
                    "nonfinite parent component " + Component);
            }
        }
        Rejected(new[] { TRS(PX: float.MaxValue), TRS(SX: 2f) }, 2, 2, "scale overflow");
        Rejected(new[] { TRS(PX: float.MaxValue), TRS(PX: float.MaxValue) },
            2, 2, "translation overflow");
        Rejected(new[] { Leaf, TRS(QX: float.MaxValue) }, 2, 2, "quaternion arithmetic overflow");
        Rejected(new[] {
            TRS(float.MaxValue, -float.MaxValue),
            TRS(QZ: (float)Math.Sin(Math.PI / 8.0), QW: (float)Math.Cos(Math.PI / 8.0))
        }, 2, 2, "rotation overflow");
        Rejected(new[] { TRS(PX: float.MaxValue / 4f), TRS(SX: 2f), TRS(SX: 4f) },
            3, 3, "later parent overflow does not publish partial result");
        Expected(new[] { TRS(float.MaxValue, -float.MaxValue, float.Epsilon) },
            1, 1, float.MaxValue, -float.MaxValue, float.Epsilon, "finite float extremes");
    }

    private static void CopiedValues()
    {
        LocalTRS SourceLeaf = TRS(1f, 2f, 3f);
        LocalTRS CapturedLeaf = SourceLeaf;
        LocalTRS[] Scratch = { CapturedLeaf, TRS(PX: 4f, SX: 2f) };
        LocalTRS CapturedParent = Scratch[1];
        Position Result;
        Check(Model.TryCompose(Scratch, 2, 2, out Result), "copy captured");
        Check(Scratch[0].PX == 1f && Scratch[1].PX == 4f && Scratch[1].SX == 2f, "scratch not modified");
        SourceLeaf = TRS(90f, 90f, 90f);
        Scratch[0] = SourceLeaf;
        Scratch[1] = TRS(PX: float.NaN);
        Check(CapturedLeaf.PX == 1f && CapturedLeaf.PY == 2f && CapturedLeaf.PZ == 3f,
            "leaf copy independent of source replacement");
        Check(CapturedParent.PX == 4f && CapturedParent.SX == 2f, "TRS copy independent of array replacement");
        Check(Result.X == 6f && Result.Y == 2f && Result.Z == 3f, "result independent of source replacement");
        Expected(new[] { CapturedLeaf, CapturedParent }, 2, 2, 6, 2, 3, "copied inputs reusable");
    }

    private static void ReferenceSweep()
    {
        Random Generator = new Random(25653776);
        LocalTRS[] Scratch = new LocalTRS[Model.HardMaximumDepth];
        Scratch[0] = TRS(1.25f, -2.5f, 3.75f);
        for (int Case = 0; Case < 260; Case++)
        {
            int Count = 1 + Case % Model.HardMaximumDepth;
            for (int Index = 1; Index < Count; Index++)
            {
                double AX = Generator.NextDouble() - 0.5, AY = Generator.NextDouble() - 0.5;
                double AZ = Generator.NextDouble() - 0.5;
                double Length = Math.Sqrt(AX * AX + AY * AY + AZ * AZ);
                if (Length == 0.0) { AX = 1.0; Length = 1.0; }
                double Angle = (Generator.NextDouble() - 0.5) * Math.PI;
                double Sine = Math.Sin(Angle);
                Scratch[Index] = TRS((float)(Generator.NextDouble() * 2.0 - 1.0),
                    (float)(Generator.NextDouble() * 2.0 - 1.0), (float)(Generator.NextDouble() * 2.0 - 1.0),
                    (float)(AX / Length * Sine), (float)(AY / Length * Sine), (float)(AZ / Length * Sine),
                    (float)Math.Cos(Angle), (float)(0.7 + Generator.NextDouble() * 0.3),
                    (float)(0.7 + Generator.NextDouble() * 0.3), (float)(0.7 + Generator.NextDouble() * 0.3));
            }
            ReferenceCase(Scratch, Count, "reference case " + Case);
        }
    }

    private static void NoAllocation()
    {
        LocalTRS[] Scratch = new LocalTRS[Model.HardMaximumDepth];
        Scratch[0] = TRS(1f, 2f, 3f);
        for (int Index = 1; Index < Scratch.Length; Index++) Scratch[Index] = TRS(PX: 1f);
        Position Result;
        for (int Warmup = 0; Warmup < 128; Warmup++)
            Model.TryCompose(Scratch, Scratch.Length, Model.HardMaximumDepth, out Result);
        bool Valid = true;
        long Before = GC.GetAllocatedBytesForCurrentThread();
        for (int Iteration = 0; Iteration < 1024; Iteration++)
        {
            Valid &= Model.TryCompose(Scratch, Scratch.Length, Model.HardMaximumDepth, out Result);
            Valid &= Result.X == 65f && Result.Y == 2f && Result.Z == 3f;
            Valid &= !Model.TryCompose(Scratch, Scratch.Length, 0, out Result);
            Valid &= Result.X == 0f && Result.Y == 0f && Result.Z == 0f;
        }
        long After = GC.GetAllocatedBytesForCurrentThread();
        Check(Valid, "allocation probe results");
        Check(After == Before, "successful and rejected composition allocate no managed bytes");
    }

    private static int Main()
    {
        try
        {
            Geometry();
            DepthAndPrefix();
            NonfiniteAndOverflow();
            CopiedValues();
            ReferenceSweep();
            NoAllocation();
            Console.WriteLine("[CarbonLuau:EntityPositionModel] PASS Checks=" + Checks);
            return 0;
        }
        catch (Exception Error)
        {
            Console.Error.WriteLine("[CarbonLuau:EntityPositionModel] FAIL " + Error.Message);
            return 1;
        }
    }
}
