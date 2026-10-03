namespace BlueHeighliner.MicroGate;

public sealed class WindowsMicroGateDeviceOpenerTests
{
    public WindowsMicroGateDeviceOpenerTests() => native.Setup(x => x.OpenByName("COM1", out handle)).Returns(0u);

    private readonly Mock<IWindowsNative> native = new();
    private nint handle = 9;

    [Fact]
    public void Open_WhenOpenFails_Throws()
    {
        nint failedHandle = 0;
        native.Setup(x => x.OpenByName("COM2", out failedHandle)).Returns(2u);
        WindowsMicroGateDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open("COM2", new MicroGatePeerOptions()));

        Assert.Contains("COM2", exception.Message);
    }

    [Fact]
    public void Open_WhileTheDriverStillHoldsThePort_RetriesUntilItIsReleased()
    {
        int attempts = 0;
        nint released = 9;
        native.Setup(x => x.OpenByName("COM3", out released)).Returns(() => ++attempts < 3 ? 2404u : 0u);
        WindowsMicroGateDeviceOpener opener = new(native.Object, TimeSpan.FromSeconds(5));

        IMicroGateDevice device = opener.Open("COM3", new MicroGatePeerOptions());

        Assert.NotNull(device);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public void Open_WhenThePortStaysInUse_GivesUpAfterTheRetryWindow()
    {
        nint busy = 0;
        native.Setup(x => x.OpenByName("COM4", out busy)).Returns(2404u);
        WindowsMicroGateDeviceOpener opener = new(native.Object, TimeSpan.FromMilliseconds(200));

        IOException exception = Assert.Throws<IOException>(() => opener.Open("COM4", new MicroGatePeerOptions()));

        Assert.Contains("COM4", exception.Message);
        native.Verify(x => x.OpenByName("COM4", out busy), Times.AtLeast(2));
    }

    [Fact]
    public void Open_ConfiguresPortFromOptionsAndReturnsDevice()
    {
        MicroGatePeerOptions options = new()
        {
            Link = new()
            {
                Encoding = MicroGateEncoding.BiphaseMark,
                Crc = MicroGateCrc.None,
                ReceiveClockSource = MicroGateReceiveClockSource.BaudRateGenerator,
                TransmitClockSource = MicroGateTransmitClockSource.PhaseLockedLoop,
                PhaseLockedLoopDivisor = MicroGatePhaseLockedLoopDivisor.DivideBy16,
                ClockSpeed = 9600,
            },
            IdlePattern = MicroGateIdlePattern.Mark,
            UnderrunAction = MicroGateUnderrunAction.Flag,
            PreambleLength = MicroGatePreambleLength.Bits32,
            PreamblePattern = MicroGatePreamblePattern.Ones,
            Loopback = true,
        };
        WindowsMicroGateDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("COM1", options);
        device.Dispose();

        native.Verify(
            x => x.SetParams(
                9,
                It.Is<MghdlcParams>(p =>
                    p.Mode == MghdlcConstants.ModeHdlc
                    && p.Loopback == 1
                    && p.Flags == (0x200 | 0x400 | 0x2000 | 0x0002)
                    && p.Encoding == 4
                    && p.ClockSpeed == 9600
                    && p.CrcType == 0
                    && p.Addr == 0xFF
                    && p.PreambleLength == 2
                    && p.PreamblePattern == 5)),
            Times.Once);
        native.Verify(x => x.SetIdleMode(9, 6u), Times.Once);
        native.Verify(x => x.EnableReceiver(9, true), Times.Once);
        native.Verify(x => x.EnableTransmitter(It.IsAny<nint>(), It.IsAny<bool>()), Times.Never);
        native.Verify(x => x.Close(9), Times.Once);
    }

    [Fact]
    public void Open_WithDefaultOptions_AppliesPhysicalDefaultsAndDisablesHardwareAddressFilter()
    {
        WindowsMicroGateDeviceOpener opener = new(native.Object);

        opener.Open("COM1", new MicroGatePeerOptions());

        native.Verify(
            x => x.SetParams(
                9,
                It.Is<MghdlcParams>(p =>
                    p.Loopback == 0
                    && p.Flags == 0
                    && p.Encoding == 0
                    && p.ClockSpeed == 4800
                    && p.CrcType == 2
                    && p.Addr == 0xFF
                    && p.PreambleLength == 0
                    && p.PreamblePattern == 0)),
            Times.Once);
        native.Verify(x => x.SetIdleMode(9, 0u), Times.Once);
    }

    [Fact]
    public void Open_SelectsTheRs232InterfaceAndDiscardsReceiveErrors()
    {
        WindowsMicroGateDeviceOpener opener = new(native.Object);

        opener.Open("COM1", new MicroGatePeerOptions());

        native.Verify(x => x.SetOption(9, 6u, 1u), Times.Once);
        native.Verify(x => x.SetOption(9, 8u, 1u), Times.Once);
    }

    [Fact]
    public void Open_WhenTheOptionsCannotBeSet_StillConfiguresAndReturnsTheDevice()
    {
        native.Setup(x => x.SetOption(9, It.IsAny<uint>(), It.IsAny<uint>())).Returns(5u);
        WindowsMicroGateDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("COM1", new MicroGatePeerOptions());
        device.Dispose();

        native.Verify(x => x.SetParams(9, It.IsAny<MghdlcParams>()), Times.Once);
        native.Verify(x => x.EnableReceiver(9, true), Times.Once);
        native.Verify(x => x.Close(9), Times.Once);
    }

    [Fact]
    public void Open_WhenConfigurationFails_ClosesHandleAndRethrows()
    {
        native.Setup(x => x.SetParams(9, It.IsAny<MghdlcParams>())).Throws<InvalidOperationException>();
        WindowsMicroGateDeviceOpener opener = new(native.Object);

        Assert.Throws<InvalidOperationException>(() => opener.Open("COM1", new MicroGatePeerOptions()));

        native.Verify(x => x.Close(9), Times.Once);
    }

    [Theory]
    [InlineData("set the port parameters")]
    [InlineData("set the idle pattern")]
    [InlineData("enable the receiver")]
    public void Open_WhenAConfigurationStepFails_ThrowsIoExceptionAndClosesHandle(string step)
    {
        switch (step)
        {
            case "set the port parameters":
                native.Setup(x => x.SetParams(9, It.IsAny<MghdlcParams>())).Returns(5u);
                break;
            case "set the idle pattern":
                native.Setup(x => x.SetIdleMode(9, It.IsAny<uint>())).Returns(5u);
                break;
            default:
                native.Setup(x => x.EnableReceiver(9, true)).Returns(5u);
                break;
        }

        WindowsMicroGateDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open("COM1", new MicroGatePeerOptions()));

        Assert.Contains(step, exception.Message);
        native.Verify(x => x.Close(9), Times.Once);
    }
}
