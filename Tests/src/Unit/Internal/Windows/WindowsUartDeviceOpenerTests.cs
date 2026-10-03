namespace BlueHeighliner.MicroGate;

public sealed class WindowsUartDeviceOpenerTests
{
    public WindowsUartDeviceOpenerTests() => native.Setup(x => x.OpenByName("COM1", out handle)).Returns(0u);

    private readonly Mock<IWindowsNative> native = new();
    private nint handle = 9;

    [Fact]
    public void Open_WhenOpenFails_Throws()
    {
        nint failedHandle = 0;
        native.Setup(x => x.OpenByName("COM2", out failedHandle)).Returns(2u);
        WindowsUartDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open("COM2", new UartPeerOptions()));

        Assert.Contains("COM2", exception.Message);
    }

    [Fact]
    public void Open_ConfiguresThePortFromOptionsAndReturnsDevice()
    {
        UartPeerOptions options = new() { BaudRate = 57600, DataBits = 6, StopBits = UartStopBits.Two, Parity = UartParity.Even, Loopback = true };
        native.Setup(x => x.SetParams(9, It.IsAny<MghdlcParams>())).Returns(0u);
        native.Setup(x => x.EnableReceiver(9, true)).Returns(0u);
        WindowsUartDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("COM1", options);

        Assert.NotNull(device);
        native.Verify(
            x => x.SetParams(
                9,
                It.Is<MghdlcParams>(p =>
                    p.Mode == MghdlcConstants.ModeAsync
                    && p.Loopback == 1
                    && p.DataRate == 57600
                    && p.DataBits == 6
                    && p.StopBits == 2
                    && p.Parity == 1)),
            Times.Once);
        native.Verify(x => x.EnableReceiver(9, true), Times.Once);
        native.Verify(x => x.EnableTransmitter(It.IsAny<nint>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void Open_WhileTheDriverStillHoldsThePort_RetriesUntilItIsReleased()
    {
        int attempts = 0;
        nint released = 9;
        native.Setup(x => x.OpenByName("COM3", out released)).Returns(() => ++attempts < 3 ? 2404u : 0u);
        WindowsUartDeviceOpener opener = new(native.Object, TimeSpan.FromSeconds(5));

        IMicroGateDevice device = opener.Open("COM3", new UartPeerOptions());

        Assert.NotNull(device);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public void Open_WhenSettingParametersFails_ThrowsAndClosesTheDevice()
    {
        native.Setup(x => x.SetParams(9, It.IsAny<MghdlcParams>())).Returns(87u);
        WindowsUartDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open("COM1", new UartPeerOptions()));

        Assert.Contains("port parameters", exception.Message);
        native.Verify(x => x.Close(9), Times.Once);
    }
}
