namespace BlueHeighliner.MicroGate;

public sealed class LimitedMemoryOwnerTests
{
    private readonly Mock<IMemoryOwner<byte>> owner = new();

    [Fact]
    public void Memory_ExposesOnlyTheFirstLengthBytes()
    {
        owner.SetupGet(x => x.Memory).Returns(new byte[] { 1, 2, 3, 4, 5 });
        LimitedMemoryOwner limited = new(owner.Object, 3);

        Assert.Equal(new byte[] { 1, 2, 3 }, limited.Memory.ToArray());
    }

    [Fact]
    public void Dispose_DisposesWrappedOwner()
    {
        LimitedMemoryOwner limited = new(owner.Object, 0);

        limited.Dispose();

        owner.Verify(x => x.Dispose(), Times.Once);
    }
}
