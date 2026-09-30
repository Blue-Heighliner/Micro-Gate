namespace BlueHeighliner.MicroGate;

public sealed class LinuxMicroGateMonitorDeviceOpenerTests
{
    public LinuxMicroGateMonitorDeviceOpenerTests() => native.Setup(x => x.Open(It.IsAny<string>())).Returns(7);

    private readonly Mock<ILinuxNative> native = new();

    [Theory]
    [InlineData("ttySLG0", "/dev/ttySLG0")]
    [InlineData("/dev/ttyUSB1", "/dev/ttyUSB1")]
    [InlineData("/tmp/fake", "/tmp/fake")]
    public void Open_WhenOpenFails_ThrowsWithNormalizedPath(string portName, string expectedPath)
    {
        native.Setup(x => x.Open(expectedPath)).Returns(-1);
        LinuxMicroGateMonitorDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open(portName, new MicroGateMonitorOptions()));

        Assert.Contains(expectedPath, exception.Message);
    }

    [Fact]
    public void Open_ConfiguresPortFromOptionsAndReturnsDevice()
    {
        MicroGateMonitorOptions options = new()
        {
            Encoding = MicroGateEncoding.NrziSpace,
            Crc = MicroGateCrc.Crc32Ccitt,
            HardwareAddressFilter = 0x21,
        };
        LinuxMicroGateMonitorDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("ttySLG0", options);
        device.Dispose();

        native.Verify(x => x.Open("/dev/ttySLG0"), Times.Once);
        native.Verify(x => x.SelectHdlcLineDiscipline(7), Times.Once);
        native.Verify(x => x.SetParams(7, It.Is<SynclinkParams>(p => p.Mode == SynclinkConstants.ModeHdlc && p.Encoding == 3 && p.CrcType == 2 && p.AddressFilter == 0x21)), Times.Once);
        native.Verify(x => x.EnableReceiver(7, true), Times.Once);
        native.Verify(x => x.ClearNonBlocking(7), Times.Once);
        native.Verify(x => x.Close(7), Times.Once);
    }

    [Fact]
    public void Open_NeverEnablesTheTransmitterOrSetsATransmitIdlePattern()
    {
        LinuxMicroGateMonitorDeviceOpener opener = new(native.Object);

        opener.Open("ttySLG0", new MicroGateMonitorOptions());

        native.Verify(x => x.EnableTransmitter(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        native.Verify(x => x.SetTransmitIdle(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Open_WithDefaultOptions_DisablesHardwareAddressFilter()
    {
        LinuxMicroGateMonitorDeviceOpener opener = new(native.Object);

        opener.Open("ttySLG0", new MicroGateMonitorOptions());

        native.Verify(x => x.SetParams(7, It.Is<SynclinkParams>(p => p.Encoding == 0 && p.CrcType == 2 && p.AddressFilter == 0xFF)), Times.Once);
    }

    [Fact]
    public void Open_SelectsTheRs232Interface()
    {
        LinuxMicroGateMonitorDeviceOpener opener = new(native.Object);

        opener.Open("ttySLG0", new MicroGateMonitorOptions());

        native.Verify(x => x.SetInterface(7, 1), Times.Once);
    }

    [Fact]
    public void Open_WhenTheInterfaceCannotBeSet_StillConfiguresAndReturnsTheDevice()
    {
        native.Setup(x => x.SetInterface(7, It.IsAny<int>())).Returns(-1);
        LinuxMicroGateMonitorDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("ttySLG0", new MicroGateMonitorOptions());
        device.Dispose();

        native.Verify(x => x.SetParams(7, It.IsAny<SynclinkParams>()), Times.Once);
        native.Verify(x => x.EnableReceiver(7, true), Times.Once);
        native.Verify(x => x.Close(7), Times.Once);
    }

    [Fact]
    public void Open_WhenConfigurationFails_ClosesDescriptorAndRethrows()
    {
        native.Setup(x => x.SetParams(7, It.IsAny<SynclinkParams>())).Throws<InvalidOperationException>();
        LinuxMicroGateMonitorDeviceOpener opener = new(native.Object);

        Assert.Throws<InvalidOperationException>(() => opener.Open("ttySLG0", new MicroGateMonitorOptions()));

        native.Verify(x => x.Close(7), Times.Once);
    }

    [Theory]
    [InlineData("select the HDLC line discipline")]
    [InlineData("set the port parameters")]
    [InlineData("enable the receiver")]
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
            case "enable the receiver":
                native.Setup(x => x.EnableReceiver(7, true)).Returns(-1);
                break;
            default:
                native.Setup(x => x.ClearNonBlocking(7)).Returns(-1);
                break;
        }

        LinuxMicroGateMonitorDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open("ttySLG0", new MicroGateMonitorOptions()));

        Assert.Contains(step, exception.Message);
        Assert.Contains("/dev/ttySLG0", exception.Message);
        native.Verify(x => x.Close(7), Times.Once);
    }
}
