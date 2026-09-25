namespace BlueHeighliner.MicroGate;

public sealed class LinuxMicroGateConnectorTests : IDisposable
{
    private readonly DeviceHarness harness = new();
    private readonly Mock<ILinuxNative> native = new();
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public LinuxMicroGateConnectorTests()
    {
        native.Setup(x => x.Open(It.IsAny<string>())).Returns(7);
        native.Setup(x => x.Read(7, It.IsAny<byte[]>())).Returns((int _, byte[] buffer) => harness.Device.Object.Read(buffer));
        native.Setup(x => x.Write(7, It.IsAny<byte[]>())).Returns((int _, byte[] buffer) =>
        {
            harness.Device.Object.Write(buffer);
            return buffer.Length;
        });
        native.Setup(x => x.EnableReceiver(7, false)).Callback(() => harness.Device.Object.DisableReceiver());
    }

    public void Dispose() => harness.Dispose();

    [Theory]
    [InlineData("ttySLG0", "/dev/ttySLG0")]
    [InlineData("/dev/ttyUSB1", "/dev/ttyUSB1")]
    public async Task Connect_WhenOpenFails_ThrowsWithNormalizedPath(string portName, string expectedPath)
    {
        native.Setup(x => x.Open(expectedPath)).Returns(-1);
        LinuxMicroGateConnector connector = new(native.Object);

        IOException exception = await Assert.ThrowsAsync<IOException>(async () => await connector.Connect(portName, new MicroGateConnectionOptions(), CancellationToken.None));

        Assert.Contains(expectedPath, exception.Message);
    }

    [Fact]
    public async Task Connect_ConfiguresPortFromOptionsAndEstablishes()
    {
        MicroGateConnectionOptions options = new()
        {
            Encoding = MicroGateEncoding.NrziSpace,
            Crc = MicroGateCrc.Crc32Ccitt,
            IdlePattern = MicroGateIdlePattern.Ones,
            HardwareAddressFilter = 0x21,
        };
        LinuxMicroGateConnector connector = new(native.Object);

        ValueTask<IMicroGateConnection> pending = connector.Connect("ttySLG0", options, CancellationToken.None);
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await using IMicroGateConnection connection = await pending.AsTask().WaitAsync(timeout);

        Assert.True(connection.IsConnected);
        native.Verify(x => x.Open("/dev/ttySLG0"), Times.Once);
        native.Verify(x => x.SelectHdlcLineDiscipline(7), Times.Once);
        native.Verify(x => x.SetParams(7, It.Is<SynclinkParams>(p => p.Mode == SynclinkConstants.ModeHdlc && p.Encoding == 3 && p.CrcType == 2 && p.AddressFilter == 0x21)), Times.Once);
        native.Verify(x => x.SetTransmitIdle(7, 3), Times.Once);
        native.Verify(x => x.EnableReceiver(7, true), Times.Once);
        native.Verify(x => x.EnableTransmitter(7, true), Times.Once);
        native.Verify(x => x.ClearNonBlocking(7), Times.Once);
    }

    [Fact]
    public async Task Connect_WithDefaultOptions_DisablesHardwareAddressFilter()
    {
        LinuxMicroGateConnector connector = new(native.Object);

        ValueTask<IMicroGateConnection> pending = connector.Connect("ttySLG0", new MicroGateConnectionOptions(), CancellationToken.None);
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await using IMicroGateConnection connection = await pending.AsTask().WaitAsync(timeout);

        native.Verify(x => x.SetParams(7, It.Is<SynclinkParams>(p => p.Encoding == 0 && p.CrcType == 1 && p.AddressFilter == 0xFF)), Times.Once);
        native.Verify(x => x.SetTransmitIdle(7, 0), Times.Once);
    }

    [Fact]
    public async Task Connect_WhenCanceled_ThrowsAndClosesDescriptor()
    {
        using CancellationTokenSource cancellation = new();
        LinuxMicroGateConnector connector = new(native.Object);

        Task<IMicroGateConnection> pending = connector.Connect("ttySLG0", new MicroGateConnectionOptions(), cancellation.Token).AsTask();
        await harness.NextWritten(0);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        native.Verify(x => x.Close(7), Times.Once);
    }

    [Fact]
    public async Task Connect_WhenConfigurationFails_ClosesDescriptorAndRethrows()
    {
        native.Setup(x => x.SetParams(7, It.IsAny<SynclinkParams>())).Throws<InvalidOperationException>();
        LinuxMicroGateConnector connector = new(native.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connector.Connect("ttySLG0", new MicroGateConnectionOptions(), CancellationToken.None));

        native.Verify(x => x.Close(7), Times.Once);
    }
}
