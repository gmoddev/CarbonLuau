using System;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal struct InventoryQuantityChange
        {
            internal readonly string Item;
            internal readonly long Before, After;
            internal long Delta { get { return After - Before; } }
            internal InventoryQuantityChange(string Item, long Before, long After)
            { this.Item = Item; this.Before = Before; this.After = After; }
        }

        // Only bounded strings/integers survive the synchronous D18 inspection.
        // Sorted insertion supplies an explicit <=128^2 ordinal-comparison bound.
        internal sealed class InventoryQuantitySnapshot
        {
            private readonly string[] Names = new string[FacadePolicy.InventoryStacks];
            private readonly long[] Quantities = new long[FacadePolicy.InventoryStacks];
            internal int Length { get; private set; }
            internal void Add(string Name, object Definition, long Amount, object[] Definitions)
            {
                int Index = 0;
                while (Index < Length && StringComparer.Ordinal.Compare(Names[Index], Name) < 0) Index++;
                if (Index < Length && Names[Index] == Name) {
                    if (!ReferenceEquals(Definitions[Index], Definition)) throw new FacadeException("ambiguous inventory item definition");
                    if (Amount > FacadePolicy.MaxExactLuauInteger - Quantities[Index])
                        throw new FacadeException("physical item quantity exceeds exact integer range");
                    Quantities[Index] += Amount; return;
                }
                if (Length == Names.Length || Amount > FacadePolicy.MaxExactLuauInteger)
                    throw new FacadeException("inventory snapshot exceeds supported bound");
                for (int Next = Length; Next > Index; Next--) {
                    Names[Next] = Names[Next - 1]; Quantities[Next] = Quantities[Next - 1]; Definitions[Next] = Definitions[Next - 1];
                }
                Names[Index] = Name; Quantities[Index] = Amount; Definitions[Index] = Definition; Length++;
            }
            internal long Count(string Name)
            {
                FacadePolicy.ItemShortName(Name);
                int Index = Array.BinarySearch(Names, 0, Length, Name, StringComparer.Ordinal);
                return Index < 0 ? 0 : Quantities[Index];
            }
            internal static InventoryQuantityChange[] Difference(InventoryQuantitySnapshot Before, InventoryQuantitySnapshot After)
            {
                if (Before == null || After == null) throw new ArgumentNullException("snapshot");
                var Changes = new InventoryQuantityChange[FacadePolicy.InventoryStacks * 2];
                int Left = 0, Right = 0, Used = 0;
                while (Left < Before.Length || Right < After.Length) {
                    int Order = Left == Before.Length ? 1 : Right == After.Length ? -1 :
                        StringComparer.Ordinal.Compare(Before.Names[Left], After.Names[Right]);
                    string Name = Order <= 0 ? Before.Names[Left] : After.Names[Right];
                    long Old = Order <= 0 ? Before.Quantities[Left++] : 0;
                    long Next = Order >= 0 ? After.Quantities[Right++] : 0;
                    if (Old != Next) Changes[Used++] = new InventoryQuantityChange(Name, Old, Next);
                }
                Array.Resize(ref Changes, Used); return Changes;
            }
        }
    }
}
