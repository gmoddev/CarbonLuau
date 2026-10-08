"""Finite weak lifetime-catalog research; no Rust adapter or runtime policy.

Small capacities/budgets exercise boundaries, not selected production limits.
Synthetic entities stand in for the qualified full-Spawn observation model.
"""
import gc
import itertools
import weakref


class Entity:
    def __init__(self, Id):
        self.Id = Id
        self.Epoch = 0
        self.Completed = False
        self.Alive = True
        self.Keyed = True
        self.Retired = False
        self.Position = 0
        self.Prefab = 'example'


class Catalog:
    def __init__(self, Capacity, MaximumSequence=2**64-1):
        self.Slots = [None] * Capacity
        self.Free = list(reversed(range(Capacity)))
        self.Links = weakref.WeakKeyDictionary()
        self.HighWater = 0
        self.Sequence = 0
        self.MaximumSequence = MaximumSequence
        self.Continuous = True
        self.Ready = False
        self.SweepCursor = 0

    def RemoveSlot(self, Index):
        Entry = self.Slots[Index]
        if Entry is None:
            return
        Target = Entry[0]()
        if Target is not None and self.Links.get(Target) == Index:
            del self.Links[Target]
        self.Slots[Index] = None
        self.Free.append(Index)

    def Begin(self, Target):
        Index = self.Links.get(Target)
        if Index is not None:
            self.RemoveSlot(Index)
        Target.Epoch += 1
        Target.Completed = False
        Target.Retired = False

    def Complete(self, Target, Success=True):
        Target.Completed = Success
        if not Success or not self.Continuous:
            return
        Existing = self.Links.get(Target)
        if Existing is not None:
            assert self.Slots[Existing][1] == Target.Epoch
            return  # idempotent completion does not allocate twice
        if not self.Free or self.Sequence == self.MaximumSequence:
            self.Continuous = False
            return  # never return success over an incomplete catalog
        Index = self.Free.pop()
        self.Sequence += 1
        self.Slots[Index] = (weakref.ref(Target), Target.Epoch, self.Sequence)
        self.Links[Target] = Index
        self.HighWater = max(self.HighWater, Index + 1)

    def QualifyStartup(self):
        self.Ready = self.Continuous

    def Current(self, Target, Epoch, Id=None, Prefab=None):
        return (Target is not None and Target.Alive and Target.Completed
                and Target.Epoch == Epoch and not Target.Retired
                and (Id is None or Target.Id == Id)
                and (Prefab is None or Target.Prefab == Prefab))

    def Sweep(self, Budget):
        Work = 0
        while Work < Budget and self.HighWater:
            Index = self.SweepCursor % self.HighWater
            self.SweepCursor = (Index + 1) % self.HighWater
            Work += 1
            Entry = self.Slots[Index]
            if Entry is not None and not self.Current(Entry[0](), Entry[1]):
                self.RemoveSlot(Index)
        return Work

    def Check(self):
        Live = [Index for Index, Slot in enumerate(self.Slots) if Slot is not None]
        assert len(Live) + len(self.Free) == len(self.Slots)
        assert len(set(self.Free)) == len(self.Free)
        assert len(self.Links) <= len(Live)
        assert all(self.Slots[Index] is None for Index in self.Free)
        for Target, Index in self.Links.items():
            assert self.Slots[Index][0]() is Target
        return len(Live)


class Query:
    def __init__(self, Directory, ResultLimit, Deadline, Authority):
        self.Directory = weakref.ref(Directory)
        self.Upper = Directory.Sequence
        self.End = Directory.HighWater
        self.Cursor = 0
        self.Results = []
        self.ResultLimit = ResultLimit
        self.Deadline = Deadline
        self.Authority = Authority
        self.State = 'Pending' if Directory.Ready and Directory.Continuous else 'Unavailable'
        self.Entered = 0
        self.Inspected = 0

    def Step(self, Directory, Budget, Now, Authority, Match):
        if self.State != 'Pending':
            return 0
        if self.Directory() is not Directory or Authority != self.Authority:
            self.State = 'Discarded'
        elif not Directory.Continuous:
            self.State = 'Unavailable'
        elif Now >= self.Deadline:
            self.State = 'Expired'
        if self.State != 'Pending':
            self.Results.clear()
            return 0
        Used = 0
        while Used < Budget and self.Cursor < self.End:
            Entry = Directory.Slots[self.Cursor]
            self.Cursor += 1
            Used += 1
            self.Inspected += 1
            if Entry is None or Entry[2] > self.Upper:
                continue
            Target = Entry[0]()
            if not Directory.Current(Target, Entry[1]) or not Target.Keyed:
                continue
            CapturedId, CapturedPrefab = Target.Id, Target.Prefab
            Matches = Match(Target)
            if not Directory.Continuous:
                self.State = 'Unavailable'
            elif (not Directory.Current(Target, Entry[1], CapturedId, CapturedPrefab)
                  or not Target.Keyed):
                self.State = 'CandidateChanged'
            if self.State != 'Pending':
                self.Results.clear()
                return Used
            if Matches:
                if len(self.Results) == self.ResultLimit:
                    self.State = 'TooManyResults'
                    self.Results.clear()
                    return Used
                self.Results.append((weakref.ref(Target), Entry[1], CapturedId, CapturedPrefab))
        if self.Cursor == self.End:
            self.State = 'Queued'
        return Used

    def Deliver(self, Directory, Now, Authority):
        if self.State != 'Queued':
            return None
        if self.Directory() is not Directory or Authority != self.Authority:
            self.State = 'Discarded'
        elif not Directory.Continuous:
            self.State = 'Unavailable'
        elif Now >= self.Deadline:
            self.State = 'Expired'
        elif any(not Directory.Current(Reference(), Epoch, Id, Prefab) or not Reference().Keyed
                 for Reference, Epoch, Id, Prefab in self.Results):
            self.State = 'ResultRetired'
        else:
            self.State = 'Delivered'
            self.Entered += 1
            # Test output only: real completion must publish weak-bound facades.
            return tuple(Id for Reference, Epoch, Id, Prefab in self.Results)
        self.Results.clear()
        return None


def Spawn(Directory, Id):
    Target = Entity(Id)
    Directory.Begin(Target)
    Directory.Complete(Target)
    return Target


def Finish(Scan, Directory, Match=lambda Target: True, Budget=1):
    while Scan.State == 'Pending':
        assert Scan.Step(Directory, Budget, 0, 1, Match) <= Budget
    return Scan.Deliver(Directory, 0, 1)


def Main():
    Cases = 0
    # Every removal mask, cursor position and reuse of freed slots. Surviving
    # initial lifetimes stay fixed; new births never enter the old query.
    for Capacity in range(1, 7):
        for Mask in itertools.product((False, True), repeat=Capacity):
            for Cursor in range(Capacity + 1):
                Directory = Catalog(Capacity)
                Targets = [Spawn(Directory, Index) for Index in range(Capacity)]
                Directory.QualifyStartup()
                Scan = Query(Directory, Capacity, 100, 1)
                Scan.Step(Directory, Cursor, 0, 1, lambda Target: True)
                New = []
                for Index, Remove in enumerate(Mask):
                    if Remove:
                        Directory.Begin(Targets[Index])  # immediate old-epoch fencing
                        New.append(Spawn(Directory, Capacity + Index))
                ExpectedVisits = [Index for Index in range(Cursor, Capacity) if not Mask[Index]]
                Seen = []
                while Scan.State == 'Pending':
                    assert Scan.Step(Directory, 1, 0, 1,
                                     lambda Target: Seen.append(Target.Id) or True) <= 1
                assert Seen == ExpectedVisits
                Result = Scan.Deliver(Directory, 0, 1)
                if any(Mask[:Cursor]):
                    assert Result is None and Scan.State == 'ResultRetired'
                else:
                    Expected = tuple(Index for Index in range(Capacity) if not Mask[Index])
                    assert Result == Expected
                assert Scan.Inspected == Capacity and Directory.Check() <= Capacity
                Cases += 1

    Directory = Catalog(3)
    A = Spawn(Directory, 7)
    Directory.QualifyStartup()
    Directory.Complete(A)
    assert Directory.Check() == 1
    Scan = Query(Directory, 3, 100, 1)
    Directory.Begin(A)
    Directory.Complete(A, Success=False)
    assert Finish(Scan, Directory) == () and Directory.Check() == 0
    Directory.Begin(A)
    Directory.Complete(A)
    assert Finish(Query(Directory, 3, 100, 1), Directory) == (7,)
    Old = Query(Directory, 3, 100, 1)
    Old.Step(Directory, 3, 0, 1, lambda Target: True)
    Directory.Begin(A)
    Directory.Complete(A)
    assert Old.Deliver(Directory, 0, 1) is None
    assert Old.State == 'ResultRetired'  # same object and ID, new epoch
    for Property, Value in (('Id', 9), ('Prefab', 'changed')):
        Previous = getattr(A, Property)
        Old = Query(Directory, 3, 100, 1)
        Old.Step(Directory, 3, 0, 1, lambda Target: True)
        setattr(A, Property, Value)
        assert Old.Deliver(Directory, 0, 1) is None and Old.State == 'ResultRetired'
        setattr(A, Property, Previous)

    B = Spawn(Directory, 8)
    Scan = Query(Directory, 3, 100, 1)
    Scan.Step(Directory, 1, 0, 1, lambda Target: Target.Position <= 5)
    A.Position = 100
    B.Position = 1
    assert Finish(Scan, Directory, lambda Target: Target.Position <= 5) == (7, 8)
    A.Position = 0
    # Transient registry loss does not invent a new lifetime/birth. Omission at
    # encounter is permitted; stale admitted results must still fail delivery.
    B.Keyed = False
    assert Finish(Query(Directory, 3, 100, 1), Directory) == (7,)
    B.Keyed = True
    assert Finish(Query(Directory, 3, 100, 1), Directory) == (7, 8)

    for Matches in (False, True):
        Scan = Query(Directory, 3, 100, 1)
        def LoseOccupancyDuringRead(Target):
            Target.Keyed = False
            return Matches
        Scan.Step(Directory, 1, 0, 1, LoseOccupancyDuringRead)
        assert Scan.State == 'CandidateChanged' and not Scan.Results
        A.Keyed = True
        assert Scan.Deliver(Directory, 0, 1) is None

    Scan = Query(Directory, 3, 100, 1)
    def RespawnDuringRead(Target):
        Directory.Begin(Target)
        Directory.Complete(Target)
        return True
    Scan.Step(Directory, 3, 0, 1, RespawnDuringRead)
    assert Scan.State == 'CandidateChanged' and not Scan.Results
    assert Finish(Query(Directory, 1, 100, 1), Directory) is None
    Scan = Query(Directory, 3, 1, 1)
    Scan.Step(Directory, 3, 0, 1, lambda Target: True)
    assert Scan.Deliver(Directory, 1, 1) is None and Scan.State == 'Expired'
    Scan = Query(Directory, 3, 100, 1)
    assert Scan.Step(Directory, 1, 0, 2, lambda Target: True) == 0
    assert Scan.State == 'Discarded'
    Scan = Query(Directory, 3, 100, 1)
    Scan.Step(Directory, 3, 0, 1, lambda Target: True)
    assert Scan.Deliver(Directory, 0, 2) is None and Scan.Entered == 0
    Scan = Query(Directory, 3, 100, 1)
    assert Finish(Scan, Directory) == (7, 8)
    assert Scan.Deliver(Directory, 0, 1) is None and Scan.Entered == 1
    FreshDomain = Query(Directory, 3, 100, 2)
    FreshDomain.Step(Directory, 3, 0, 2, lambda Target: True)
    assert FreshDomain.Deliver(Directory, 0, 2) == (7, 8)
    assert Directory.Check() == 2  # domain replacement never deletes host membership
    Old = Query(Directory, 3, 100, 1)
    Old.Step(Directory, 3, 0, 1, lambda Target: True)
    OtherHost = Catalog(3)
    OtherHost.QualifyStartup()
    assert Old.Deliver(OtherHost, 0, 1) is None and Old.State == 'Discarded'

    Directory = Catalog(1)
    A = Spawn(Directory, 7)
    Directory.QualifyStartup()
    Old = Query(Directory, 1, 100, 1)
    Old.Step(Directory, 1, 0, 1, lambda Target: True)
    A.Alive = False
    A.Keyed = False
    Directory.Sweep(1)
    B = Spawn(Directory, 7)  # same slot, same ID/prefab/epoch, different object
    assert Old.Deliver(Directory, 0, 1) is None and Old.State == 'ResultRetired'
    assert Finish(Query(Directory, 1, 100, 1), Directory) == (7,)

    Directory = Catalog(1)
    A = Spawn(Directory, 1)
    assert Query(Directory, 1, 100, 1).State == 'Unavailable'  # bootstrap closed
    Directory.QualifyStartup()
    Pending = Query(Directory, 1, 100, 1)
    B = Spawn(Directory, 2)
    assert not Directory.Continuous and Directory.Check() == 1
    assert Pending.Step(Directory, 1, 0, 1, lambda Target: True) == 0
    assert Pending.State == 'Unavailable'
    Directory = Catalog(1, MaximumSequence=1)
    A = Spawn(Directory, 1)
    Directory.Begin(A)
    Directory.Complete(A)
    assert not Directory.Continuous and Directory.Check() == 0

    Directory = Catalog(4)
    A = Spawn(Directory, 1)
    Reference = weakref.ref(A)
    del A
    gc.collect()
    assert Reference() is None  # catalog must not own the host object
    for _ in range(4):
        assert Directory.Sweep(1) <= 1
    assert Directory.Check() == 0

    Directory = Catalog(4)
    Targets = [Spawn(Directory, Index) for Index in range(4)]
    Directory.QualifyStartup()
    for GlobalBudget in (1, 2, 5):
        Queries = [Query(Directory, 4, 100, 1) for _ in range(3)]
        Ready, Visits, Turn = list(range(3)), [], 0
        while Ready:
            Used = 0
            while Ready and Used < GlobalBudget:
                Identity = Ready.pop(0)
                Visits.append(Identity)
                Used += Queries[Identity].Step(Directory, 1, Turn, 1, lambda Target: True)
                if Queries[Identity].State == 'Pending':
                    Ready.append(Identity)
            assert Used <= GlobalBudget
            Turn += 1
        assert Visits == [0, 1, 2] * 4
        assert all(Scan.Deliver(Directory, Turn, 1) == tuple(range(4)) for Scan in Queries)

    print('[CarbonLuau:CatalogResearch] MODEL_PASS '
          f'MutationCursorCases={Cases} FixedSlotWatermark=pass FailedSpawn=pass '
          'EpochABA=pass EncounterTime=pass CandidateRace=pass Overflow=fail-closed '
          'WeakRetention=pass BoundedSweep=pass Deadline=pass Retirement=pass '
          'AtMostOnce=pass Fairness=pass IdPrefabCapture=pass HostDomainLifetime=pass '
          'DifferentObjectIdSlotReuse=pass PostReadOccupancy=pass')
    print('[CarbonLuau:CatalogResearch] CONDITIONAL_ONLY '
          'ProductionObserverIntegration=unqualified HostReadBound=unqualified '
          'NumericPolicy=not-selected')


if __name__ == '__main__':
    Main()
