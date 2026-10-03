namespace BlueHeighliner.MicroGate;

public sealed class PassthroughRelayTests : IAsyncLifetime
{
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(10);
    private readonly HdlcPeerOptions endpoint = new() { RetryInterval = TimeSpan.FromMilliseconds(200) };
    private readonly HdlcPeerOptions relay = new() { EnableMonitor = true };
    private HdlcPeer left = null!;
    private HdlcPeer right = null!;
    private HdlcPeer relayLeft = null!;
    private HdlcPeer relayRight = null!;

    public async Task InitializeAsync()
    {
        (SocketMicroGateDevice leftDevice, SocketMicroGateDevice relayLeftDevice) = await SocketMicroGateDevice.CreatePair();
        (SocketMicroGateDevice relayRightDevice, SocketMicroGateDevice rightDevice) = await SocketMicroGateDevice.CreatePair();
        left = Create(leftDevice);
        right = Create(rightDevice);
        relayLeft = Create(relayLeftDevice);
        relayRight = Create(relayRightDevice);
    }

    public async Task DisposeAsync()
    {
        await left.DisposeAsync();
        await right.DisposeAsync();
        await relayLeft.DisposeAsync();
        await relayRight.DisposeAsync();
    }

    [Fact]
    public async Task Relay_ForwardsFramesBothWaysSoTheEndpointsConnectAndExchangeDataAndTheRelayLogsThem()
    {
        TestObserver<HdlcFrame> fromLeft = new();
        TestObserver<HdlcFrame> fromRight = new();
        relayLeft.Monitored.Subscribe(new CallbackObserver<HdlcFrame>(frame =>
        {
            fromLeft.OnNext(frame);
            relayRight.Forward(frame.Raw).AsTask().Wait(timeout);
        }));
        relayRight.Monitored.Subscribe(new CallbackObserver<HdlcFrame>(frame =>
        {
            fromRight.OnNext(frame);
            relayLeft.Forward(frame.Raw).AsTask().Wait(timeout);
        }));
        PayloadObserver atRight = new();
        PayloadObserver atLeft = new();
        right.Receiver = atRight.Receive;
        left.Receiver = atLeft.Receive;

        await Task.WhenAll(relayLeft.Start("relay-left", relay).AsTask(), relayRight.Start("relay-right", relay).AsTask()).WaitAsync(timeout);
        await left.Start("left", endpoint);
        await right.Start("right", endpoint);
        await Task.WhenAll(left.Connect(0x01, 0x03).AsTask(), right.Connect(0x03, 0x01).AsTask()).WaitAsync(timeout);
        await left.Send(new byte[] { 1, 2, 3 });
        await right.Send(new byte[] { 9, 8 });

        Assert.Equal(new byte[] { 1, 2, 3 }, await atRight.Next());
        Assert.Equal(new byte[] { 9, 8 }, await atLeft.Next());
        Assert.Contains(fromLeft.Seen, frame => frame.Kind == HdlcFrameKind.Information && frame.Payload.Span.SequenceEqual(new byte[] { 1, 2, 3 }));
        Assert.Contains(fromRight.Seen, frame => frame.Kind == HdlcFrameKind.Information && frame.Payload.Span.SequenceEqual(new byte[] { 9, 8 }));
        Assert.Contains(fromLeft.Seen, frame => frame.Kind == HdlcFrameKind.SetAsynchronousBalancedMode);
        Assert.Contains(fromRight.Seen, frame => frame.Kind == HdlcFrameKind.UnnumberedAcknowledge);
    }

    private HdlcPeer Create(IMicroGateDevice device)
    {
        Mock<IMicroGateDeviceOpener> opener = new();
        opener.Setup(x => x.Open(It.IsAny<string>(), It.IsAny<HdlcPeerOptions>())).Returns(device);
        return new HdlcPeer(opener.Object, opener.Object);
    }
}
