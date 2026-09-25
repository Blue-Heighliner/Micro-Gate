namespace BlueHeighliner.MicroGate;

public sealed class MicroGatePeerFactoryTests
{
    [Fact]
    public async Task Create_ReturnsANewIdlePeerEachTime()
    {
        MicroGatePeerFactory factory = new();

        await using IMicroGatePeer first = factory.Create();
        await using IMicroGatePeer second = factory.Create();

        Assert.NotSame(first, second);
        Assert.Equal(MicroGatePeerState.Idle, first.State);
        Assert.Equal(MicroGatePeerState.Idle, second.State);
        Assert.IsType<MicroGatePeer>(first);
    }

    [Fact]
    public void Factory_IsUsableThroughItsInterface()
    {
        IMicroGatePeerFactory factory = new MicroGatePeerFactory();

        Assert.NotNull(factory.Create());
    }
}
