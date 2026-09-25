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
    public void Open_ConfiguresPortFromOptionsAndReturnsDevice()
    {
        MicroGatePeerOptions options = new()
        {
            Encoding = MicroGateEncoding.BiphaseMark,
            Crc = MicroGateCrc.None,
            IdlePattern = MicroGateIdlePattern.Mark,
            HardwareAddressFilter = 0x05,
        };
        WindowsMicroGateDeviceOpener opener = new(native.Object);

        IMicroGateDevice device = opener.Open("COM1", options);
        device.Dispose();

        native.Verify(x => x.SetParams(9, It.Is<MghdlcParams>(p => p.Mode == MghdlcConstants.ModeHdlc && p.Encoding == 4 && p.CrcType == 0 && p.Addr == 0x05)), Times.Once);
        native.Verify(x => x.SetIdleMode(9, 6u), Times.Once);
        native.Verify(x => x.EnableReceiver(9, true), Times.Once);
        native.Verify(x => x.EnableTransmitter(9, true), Times.Once);
        native.Verify(x => x.Close(9), Times.Once);
    }

    [Fact]
    public void Open_WithDefaultOptions_DisablesHardwareAddressFilter()
    {
        WindowsMicroGateDeviceOpener opener = new(native.Object);

        opener.Open("COM1", new MicroGatePeerOptions());

        native.Verify(x => x.SetParams(9, It.Is<MghdlcParams>(p => p.Encoding == 0 && p.CrcType == 1 && p.Addr == 0xFF)), Times.Once);
        native.Verify(x => x.SetIdleMode(9, 0u), Times.Once);
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
    [InlineData("enable the transmitter")]
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
            case "enable the receiver":
                native.Setup(x => x.EnableReceiver(9, true)).Returns(5u);
                break;
            default:
                native.Setup(x => x.EnableTransmitter(9, true)).Returns(5u);
                break;
        }

        WindowsMicroGateDeviceOpener opener = new(native.Object);

        IOException exception = Assert.Throws<IOException>(() => opener.Open("COM1", new MicroGatePeerOptions()));

        Assert.Contains(step, exception.Message);
        native.Verify(x => x.Close(9), Times.Once);
    }
}
