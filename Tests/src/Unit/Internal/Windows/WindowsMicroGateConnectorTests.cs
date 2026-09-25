namespace BlueHeighliner.MicroGate;

public sealed class WindowsMicroGateConnectorTests : IDisposable
{
    private readonly DeviceHarness harness = new();
    private readonly Mock<IWindowsNative> native = new();
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);
    private nint handle = 9;

    public WindowsMicroGateConnectorTests()
    {
        native.Setup(x => x.OpenByName("COM1", out handle)).Returns(0u);
        native.Setup(x => x.Read(9, It.IsAny<byte[]>())).Returns((nint _, byte[] buffer) => harness.Device.Object.Read(buffer));
        native.Setup(x => x.Write(9, It.IsAny<byte[]>())).Returns((nint _, byte[] buffer) =>
        {
            harness.Device.Object.Write(buffer);
            return buffer.Length;
        });
        native.Setup(x => x.EnableReceiver(9, false)).Callback(() => harness.Device.Object.DisableReceiver());
    }

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Connect_WhenOpenFails_Throws()
    {
        nint failedHandle = 0;
        native.Setup(x => x.OpenByName("COM2", out failedHandle)).Returns(2u);
        WindowsMicroGateConnector connector = new(native.Object);

        IOException exception = await Assert.ThrowsAsync<IOException>(async () => await connector.Connect("COM2", new MicroGateConnectionOptions(), CancellationToken.None));

        Assert.Contains("COM2", exception.Message);
    }

    [Fact]
    public async Task Connect_ConfiguresPortFromOptionsAndEstablishes()
    {
        MicroGateConnectionOptions options = new()
        {
            Encoding = MicroGateEncoding.BiphaseMark,
            Crc = MicroGateCrc.None,
            IdlePattern = MicroGateIdlePattern.Mark,
            HardwareAddressFilter = 0x05,
        };
        WindowsMicroGateConnector connector = new(native.Object);

        ValueTask<IMicroGateConnection> pending = connector.Connect("COM1", options, CancellationToken.None);
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await using IMicroGateConnection connection = await pending.AsTask().WaitAsync(timeout);

        Assert.True(connection.IsConnected);
        native.Verify(x => x.SetParams(9, It.Is<MghdlcParams>(p => p.Mode == MghdlcConstants.ModeHdlc && p.Encoding == 4 && p.CrcType == 0 && p.Addr == 0x05)), Times.Once);
        native.Verify(x => x.SetIdleMode(9, 6u), Times.Once);
        native.Verify(x => x.EnableReceiver(9, true), Times.Once);
        native.Verify(x => x.EnableTransmitter(9, true), Times.Once);
    }

    [Fact]
    public async Task Connect_WithDefaultOptions_DisablesHardwareAddressFilter()
    {
        WindowsMicroGateConnector connector = new(native.Object);

        ValueTask<IMicroGateConnection> pending = connector.Connect("COM1", new MicroGateConnectionOptions(), CancellationToken.None);
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await using IMicroGateConnection connection = await pending.AsTask().WaitAsync(timeout);

        native.Verify(x => x.SetParams(9, It.Is<MghdlcParams>(p => p.Encoding == 0 && p.CrcType == 1 && p.Addr == 0xFF)), Times.Once);
        native.Verify(x => x.SetIdleMode(9, 0u), Times.Once);
    }

    [Fact]
    public async Task Connect_WhenCanceled_ThrowsAndClosesHandle()
    {
        using CancellationTokenSource cancellation = new();
        WindowsMicroGateConnector connector = new(native.Object);

        Task<IMicroGateConnection> pending = connector.Connect("COM1", new MicroGateConnectionOptions(), cancellation.Token).AsTask();
        await harness.NextWritten(0);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        native.Verify(x => x.Close(9), Times.Once);
    }

    [Fact]
    public async Task Connect_WhenConfigurationFails_ClosesHandleAndRethrows()
    {
        native.Setup(x => x.SetParams(9, It.IsAny<MghdlcParams>())).Throws<InvalidOperationException>();
        WindowsMicroGateConnector connector = new(native.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connector.Connect("COM1", new MicroGateConnectionOptions(), CancellationToken.None));

        native.Verify(x => x.Close(9), Times.Once);
    }
}
