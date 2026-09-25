namespace BlueHeighliner.MicroGate;

public sealed class LinuxMicroGateDeviceTests
{
    private readonly Mock<ILinuxNative> native = new();

    [Fact]
    public void Read_DelegatesToNative()
    {
        byte[] buffer = new byte[8];
        native.Setup(x => x.WaitReadable(7, It.IsAny<int>())).Returns(1);
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

    [Fact]
    public void Write_UsesTheFrameArrayWithoutCopyingWhenItIsExact()
    {
        byte[] frame = [1, 2];
        native.Setup(x => x.Write(7, It.IsAny<byte[]>())).Returns(2);
        LinuxMicroGateDevice device = new(native.Object, 7);

        device.Write(frame);

        native.Verify(x => x.Write(7, It.Is<byte[]>(b => ReferenceEquals(b, frame))), Times.Once);
    }

    [Fact]
    public void Write_CopiesWhenTheFrameIsOnlyPartOfAnArray()
    {
        byte[] backing = [9, 1, 2, 9];
        native.Setup(x => x.Write(7, It.IsAny<byte[]>())).Returns(2);
        LinuxMicroGateDevice device = new(native.Object, 7);

        device.Write(backing.AsMemory(1, 2));

        native.Verify(x => x.Write(7, It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2 }))), Times.Once);
    }

    [Fact]
    public void Read_KeepsPollingWhileNothingArrivesAndReadsOnceReadable()
    {
        byte[] buffer = new byte[8];
        int polls = 0;
        native.Setup(x => x.WaitReadable(7, It.IsAny<int>())).Returns(() => ++polls < 3 ? 0 : 1);
        native.Setup(x => x.Read(7, buffer)).Returns(2);
        LinuxMicroGateDevice device = new(native.Object, 7);

        Assert.Equal(2, device.Read(buffer));

        Assert.Equal(3, polls);
    }

    [Fact]
    public void Read_ReturnsFailureWhenPollingFails()
    {
        native.Setup(x => x.WaitReadable(7, It.IsAny<int>())).Returns(-1);
        LinuxMicroGateDevice device = new(native.Object, 7);

        Assert.Equal(-1, device.Read(new byte[8]));
        native.Verify(x => x.Read(It.IsAny<int>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task Read_ReturnsZeroOnceTheReceiverIsDisabledEvenIfNothingArrives()
    {
        native.Setup(x => x.WaitReadable(7, It.IsAny<int>())).Returns(0);
        LinuxMicroGateDevice device = new(native.Object, 7);
        Task<int> reading = Task.Run(() => device.Read(new byte[8]));
        await Task.Delay(50);
        Assert.False(reading.IsCompleted);

        device.DisableReceiver();

        Assert.Equal(0, await reading.WaitAsync(TimeSpan.FromSeconds(5)));
        native.Verify(x => x.Read(It.IsAny<int>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public void DisableTransmitter_DisablesTransmitterOnDescriptor()
    {
        LinuxMicroGateDevice device = new(native.Object, 7);

        device.DisableTransmitter();

        native.Verify(x => x.EnableTransmitter(7, false), Times.Once);
    }
}
