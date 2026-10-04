"""Research-only fixed-slot dirty queue. Not a production movement adapter.

All tests operate on synthetic slots/tokens, not Unity or Entity admission.
Storage formulas describe packed hypothetical arrays, not Python heap usage.
No numeric runtime policy is selected, and notification coverage is unproven.
"""

from collections import deque


class DirtySet:
    def __init__(self, Slots, Capacity):
        assert 0 < Capacity <= Slots
        self.Current = [0] * Slots
        self.Dirty = [0] * Slots
        self.Queued = [False] * Slots
        self.Ring = [0] * Capacity
        self.Head = 0
        self.Count = 0
        self.Untrusted = False

    def Acquire(self, Slot, Token):
        assert Token > 0 and self.Current[Slot] == 0
        self.Current[Slot] = Token

    def Retire(self, Slot):
        self.Current[Slot] = 0
        self.Dirty[Slot] = 0
        # Do not remove/requeue a slot: a stale queued entry may safely carry a
        # later valid mark for that slot. Tokens never transfer authority.

    def Mark(self, Slot, Token):
        if self.Untrusted:
            return False
        if Token == 0 or self.Current[Slot] != Token:
            return False
        if not self.Queued[Slot]:
            if self.Count == len(self.Ring):
                self.Untrusted = True
                return False
            Tail = (self.Head + self.Count) % len(self.Ring)
            self.Ring[Tail] = Slot
            self.Count += 1
            self.Queued[Slot] = True
        self.Dirty[Slot] = Token
        return True

    def DrainOne(self):
        if self.Untrusted or self.Count == 0:
            return None
        Slot = self.Ring[self.Head]
        self.Head = (self.Head + 1) % len(self.Ring)
        self.Count -= 1
        self.Queued[Slot] = False
        Token = self.Dirty[Slot]
        self.Dirty[Slot] = 0
        return (Slot, Token) if Token != 0 and Token == self.Current[Slot] else None

    def State(self):
        Pending = tuple(self.Ring[(self.Head + Index) % len(self.Ring)]
                        for Index in range(self.Count))
        return (tuple(self.Current), tuple(self.Dirty), tuple(self.Queued),
                Pending, self.Untrusted)

    def Check(self):
        Pending = self.State()[3]
        assert self.Count <= len(self.Ring)
        assert len(set(Pending)) == self.Count
        assert set(Pending) == {Slot for Slot, Value in enumerate(self.Queued) if Value}
        for Slot, Token in enumerate(self.Dirty):
            assert Token == 0 or (Token == self.Current[Slot] and self.Queued[Slot])

    def Copy(self):
        Result = DirtySet(len(self.Current), len(self.Ring))
        Result.Current = self.Current.copy()
        Result.Dirty = self.Dirty.copy()
        Result.Queued = self.Queued.copy()
        Result.Ring = self.Ring.copy()
        Result.Head = self.Head
        Result.Count = self.Count
        Result.Untrusted = self.Untrusted
        return Result


def Main():
    Checks = 0
    Initial = DirtySet(2, 2)
    # Each slot may acquire two distinct synthetic lifetimes in this finite
    # exhaustive model. Exhausting this test alphabet is not a runtime cap.
    Frontier = deque([(Initial, (0, 0), frozenset())])
    Seen = set()
    while Frontier:
        Model, Next, Expected = Frontier.popleft()
        Key = (Model.State(), Next, Expected)
        if Key in Seen:
            continue
        Seen.add(Key)
        Model.Check()
        Checks += 1
        for Slot in range(2):
            if Model.Current[Slot] == 0 and Next[Slot] < 2:
                Child = Model.Copy()
                Sequence = list(Next)
                Sequence[Slot] += 1
                Child.Acquire(Slot, 1 + Slot * 2 + Sequence[Slot])
                Frontier.append((Child, tuple(Sequence), Expected))
            if Model.Current[Slot] != 0:
                Child = Model.Copy()
                Token = Model.Current[Slot]
                assert Child.Mark(Slot, Token)
                Frontier.append((Child, Next, Expected | {(Slot, Token)}))
                Child = Model.Copy()
                Child.Retire(Slot)
                Frontier.append((Child, Next, Expected - {(Slot, Token)}))
            Child = Model.Copy()
            Before = Child.State()
            assert not Child.Mark(Slot, 99)
            assert Before == Child.State()
            Checks += 1
        if Model.Count:
            Child = Model.Copy()
            Delivered = Child.DrainOne()
            if Delivered is not None:
                assert Delivered in Expected
            Remaining = Expected - ({Delivered} if Delivered is not None else set())
            if Child.Count == 0:
                assert not Remaining
            Frontier.append((Child, Next, frozenset(Remaining)))

    Boundary = DirtySet(3, 2)
    for Slot in range(3):
        Boundary.Acquire(Slot, Slot + 1)
    assert Boundary.Mark(0, 1) and Boundary.Mark(1, 2)
    for Repeat in range(1000):
        assert Boundary.Mark(0, 1) and Boundary.Count == 2
    assert not Boundary.Mark(2, 3) and Boundary.Untrusted
    assert Boundary.DrainOne() is None
    assert not Boundary.Mark(0, 1)
    Boundary.Check()
    Checks += 1006

    Reuse = DirtySet(1, 1)
    Reuse.Acquire(0, 1)
    assert Reuse.Mark(0, 1)
    Reuse.Retire(0)
    Reuse.Acquire(0, 2)
    assert not Reuse.Mark(0, 1)
    assert Reuse.Mark(0, 2) and Reuse.Count == 1
    assert Reuse.DrainOne() == (0, 2)
    assert Reuse.DrainOne() is None
    Reuse.Check()
    Checks += 7

    print('[CarbonLuau:MovementResearch] DIRTY_MODEL_PASS '
          f'States={len(Seen)} Checks={Checks} '
          'PackedBytes=17*N+4*Q+O(1) AlignedConservativeBytes=24*N+4*Q+O(1)')
    print('[CarbonLuau:MovementResearch] CONDITIONAL_ONLY '
          'FixedSlotRouting=required WriterCoverage=unproven '
          'QueryFreshness=unproven RuntimePolicy=not-selected')


if __name__ == '__main__':
    Main()
