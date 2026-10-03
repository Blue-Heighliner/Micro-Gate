namespace BlueHeighliner.MicroGate;

public sealed class HdlcPeerAcknowledgementTests : IDisposable
{
    private readonly DeviceHarness harness = new(new HdlcPeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, AcknowledgeDelay = TimeSpan.FromMilliseconds(150) });
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Received_WithNothingToSend_IsAcknowledgedBySeparateRrNoSoonerThanTheDelay()
    {
        using DeviceHarness timed = new(new HdlcPeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, AcknowledgeDelay = TimeSpan.FromMilliseconds(300), EnableMonitor = true });
        await using HdlcPeer peer = await timed.Connect();
        TestObserver<HdlcFrame> received = new();
        TestObserver<HdlcFrame> sent = new();
        peer.Monitored.Subscribe(received);
        peer.Transmitted.Subscribe(sent);

        timed.Receive(timed.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1 }));
        HdlcFrame inbound = await received.Next();
        HdlcFrame acknowledgement = await sent.Next();

        Assert.Equal(HdlcFrameKind.ReceiveReady, acknowledgement.Kind);
        Assert.Equal(1, acknowledgement.ReceiveSequence);
        Assert.True(acknowledgement.Timestamp - inbound.Timestamp >= TimeSpan.FromMilliseconds(250), $"The acknowledgement followed after {acknowledgement.Timestamp - inbound.Timestamp}.");
    }

    [Fact]
    public async Task Received_WhenAnInformationFrameIsSentWithinTheDelay_IsAcknowledgedByThatFrameAndNoRrFollows()
    {
        using DeviceHarness slow = new(new HdlcPeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, AcknowledgeDelay = TimeSpan.FromSeconds(1) });
        await using HdlcPeer peer = await slow.Connect();
        SemaphoreSlim delivered = new(0);
        peer.Receiver = owner =>
        {
            owner.Dispose();
            delivered.Release();
        };

        slow.Receive(slow.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1 }));
        await delivered.WaitAsync(timeout);
        await peer.Send(new byte[] { 2 });
        HdlcWireFrame sent = await slow.NextWritten(1);
        await Task.Delay(1500);

        Assert.Equal(HdlcWireFrameKind.Information, sent.Kind);
        Assert.Equal(1, sent.ReceiveSequence);
        Assert.Equal(2, slow.Written.Count);
    }

    [Fact]
    public async Task Received_WithAZeroDelay_IsAcknowledgedImmediately()
    {
        using DeviceHarness immediate = new(new HdlcPeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, AcknowledgeDelay = TimeSpan.Zero });
        await using HdlcPeer peer = await immediate.Connect();

        immediate.Receive(immediate.Peer(HdlcWireFrameKind.Information, false, 0, new byte[] { 1 }));

        Assert.Equal(HdlcWireFrameKind.ReceiveReady, (await immediate.NextWritten(1)).Kind);
    }

    [Fact]
    public async Task Start_WithANegativeAcknowledgeDelay_Throws()
    {
        await using HdlcPeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.Start("port", harness.Options with { AcknowledgeDelay = TimeSpan.FromMilliseconds(-1) }));

        Assert.Equal(HdlcPeerState.Idle, peer.State);
    }
}
