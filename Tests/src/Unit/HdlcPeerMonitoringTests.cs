namespace BlueHeighliner.MicroGate;

public sealed class HdlcPeerMonitoringTests : IDisposable
{
    private readonly DeviceHarness harness = new(new HdlcPeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, EnableMonitor = true });
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Start_ReadiesTheDeviceWithoutConnectingOrTransmitting()
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await peer.Start("port", harness.Options).AsTask().WaitAsync(timeout);

        Assert.Equal(HdlcPeerState.Ready, peer.State);
        Assert.False(peer.IsConnected);
        Assert.Empty(harness.Written);
        harness.Device.Verify(x => x.EnableTransmitter(), Times.Never);
    }

    [Fact]
    public async Task Monitored_WhenEnabled_ReportsEveryReceivedFrameInOrderWithoutAnswering()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcFrame> frames = new();
        peer.Monitored.Subscribe(frames);
        await peer.Start("port", harness.Options);

        harness.Receive(harness.Peer(HdlcWireFrameKind.SetAsynchronousBalancedMode));
        harness.Receive(harness.Peer(HdlcWireFrameKind.Information, sendSequence: 0, payload: new byte[] { 1, 2, 3 }));
        harness.Receive(new byte[] { 0x01 });

        List<HdlcFrame> seen = await frames.Next(3);
        Assert.Equal([HdlcFrameKind.SetAsynchronousBalancedMode, HdlcFrameKind.Information, HdlcFrameKind.Malformed], seen.Select(frame => frame.Kind));
        Assert.Equal(new byte[] { 1, 2, 3 }, seen[1].Payload.ToArray());
        Assert.NotNull(seen[2].ErrorMessage);
        Assert.Empty(harness.Written);
        Assert.Equal(HdlcPeerState.Ready, peer.State);
    }

    [Fact]
    public async Task Monitored_WhenDisabled_ReportsNothing()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcFrame> frames = new();
        peer.Monitored.Subscribe(frames);
        await peer.Start("port", harness.Options with { EnableMonitor = false });

        harness.Receive(harness.Peer(HdlcWireFrameKind.SetAsynchronousBalancedMode));
        await Task.Delay(200);

        Assert.Empty(frames.Seen);
    }

    [Fact]
    public async Task Monitored_WhileEstablished_StillReportsFrames()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcFrame> frames = new();
        peer.Monitored.Subscribe(frames);
        await peer.Start("port", harness.Options);

        Task establishing = peer.Connect(harness.Address, harness.RemoteAddress).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await establishing.WaitAsync(timeout);

        Assert.Equal(HdlcFrameKind.UnnumberedAcknowledge, (await frames.Next()).Kind);
    }

    [Fact]
    public async Task Monitored_CompletesWhenThePeerIsDisposed()
    {
        HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcFrame> frames = new();
        peer.Monitored.Subscribe(frames);
        await peer.Start("port", harness.Options);

        await peer.DisposeAsync();

        await frames.Completed.WaitAsync(timeout);
    }

    [Fact]
    public async Task Monitored_ObserverException_IsReportedOnExceptions()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        TestObserver<Exception> errors = new();
        peer.Exceptions.Subscribe(errors);
        peer.Monitored.Subscribe(new CallbackObserver<HdlcFrame>(_ => throw new InvalidOperationException("observer failed")));
        await peer.Start("port", harness.Options);

        harness.Receive(harness.Peer(HdlcWireFrameKind.SetAsynchronousBalancedMode));

        Assert.Equal("observer failed", (await errors.Next()).Message);
    }

    [Fact]
    public async Task Connect_EnablesTheTransmitterAndSendsTheConnectionRequest()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        await peer.Start("port", harness.Options);

        Task establishing = peer.Connect(harness.Address, harness.RemoteAddress).AsTask();

        Assert.Equal(HdlcWireFrameKind.SetAsynchronousBalancedMode, (await harness.NextWritten(0)).Kind);
        harness.Device.Verify(x => x.EnableTransmitter(), Times.Once);
        Assert.Equal(HdlcPeerState.Connecting, peer.State);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await establishing.WaitAsync(timeout);
        Assert.Equal(HdlcPeerState.Connected, peer.State);
    }

    [Fact]
    public async Task Connect_BeforeStartOrTwice_Throws()
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Connect(harness.Address, harness.RemoteAddress));

        await peer.Start("port", harness.Options);
        Task establishing = peer.Connect(harness.Address, harness.RemoteAddress).AsTask();
        await harness.NextWritten(0);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Connect(harness.Address, harness.RemoteAddress));
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await establishing.WaitAsync(timeout);
    }

    [Fact]
    public async Task Send_BeforeEstablishing_Throws()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        await peer.Start("port", harness.Options);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Send(new byte[] { 1 }));
    }

    [Fact]
    public async Task Forward_WritesTheFrameUnchangedAndEnablesTheTransmitter()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        await peer.Start("port", harness.Options);
        byte[] raw = [0x21, 0x10, 9, 8, 7];

        await peer.Forward(raw).AsTask().WaitAsync(timeout);

        Assert.Equal([raw], harness.Written);
        harness.Device.Verify(x => x.EnableTransmitter(), Times.Once);
        Assert.Equal(HdlcPeerState.Ready, peer.State);
    }

    [Fact]
    public async Task Forward_BeforeStartOrAfterEstablishing_Throws()
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Forward(new byte[] { 1 }));

        await peer.Start("port", harness.Options);
        Task establishing = peer.Connect(harness.Address, harness.RemoteAddress).AsTask();
        await harness.NextWritten(0);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await peer.Forward(new byte[] { 1 }));
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await establishing.WaitAsync(timeout);
    }

    [Fact]
    public async Task Transmitted_WhenEnabled_ReportsEveryFrameWrittenInOrder()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcFrame> sent = new();
        peer.Transmitted.Subscribe(sent);
        await peer.Start("port", harness.Options);
        Task connecting = peer.Connect(harness.Address, harness.RemoteAddress).AsTask();
        await harness.NextWritten(0);
        harness.Receive(harness.Peer(HdlcWireFrameKind.UnnumberedAcknowledge));
        await connecting.WaitAsync(timeout);

        await peer.Send(new byte[] { 4, 5 });
        List<HdlcFrame> seen = await sent.Next(2);

        Assert.Equal([HdlcFrameKind.SetAsynchronousBalancedMode, HdlcFrameKind.Information], seen.Select(frame => frame.Kind));
        Assert.Equal(0xFF, seen[0].Address);
        Assert.Equal(new byte[] { 4, 5 }, seen[1].Payload.ToArray());
        Assert.Equal(harness.Written.Select(frame => frame.ToArray()), seen.Select(frame => frame.Raw.ToArray()));
    }

    [Fact]
    public async Task Transmitted_WhenDisabled_ReportsNothing()
    {
        await using HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcFrame> sent = new();
        peer.Transmitted.Subscribe(sent);
        await peer.Start("port", harness.Options with { EnableMonitor = false });

        await peer.Forward(new byte[] { 0x21, 0x10 });

        Assert.Empty(sent.Seen);
        Assert.Single(harness.Written);
    }

    [Fact]
    public async Task Transmitted_ReportsForwardedFramesAndCompletesWhenThePeerIsDisposed()
    {
        HdlcPeer peer = harness.CreatePeer();
        TestObserver<HdlcFrame> sent = new();
        peer.Transmitted.Subscribe(sent);
        await peer.Start("port", harness.Options);

        await peer.Forward(new byte[] { 0x21, 0x10 });
        HdlcFrame frame = await sent.Next();
        await peer.DisposeAsync();

        Assert.Equal(new byte[] { 0x21, 0x10 }, frame.Raw.ToArray());
        await sent.Completed.WaitAsync(timeout);
    }
}
