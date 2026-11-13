namespace RadarPlural.Domain.Billing;

// Atomic reservation prevents parallel requests overspending. Persist in a transactional ledger before production AI calls.
public sealed class UsageBudget
{
    private readonly int quota;
    private readonly decimal budget;
    public UsageBudget(int quota, decimal budget)
    {
        if (quota < 0 || budget < 0) throw new ArgumentOutOfRangeException(nameof(quota));
        this.quota = quota;
        this.budget = budget;
    }
    private readonly object gate = new();
    private readonly HashSet<Guid> requests = [];
    private int used;
    private decimal spent;

    public bool TryReserve(Guid requestId, decimal estimatedCost)
    {
        lock (gate)
        {
            if (estimatedCost < 0) throw new ArgumentOutOfRangeException(nameof(estimatedCost));
            if (requests.Contains(requestId)) return true;
            if (used >= quota || spent + estimatedCost > budget) return false;
            requests.Add(requestId);
            used++;
            spent += estimatedCost;
            return true;
        }
    }
    public (int Used, decimal Spent) Snapshot()
    {
        lock (gate) return (used, spent);
    }
}
