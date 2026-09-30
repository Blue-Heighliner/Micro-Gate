namespace BlueHeighliner.MicroGate;

public sealed class PooledBufferTests
{
    [Fact]
    public void Memory_IsExactlyTheDataEvenThoughThePoolRentsMore()
    {
        byte[] source = [1, 2, 3];
        using PooledBuffer buffer = new(source);
        source[0] = 9;

        Assert.Equal(new byte[] { 1, 2, 3 }, buffer.Memory.ToArray());
    }

    [Fact]
    public void Memory_OfEmptyData_IsEmpty()
    {
        using PooledBuffer buffer = new(ReadOnlySpan<byte>.Empty);

        Assert.True(buffer.Memory.IsEmpty);
    }

    [Fact]
    public void Memory_AfterDispose_Throws()
    {
        PooledBuffer buffer = new(new byte[] { 1 });
        buffer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => buffer.Memory);
    }

    [Fact]
    public void Dispose_CalledTwice_IsHarmless()
    {
        PooledBuffer buffer = new(new byte[] { 1 });

        buffer.Dispose();
        buffer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => buffer.Memory);
    }
}
