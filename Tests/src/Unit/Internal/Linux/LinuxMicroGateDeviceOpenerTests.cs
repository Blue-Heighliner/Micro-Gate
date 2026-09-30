namespace BlueHeighliner.MicroGate;

public sealed class LinuxMicroGateDeviceOpenerTests
{
    public LinuxMicroGateDeviceOpenerTests() => native.Setup(x => x.Open(It.IsAny<string>())).Returns(7);

    private readonly Mock<ILinuxNative> native = new();

    [Theory]
    [InlineData("ttySLG0", "/dev/ttySLG0")]
    [InlineData("/dev/ttyUSB1", "/dev/ttyUSB1")]
    [InlineData("/tmp/fake", "/tmp/fake")]
    public void Open_WhenOpenFails_ThrowsWithNormalizedPath(string portName, string expectedPath)
    {
        native.Setup(x => x.Open(expectedPath)).Returns(-1);
        LinuxMicroGateDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open(portName, new MicroGatePeerOptions()));

        Assert.Contains(expectedPath, exception.Message);
    }

    [Fact]
    public void Open_ConfiguresPortFromOptionsAndReturnsDevice()
    {
        MicroGatePeerOptions options = new()
        {
            Link = new()
            {
                Encoding = MicroGateEncoding.NrziSpace,
                Crc = MicroGateCrc.Crc32Ccitt,
                ReceiveClockSource = MicroGateReceiveClockSource.BaudRateGenerator,
                TransmitClockSource = MicroGateTransmitClockSource.PhaseLockedLoop,
                PhaseLockedLoopDivisor = MicroGatePhaseLockedLoopDivisor.DivideBy16,
                ClockSpeed = 9600,
            },
            IdlePattern = MicroGateIdlePattern.Ones,
            UnderrunAction = MicroGateUnderrunAction.Flag,
            PreambleLength = MicroGatePreambleLength.Bits32,
            PreamblePattern = MicroGatePreamblePattern.Ones,
            Loopback = true,
        };
        LinuxMicroGateDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("ttySLG0", options);
        device.Dispose();

        native.Verify(x => x.Open("/dev/ttySLG0"), Times.Once);
        native.Verify(x => x.SelectHdlcLineDiscipline(7), Times.Once);
        native.Verify(
            x => x.SetParams(
                7,
                It.Is<SynclinkParams>(p =>
                    p.Mode == SynclinkConstants.ModeHdlc
                    && p.Loopback == 1
                    && p.Flags == (0x200 | 0x400 | 0x2000 | 0x0002)
                    && p.Encoding == 3
                    && p.ClockSpeed == 9600
                    && p.CrcType == 2
                    && p.AddressFilter == 0xFF
                    && p.PreambleLength == 2
                    && p.Preamble == 5)),
            Times.Once);
        native.Verify(x => x.SetTransmitIdle(7, 3), Times.Once);
        native.Verify(x => x.EnableReceiver(7, true), Times.Once);
        native.Verify(x => x.EnableTransmitter(7, true), Times.Once);
        native.Verify(x => x.ClearNonBlocking(7), Times.Once);
        native.Verify(x => x.Close(7), Times.Once);
    }

    [Fact]
    public void Open_WithDefaultOptions_AppliesPhysicalDefaultsAndDisablesHardwareAddressFilter()
    {
        LinuxMicroGateDeviceOpener opener = new(native.Object);

        opener.Open("ttySLG0", new MicroGatePeerOptions());

        native.Verify(
            x => x.SetParams(
                7,
                It.Is<SynclinkParams>(p =>
                    p.Loopback == 0
                    && p.Flags == 0
                    && p.Encoding == 0
                    && p.ClockSpeed == 4800
                    && p.CrcType == 2
                    && p.AddressFilter == 0xFF
                    && p.PreambleLength == 0
                    && p.Preamble == 0)),
            Times.Once);
        native.Verify(x => x.SetTransmitIdle(7, 0), Times.Once);
    }

    [Fact]
    public void Open_SelectsTheRs232Interface()
    {
        LinuxMicroGateDeviceOpener opener = new(native.Object);

        opener.Open("ttySLG0", new MicroGatePeerOptions());

        native.Verify(x => x.SetInterface(7, 1), Times.Once);
    }

    [Fact]
    public void Open_WhenTheInterfaceCannotBeSet_StillConfiguresAndReturnsTheDevice()
    {
        native.Setup(x => x.SetInterface(7, It.IsAny<int>())).Returns(-1);
        LinuxMicroGateDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("ttySLG0", new MicroGatePeerOptions());
        device.Dispose();

        native.Verify(x => x.SetParams(7, It.IsAny<SynclinkParams>()), Times.Once);
        native.Verify(x => x.EnableReceiver(7, true), Times.Once);
        native.Verify(x => x.Close(7), Times.Once);
    }

    [Fact]
    public void Open_WhenConfigurationFails_ClosesDescriptorAndRethrows()
    {
        native.Setup(x => x.SetParams(7, It.IsAny<SynclinkParams>())).Throws<InvalidOperationException>();
        LinuxMicroGateDeviceOpener opener = new(native.Object);

        Assert.Throws<InvalidOperationException>(() => opener.Open("ttySLG0", new MicroGatePeerOptions()));

        native.Verify(x => x.Close(7), Times.Once);
    }

    [Theory]
    [InlineData("select the HDLC line discipline")]
    [InlineData("set the port parameters")]
    [InlineData("set the idle pattern")]
    [InlineData("enable the receiver")]
    [InlineData("enable the transmitter")]
    [InlineData("make the device blocking")]
    public void Open_WhenAConfigurationStepFails_ThrowsIoExceptionAndClosesDescriptor(string step)
    {
        switch (step)
        {
            case "select the HDLC line discipline":
                native.Setup(x => x.SelectHdlcLineDiscipline(7)).Returns(-1);
                break;
            case "set the port parameters":
                native.Setup(x => x.SetParams(7, It.IsAny<SynclinkParams>())).Returns(-1);
                break;
            case "set the idle pattern":
                native.Setup(x => x.SetTransmitIdle(7, It.IsAny<int>())).Returns(-1);
                break;
            case "enable the receiver":
                native.Setup(x => x.EnableReceiver(7, true)).Returns(-1);
                break;
            case "enable the transmitter":
                native.Setup(x => x.EnableTransmitter(7, true)).Returns(-1);
                break;
            default:
                native.Setup(x => x.ClearNonBlocking(7)).Returns(-1);
                break;
        }

        LinuxMicroGateDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open("ttySLG0", new MicroGatePeerOptions()));

        Assert.Contains(step, exception.Message);
        Assert.Contains("/dev/ttySLG0", exception.Message);
        native.Verify(x => x.Close(7), Times.Once);
    }
}
