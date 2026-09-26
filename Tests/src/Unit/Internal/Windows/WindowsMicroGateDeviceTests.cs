namespace BlueHeighliner.MicroGate;

public sealed class WindowsMicroGateDeviceTests
{
    private readonly Mock<IWindowsNative> native = new();

    [Fact]
    public void Read_DelegatesToNative()
    {
        byte[] buffer = new byte[8];
        native.Setup(x => x.Read(9, buffer)).Returns(4);
        WindowsMicroGateDevice device = new(native.Object, 9);

        Assert.Equal(4, device.Read(buffer));
    }

    [Fact]
    public void Write_WritesFrame()
    {
        native.Setup(x => x.Write(9, It.IsAny<byte[]>())).Returns(2);
        WindowsMicroGateDevice device = new(native.Object, 9);

        device.Write(new byte[] { 1, 2 });

        native.Verify(x => x.Write(9, It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2 }))), Times.Once);
    }

    [Fact]
    public void Write_WhenShort_Throws()
    {
        native.Setup(x => x.Write(9, It.IsAny<byte[]>())).Returns(0);
        WindowsMicroGateDevice device = new(native.Object, 9);

        Assert.Throws<IOException>(() => device.Write(new byte[] { 1, 2 }));
    }

    [Fact]
    public void DisableReceiver_DisablesReceiverOnHandle()
    {
        WindowsMicroGateDevice device = new(native.Object, 9);

        device.DisableReceiver();

        native.Verify(x => x.EnableReceiver(9, false), Times.Once);
    }

    [Fact]
    public void Dispose_ClosesHandle()
    {
        WindowsMicroGateDevice device = new(native.Object, 9);

        device.Dispose();

        native.Verify(x => x.Close(9), Times.Once);
    }

    [Fact]
    public void DisableReceiver_AlsoCancelsBlockedRead()
    {
        WindowsMicroGateDevice device = new(native.Object, 9);

        device.DisableReceiver();

        native.Verify(x => x.CancelReceive(9), Times.Once);
    }

    [Fact]
    public void Write_UsesTheFrameArrayWithoutCopyingWhenItIsExact()
    {
        byte[] frame = [1, 2];
        native.Setup(x => x.Write(9, It.IsAny<byte[]>())).Returns(2);
        WindowsMicroGateDevice device = new(native.Object, 9);

        device.Write(frame);

        native.Verify(x => x.Write(9, It.Is<byte[]>(b => ReferenceEquals(b, frame))), Times.Once);
    }

    [Fact]
    public void DisableTransmitter_AlsoCancelsBlockedWrite()
    {
        WindowsMicroGateDevice device = new(native.Object, 9);

        device.DisableTransmitter();

        native.Verify(x => x.EnableTransmitter(9, false), Times.Once);
        native.Verify(x => x.CancelTransmit(9), Times.Once);
    }

    [Fact]
    public void Read_AfterTheReceiverIsDisabled_ReturnsZeroWithoutCallingTheDriver()
    {
        WindowsMicroGateDevice device = new(native.Object, 9);

        device.DisableReceiver();

        Assert.Equal(0, device.Read(new byte[8]));
        native.Verify(x => x.Read(It.IsAny<nint>(), It.IsAny<byte[]>()), Times.Never);
    }
}
