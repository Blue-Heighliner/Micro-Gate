namespace BlueHeighliner.MicroGate;

public sealed class LinuxUartDeviceOpenerTests
{
    public LinuxUartDeviceOpenerTests() => native.Setup(x => x.Open(It.IsAny<string>())).Returns(7);

    private readonly Mock<ILinuxNative> native = new();

    [Theory]
    [InlineData("ttyUSB0", "/dev/ttyUSB0")]
    [InlineData("/dev/ttyUSB1", "/dev/ttyUSB1")]
    public void Open_WhenOpenFails_ThrowsWithNormalizedPath(string portName, string expectedPath)
    {
        native.Setup(x => x.Open(expectedPath)).Returns(-1);
        LinuxUartDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open(portName, new UartPeerOptions()));

        Assert.Contains(expectedPath, exception.Message);
    }

    [Fact]
    public void Open_ConfiguresTheTerminalAndDriverFromOptionsAndReturnsDevice()
    {
        UartPeerOptions options = new() { BaudRate = 19200, DataBits = 7, StopBits = UartStopBits.Two, Parity = UartParity.Odd, Loopback = true };
        LinuxUartDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("ttyUSB0", options);
        device.Dispose();

        native.Verify(x => x.SelectTtyLineDiscipline(7), Times.Once);
        native.Verify(x => x.SelectHdlcLineDiscipline(It.IsAny<int>()), Times.Never);
        native.Verify(x => x.ConfigureAsynchronous(7, 19200, 7, 2, 2), Times.Once);
        native.Verify(
            x => x.SetParams(
                7,
                It.Is<SynclinkParams>(p =>
                    p.Mode == SynclinkConstants.ModeAsync
                    && p.Loopback == 1
                    && p.DataRate == 19200
                    && p.DataBits == 7
                    && p.StopBits == 2
                    && p.Parity == 2)),
            Times.Once);
        native.Verify(x => x.EnableReceiver(7, true), Times.Once);
        native.Verify(x => x.EnableTransmitter(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        native.Verify(x => x.ClearNonBlocking(7), Times.Once);
        native.Verify(x => x.Close(7), Times.Once);
    }

    [Fact]
    public void Open_WithDefaults_UsesOneStopBitAndNoParity()
    {
        LinuxUartDeviceOpener opener = new(native.Object);

        opener.Open("ttyUSB0", new UartPeerOptions()).Dispose();

        native.Verify(x => x.ConfigureAsynchronous(7, 9600, 8, 1, 0), Times.Once);
        native.Verify(x => x.SetParams(7, It.Is<SynclinkParams>(p => p.StopBits == 1 && p.Parity == 0 && p.Loopback == 0)), Times.Once);
    }

    [Fact]
    public void Open_WhenSetInterfaceFails_StillSucceeds()
    {
        native.Setup(x => x.SetInterface(It.IsAny<int>(), It.IsAny<int>())).Returns(-1);
        LinuxUartDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("ttyUSB0", new UartPeerOptions());
        device.Dispose();

        native.Verify(x => x.EnableReceiver(7, true), Times.Once);
    }

    [Fact]
    public void Open_WhenAConfigurationStepFails_ThrowsAndClosesTheDevice()
    {
        native.Setup(x => x.SetParams(7, It.IsAny<SynclinkParams>())).Returns(-1);
        LinuxUartDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open("ttyUSB0", new UartPeerOptions()));

        Assert.Contains("port parameters", exception.Message);
        native.Verify(x => x.Close(7), Times.Once);
        native.Verify(x => x.EnableReceiver(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void Open_WhenTheTerminalCannotBeConfigured_ThrowsAndClosesTheDevice()
    {
        native.Setup(x => x.ConfigureAsynchronous(7, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).Returns(-1);
        LinuxUartDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open("ttyUSB0", new UartPeerOptions()));

        Assert.Contains("configure the terminal", exception.Message);
        native.Verify(x => x.Close(7), Times.Once);
    }
}
