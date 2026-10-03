namespace BlueHeighliner.MicroGate;

public sealed class HdlcPeerFactoryTests
{
    [Fact]
    public async Task Create_ReturnsANewIdlePeerEachTime()
    {
        HdlcPeerFactory factory = new();

        await using IHdlcPeer first = factory.Create();
        await using IHdlcPeer second = factory.Create();

        Assert.NotSame(first, second);
        Assert.Equal(HdlcPeerState.Idle, first.State);
        Assert.Equal(HdlcPeerState.Idle, second.State);
        Assert.IsType<HdlcPeer>(first);
    }

    [Fact]
    public void Factory_IsUsableThroughItsInterface()
    {
        IHdlcPeerFactory factory = new HdlcPeerFactory();

        Assert.NotNull(factory.Create());
    }
}
