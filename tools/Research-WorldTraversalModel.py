"""Finite async traversal research. Synthetic collections, never a host adapter.

Numeric limits below exercise boundaries; they are not production policy.
Source IL separately establishes that the host collection uses swap removal.
"""

class Realm:
    def __init__(self, Values, Keys=None):
        self.Values = list(Values)
        self.Keys = list(Values) if Keys is None else list(Keys)
        self.Generation = 1
        self.MutationDepth = 0

    def BeginMutation(self):
        self.Generation += 1
        self.MutationDepth += 1

    def EndMutation(self):
        assert self.MutationDepth > 0
        self.MutationDepth -= 1

    def Remove(self, Index):
        self.Generation += 1
        self.Values[Index] = self.Values[-1]
        self.Values.pop()
        self.Keys[Index] = self.Keys[-1]
        self.Keys.pop()

    def Add(self, Value):
        self.Generation += 1
        self.Values.append(Value)
        self.Keys.append(Value)

    def Replace(self, Index, Value):
        self.Generation += 1
        self.Values[Index] = Value

    def Clear(self):
        self.Generation += 1
        self.Values.clear()
        self.Keys.clear()


class Query:
    def __init__(self, Host, Maximum, ResultLimit, Deadline, Authority):
        self.Generation = Host.Generation
        self.End = len(Host.Values)
        self.Cursor = 0
        self.Maximum = Maximum
        self.ResultLimit = ResultLimit
        self.Deadline = Deadline
        self.Authority = Authority
        self.Results = []
        self.State = 'Pending' if self.End <= Maximum else 'TooMuchWork'
        if Host.MutationDepth:
            self.State = 'WorldChanged'
        self.Entered = 0

    def Step(self, Host, Budget, Now, Authority, Match):
        if self.State != 'Pending':
            return 0
        if Authority != self.Authority:
            self.State = 'Discarded'
        elif Now >= self.Deadline:
            self.State = 'DeadlineExceeded'
        elif Host.MutationDepth or Host.Generation != self.Generation:
            self.State = 'WorldChanged'
        if self.State != 'Pending':
            self.Results.clear()
            return 0
        Inspected = 0
        while Inspected < Budget and self.Cursor < self.End:
            Value = Host.Values[self.Cursor]
            Key = Host.Keys[self.Cursor]
            self.Cursor += 1
            Inspected += 1
            # In this synthetic alphabet Value also denotes its canonical key.
            # Production must compare slot key against captured current ID,
            # while deduplicating results by exact lifetime token, not by ID.
            if Key != Value:
                continue
            Matches = Match(Value)
            # A host read may reenter non-Luau host code. Do not trust the next
            # array slot, or publish results, after that code mutates membership.
            if Host.MutationDepth or Host.Generation != self.Generation:
                self.State = 'WorldChanged'
                self.Results.clear()
                return Inspected
            if Matches and Value not in self.Results:
                if len(self.Results) == self.ResultLimit:
                    self.State = 'TooManyResults'
                    self.Results.clear()
                    return Inspected
                self.Results.append(Value)
        if self.Cursor == self.End:
            self.State = 'Queued'
        return Inspected

    def Deliver(self, Host, Authority, Now=0):
        if self.State != 'Queued':
            return None
        if Authority != self.Authority:
            self.State = 'Discarded'
            self.Results.clear()
            return None
        if Host.MutationDepth or Host.Generation != self.Generation:
            self.State = 'WorldChanged'
            self.Results.clear()
            return None
        if Now >= self.Deadline:
            self.State = 'DeadlineExceeded'
            self.Results.clear()
            return None
        self.State = 'Delivered'
        self.Entered += 1
        return tuple(self.Results)


def Main():
    Host = Realm(['A', 'B', 'C'])
    Seen = [Host.Values[0]]
    Host.Remove(0)
    Seen += Host.Values[1:]
    assert Seen == ['A', 'B'] and 'C' not in Seen
    print('[CarbonLuau:TraversalResearch] NAIVE_CURSOR_SKIP_REPRODUCED')

    Host = Realm(['A', 'B', 'C'])
    Count = len(Host.Values)
    Host.Remove(0)
    Host.Add('D')
    assert len(Host.Values) == Count and Host.Values[0] == 'C'
    print('[CarbonLuau:TraversalResearch] COUNT_NEUTRAL_CHURN_REPRODUCED')

    Runs = 0
    for Population in range(1, 9):
        for Budget in range(1, 5):
            for RemoveIndex in range(Population):
                Host = Realm(range(Population))
                Scan = Query(Host, 8, 8, 100, 1)
                Work = Scan.Step(Host, Budget, 0, 1, lambda Value: True)
                assert Work <= Budget
                Host.Remove(RemoveIndex)
                if Scan.State == 'Pending':
                    assert Scan.Step(Host, Budget, 1, 1, lambda Value: True) == 0
                else:
                    assert Scan.Deliver(Host, 1) is None
                assert Scan.State == 'WorldChanged' and not Scan.Results
                Runs += 1

    for Population in range(9):
        for Budget in range(1, 5):
            Host = Realm(range(Population))
            Scan = Query(Host, 8, 8, 100, 1)
            Total = 0
            while Scan.State == 'Pending':
                Total += Scan.Step(Host, Budget, 0, 1, lambda Value: Value % 2 == 0)
            Expected = tuple(Value for Value in Host.Values if Value % 2 == 0)
            assert Scan.Deliver(Host, 1) == Expected
            assert Total == Population and Scan.Entered == 1
            assert Scan.Deliver(Host, 1) is None
            Runs += 1

    Host = Realm([0, 1, 2])
    Positions = [0, 10, 20]
    Scan = Query(Host, 8, 8, 100, 1)
    assert Scan.Step(Host, 1, 0, 1, lambda Value: Positions[Value] <= 5) == 1
    Positions[0] = 100  # already encountered: not evaluated a second time
    Positions[1] = 1    # not encountered yet: current value is used
    Scan.Step(Host, 8, 1, 1, lambda Value: Positions[Value] <= 5)
    assert Scan.Deliver(Host, 1) == (0, 1)

    for RetireAfterQueue in (False, True):
        Scan = Query(Host, 8, 8, 100, 1)
        if RetireAfterQueue:
            Scan.Step(Host, 8, 0, 1, lambda Value: True)
            assert Scan.Deliver(Host, 2) is None
        else:
            assert Scan.Step(Host, 1, 0, 2, lambda Value: True) == 0
        assert Scan.State == 'Discarded' and Scan.Entered == 0
    Scan = Query(Host, 8, 8, 1, 1)
    assert Scan.Step(Host, 1, 1, 1, lambda Value: True) == 0
    assert Scan.State == 'DeadlineExceeded'
    Scan = Query(Host, 8, 1, 100, 1)
    assert Scan.Step(Host, 8, 0, 1, lambda Value: True) == 2
    assert Scan.State == 'TooManyResults' and not Scan.Results
    assert Query(Host, 2, 8, 100, 1).State == 'TooMuchWork'

    for Mutation in (lambda Realm: Realm.Replace(0, 99),
                     lambda Realm: Realm.Clear(),
                     lambda Realm: Realm.Add(99)):
        Host = Realm([0, 1, 2])
        Scan = Query(Host, 8, 8, 100, 1)
        Scan.Step(Host, 1, 0, 1, lambda Value: True)
        Mutation(Host)
        Scan.Step(Host, 1, 1, 1, lambda Value: True)
        assert Scan.State == 'WorldChanged' and not Scan.Results

    Host = Realm([0, 1, 2])
    Scan = Query(Host, 8, 8, 100, 1)
    def MutatingMatch(Value):
        Host.Remove(0)
        return True
    assert Scan.Step(Host, 8, 0, 1, MutatingMatch) == 1
    assert Scan.State == 'WorldChanged' and not Scan.Results
    Host = Realm([0])
    Scan = Query(Host, 8, 8, 1, 1)
    Scan.Step(Host, 8, 0, 1, lambda Value: True)
    assert Scan.Deliver(Host, 1, Now=1) is None
    assert Scan.State == 'DeadlineExceeded' and Scan.Entered == 0
    Host = Realm([0, 0, 0], Keys=[0, 1, 2])
    Scan = Query(Host, 8, 1, 100, 1)
    assert Scan.Step(Host, 8, 0, 1, lambda Value: True) == 3
    assert Scan.Deliver(Host, 1) == (0,)
    Host = Realm([0, 0], Keys=[0, 1])
    Scan = Query(Host, 8, 8, 100, 1)
    assert Scan.Step(Host, 1, 0, 1, lambda Value: False) == 1
    # Alias encountered after movement must not evaluate the entity again.
    assert Scan.Step(Host, 1, 1, 1, lambda Value: True) == 1
    assert Scan.Deliver(Host, 1) == ()

    Host = Realm([0])
    BeforePrefix = Query(Host, 8, 8, 100, 1)
    Host.BeginMutation()
    assert Query(Host, 8, 8, 100, 1).State == 'WorldChanged'
    assert BeforePrefix.Step(Host, 1, 0, 1, lambda Value: True) == 0
    assert BeforePrefix.State == 'WorldChanged'
    Host.BeginMutation()
    Host.EndMutation()
    assert Query(Host, 8, 8, 100, 1).State == 'WorldChanged'
    Host.EndMutation()
    assert Query(Host, 8, 8, 100, 1).State == 'Pending'

    # Stable membership-order alternative: private sequence watermark, not an
    # engine or query snapshot. Deletion cannot move an unvisited sequence back.
    Catalog = {1: 'A', 2: 'B', 3: 'C'}
    Upper, Cursor = 3, 1
    del Catalog[1]
    Catalog[4] = 'D'
    Remaining = [Catalog[Sequence] for Sequence in sorted(Catalog)
                 if Cursor < Sequence <= Upper]
    assert Remaining == ['B', 'C']
    # Sorting is MODEL ONLY. Production needs a bounded ordered successor
    # structure; sorting the world per turn/query is not proposed.

    # Actual finite cursor advancement under a shared budget. Preserve the
    # next-ready cursor across turns; restarting at query zero starves peers.
    for GlobalBudget in (1, 2, 5):
        Host = Realm(range(8))
        Queries = [Query(Host, 8, 8, 100, 1) for _ in range(3)]
        Ready, Opportunities, Turn = list(range(3)), [], 0
        while Ready:
            Used = 0
            while Ready and Used < GlobalBudget:
                Identity = Ready.pop(0)
                Scan = Queries[Identity]
                Opportunities.append(Identity)
                Used += Scan.Step(Host, 1, Turn, 1, lambda Value: True)
                if Scan.State == 'Pending':
                    Ready.append(Identity)
            assert Used <= GlobalBudget
            assert sum(len(Scan.Results) for Scan in Queries) <= 3 * 8
            Turn += 1
        assert Opportunities == [0, 1, 2] * 8
        assert all(Scan.Deliver(Host, 1, Now=Turn) == tuple(range(8))
                   for Scan in Queries)
    print('[CarbonLuau:TraversalResearch] MODEL_PASS '
          f'MutationCases={Runs - 36} StableCases=36 '
          'EncounterTime=pass Retirement=pass Deadline=pass ResultLimit=pass '
          'AtMostOnce=pass WatermarkExample=pass GlobalBudgetFairness=pass '
          'ReplaceClearAdd=pass ReentrantMutation=pass QueuedExpiry=pass '
          'BoundedResultDedup=pass CanonicalAliasSlot=pass MutationWindow=pass')
    print('[CarbonLuau:TraversalResearch] CONDITIONAL_ONLY '
          'HostMutationFence=unqualified PerCandidateHostCost=unqualified '
          'RuntimePolicy=not-selected')


if __name__ == '__main__':
    Main()
