namespace BlueHeighliner.MicroGate;

public sealed class HdlcStateMachineTests
{
    private MicroGateConnectionOptions Options(bool disablePollFinal = false) =>
        new() { Address = 0x11, DisablePollFinalBit = disablePollFinal };

    private (HdlcStateMachine Local, HdlcStateMachine Remote) EstablishConnectedPair(MicroGateConnectionOptions options)
    {
        HdlcStateMachine local = new(options);
        HdlcStateMachine remote = new(options);

        HdlcReceiveResult remoteAfterConnect = remote.Receive(local.CreateConnect());
        local.Receive(remoteAfterConnect.Response!.Value);

        return (local, remote);
    }

    [Fact]
    public void CreateConnect_TransitionsToConnecting_AndProducesSabmFrame()
    {
        HdlcStateMachine machine = new(Options());

        ReadOnlyMemory<byte> bytes = machine.CreateConnect();
        HdlcFrame frame = HdlcFrame.Parse(bytes.Span);

        Assert.Equal(HdlcConnectionState.Connecting, machine.State);
        Assert.Equal(HdlcFrameKind.SetAsynchronousBalancedMode, frame.Kind);
        Assert.True(frame.PollFinal);
        Assert.Equal(0x11, frame.Address);
    }

    [Fact]
    public void Receive_Sabm_TransitionsToConnected_AndRespondsWithMirroredUa()
    {
        HdlcStateMachine machine = new(Options());
        HdlcFrame sabm = new() { Address = 0x11, Kind = HdlcFrameKind.SetAsynchronousBalancedMode, PollFinal = true };

        HdlcReceiveResult result = machine.Receive(sabm.ToArray());

        Assert.Equal(HdlcConnectionState.Connected, result.State);
        Assert.Equal(HdlcConnectionState.Connected, machine.State);
        Assert.NotNull(result.Response);
        HdlcFrame response = HdlcFrame.Parse(result.Response!.Value.Span);
        Assert.Equal(HdlcFrameKind.UnnumberedAcknowledge, response.Kind);
        Assert.True(response.PollFinal);
    }

    [Fact]
    public void DisablePollFinalBit_NeverSetsPollFinalBit()
    {
        HdlcStateMachine machine = new(Options(disablePollFinal: true));

        ReadOnlyMemory<byte> connectBytes = machine.CreateConnect();
        Assert.False(HdlcFrame.Parse(connectBytes.Span).PollFinal);

        HdlcFrame sabm = new() { Address = 0x11, Kind = HdlcFrameKind.SetAsynchronousBalancedMode, PollFinal = true };
        HdlcReceiveResult result = machine.Receive(sabm.ToArray());
        Assert.False(HdlcFrame.Parse(result.Response!.Value.Span).PollFinal);
    }

    [Fact]
    public void Receive_WrongAddress_IsIgnored()
    {
        HdlcStateMachine machine = new(Options());
        HdlcFrame sabm = new() { Address = 0x22, Kind = HdlcFrameKind.SetAsynchronousBalancedMode, PollFinal = true };

        HdlcReceiveResult result = machine.Receive(sabm.ToArray());

        Assert.Equal(HdlcConnectionState.Disconnected, result.State);
        Assert.Null(result.Response);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void CreateInformation_WhileDisconnected_Throws()
    {
        HdlcStateMachine machine = new(Options());

        Assert.Throws<InvalidOperationException>(() => machine.CreateInformation(new byte[] { 1 }));
    }

    [Fact]
    public void FullHandshake_ThenInformationExchange_DeliversPayloadBothWays()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        Assert.Equal(HdlcConnectionState.Connected, local.State);
        Assert.Equal(HdlcConnectionState.Connected, remote.State);

        byte[] payload = [10, 20, 30];
        ReadOnlyMemory<byte> information = local.CreateInformation(payload);
        HdlcReceiveResult remoteAfterInformation = remote.Receive(information);

        Assert.Equal(payload, remoteAfterInformation.Payload!.Value.ToArray());
        Assert.NotNull(remoteAfterInformation.Response);

        HdlcReceiveResult localAfterAck = local.Receive(remoteAfterInformation.Response!.Value);
        Assert.Null(localAfterAck.Payload);
        Assert.Equal(HdlcConnectionState.Connected, localAfterAck.State);
    }

    [Fact]
    public void Receive_InformationWithUnexpectedSequence_RespondsWithReject()
    {
        MicroGateConnectionOptions options = Options();
        (_, HdlcStateMachine remote) = EstablishConnectedPair(options);

        HdlcFrame outOfOrder = new()
        {
            Address = options.Address,
            Kind = HdlcFrameKind.Information,
            PollFinal = false,
            SendSequence = 5,
            ReceiveSequence = 0,
            Payload = new byte[] { 1 },
        };

        HdlcReceiveResult result = remote.Receive(outOfOrder.ToArray());

        Assert.Null(result.Payload);
        Assert.NotNull(result.Response);
        Assert.Equal(HdlcFrameKind.Reject, HdlcFrame.Parse(result.Response!.Value.Span).Kind);
    }

    [Fact]
    public void CreateDisconnect_ThenReceiveUa_TransitionsToDisconnected()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = EstablishConnectedPair(Options());

        ReadOnlyMemory<byte> disconnect = local.CreateDisconnect();
        Assert.Equal(HdlcConnectionState.Disconnecting, local.State);

        HdlcReceiveResult remoteAfterDisconnect = remote.Receive(disconnect);
        Assert.Equal(HdlcConnectionState.Disconnected, remoteAfterDisconnect.State);

        HdlcReceiveResult localAfterUa = local.Receive(remoteAfterDisconnect.Response!.Value);
        Assert.Equal(HdlcConnectionState.Disconnected, localAfterUa.State);
    }

    [Fact]
    public void DisablePollFinalBit_KeepsPollFinalZeroWhenPeerPolls()
    {
        MicroGateConnectionOptions disabled = Options(disablePollFinal: true);
        (HdlcStateMachine local, HdlcStateMachine remote) = EstablishConnectedPair(disabled);
        HdlcFrame polled = new()
        {
            Address = disabled.Address,
            Kind = HdlcFrameKind.Information,
            PollFinal = true,
            SendSequence = 0,
            Payload = new byte[] { 1 },
        };

        HdlcReceiveResult result = remote.Receive(polled.ToArray());

        Assert.False(HdlcFrame.Parse(result.Response!.Value.Span).PollFinal);
        Assert.False(HdlcFrame.Parse(local.CreateInformation(new byte[] { 2 }).Span).PollFinal);
    }

    [Fact]
    public void Receive_DisconnectedMode_WhileConnected_TransitionsToDisconnectedWithoutResponse()
    {
        (_, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        HdlcFrame frame = new() { Address = 0x11, Kind = HdlcFrameKind.DisconnectedMode, PollFinal = false };

        HdlcReceiveResult result = remote.Receive(frame.ToArray());

        Assert.Equal(HdlcConnectionState.Disconnected, result.State);
        Assert.Null(result.Response);
    }

    [Fact]
    public void Receive_FrameReject_WhileConnected_TransitionsToDisconnected()
    {
        (_, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        HdlcFrame frame = new() { Address = 0x11, Kind = HdlcFrameKind.FrameReject, PollFinal = false };

        Assert.Equal(HdlcConnectionState.Disconnected, remote.Receive(frame.ToArray()).State);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Receive_SupervisoryFrame_IsObservedWithoutStateChangeOrResponse(int kindIndex)
    {
        HdlcFrameKind[] kinds = [HdlcFrameKind.ReceiveReady, HdlcFrameKind.ReceiveNotReady, HdlcFrameKind.Reject];
        (_, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        HdlcFrame frame = new() { Address = 0x11, Kind = kinds[kindIndex - 1], PollFinal = false, ReceiveSequence = 0 };

        HdlcReceiveResult result = remote.Receive(frame.ToArray());

        Assert.Equal(HdlcConnectionState.Connected, result.State);
        Assert.Null(result.Response);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void Receive_InformationWhileDisconnected_IsIgnored()
    {
        HdlcStateMachine machine = new(Options());
        HdlcFrame frame = new() { Address = 0x11, Kind = HdlcFrameKind.Information, PollFinal = false, Payload = new byte[] { 1 } };

        HdlcReceiveResult result = machine.Receive(frame.ToArray());

        Assert.Equal(HdlcConnectionState.Disconnected, result.State);
        Assert.Null(result.Payload);
        Assert.Null(result.Response);
    }

    [Fact]
    public void Receive_UnsolicitedUa_WhileConnected_LeavesStateConnected()
    {
        (_, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        HdlcFrame frame = new() { Address = 0x11, Kind = HdlcFrameKind.UnnumberedAcknowledge, PollFinal = true };

        Assert.Equal(HdlcConnectionState.Connected, remote.Receive(frame.ToArray()).State);
    }

    [Fact]
    public void Receive_UaWhileDisconnected_LeavesStateDisconnected()
    {
        HdlcStateMachine machine = new(Options());
        HdlcFrame frame = new() { Address = 0x11, Kind = HdlcFrameKind.UnnumberedAcknowledge, PollFinal = true };

        Assert.Equal(HdlcConnectionState.Disconnected, machine.Receive(frame.ToArray()).State);
    }

    [Fact]
    public void Receive_MalformedFrame_Throws()
    {
        HdlcStateMachine machine = new(Options());

        Assert.Throws<HdlcFrameException>(() => machine.Receive(new byte[] { 0x11 }));
    }

    [Fact]
    public void CreateInformation_SequenceWrapsModuloEight()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair(Options());

        int last = -1;
        for (int i = 0; i < 9; i++)
        {
            last = HdlcFrame.Parse(local.CreateInformation(new byte[] { 1 }).Span).SendSequence;
        }

        Assert.Equal(0, last);
    }

    [Fact]
    public void Receive_InformationSequenceWrapsModuloEight()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = EstablishConnectedPair(Options());

        HdlcReceiveResult result = new() { State = HdlcConnectionState.Connected };
        for (int i = 0; i < 9; i++)
        {
            result = remote.Receive(local.CreateInformation(new byte[] { (byte)i }));
        }

        Assert.Equal(new byte[] { 8 }, result.Payload!.Value.ToArray());
        Assert.Equal(1, HdlcFrame.Parse(result.Response!.Value.Span).ReceiveSequence);
    }

    [Fact]
    public void Receive_SabmWhileConnected_ResetsSequencesAndRespondsWithUa()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        remote.Receive(local.CreateInformation(new byte[] { 1 }));
        HdlcFrame sabm = new() { Address = 0x11, Kind = HdlcFrameKind.SetAsynchronousBalancedMode, PollFinal = true };

        HdlcReceiveResult result = remote.Receive(sabm.ToArray());
        HdlcFrame information = new() { Address = 0x11, Kind = HdlcFrameKind.Information, PollFinal = false, SendSequence = 0, Payload = new byte[] { 2 } };
        HdlcReceiveResult afterReset = remote.Receive(information.ToArray());

        Assert.Equal(HdlcFrameKind.UnnumberedAcknowledge, HdlcFrame.Parse(result.Response!.Value.Span).Kind);
        Assert.Equal(new byte[] { 2 }, afterReset.Payload!.Value.ToArray());
    }
}
