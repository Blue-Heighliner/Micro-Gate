namespace BlueHeighliner.MicroGate;

public sealed class HdlcStateMachineAcknowledgementTests
{
    private (HdlcStateMachine Local, HdlcStateMachine Remote) ConnectedPair(HdlcPeerOptions options)
    {
        HdlcStateMachine local = new(options, 0x11, 0x12);
        HdlcStateMachine remote = new(options, 0x12, 0x11);
        local.Receive(remote.Receive(local.CreateConnect()).Response!.Value);
        return (local, remote);
    }

    [Fact]
    public void Receive_InSequenceInformation_WaitsForTheNextInformationFrameToAcknowledge()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = ConnectedPair(new HdlcPeerOptions());

        HdlcReceiveResult delivered = remote.Receive(local.CreateInformation(new byte[] { 1 }));
        HdlcWireFrame piggybacked = HdlcWireFrame.Parse(remote.CreateInformation(new byte[] { 2 }));

        Assert.Null(delivered.Response);
        Assert.Equal(1, piggybacked.ReceiveSequence);
        Assert.False(remote.AcknowledgementPending);
        Assert.True(remote.CreateAcknowledgement().IsEmpty);
    }

    [Fact]
    public void CreateAcknowledgement_WhenNothingCarriedTheAcknowledgement_ProducesAnRrWithTheReceiveSequence()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = ConnectedPair(new HdlcPeerOptions());
        remote.Receive(local.CreateInformation(new byte[] { 1 }));

        HdlcWireFrame acknowledgement = HdlcWireFrame.Parse(remote.CreateAcknowledgement());

        Assert.Equal(HdlcWireFrameKind.ReceiveReady, acknowledgement.Kind);
        Assert.Equal(0x12, acknowledgement.Address);
        Assert.Equal(1, acknowledgement.ReceiveSequence);
        Assert.False(acknowledgement.PollFinal);
        Assert.False(remote.AcknowledgementPending);
    }

    [Fact]
    public void Receive_InformationWithThePollBitSet_IsAcknowledgedAtOnceWithAFinalResponse()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = ConnectedPair(new HdlcPeerOptions());
        HdlcWireFrame polled = new() { Address = 0x12, Kind = HdlcWireFrameKind.Information, PollFinal = true, SendSequence = 0, ReceiveSequence = 0, Payload = new byte[] { 1 } };

        HdlcReceiveResult result = remote.Receive(polled.ToArray());

        HdlcWireFrame answer = HdlcWireFrame.Parse(result.Response!.Value);
        Assert.Equal(HdlcWireFrameKind.ReceiveReady, answer.Kind);
        Assert.True(answer.PollFinal);
        Assert.False(remote.AcknowledgementPending);
        local.Dispose();
    }

    [Fact]
    public void Receive_AGapInTheSequence_IsRejectedAtOnce()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = ConnectedPair(new HdlcPeerOptions());
        local.CreateInformation(new byte[] { 1 });

        HdlcReceiveResult result = remote.Receive(local.CreateInformation(new byte[] { 2 }));

        Assert.Equal(HdlcWireFrameKind.Reject, HdlcWireFrame.Parse(result.Response!.Value).Kind);
    }

    [Fact]
    public void Receive_FourInformationFramesWaitingForAnAcknowledgement_AreAcknowledgedAtOnce()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = ConnectedPair(new HdlcPeerOptions());

        List<HdlcReceiveResult> results = [.. Enumerable.Range(0, 4).Select(i => remote.Receive(local.CreateInformation(new byte[] { (byte)i })))];

        Assert.All(results.Take(3), result => Assert.Null(result.Response));
        Assert.Equal(4, HdlcWireFrame.Parse(results[3].Response!.Value).ReceiveSequence);
        Assert.False(remote.AcknowledgementPending);
    }

    [Fact]
    public void Receive_WithAZeroAcknowledgeDelay_AcknowledgesEveryFrameAtOnce()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = ConnectedPair(new HdlcPeerOptions { AcknowledgeDelay = TimeSpan.Zero });

        HdlcReceiveResult result = remote.Receive(local.CreateInformation(new byte[] { 1 }));

        Assert.Equal(HdlcWireFrameKind.ReceiveReady, HdlcWireFrame.Parse(result.Response!.Value).Kind);
        Assert.False(remote.AcknowledgementPending);
    }

    [Fact]
    public void CreateRetransmission_CarriesTheCurrentReceiveSequenceSoItAcknowledgesToo()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = ConnectedPair(new HdlcPeerOptions());
        remote.CreateInformation(new byte[] { 9 });
        remote.Receive(local.CreateInformation(new byte[] { 1 }));

        HdlcWireFrame resent = HdlcWireFrame.Parse(remote.CreateRetransmission()[0]);

        Assert.Equal(1, resent.ReceiveSequence);
        Assert.False(remote.AcknowledgementPending);
    }
}
