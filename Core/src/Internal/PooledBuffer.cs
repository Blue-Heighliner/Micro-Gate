namespace BlueHeighliner.MicroGate;

/// <summary>
/// An <see cref="IMemoryOwner{T}"/> holding a copy of some data in an array rented from the shared array pool, whose memory is exactly as long as the data. Disposing it returns the array to the pool once, however many times it is called.
/// </summary>
internal sealed class PooledBuffer : IMemoryOwner<byte>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PooledBuffer"/> class holding a copy of <paramref name="data"/>.
    /// </summary>
    /// <param name="data">The data to copy into the buffer.</param>
    public PooledBuffer(ReadOnlySpan<byte> data)
    {
        array = ArrayPool<byte>.Shared.Rent(data.Length);
        length = data.Length;
        data.CopyTo(array);
    }

    private readonly byte[] array;
    private readonly int length;
    private int disposed;

    /// <inheritdoc />
    public Memory<byte> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            return array.AsMemory(0, length);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }
}
