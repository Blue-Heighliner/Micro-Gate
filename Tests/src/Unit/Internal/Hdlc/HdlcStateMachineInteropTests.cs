namespace BlueHeighliner.MicroGate;

public sealed class HdlcStateMachineInteropTests
{
    private readonly byte dceAddress = 19;
    private readonly byte dteAddress = 45;

    [Fact]
    public void AsTheDte_ReproducesTheCapturedExchange()
    {
        using HdlcStateMachine dte = new(new MicroGatePeerOptions(), dteAddress, dceAddress);

        Assert.Equal([255, 47], dte.CreateConnect().ToArray());

        Assert.Null(dte.Receive(new byte[] { 19, 99 }).Response);
        Assert.Equal(HdlcConnectionState.Connected, dte.State);

        HdlcReceiveResult firstData = dte.Receive(new byte[] { 45, 0, 0xAA });
        Assert.Equal(new byte[] { 0xAA }, firstData.Payload!.Value.ToArray());
        Assert.Null(firstData.Response);
        Assert.True(dte.AcknowledgementPending);
        Assert.Equal([45, 33], dte.CreateAcknowledgement().ToArray());

        Assert.Equal([19, 32, 0xBB], dte.CreateInformation(new byte[] { 0xBB }).ToArray());

        HdlcReceiveResult secondData = dte.Receive(new byte[] { 45, 34, 0xCC });
        Assert.Equal(1, secondData.Acknowledged);
        Assert.Equal(new byte[] { 0xCC }, secondData.Payload!.Value.ToArray());
        Assert.Null(secondData.Response);

        Assert.Equal([19, 66, 0xDD], dte.CreateInformation(new byte[] { 0xDD }).ToArray());
        Assert.False(dte.AcknowledgementPending);
        Assert.True(dte.CreateAcknowledgement().IsEmpty);

        Assert.Equal([[19, 81]], dte.CreateTimeoutRecovery().Select(frame => frame.ToArray()));
        HdlcReceiveResult answer = dte.Receive(new byte[] { 19, 81 });
        Assert.Equal(1, answer.Acknowledged);
        Assert.False(answer.Retransmit);
        Assert.Equal(0, dte.OutstandingCount);
    }

    [Fact]
    public void AsTheDce_ReproducesTheCapturedExchange()
    {
        using HdlcStateMachine dce = new(new MicroGatePeerOptions(), dceAddress, dteAddress);

        HdlcReceiveResult connected = dce.Receive(new byte[] { 255, 47 });
        Assert.Equal(HdlcConnectionState.Connected, dce.State);
        Assert.Equal([19, 99], connected.Response!.Value.ToArray());

        Assert.Equal([45, 0, 0xAA], dce.CreateInformation(new byte[] { 0xAA }).ToArray());

        HdlcReceiveResult acknowledgement = dce.Receive(new byte[] { 45, 33 });
        Assert.Equal(1, acknowledgement.Acknowledged);
        Assert.Null(acknowledgement.Response);

        HdlcReceiveResult firstData = dce.Receive(new byte[] { 19, 32, 0xBB });
        Assert.Equal(new byte[] { 0xBB }, firstData.Payload!.Value.ToArray());
        Assert.Null(firstData.Response);

        Assert.Equal([45, 34, 0xCC], dce.CreateInformation(new byte[] { 0xCC }).ToArray());
        Assert.False(dce.AcknowledgementPending);

        HdlcReceiveResult secondData = dce.Receive(new byte[] { 19, 66, 0xDD });
        Assert.Equal(1, secondData.Acknowledged);
        Assert.Equal(new byte[] { 0xDD }, secondData.Payload!.Value.ToArray());
        Assert.Null(secondData.Response);

        HdlcReceiveResult poll = dce.Receive(new byte[] { 19, 81 });
        Assert.Equal([19, 81], poll.Response!.Value.ToArray());
        Assert.False(dce.AcknowledgementPending);
    }

    [Fact]
    public void AsTheDte_WhenThePollAnswerShowsAFrameWasNotReceived_ResendsIt()
    {
        using HdlcStateMachine dte = new(new MicroGatePeerOptions(), dteAddress, dceAddress);
        dte.CreateConnect();
        dte.Receive(new byte[] { 19, 99 });
        dte.CreateInformation(new byte[] { 0xBB });

        dte.CreateTimeoutRecovery();
        HdlcReceiveResult answer = dte.Receive(new byte[] { 19, 0x11 });

        Assert.Equal(0, answer.Acknowledged);
        Assert.True(answer.Retransmit);
        Assert.Equal([[19, 0, 0xBB]], dte.CreateRetransmission().Select(frame => frame.ToArray()));
    }

    [Fact]
    public void AsTheDce_WithPollFinalDisabled_StillAnswersAPollWithAFinalResponse()
    {
        using HdlcStateMachine dce = new(new MicroGatePeerOptions { DisablePollFinalBit = true }, dceAddress, dteAddress);
        dce.Receive(new byte[] { 255, 47 });

        HdlcReceiveResult poll = dce.Receive(new byte[] { 19, 0x11 });

        Assert.Equal([19, 0x01], poll.Response!.Value.ToArray());
    }
}
