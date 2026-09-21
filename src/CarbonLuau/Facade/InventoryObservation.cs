using System;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public sealed class ItemDirectory
        {
            private readonly Func<string, object> Lookup;
            private readonly int Owner = System.Threading.Thread.CurrentThread.ManagedThreadId;
            public ItemDirectory(Func<string, object> Lookup)
            { this.Lookup = Lookup ?? throw new ArgumentNullException("Lookup"); }
            public object Resolve(string ShortName)
            {
                if (System.Threading.Thread.CurrentThread.ManagedThreadId != Owner)
                    throw new FacadeException("facade owner-thread required");
                FacadePolicy.ItemShortName(ShortName);
                return Lookup(ShortName);
            }
        }

        public struct PhysicalInventoryStack
        {
            public readonly object Parent, Definition;
            public readonly long Amount;
            public readonly bool Valid;
            public PhysicalInventoryStack(object Parent, object Definition, long Amount, bool Valid)
            { this.Parent = Parent; this.Definition = Definition; this.Amount = Amount; this.Valid = Valid; }
        }

        public sealed class PhysicalInventoryContainer
        {
            public object Identity;
            public int StackCount;
            public Func<int, PhysicalInventoryStack> Read;
        }

        public sealed class PhysicalInventorySource
        {
            public PhysicalInventoryContainer Main, Belt, Wear;
        }

        // Shared physical semantics for Player-1C and later D13 PREPARE/VERIFY.
        // The source adapter exposes only the three accepted top-level containers.
        public static class PhysicalInventoryObservation
        {
            public static long Count(PhysicalInventorySource Source, object Definition)
            { return Observe(Source, Definition, 0); }
            public static bool Has(PhysicalInventorySource Source, object Definition, long Amount)
            {
                if (Amount < 1 || Amount > FacadePolicy.MaxExactLuauInteger)
                    throw new FacadeException("item amount must be an exact positive integer");
                return Observe(Source, Definition, Amount) >= Amount;
            }
            private static long Observe(PhysicalInventorySource Source, object Definition, long Threshold)
            {
                if (Source == null || Definition == null) throw new FacadeException("inventory observation is unavailable");
                var Containers = new[] {Source.Main, Source.Belt, Source.Wear};
                int Inspected = 0;
                foreach (PhysicalInventoryContainer Container in Containers) {
                    if (Container == null || Container.Identity == null || Container.Read == null || Container.StackCount < 0)
                        throw new FacadeException("Player inventory state is invalid");
                    try { Inspected = checked(Inspected + Container.StackCount); }
                    catch (OverflowException) { throw new FacadeException("Player inventory exceeds inspection bound"); }
                    if (Inspected > FacadePolicy.InventoryStacks)
                        throw new FacadeException("Player inventory exceeds 128-stack inspection bound");
                }
                long Total = 0;
                foreach (PhysicalInventoryContainer Container in Containers) for (int Index = 0; Index < Container.StackCount; ++Index) {
                    PhysicalInventoryStack Stack = Container.Read(Index);
                    if (!Stack.Valid || Stack.Amount <= 0 || !Object.ReferenceEquals(Stack.Parent, Container.Identity) ||
                        !Object.ReferenceEquals(Stack.Definition, Definition)) continue;
                    try { Total = checked(Total + Stack.Amount); }
                    catch (OverflowException) { throw new FacadeException("physical item quantity exceeds exact integer range"); }
                    if (Total > FacadePolicy.MaxExactLuauInteger)
                        throw new FacadeException("physical item quantity exceeds exact integer range");
                    if (Threshold != 0 && Total >= Threshold) return Total;
                }
                return Total;
            }
        }
    }
}
