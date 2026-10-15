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
    public void Dispose_LowersSignalsBeforeClosing()
    {
        List<string> calls = [];
        native.Setup(x => x.LowerSignals(7)).Callback(() => calls.Add("lower"));
        native.Setup(x => x.Close(7)).Callback(() => calls.Add("close"));
        LinuxMicroGateDevice device = new(native.Object, 7);

        device.Dispose();

        Assert.Equal(["lower", "close"], calls);
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

    [Fact]
    public void EnableTransmitter_EnablesTransmitterOnDescriptor()
    {
        LinuxMicroGateDevice device = new(native.Object, 7);

        device.EnableTransmitter();

        native.Verify(x => x.EnableTransmitter(7, true), Times.Once);
    }

    [Fact]
    public void EnableTransmitter_WhenItFails_Throws()
    {
        native.Setup(x => x.EnableTransmitter(7, true)).Returns(-1);
        LinuxMicroGateDevice device = new(native.Object, 7);

        Assert.Throws<IOException>(device.EnableTransmitter);
    }

    [Theory]
    [InlineData(0x000, MicroGateSignals.None)]
    [InlineData(0x040, MicroGateSignals.DataCarrierDetect)]
    [InlineData(0x100, MicroGateSignals.DataSetReady)]
    [InlineData(0x020, MicroGateSignals.ClearToSend)]
    [InlineData(0x160, MicroGateSignals.DataCarrierDetect | MicroGateSignals.DataSetReady | MicroGateSignals.ClearToSend)]
    [InlineData(0x002, MicroGateSignals.DataTerminalReady)]
    [InlineData(0x004, MicroGateSignals.RequestToSend)]
    [InlineData(0x166, MicroGateSignals.DataCarrierDetect | MicroGateSignals.DataSetReady | MicroGateSignals.ClearToSend | MicroGateSignals.DataTerminalReady | MicroGateSignals.RequestToSend)]
    [InlineData(0x080, MicroGateSignals.None)]
    public void ReadSignals_MapsTheWiresAndIgnoresTheRingIndicator(int bits, MicroGateSignals expected)
    {
        native.Setup(x => x.GetSignals(7)).Returns(bits);
        LinuxMicroGateDevice device = new(native.Object, 7);

        Assert.Equal(expected, device.ReadSignals());
    }

    [Fact]
    public void ReadSignals_WhenTheQueryFails_ReturnsNull()
    {
        native.Setup(x => x.GetSignals(7)).Returns(-1);
        LinuxMicroGateDevice device = new(native.Object, 7);

        Assert.Null(device.ReadSignals());
    }

    [Fact]
    public void WaitSignals_WaitsForTheOppositeOfEachInputWireThenReadsThem()
    {
        native.Setup(x => x.WaitSignalEvents(7, 0x01 | 0x20 | 0x08)).Returns(0x01);
        native.Setup(x => x.GetSignals(7)).Returns(0x040 | 0x100 | 0x020);
        LinuxMicroGateDevice device = new(native.Object, 7);

        MicroGateSignals? reading = device.WaitSignals(MicroGateSignals.DataCarrierDetect | MicroGateSignals.ClearToSend | MicroGateSignals.DataTerminalReady);

        Assert.Equal(MicroGateSignals.DataCarrierDetect | MicroGateSignals.DataSetReady | MicroGateSignals.ClearToSend, reading);
    }

    [Fact]
    public void WaitSignals_WhenInterruptedWithoutACancel_WaitsAgain()
    {
        native.SetupSequence(x => x.WaitSignalEvents(7, It.IsAny<int>())).Returns(0).Returns(0x10);
        native.Setup(x => x.GetSignals(7)).Returns(0x040);
        LinuxMicroGateDevice device = new(native.Object, 7);

        Assert.Equal(MicroGateSignals.DataCarrierDetect, device.WaitSignals(MicroGateSignals.None));

        native.Verify(x => x.WaitSignalEvents(7, It.IsAny<int>()), Times.Exactly(2));
    }

    [Fact]
    public void WaitSignals_WhenTheEventsWakeWithoutAnInputChange_WaitsAgain()
    {
        native.Setup(x => x.WaitSignalEvents(7, It.IsAny<int>())).Returns(0x10);
        native.SetupSequence(x => x.GetSignals(7)).Returns(0x000).Returns(0x040);
        LinuxMicroGateDevice device = new(native.Object, 7);

        Assert.Equal(MicroGateSignals.DataCarrierDetect, device.WaitSignals(MicroGateSignals.None));

        native.Verify(x => x.WaitSignalEvents(7, It.IsAny<int>()), Times.Exactly(2));
    }

    [Fact]
    public void WaitSignals_WhenTheCallIsCancelledWhileWaiting_ReturnsNull()
    {
        LinuxMicroGateDevice device = new(native.Object, 7);
        native.Setup(x => x.WaitSignalEvents(7, It.IsAny<int>())).Returns(() =>
        {
            device.CancelSignalWait();
            return 0;
        });

        Assert.Null(device.WaitSignals(MicroGateSignals.None));
        Assert.Null(device.WaitSignals(MicroGateSignals.None));

        native.Verify(x => x.WaitSignalEvents(7, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void WaitSignals_WhenTheEventsAreUnsupported_FallsBackToPollingTheWires()
    {
        native.Setup(x => x.WaitSignalEvents(7, It.IsAny<int>())).Returns(-1);
        native.SetupSequence(x => x.GetSignals(7)).Returns(0x000).Returns(0x100);
        LinuxMicroGateDevice device = new(native.Object, 7);

        Assert.Equal(MicroGateSignals.DataSetReady, device.WaitSignals(MicroGateSignals.None));
    }

    [Fact]
    public async Task CancelSignalWait_InterruptsTheThreadThatIsWaiting()
    {
        SemaphoreSlim waiting = new(0);
        SemaphoreSlim interrupted = new(0);
        native.Setup(x => x.CurrentThread()).Returns(42);
        native.Setup(x => x.InterruptThread(42)).Callback(() => interrupted.Release());
        native.Setup(x => x.WaitSignalEvents(7, It.IsAny<int>())).Returns(() =>
        {
            waiting.Release();
            interrupted.Wait();
            return 0;
        });
        LinuxMicroGateDevice device = new(native.Object, 7);
        Task<MicroGateSignals?> wait = Task.Run(() => device.WaitSignals(MicroGateSignals.None));
        Assert.True(await waiting.WaitAsync(TimeSpan.FromSeconds(10)));

        device.CancelSignalWait();

        Assert.Null(await wait.WaitAsync(TimeSpan.FromSeconds(10)));
        native.Verify(x => x.InterruptThread(42), Times.AtLeastOnce);
    }

    [Fact]
    public void CancelSignalWait_WhenNothingIsWaiting_InterruptsNothing()
    {
        LinuxMicroGateDevice device = new(native.Object, 7);

        device.CancelSignalWait();

        native.Verify(x => x.InterruptThread(It.IsAny<nuint>()), Times.Never);
    }
}
