namespace BlueHeighliner.MicroGate;

public sealed class LinuxMicroGateDeviceTests
{
    private readonly Mock<ILinuxNative> native = new();

    [Fact]
    public void Read_DelegatesToNative()
    {
        byte[] buffer = new byte[8];
        native.Setup(x => x.Read(7, buffer)).Returns(3);
        LinuxMicroGateDevice device = new(native.Object, 7);

        Assert.Equal(3, device.Read(buffer));
    }

    [Fact]
    public void Write_WritesFrameThenDrains()
    {
        native.Setup(x => x.Write(7, It.IsAny<byte[]>())).Returns(2);
        LinuxMicroGateDevice device = new(native.Object, 7);

        device.Write(new byte[] { 1, 2 });

        native.Verify(x => x.Write(7, It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2 }))), Times.Once);
        native.Verify(x => x.Drain(7), Times.Once);
    }

    [Fact]
    public void Write_WhenShort_Throws()
    {
        native.Setup(x => x.Write(7, It.IsAny<byte[]>())).Returns(-1);
        LinuxMicroGateDevice device = new(native.Object, 7);

        Assert.Throws<IOException>(() => device.Write(new byte[] { 1, 2 }));

        native.Verify(x => x.Drain(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void DisableReceiver_DisablesReceiverOnDescriptor()
    {
        LinuxMicroGateDevice device = new(native.Object, 7);

        device.DisableReceiver();

        native.Verify(x => x.EnableReceiver(7, false), Times.Once);
    }

    [Fact]
    public void Dispose_ClosesDescriptor()
    {
        LinuxMicroGateDevice device = new(native.Object, 7);

        device.Dispose();

        native.Verify(x => x.Close(7), Times.Once);
    }
}
