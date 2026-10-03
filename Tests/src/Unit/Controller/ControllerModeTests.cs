namespace BlueHeighliner.MicroGate;

public sealed class ControllerModeTests
{
    [Theory]
    [InlineData("HdlcPeer", "HDLC Peer", false, false, false, true)]
    [InlineData("HdlcMonitor", "HDLC Monitor", false, false, true, false)]
    [InlineData("HdlcPassthrough", "HDLC Passthrough", false, true, false, false)]
    [InlineData("UartPeer", "UART Peer", true, false, false, true)]
    [InlineData("UartMonitor", "UART Monitor", true, false, true, false)]
    [InlineData("UartPassthrough", "UART Passthrough", true, true, false, false)]
    public void Mode_DescribesItself(string name, string title, bool isUart, bool isPassthrough, bool isMonitor, bool isPeer)
    {
        ControllerMode mode = Enum.Parse<ControllerMode>(name);
        Assert.Equal(title, mode.Title);
        Assert.Equal(isUart, mode.IsUart);
        Assert.Equal(isPassthrough, mode.IsPassthrough);
        Assert.Equal(isMonitor, mode.IsMonitor);
        Assert.Equal(isPeer, mode.IsPeer);
    }

    [Fact]
    public void Title_OfAnUndefinedMode_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ControllerMode)99).Title);
}
