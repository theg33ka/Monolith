namespace Content.Server._Forge.Horizon.Domain;

/// <summary>
/// Fixed-capacity FIFO used to spread strategic work across ticks.
/// </summary>
public sealed class BoundedWorkQueue<T>
{
    private readonly Queue<T> _queue = new();

    public int Capacity { get; }
    public int Count => _queue.Count;
    public int Rejected { get; private set; }

    public BoundedWorkQueue(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Capacity = capacity;
    }

    public bool TryEnqueue(T item)
    {
        if (_queue.Count >= Capacity)
        {
            Rejected++;
            return false;
        }

        _queue.Enqueue(item);
        return true;
    }

    public int Drain(int limit, Action<T> handler)
    {
        if (limit <= 0)
            return 0;

        var processed = 0;
        while (processed < limit && _queue.TryDequeue(out var item))
        {
            handler(item);
            processed++;
        }

        return processed;
    }

    public void Clear()
    {
        _queue.Clear();
        Rejected = 0;
    }
}
