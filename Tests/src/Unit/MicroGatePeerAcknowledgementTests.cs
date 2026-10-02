namespace BlueHeighliner.MicroGate;

public sealed class MicroGatePeerAcknowledgementTests : IDisposable
{
    private readonly DeviceHarness harness = new(new MicroGatePeerOptions { RetryInterval = TimeSpan.FromMinutes(1), RetransmitInterval = null, AcknowledgeDelay = TimeSpan.FromMilliseconds(150) });
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Received_WithNothingToSend_IsAcknowledgedBySeparateRrAfterTheDelay()
    {
        await using MicroGatePeer peer = await harness.Connect();

        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));
        await Task.Delay(50);
        int beforeDelay = harness.Written.Count;
        HdlcFrame acknowledgement = await harness.NextWritten(1);

        Assert.Equal(1, beforeDelay);
        Assert.Equal(HdlcFrameKind.ReceiveReady, acknowledgement.Kind);
        Assert.Equal(1, acknowledgement.ReceiveSequence);
    }

    [Fact]
    public async Task Received_WhenAnInformationFrameIsSentWithinTheDelay_IsAcknowledgedByThatFrameAndNoRrFollows()
    {
        await using MicroGatePeer peer = await harness.Connect();

        harness.Receive(harness.Peer(HdlcFrameKind.Information, false, 0, new byte[] { 1 }));
        await Task.Delay(30);
        await peer.Send(new byte[] { 2 });
        HdlcFrame sent = await harness.NextWritten(1);
        await Task.Delay(400);

        Assert.Equal(HdlcFrameKind.Information, sent.Kind);
        Assert.Equal(1, sent.ReceiveSequence);
        Assert.Equal(2, harness.Written.Count);
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
