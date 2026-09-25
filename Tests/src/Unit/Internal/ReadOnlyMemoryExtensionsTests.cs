namespace BlueHeighliner.MicroGate;

public sealed class ReadOnlyMemoryExtensionsTests
{
    [Fact]
    public void ToExactArray_WhenMemoryIsWholeArray_ReturnsThatArray()
    {
        byte[] array = [1, 2, 3];

        Assert.Same(array, ((ReadOnlyMemory<byte>)array).ToExactArray());
    }

    [Fact]
    public void ToExactArray_WhenMemoryIsPartOfArray_ReturnsCopyOfThoseBytes()
    {
        byte[] array = [1, 2, 3, 4];

        byte[] result = ((ReadOnlyMemory<byte>)array.AsMemory(1, 2)).ToExactArray();

        Assert.Equal(new byte[] { 2, 3 }, result);
        Assert.NotSame(array, result);
    }

    [Fact]
    public void ToExactArray_WhenMemoryIsEmpty_ReturnsEmptyArray()
    {
        Assert.Empty(ReadOnlyMemory<byte>.Empty.ToExactArray());
    }
}
