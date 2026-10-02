namespace BlueHeighliner.MicroGate;

public sealed class MicroGatePeerMonitoringTests : IDisposable
{
    private readonly DeviceHarness harness = new(new MicroGatePeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, EnableMonitor = true });
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Start_ReadiesTheDeviceWithoutConnectingOrTransmitting()
    {
        await using MicroGatePeer peer = harness.CreatePeer();

        await peer.Start("port", harness.Options).AsTask().WaitAsync(timeout);

        Assert.Equal(MicroGatePeerState.Ready, peer.State);
        Assert.False(peer.IsConnected);
        Assert.Empty(harness.Written);
        harness.Device.Verify(x => x.EnableTransmitter(), Times.Never);
    }

    [Fact]
    public async Task Monitored_WhenEnabled_ReportsEveryReceivedFrameInOrderWithoutAnswering()
    {
        await using MicroGatePeer peer = harness.CreatePeer();
        TestObserver<MicroGateFrame> frames = new();
        peer.Monitored.Subscribe(frames);
        await peer.Start("port", harness.Options);

        harness.Receive(harness.Peer(HdlcFrameKind.SetAsynchronousBalancedMode));
        harness.Receive(harness.Peer(HdlcFrameKind.Information, sendSequence: 0, payload: new byte[] { 1, 2, 3 }));
        harness.Receive(new byte[] { 0x01 });

        List<MicroGateFrame> seen = await frames.Next(3);
        Assert.Equal([MicroGateFrameKind.SetAsynchronousBalancedMode, MicroGateFrameKind.Information, MicroGateFrameKind.Malformed], seen.Select(frame => frame.Kind));
        Assert.Equal(new byte[] { 1, 2, 3 }, seen[1].Payload.ToArray());
        Assert.NotNull(seen[2].ErrorMessage);
        Assert.Empty(harness.Written);
        Assert.Equal(MicroGatePeerState.Ready, peer.State);
    }

    [Fact]
    public async Task Monitored_WhenDisabled_ReportsNothing()
    {
        await using MicroGatePeer peer = harness.CreatePeer();
        TestObserver<MicroGateFrame> frames = new();
        peer.Monitored.Subscribe(frames);
        await peer.Start("port", harness.Options with { EnableMonitor = false });

        harness.Receive(harness.Peer(HdlcFrameKind.SetAsynchronousBalancedMode));
        await Task.Delay(200);

        Assert.Empty(frames.Seen);
    }

    [Fact]
    public async Task Monitored_WhileEstablished_StillReportsFrames()
    {
        await using MicroGatePeer peer = harness.CreatePeer();
        TestObserver<MicroGateFrame> frames = new();
        peer.Monitored.Subscribe(frames);
        await peer.Start("port", harness.Options);

        Task establishing = peer.Connect(harness.Address, harness.RemoteAddress).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await establishing.WaitAsync(timeout);

        Assert.Equal(MicroGateFrameKind.UnnumberedAcknowledge, (await frames.Next()).Kind);
    }

    [Fact]
    public async Task Monitored_CompletesWhenThePeerIsDisposed()
    {
        MicroGatePeer peer = harness.CreatePeer();
        TestObserver<MicroGateFrame> frames = new();
        peer.Monitored.Subscribe(frames);
        await peer.Start("port", harness.Options);

        await peer.DisposeAsync();

        await frames.Completed.WaitAsync(timeout);
    }

    [Fact]
    public async Task Monitored_ObserverException_IsReportedOnExceptions()
    {
        await using MicroGatePeer peer = harness.CreatePeer();
        TestObserver<Exception> errors = new();
        peer.Exceptions.Subscribe(errors);
        peer.Monitored.Subscribe(new CallbackObserver<MicroGateFrame>(_ => throw new InvalidOperationException("observer failed")));
        await peer.Start("port", harness.Options);

        harness.Receive(harness.Peer(HdlcFrameKind.SetAsynchronousBalancedMode));

        Assert.Equal("observer failed", (await errors.Next()).Message);
    }

    [Fact]
    public async Task Connect_EnablesTheTransmitterAndSendsTheConnectionRequest()
    {
        await using MicroGatePeer peer = harness.CreatePeer();
        await peer.Start("port", harness.Options);

        Task establishing = peer.Connect(harness.Address, harness.RemoteAddress).AsTask();

        Assert.Equal(HdlcFrameKind.SetAsynchronousBalancedMode, (await harness.NextWritten(0)).Kind);
        harness.Device.Verify(x => x.EnableTransmitter(), Times.Once);
        Assert.Equal(MicroGatePeerState.Connecting, peer.State);
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await establishing.WaitAsync(timeout);
        Assert.Equal(MicroGatePeerState.Connected, peer.State);
    }

    [Fact]
    public async Task Connect_BeforeStartOrTwice_Throws()
    {
        await using MicroGatePeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Connect(harness.Address, harness.RemoteAddress));

        await peer.Start("port", harness.Options);
        Task establishing = peer.Connect(harness.Address, harness.RemoteAddress).AsTask();
        await harness.NextWritten(0);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Connect(harness.Address, harness.RemoteAddress));
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await establishing.WaitAsync(timeout);
    }

    [Fact]
    public async Task Send_BeforeEstablishing_Throws()
    {
        await using MicroGatePeer peer = harness.CreatePeer();
        await peer.Start("port", harness.Options);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(new byte[] { 1 }));
    }

    [Fact]
    public async Task Forward_WritesTheFrameUnchangedAndEnablesTheTransmitter()
    {
        await using MicroGatePeer peer = harness.CreatePeer();
        await peer.Start("port", harness.Options);
        byte[] raw = [0x21, 0x10, 9, 8, 7];

        await peer.Forward(raw).AsTask().WaitAsync(timeout);

        Assert.Equal([raw], harness.Written);
        harness.Device.Verify(x => x.EnableTransmitter(), Times.Once);
        Assert.Equal(MicroGatePeerState.Ready, peer.State);
    }

    [Fact]
    public async Task Forward_BeforeStartOrAfterEstablishing_Throws()
    {
        await using MicroGatePeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Forward(new byte[] { 1 }));

        await peer.Start("port", harness.Options);
        Task establishing = peer.Connect(harness.Address, harness.RemoteAddress).AsTask();
        await harness.NextWritten(0);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Forward(new byte[] { 1 }));
        harness.Receive(harness.Peer(HdlcFrameKind.UnnumberedAcknowledge));
        await establishing.WaitAsync(timeout);
    }
}
