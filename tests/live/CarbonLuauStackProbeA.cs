// TEST ONLY. No network, config, data-file, registry enumeration or automatic stack writes.
using System;
using System.Reflection;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("CarbonLuauStackProbeA", "gmoddev", "1.0.0")]
    [Description("Bounded neutral-by-default D8 stack hook fixture")]
    public sealed class CarbonLuauStackProbeA : CarbonPlugin
    {
        private string Selected;
        private int MaxMode, MaxValue, CanMode, SplitMode, DropMode;
        private int MaxCalls, CanCalls, SplitCalls, DropCalls;
        private bool InsideDrop;
        private Item Child, Changed;
        private BaseEntity ChildWorld;
        private object OriginalUid;

        private bool Matches(Item Value) { return Selected != null && Value != null && Value.info != null && Value.info.shortname == Selected; }
        private static void Check(bool Condition, string Message) { if (!Condition) throw new InvalidOperationException("D8 stack probe: " + Message); }
        private static int Count(int Value) { Check(Value < 10000, "hook count bound"); return Value + 1; }
        private object D8StackConfigure(string Name, int MaximumMode, int MaximumValue, int StackMode, int ItemSplitMode, int DroppedMode)
        {
            Check(Child == null && ChildWorld == null && Changed == null, "previous owned callback result not claimed");
            Check(Name == null || Name == "sulfur" || Name == "gears" || Name == "antiradpills" ||
                Name == "lowgradefuel" || Name == "hat.bunnyhat" || Name == "wood", "finite representative identity");
            Check(MaximumMode >= 0 && MaximumMode <= 2 && StackMode >= 0 && StackMode <= 2 &&
                ItemSplitMode >= 0 && ItemSplitMode <= 1 && DroppedMode >= 0 && DroppedMode <= 3, "bounded private mode");
            Selected = Name; MaxMode = MaximumMode; MaxValue = MaximumValue; CanMode = StackMode;
            SplitMode = ItemSplitMode; DropMode = DroppedMode;
            MaxCalls = CanCalls = SplitCalls = DropCalls = 0;
            return "OK";
        }
        private object D8StackState()
        { return new object[] { MaxCalls, CanCalls, SplitCalls, DropCalls }; }
        private object D8StackTake()
        {
            var Result = new object[] { Child, ChildWorld, Changed, OriginalUid };
            Child = null; ChildWorld = null; Changed = null; OriginalUid = null;
            return Result;
        }
        private object OnMaxStackable(Item Value)
        {
            if (!Matches(Value)) return null;
            MaxCalls = Count(MaxCalls);
            return MaxMode == 0 ? null : MaxMode == 1 ? (object)MaxValue : "D8-invalid-stack-type";
        }
        private object CanStackItem(Item Value, Item Target)
        {
            if (!Matches(Value)) return null;
            CanCalls = Count(CanCalls);
            return CanMode == 0 ? null : (object)(CanMode == 2);
        }
        private object OnItemSplit(Item Value, int Amount)
        {
            if (!Matches(Value)) return null;
            SplitCalls = Count(SplitCalls);
            if (SplitMode == 0) return null;
            Check(Child == null && Amount > 0 && Amount < Value.amount && Amount <= 1000, "one bounded split override");
            Child = ItemManager.Create(Value.info, Amount, 0);
            Check(Child != null && Child.uid.Value != 0 && Child.uid.Value != Value.uid.Value &&
                ReferenceEquals(Child.info, Value.info) && Child.amount == Amount && Child.parent == null && Child.GetWorldEntity() == null,
                "exact returned split override Item");
            Value.amount -= Amount; Value.MarkDirty();
            return Child;
        }
        private void OnItemDropped(Item Value, BaseEntity Entity)
        {
            if (!Matches(Value)) return;
            DropCalls = Count(DropCalls);
            if (InsideDrop || DropMode == 0) return;
            Check(Entity != null && ReferenceEquals(Value.GetWorldEntity(), Entity), "actual attached drop callback");
            InsideDrop = true;
            try {
                if (DropMode == 1) { Value.amount++; Value.MarkDirty(); }
                else if (DropMode == 2) {
                    Check(Changed == null, "one terminal UID mutation");
                    FieldInfo Uid = typeof(Item).GetField("uid", BindingFlags.Instance | BindingFlags.Public);
                    Check(Uid != null, "exact public UID field");
                    Changed = Value; OriginalUid = Uid.GetValue(Value);
                    Uid.SetValue(Value, Activator.CreateInstance(Uid.FieldType));
                    Check(Value.uid.Value == 0, "actual terminal UID mutation observed");
                } else {
                    Check(Child == null && Value.amount >= 2, "one bounded drop split");
                    Child = Value.SplitItem(1);
                    Check(Child != null && Child.uid.Value != 0 && Child.uid.Value != Value.uid.Value && Child.amount == 1,
                        "actual returned split Item");
                    ChildWorld = Child.Drop(Entity.transform.position + new Vector3(0.01f, 0, 0), Vector3.zero);
                    if (ChildWorld != null) ChildWorld.enableSaving = false;
                }
            } finally { InsideDrop = false; }
        }
    }
}

