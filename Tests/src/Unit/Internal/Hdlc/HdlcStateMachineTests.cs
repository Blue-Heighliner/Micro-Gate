namespace BlueHeighliner.MicroGate;

public sealed class HdlcStateMachineTests
{
    private HdlcPeerOptions Options(bool disablePollFinal = false) =>
        new() { DisablePollFinalBit = disablePollFinal, AcknowledgeDelay = TimeSpan.Zero };

    private (HdlcStateMachine Local, HdlcStateMachine Remote) EstablishConnectedPair(HdlcPeerOptions options)
    {
        HdlcStateMachine local = new(options, 0x11, 0x12);
        HdlcStateMachine remote = new(options, 0x12, 0x11);

        HdlcReceiveResult remoteAfterConnect = remote.Receive(local.CreateConnect());
        local.Receive(remoteAfterConnect.Response!.Value);

        return (local, remote);
    }

    [Fact]
    public void CreateConnect_TransitionsToConnecting_AndProducesSabmFrame()
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);

        ReadOnlyMemory<byte> bytes = machine.CreateConnect();
        HdlcWireFrame frame = HdlcWireFrame.Parse(bytes);

        Assert.Equal(HdlcConnectionState.Connecting, machine.State);
        Assert.Equal(HdlcWireFrameKind.SetAsynchronousBalancedMode, frame.Kind);
        Assert.False(frame.PollFinal);
        Assert.Equal(0xFF, frame.Address);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Receive_SabmToTheBroadcastAddress_ConnectsAndAnswersWithTheLocalAddress(bool poll)
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);

        HdlcReceiveResult result = machine.Receive(new HdlcWireFrame { Address = 0xFF, Kind = HdlcWireFrameKind.SetAsynchronousBalancedMode, PollFinal = poll }.ToArray());

        Assert.Equal(HdlcConnectionState.Connected, machine.State);
        HdlcWireFrame ua = HdlcWireFrame.Parse(result.Response!.Value);
        Assert.Equal(HdlcWireFrameKind.UnnumberedAcknowledge, ua.Kind);
        Assert.Equal(0x11, ua.Address);
        Assert.Equal(poll, ua.PollFinal);
    }

    [Fact]
    public void Receive_DisconnectToTheBroadcastAddress_DisconnectsAndAnswersWithTheLocalAddress()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair(Options());

        HdlcReceiveResult result = local.Receive(new HdlcWireFrame { Address = 0xFF, Kind = HdlcWireFrameKind.Disconnect, PollFinal = false }.ToArray());

        Assert.Equal(HdlcConnectionState.Disconnected, local.State);
        HdlcWireFrame answer = HdlcWireFrame.Parse(result.Response!.Value);
        Assert.Equal(HdlcWireFrameKind.UnnumberedAcknowledge, answer.Kind);
        Assert.Equal(0x11, answer.Address);
    }

    [Fact]
    public void Receive_AnInformationFrameToTheBroadcastAddress_IsAcceptedAndAnsweredWithTheLocalAddress()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair(Options());

        HdlcReceiveResult result = local.Receive(new HdlcWireFrame { Address = 0xFF, Kind = HdlcWireFrameKind.Information, PollFinal = false, SendSequence = 0, ReceiveSequence = 0, Payload = new byte[] { 1 } }.ToArray());

        Assert.Equal(new byte[] { 1 }, result.Payload!.Value.ToArray());
        Assert.Equal(0x11, HdlcWireFrame.Parse(result.Response!.Value).Address);
    }

    [Fact]
    public void Receive_APollToTheBroadcastAddress_IsAnsweredWithAFinalResponseCarryingTheLocalAddress()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair(Options());

        HdlcReceiveResult result = local.Receive(new HdlcWireFrame { Address = 0xFF, Kind = HdlcWireFrameKind.ReceiveReady, PollFinal = true, ReceiveSequence = 0 }.ToArray());

        HdlcWireFrame answer = HdlcWireFrame.Parse(result.Response!.Value);
        Assert.Equal(0x11, answer.Address);
        Assert.True(answer.PollFinal);
    }

    [Fact]
    public void Receive_AResponseToTheBroadcastAddress_IsNotTreatedAsAnAnswer()
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);
        machine.CreateConnect();

        machine.Receive(new HdlcWireFrame { Address = 0xFF, Kind = HdlcWireFrameKind.UnnumberedAcknowledge, PollFinal = false }.ToArray());

        Assert.Equal(HdlcConnectionState.Connecting, machine.State);
    }

    [Fact]
    public void Receive_Sabm_TransitionsToConnected_AndRespondsWithMirroredUa()
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);
        HdlcWireFrame sabm = new() { Address = 0x11, Kind = HdlcWireFrameKind.SetAsynchronousBalancedMode, PollFinal = true };

        HdlcReceiveResult result = machine.Receive(sabm.ToArray());

        Assert.Equal(HdlcConnectionState.Connected, result.State);
        Assert.Equal(HdlcConnectionState.Connected, machine.State);
        Assert.NotNull(result.Response);
        HdlcWireFrame response = HdlcWireFrame.Parse(result.Response!.Value);
        Assert.Equal(HdlcWireFrameKind.UnnumberedAcknowledge, response.Kind);
        Assert.True(response.PollFinal);
    }

    [Fact]
    public void DisablePollFinalBit_NeverSetsPollFinalBit()
    {
        HdlcStateMachine machine = new(Options(disablePollFinal: true), 0x11, 0x12);

        ReadOnlyMemory<byte> connectBytes = machine.CreateConnect();
        Assert.False(HdlcWireFrame.Parse(connectBytes).PollFinal);

        HdlcWireFrame sabm = new() { Address = 0x11, Kind = HdlcWireFrameKind.SetAsynchronousBalancedMode, PollFinal = true };
        HdlcReceiveResult result = machine.Receive(sabm.ToArray());
        Assert.False(HdlcWireFrame.Parse(result.Response!.Value).PollFinal);
    }

    [Fact]
    public void Receive_WrongAddress_IsIgnored()
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);
        HdlcWireFrame sabm = new() { Address = 0x22, Kind = HdlcWireFrameKind.SetAsynchronousBalancedMode, PollFinal = true };

        HdlcReceiveResult result = machine.Receive(sabm.ToArray());

        Assert.Equal(HdlcConnectionState.Disconnected, result.State);
        Assert.Null(result.Response);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void CreateInformation_WhileDisconnected_Throws()
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);

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
        HdlcPeerOptions options = Options();
        (_, HdlcStateMachine remote) = EstablishConnectedPair(options);

        HdlcWireFrame outOfOrder = new()
        {
            Address = 0x12,
            Kind = HdlcWireFrameKind.Information,
            PollFinal = false,
            SendSequence = 5,
            ReceiveSequence = 0,
            Payload = new byte[] { 1 },
        };

        HdlcReceiveResult result = remote.Receive(outOfOrder.ToArray());

        Assert.Null(result.Payload);
        Assert.NotNull(result.Response);
        Assert.Equal(HdlcWireFrameKind.Reject, HdlcWireFrame.Parse(result.Response!.Value).Kind);
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
        HdlcPeerOptions disabled = Options(disablePollFinal: true);
        (HdlcStateMachine local, HdlcStateMachine remote) = EstablishConnectedPair(disabled);
        HdlcWireFrame polled = new()
        {
            Address = 0x12,
            Kind = HdlcWireFrameKind.Information,
            PollFinal = true,
            SendSequence = 0,
            Payload = new byte[] { 1 },
        };

        HdlcReceiveResult result = remote.Receive(polled.ToArray());

        Assert.False(HdlcWireFrame.Parse(result.Response!.Value).PollFinal);
        Assert.False(HdlcWireFrame.Parse(local.CreateInformation(new byte[] { 2 })).PollFinal);
    }

    [Fact]
    public void Receive_DisconnectedMode_WhileConnected_TransitionsToDisconnectedWithoutResponse()
    {
        (_, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        HdlcWireFrame frame = new() { Address = 0x11, Kind = HdlcWireFrameKind.DisconnectedMode, PollFinal = false };

        HdlcReceiveResult result = remote.Receive(frame.ToArray());

        Assert.Equal(HdlcConnectionState.Disconnected, result.State);
        Assert.Null(result.Response);
    }

    [Fact]
    public void Receive_FrameReject_WhileConnected_TransitionsToDisconnected()
    {
        (_, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        HdlcWireFrame frame = new() { Address = 0x11, Kind = HdlcWireFrameKind.FrameReject, PollFinal = false };

        Assert.Equal(HdlcConnectionState.Disconnected, remote.Receive(frame.ToArray()).State);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Receive_SupervisoryFrame_IsObservedWithoutStateChangeOrResponse(int kindIndex)
    {
        HdlcWireFrameKind[] kinds = [HdlcWireFrameKind.ReceiveReady, HdlcWireFrameKind.ReceiveNotReady, HdlcWireFrameKind.Reject];
        (_, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        HdlcWireFrame frame = new() { Address = 0x11, Kind = kinds[kindIndex - 1], PollFinal = false, ReceiveSequence = 0 };

        HdlcReceiveResult result = remote.Receive(frame.ToArray());

        Assert.Equal(HdlcConnectionState.Connected, result.State);
        Assert.Null(result.Response);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void Receive_InformationWhileDisconnected_IsNotDeliveredAndAnsweredWithDisconnectedMode()
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);
        HdlcWireFrame frame = new() { Address = 0x11, Kind = HdlcWireFrameKind.Information, PollFinal = true, Payload = new byte[] { 1 } };

        HdlcReceiveResult result = machine.Receive(frame.ToArray());

        Assert.Equal(HdlcConnectionState.Disconnected, result.State);
        Assert.Null(result.Payload);
        Assert.Equal(HdlcWireFrameKind.DisconnectedMode, HdlcWireFrame.Parse(result.Response!.Value).Kind);
        Assert.True(HdlcWireFrame.Parse(result.Response!.Value).PollFinal);
    }

    [Fact]
    public void Receive_DisconnectWhileDisconnected_IsAnsweredWithDisconnectedModeNotUa()
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);
        HdlcWireFrame frame = new() { Address = 0x11, Kind = HdlcWireFrameKind.Disconnect, PollFinal = true };

        HdlcReceiveResult result = machine.Receive(frame.ToArray());

        Assert.Equal(HdlcWireFrameKind.DisconnectedMode, HdlcWireFrame.Parse(result.Response!.Value).Kind);
    }

    [Theory]
    [InlineData(0x83)]
    [InlineData(0xCF)]
    [InlineData(0x6F)]
    [InlineData(0x4F)]
    public void Receive_UnsupportedModeSettingCommand_IsAnsweredWithDisconnectedMode(byte control)
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);

        HdlcReceiveResult result = machine.Receive(new byte[] { 0x11, (byte)(control | 0x10) });

        HdlcWireFrame answer = HdlcWireFrame.Parse(result.Response!.Value);
        Assert.Equal(HdlcWireFrameKind.DisconnectedMode, answer.Kind);
        Assert.True(answer.PollFinal);
        Assert.Equal(HdlcConnectionState.Disconnected, result.State);
    }

    [Fact]
    public void Receive_TestCommand_IsEchoedAsATestResponse()
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);

        HdlcReceiveResult result = machine.Receive(new byte[] { 0x11, 0xF3, 1, 2, 3 });

        Assert.Equal(new byte[] { 0x11, 0xF3, 1, 2, 3 }, result.Response!.Value.ToArray());
    }

    [Theory]
    [InlineData(0x03)]
    [InlineData(0xAF)]
    public void Receive_UnnumberedInformationOrIdentification_IsIgnored(byte control)
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);

        HdlcReceiveResult result = machine.Receive(new byte[] { 0x11, control, 9 });

        Assert.Null(result.Response);
    }

    [Fact]
    public void Receive_UndefinedControlField_IsAnsweredWithFrameRejectNamingIt()
    {
        (_, HdlcStateMachine remote) = EstablishConnectedPair(Options());

        HdlcReceiveResult result = remote.Receive(new byte[] { 0x12, 0x0D });

        HdlcWireFrame answer = HdlcWireFrame.Parse(result.Response!.Value);
        Assert.Equal(HdlcWireFrameKind.FrameReject, answer.Kind);
        Assert.Equal(new byte[] { 0x0D, 0x00, 0x01 }, answer.Payload.ToArray());
        Assert.Equal(HdlcConnectionState.Connected, result.State);
    }

    [Fact]
    public void Receive_FrameForAnotherAddress_GetsNoResponseEvenWhenUndefined()
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);

        HdlcReceiveResult result = machine.Receive(new byte[] { 0x22, 0x0D });

        Assert.Null(result.Response);
    }

    [Fact]
    public void Receive_ReceiveNotReady_MarksPeerBusyUntilItSendsAnythingElse()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        HdlcWireFrame notReady = new() { Address = 0x12, Kind = HdlcWireFrameKind.ReceiveNotReady, PollFinal = false, ReceiveSequence = 0 };
        HdlcWireFrame ready = new() { Address = 0x12, Kind = HdlcWireFrameKind.ReceiveReady, PollFinal = false, ReceiveSequence = 0 };

        local.Receive(notReady.ToArray());
        Assert.True(local.PeerBusy);

        local.Receive(ready.ToArray());
        Assert.False(local.PeerBusy);
        Assert.False(remote.PeerBusy);
    }

    [Fact]
    public void Addressing_ConnectRequestsGoToEveryoneAndOtherCommandsToTheRemoteAddressAndResponsesCarryTheLocalOne()
    {
        HdlcStateMachine stationA = new(Options(), 0x01, 0x03);
        HdlcStateMachine stationB = new(Options(), 0x03, 0x01);

        ReadOnlyMemory<byte> sabm = stationA.CreateConnect();
        Assert.Equal(0xFF, HdlcWireFrame.Parse(sabm).Address);

        HdlcReceiveResult atB = stationB.Receive(sabm);
        HdlcWireFrame ua = HdlcWireFrame.Parse(atB.Response!.Value);
        Assert.Equal(0x03, ua.Address);
        Assert.Equal(HdlcWireFrameKind.UnnumberedAcknowledge, ua.Kind);

        stationA.Receive(atB.Response!.Value);
        Assert.Equal(HdlcConnectionState.Connected, stationA.State);

        HdlcWireFrame information = HdlcWireFrame.Parse(stationA.CreateInformation(new byte[] { 5 }));
        Assert.Equal(0x03, information.Address);

        HdlcReceiveResult delivered = stationB.Receive(stationA.CreateRetransmission()[0]);
        Assert.Equal(new byte[] { 5 }, delivered.Payload!.Value.ToArray());
        Assert.Equal(0x03, HdlcWireFrame.Parse(delivered.Response!.Value).Address);
    }

    [Fact]
    public void Addressing_CommandsAddressedToTheWrongStationAreIgnored()
    {
        HdlcStateMachine stationB = new(new(), 0x03, 0x01);
        HdlcWireFrame sabmForA = new() { Address = 0x01, Kind = HdlcWireFrameKind.SetAsynchronousBalancedMode, PollFinal = true };

        HdlcReceiveResult result = stationB.Receive(sabmForA.ToArray());

        Assert.Equal(HdlcConnectionState.Disconnected, result.State);
        Assert.Null(result.Response);
    }

    [Fact]
    public void Addressing_PolledSupervisoryCommandIsAnsweredWithFinalReceiveReady()
    {
        HdlcStateMachine stationA = new(new() { DisablePollFinalBit = false }, 0x01, 0x03);
        HdlcStateMachine stationB = new(new() { DisablePollFinalBit = false }, 0x03, 0x01);
        stationA.Receive(stationB.Receive(stationA.CreateConnect()).Response!.Value);
        HdlcWireFrame poll = new() { Address = 0x03, Kind = HdlcWireFrameKind.ReceiveReady, PollFinal = true, ReceiveSequence = 0 };

        HdlcReceiveResult result = stationB.Receive(poll.ToArray());

        HdlcWireFrame answer = HdlcWireFrame.Parse(result.Response!.Value);
        Assert.Equal(HdlcWireFrameKind.ReceiveReady, answer.Kind);
        Assert.True(answer.PollFinal);
        Assert.Equal(0x03, answer.Address);
    }

    [Fact]
    public void Receive_UnsolicitedUa_WhileConnected_LeavesStateConnected()
    {
        (_, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        HdlcWireFrame frame = new() { Address = 0x12, Kind = HdlcWireFrameKind.UnnumberedAcknowledge, PollFinal = true };

        Assert.Equal(HdlcConnectionState.Connected, remote.Receive(frame.ToArray()).State);
    }

    [Fact]
    public void Receive_UaWhileDisconnected_LeavesStateDisconnected()
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);
        HdlcWireFrame frame = new() { Address = 0x12, Kind = HdlcWireFrameKind.UnnumberedAcknowledge, PollFinal = true };

        Assert.Equal(HdlcConnectionState.Disconnected, machine.Receive(frame.ToArray()).State);
    }

    [Fact]
    public void Receive_MalformedFrame_Throws()
    {
        HdlcStateMachine machine = new(Options(), 0x11, 0x12);

        Assert.Throws<HdlcWireFrameException>(() => machine.Receive(new byte[] { 0x11 }));
    }

    [Fact]
    public void CreateInformation_SequenceWrapsModuloEightWhenAcknowledged()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = EstablishConnectedPair(Options());

        int last = -1;
        for (int i = 0; i < 9; i++)
        {
            ReadOnlyMemory<byte> information = local.CreateInformation(new byte[] { 1 });
            last = HdlcWireFrame.Parse(information).SendSequence;
            local.Receive(remote.Receive(information).Response!.Value);
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
            local.Receive(result.Response!.Value);
        }

        Assert.Equal(new byte[] { 8 }, result.Payload!.Value.ToArray());
        Assert.Equal(1, HdlcWireFrame.Parse(result.Response!.Value).ReceiveSequence);
    }

    [Fact]
    public void Receive_SabmWhileConnected_ResetsSequencesAndRespondsWithUa()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = EstablishConnectedPair(Options());
        remote.Receive(local.CreateInformation(new byte[] { 1 }));
        HdlcWireFrame sabm = new() { Address = 0x12, Kind = HdlcWireFrameKind.SetAsynchronousBalancedMode, PollFinal = true };

        HdlcReceiveResult result = remote.Receive(sabm.ToArray());
        HdlcWireFrame information = new() { Address = 0x12, Kind = HdlcWireFrameKind.Information, PollFinal = false, SendSequence = 0, Payload = new byte[] { 2 } };
        HdlcReceiveResult afterReset = remote.Receive(information.ToArray());

        Assert.Equal(HdlcWireFrameKind.UnnumberedAcknowledge, HdlcWireFrame.Parse(result.Response!.Value).Kind);
        Assert.Equal(new byte[] { 2 }, afterReset.Payload!.Value.ToArray());
    }
}
