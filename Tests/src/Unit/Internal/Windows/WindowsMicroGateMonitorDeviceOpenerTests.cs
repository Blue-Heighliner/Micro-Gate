namespace BlueHeighliner.MicroGate;

public sealed class WindowsMicroGateMonitorDeviceOpenerTests
{
    public WindowsMicroGateMonitorDeviceOpenerTests() => native.Setup(x => x.OpenByName("COM1", out handle)).Returns(0u);

    private readonly Mock<IWindowsNative> native = new();
    private nint handle = 9;

    [Fact]
    public void Open_WhenOpenFails_Throws()
    {
        nint failedHandle = 0;
        native.Setup(x => x.OpenByName("COM2", out failedHandle)).Returns(2u);
        WindowsMicroGateMonitorDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open("COM2", new MicroGateMonitorOptions()));

        Assert.Contains("COM2", exception.Message);
    }

    [Fact]
    public void Open_ConfiguresReceiveClockingFromOptions()
    {
        MicroGateMonitorOptions options = new()
        {
            ReceiveClockSource = MicroGateReceiveClockSource.BaudRateGenerator,
            PhaseLockedLoopDivisor = MicroGatePhaseLockedLoopDivisor.DivideBy8,
            ClockSpeed = 9600,
        };
        WindowsMicroGateMonitorDeviceOpener opener = new(native.Object);

        opener.Open("COM1", options);

        native.Verify(x => x.SetParams(9, It.Is<MghdlcParams>(p => p.Flags == (MghdlcConstants.ReceiveClockBrg | MghdlcConstants.DpllDivisor8) && p.ClockSpeed == 9600)), Times.Once);
    }

    [Fact]
    public void Open_ConfiguresPortFromOptionsAndReturnsDevice()
    {
        MicroGateMonitorOptions options = new()
        {
            Encoding = MicroGateEncoding.BiphaseMark,
            Crc = MicroGateCrc.None,
            HardwareAddressFilter = 0x05,
        };
        WindowsMicroGateMonitorDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("COM1", options);
        device.Dispose();

        native.Verify(x => x.SetParams(9, It.Is<MghdlcParams>(p => p.Mode == MghdlcConstants.ModeHdlc && p.Encoding == 4 && p.CrcType == 0 && p.Addr == 0x05)), Times.Once);
        native.Verify(x => x.EnableReceiver(9, true), Times.Once);
        native.Verify(x => x.Close(9), Times.Once);
    }

    [Fact]
    public void Open_NeverEnablesTheTransmitterOrSetsAnIdleMode()
    {
        WindowsMicroGateMonitorDeviceOpener opener = new(native.Object);

        opener.Open("COM1", new MicroGateMonitorOptions());

        native.Verify(x => x.EnableTransmitter(It.IsAny<nint>(), It.IsAny<bool>()), Times.Never);
        native.Verify(x => x.SetIdleMode(It.IsAny<nint>(), It.IsAny<uint>()), Times.Never);
    }

    [Fact]
    public void Open_WithDefaultOptions_DisablesHardwareAddressFilter()
    {
        WindowsMicroGateMonitorDeviceOpener opener = new(native.Object);

        opener.Open("COM1", new MicroGateMonitorOptions());

        native.Verify(x => x.SetParams(9, It.Is<MghdlcParams>(p => p.Encoding == 0 && p.CrcType == 2 && p.Addr == 0xFF)), Times.Once);
    }

    [Fact]
    public void Open_SelectsTheRs232InterfaceAndDiscardsReceiveErrors()
    {
        WindowsMicroGateMonitorDeviceOpener opener = new(native.Object);

        opener.Open("COM1", new MicroGateMonitorOptions());

        native.Verify(x => x.SetOption(9, 6u, 1u), Times.Once);
        native.Verify(x => x.SetOption(9, 8u, 1u), Times.Once);
    }

    [Fact]
    public void Open_WhenTheOptionsCannotBeSet_StillConfiguresAndReturnsTheDevice()
    {
        native.Setup(x => x.SetOption(9, It.IsAny<uint>(), It.IsAny<uint>())).Returns(5u);
        WindowsMicroGateMonitorDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("COM1", new MicroGateMonitorOptions());
        device.Dispose();

        native.Verify(x => x.SetParams(9, It.IsAny<MghdlcParams>()), Times.Once);
        native.Verify(x => x.EnableReceiver(9, true), Times.Once);
        native.Verify(x => x.Close(9), Times.Once);
    }

    [Fact]
    public void Open_WhenConfigurationFails_ClosesHandleAndRethrows()
    {
        native.Setup(x => x.SetParams(9, It.IsAny<MghdlcParams>())).Throws<InvalidOperationException>();
        WindowsMicroGateMonitorDeviceOpener opener = new(native.Object);

        Assert.Throws<InvalidOperationException>(() => opener.Open("COM1", new MicroGateMonitorOptions()));

        native.Verify(x => x.Close(9), Times.Once);
    }

    [Theory]
    [InlineData("set the port parameters")]
    [InlineData("enable the receiver")]
    public void Open_WhenAConfigurationStepFails_ThrowsIoExceptionAndClosesHandle(string step)
    {
        switch (step)
        {
            case "set the port parameters":
                native.Setup(x => x.SetParams(9, It.IsAny<MghdlcParams>())).Returns(5u);
                break;
            default:
                native.Setup(x => x.EnableReceiver(9, true)).Returns(5u);
                break;
        }

        WindowsMicroGateMonitorDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open("COM1", new MicroGateMonitorOptions()));

        Assert.Contains(step, exception.Message);
        native.Verify(x => x.Close(9), Times.Once);
    }
}
