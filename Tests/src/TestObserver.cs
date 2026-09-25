namespace BlueHeighliner.MicroGate;

internal class TestObserver<T> : IObserver<T>
{
    private readonly BlockingCollection<T> items = [];
    private readonly List<T> seen = [];
    private readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completed => completed.Task;

    public IReadOnlyList<T> Seen
    {
        get
        {
            lock (seen)
            {
                return [.. seen];
            }
        }
    }

    public void OnCompleted() => completed.TrySetResult();

    public void OnError(Exception error) => completed.TrySetException(error);

    public void OnNext(T value)
    {
        Record(value);
        lock (seen)
        {
            seen.Add(value);
        }

        items.Add(value);
    }

    public async Task<T> Next()
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));
        return await Task.Run(() => items.Take(cancellation.Token));
    }

    public async Task<List<T>> Next(int count)
    {
        List<T> result = [];
        while (result.Count < count)
        {
            result.Add(await Next());
        }

        return result;
    }

    protected virtual void Record(T value)
    {
    }
}
