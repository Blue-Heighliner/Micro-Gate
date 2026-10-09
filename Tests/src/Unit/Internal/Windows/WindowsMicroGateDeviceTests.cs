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
    public void Write_WaitsForFrameToBeSent()
    {
        native.Setup(x => x.Write(9, It.IsAny<byte[]>())).Returns(2);
        WindowsMicroGateDevice device = new(native.Object, 9);

        device.Write(new byte[] { 1, 2 });

        native.Verify(x => x.WaitAllSent(9), Times.Once);
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
    public void EnableTransmitter_EnablesTransmitterOnHandle()
    {
        WindowsMicroGateDevice device = new(native.Object, 9);

        device.EnableTransmitter();

        native.Verify(x => x.EnableTransmitter(9, true), Times.Once);
    }

    [Fact]
    public void EnableTransmitter_WhenItFails_Throws()
    {
        native.Setup(x => x.EnableTransmitter(9, true)).Returns(5u);
        WindowsMicroGateDevice device = new(native.Object, 9);

        Assert.Throws<IOException>(device.EnableTransmitter);
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

    [Theory]
    [InlineData((byte)0x00, MicroGateSignals.None)]
    [InlineData((byte)0x01, MicroGateSignals.DataCarrierDetect)]
    [InlineData((byte)0x40, MicroGateSignals.DataSetReady)]
    [InlineData((byte)0x10, MicroGateSignals.ClearToSend)]
    [InlineData((byte)0x51, MicroGateSignals.DataCarrierDetect | MicroGateSignals.DataSetReady | MicroGateSignals.ClearToSend)]
    [InlineData((byte)0x80, MicroGateSignals.DataTerminalReady)]
    [InlineData((byte)0x20, MicroGateSignals.RequestToSend)]
    [InlineData((byte)0xF1, MicroGateSignals.DataCarrierDetect | MicroGateSignals.DataSetReady | MicroGateSignals.ClearToSend | MicroGateSignals.DataTerminalReady | MicroGateSignals.RequestToSend)]
    [InlineData((byte)0x0E, MicroGateSignals.None)]
    public void ReadSignals_MapsTheWiresAndIgnoresTheRest(byte bits, MicroGateSignals expected)
    {
        byte signals = bits;
        native.Setup(x => x.GetSignals(9, out signals)).Returns(0u);
        WindowsMicroGateDevice device = new(native.Object, 9);

        Assert.Equal(expected, device.ReadSignals());
    }

    [Fact]
    public void ReadSignals_WhenTheQueryFails_ReturnsNull()
    {
        byte signals = 0;
        native.Setup(x => x.GetSignals(9, out signals)).Returns(6u);
        WindowsMicroGateDevice device = new(native.Object, 9);

        Assert.Null(device.ReadSignals());
    }

    [Fact]
    public void UartDevice_ReadSignals_DelegatesToTheFrameDevice()
    {
        byte signals = 0x40;
        native.Setup(x => x.GetSignals(9, out signals)).Returns(0u);
        WindowsUartDevice device = new(native.Object, 9);

        Assert.Equal(MicroGateSignals.DataSetReady, device.ReadSignals());
    }

    [Fact]
    public void WaitSignals_WaitsForTheOppositeOfEachInputWireThenReadsThem()
    {
        uint events = 0x02;
        byte signals = 0x01 | 0x40 | 0x10;
        native.Setup(x => x.WaitSignalEvents(9, 0x02 | 0x08 | 0x10, out events)).Returns(0u);
        native.Setup(x => x.GetSignals(9, out signals)).Returns(0u);
        WindowsMicroGateDevice device = new(native.Object, 9);

        MicroGateSignals? reading = device.WaitSignals(MicroGateSignals.DataSetReady | MicroGateSignals.ClearToSend);

        Assert.Equal(MicroGateSignals.DataCarrierDetect | MicroGateSignals.DataSetReady | MicroGateSignals.ClearToSend, reading);
    }

    [Fact]
    public void WaitSignals_WhenTheWaitIsCancelled_ReturnsNull()
    {
        WindowsMicroGateDevice device = new(native.Object, 9);
        uint none = 0;
        native.Setup(x => x.WaitSignalEvents(9, It.IsAny<uint>(), out none)).Returns(() =>
        {
            device.CancelSignalWait();
            return 0u;
        });

        Assert.Null(device.WaitSignals(MicroGateSignals.None));

        native.Verify(x => x.CancelSignalWait(9), Times.Once);
    }

    [Fact]
    public async Task WaitSignals_WhenTheEventsAreUnsupported_PollsTheWiresUntilCancelled()
    {
        uint none = 0;
        byte signals = 0x00;
        native.Setup(x => x.WaitSignalEvents(9, It.IsAny<uint>(), out none)).Returns(1u);
        native.Setup(x => x.GetSignals(9, out signals)).Returns(0u);
        WindowsMicroGateDevice device = new(native.Object, 9);
        Task<MicroGateSignals?> wait = Task.Run(() => device.WaitSignals(MicroGateSignals.None));
        await Task.Delay(350);

        device.CancelSignalWait();

        Assert.Null(await wait.WaitAsync(TimeSpan.FromSeconds(10)));
        native.Verify(x => x.WaitSignalEvents(9, It.IsAny<uint>(), out none), Times.Once);
        native.Verify(x => x.GetSignals(9, out signals), Times.AtLeast(2));
    }
}
