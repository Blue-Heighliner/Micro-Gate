namespace BlueHeighliner.MicroGate;

public sealed class HdlcStateMachineTests
{
    private static HdlcStationOptions Options(bool disablePollFinal = false) =>
        new() { Address = 0x11, DisablePollFinalBit = disablePollFinal };

    private static (HdlcStateMachine Local, HdlcStateMachine Remote) EstablishConnectedPair(HdlcStationOptions options)
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
        HdlcStationOptions options = Options();
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
}
