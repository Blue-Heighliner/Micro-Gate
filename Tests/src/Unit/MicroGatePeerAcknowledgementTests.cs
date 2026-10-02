namespace BlueHeighliner.MicroGate;

public sealed class MicroGatePeerAcknowledgementTests : IDisposable
{
    private readonly DeviceHarness harness = new(new MicroGatePeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, AcknowledgeDelay = TimeSpan.FromMilliseconds(150) });
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Received_WithNothingToSend_IsAcknowledgedBySeparateRrNoSoonerThanTheDelay()
    {
        using DeviceHarness timed = new(new MicroGatePeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, AcknowledgeDelay = TimeSpan.FromMilliseconds(300), EnableMonitor = true });
        await using MicroGatePeer peer = await timed.Connect();
        TestObserver<MicroGateFrame> received = new();
        TestObserver<MicroGateFrame> sent = new();
        peer.Monitored.Subscribe(received);
        peer.Transmitted.Subscribe(sent);

        timed.Receive(timed.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));
        MicroGateFrame inbound = await received.Next();
        MicroGateFrame acknowledgement = await sent.Next();

        Assert.Equal(MicroGateFrameKind.ReceiveReady, acknowledgement.Kind);
        Assert.Equal(1, acknowledgement.ReceiveSequence);
        Assert.True(acknowledgement.Timestamp - inbound.Timestamp >= TimeSpan.FromMilliseconds(250), $"The acknowledgement followed after {acknowledgement.Timestamp - inbound.Timestamp}.");
    }

    [Fact]
    public async Task Received_WhenAnInformationFrameIsSentWithinTheDelay_IsAcknowledgedByThatFrameAndNoRrFollows()
    {
        using DeviceHarness slow = new(new MicroGatePeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, AcknowledgeDelay = TimeSpan.FromSeconds(1) });
        await using MicroGatePeer peer = await slow.Connect();
        SemaphoreSlim delivered = new(0);
        peer.Receiver = owner =>
        {
            owner.Dispose();
            delivered.Release();
        };

        slow.Receive(slow.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));
        await delivered.WaitAsync(timeout);
        await peer.Send(new byte[] { 2 });
        HdlcFrame sent = await slow.NextWritten(1);
        await Task.Delay(1500);

        Assert.Equal(HdlcFrameKind.Information, sent.Kind);
        Assert.Equal(1, sent.ReceiveSequence);
        Assert.Equal(2, slow.Written.Count);
    }

    [Fact]
    public async Task Received_WithAZeroDelay_IsAcknowledgedImmediately()
    {
        using DeviceHarness immediate = new(new MicroGatePeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, AcknowledgeDelay = TimeSpan.Zero });
        await using MicroGatePeer peer = await immediate.Connect();

        immediate.Receive(immediate.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));

        Assert.Equal(HdlcFrameKind.ReceiveReady, (await immediate.NextWritten(1)).Kind);
    }

    [Fact]
    public async Task Start_WithANegativeAcknowledgeDelay_Throws()
    {
        await using MicroGatePeer peer = harness.CreatePeer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await peer.Start("port", harness.Options with { AcknowledgeDelay = TimeSpan.FromMilliseconds(-1) }));

        Assert.Equal(MicroGatePeerState.Idle, peer.State);
    }
}
