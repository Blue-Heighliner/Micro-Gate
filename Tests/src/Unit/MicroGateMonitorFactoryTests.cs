namespace BlueHeighliner.MicroGate;

public sealed class MicroGateMonitorFactoryTests
{
    [Fact]
    public async Task Create_ReturnsANewIdleMonitorEachTime()
    {
        MicroGateMonitorFactory factory = new();

        await using IMicroGateMonitor first = factory.Create();
        await using IMicroGateMonitor second = factory.Create();

        Assert.NotSame(first, second);
        Assert.Equal(MicroGateMonitorState.Idle, first.State);
        Assert.Equal(MicroGateMonitorState.Idle, second.State);
        Assert.IsType<MicroGateMonitor>(first);
    }

    [Fact]
    public void Factory_IsUsableThroughItsInterface()
    {
        IMicroGateMonitorFactory factory = new MicroGateMonitorFactory();

        Assert.NotNull(factory.Create());
    }
}
