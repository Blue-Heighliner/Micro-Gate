namespace BlueHeighliner.MicroGate;

public sealed class HdlcStateMachineReliabilityTests
{
    private readonly HdlcPeerOptions options = new() { AcknowledgeDelay = TimeSpan.Zero };

    private (HdlcStateMachine Local, HdlcStateMachine Remote) EstablishConnectedPair()
    {
        HdlcStateMachine local = new(options, 0x11, 0x12);
        HdlcStateMachine remote = new(options, 0x12, 0x11);
        local.Receive(remote.Receive(local.CreateConnect()).Response!.Value);
        return (local, remote);
    }

    private byte[] Frame(HdlcWireFrameKind kind, int receiveSequence = 0, int sendSequence = 0, byte[]? payload = null) =>
        new HdlcWireFrame { Address = kind is HdlcWireFrameKind.Information or HdlcWireFrameKind.SetAsynchronousBalancedMode or HdlcWireFrameKind.Disconnect ? (byte)0x11 : (byte)0x12, Kind = kind, PollFinal = false, ReceiveSequence = receiveSequence, SendSequence = sendSequence, Payload = payload ?? [] }.ToArray();

    [Fact]
    public void CreateInformation_KeepsFramesUntilWindowIsFull()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();

        for (int i = 0; i < local.WindowSize; i++)
        {
            local.CreateInformation(new byte[] { (byte)i });
        }

        Assert.Equal(7, local.WindowSize);
        Assert.Equal(7, local.OutstandingCount);
        Assert.Throws<InvalidOperationException>(() => local.CreateInformation(new byte[] { 9 }));
    }

    [Fact]
    public void Receive_ReceiveReady_AcknowledgesFramesBeforeItsSequence()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        for (int i = 0; i < 4; i++)
        {
            local.CreateInformation(new byte[] { (byte)i });
        }

        HdlcReceiveResult result = local.Receive(Frame(HdlcWireFrameKind.ReceiveReady, receiveSequence: 3));

        Assert.Equal(3, result.Acknowledged);
        Assert.Equal(1, local.OutstandingCount);
        Assert.False(result.Retransmit);
    }

    [Fact]
    public void Receive_ReceiveNotReady_AlsoAcknowledges()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        local.CreateInformation(new byte[] { 1 });

        HdlcReceiveResult result = local.Receive(Frame(HdlcWireFrameKind.ReceiveNotReady, receiveSequence: 1));

        Assert.Equal(1, result.Acknowledged);
        Assert.Equal(0, local.OutstandingCount);
    }

    [Fact]
    public void Receive_InformationFrame_AcknowledgesThroughItsReceiveSequence()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        local.CreateInformation(new byte[] { 1 });
        local.CreateInformation(new byte[] { 2 });

        HdlcReceiveResult result = local.Receive(Frame(HdlcWireFrameKind.Information, receiveSequence: 2, sendSequence: 0, payload: [7]));

        Assert.Equal(2, result.Acknowledged);
        Assert.Equal(new byte[] { 7 }, result.Payload!.Value.ToArray());
    }

    [Fact]
    public void Receive_AcknowledgementBeyondWhatWasSent_IsIgnored()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        local.CreateInformation(new byte[] { 1 });

        HdlcReceiveResult result = local.Receive(Frame(HdlcWireFrameKind.ReceiveReady, receiveSequence: 5));

        Assert.Equal(0, result.Acknowledged);
        Assert.Equal(1, local.OutstandingCount);
    }

    [Fact]
    public void Receive_Reject_AcknowledgesEarlierFramesAndAsksForTheRestToBeSentAgain()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        for (int i = 0; i < 3; i++)
        {
            local.CreateInformation(new byte[] { (byte)(10 + i) });
        }

        HdlcReceiveResult result = local.Receive(Frame(HdlcWireFrameKind.Reject, receiveSequence: 1));
        IReadOnlyList<ReadOnlyMemory<byte>> frames = local.CreateRetransmission();

        Assert.Equal(1, result.Acknowledged);
        Assert.True(result.Retransmit);
        Assert.Equal(2, frames.Count);
        HdlcWireFrame first = HdlcWireFrame.Parse(frames[0]);
        HdlcWireFrame second = HdlcWireFrame.Parse(frames[1]);
        Assert.Equal(HdlcWireFrameKind.Information, first.Kind);
        Assert.Equal(1, first.SendSequence);
        Assert.Equal(new byte[] { 11 }, first.Payload.ToArray());
        Assert.Equal(2, second.SendSequence);
        Assert.Equal(new byte[] { 12 }, second.Payload.ToArray());
        Assert.Equal(2, local.OutstandingCount);
    }

    [Fact]
    public void Receive_Reject_WithNothingOutstanding_DoesNotAskForRetransmission()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();

        HdlcReceiveResult result = local.Receive(Frame(HdlcWireFrameKind.Reject, receiveSequence: 0));

        Assert.False(result.Retransmit);
    }

    [Fact]
    public void DiscardLastInformation_TakesBackTheNewestFrameAndItsSequenceNumber()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        local.CreateInformation(new byte[] { 1 });
        local.CreateInformation(new byte[] { 2 });

        local.DiscardLastInformation();
        HdlcWireFrame next = HdlcWireFrame.Parse(local.CreateInformation(new byte[] { 3 }));

        Assert.Equal(1, next.SendSequence);
        Assert.Equal(2, local.OutstandingCount);
        Assert.Equal([new byte[] { 1 }, new byte[] { 3 }], local.CreateRetransmission().Select(frame => HdlcWireFrame.Parse(frame).Payload.ToArray()), new ByteArrayComparer());
    }

    [Fact]
    public void DiscardLastInformation_WithNothingOutstanding_DoesNothing()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();

        local.DiscardLastInformation();

        Assert.Equal(0, HdlcWireFrame.Parse(local.CreateInformation(new byte[] { 1 })).SendSequence);
    }

    [Fact]
    public void CreateRetransmission_ResendsAllOutstandingWithCurrentReceiveSequence()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        local.CreateInformation(new byte[] { 1 });
        local.CreateInformation(new byte[] { 2 });
        local.Receive(Frame(HdlcWireFrameKind.Information, receiveSequence: 0, sendSequence: 0, payload: [5]));

        IReadOnlyList<ReadOnlyMemory<byte>> frames = local.CreateRetransmission();

        Assert.Equal(2, frames.Count);
        Assert.All(frames, frame => Assert.Equal(1, HdlcWireFrame.Parse(frame).ReceiveSequence));
        Assert.Equal([0, 1], frames.Select(frame => HdlcWireFrame.Parse(frame).SendSequence));
        Assert.Equal(2, local.OutstandingCount);
    }

    [Fact]
    public void CreateRetransmission_WithNothingOutstanding_ReturnsEmpty()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();

        Assert.Empty(local.CreateRetransmission());
    }

    [Fact]
    public void Receive_OutOfSequenceInformation_RejectsOnceThenAcknowledgesUntilTheGapIsFilled()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();

        HdlcReceiveResult first = local.Receive(Frame(HdlcWireFrameKind.Information, sendSequence: 1, payload: [1]));
        HdlcReceiveResult second = local.Receive(Frame(HdlcWireFrameKind.Information, sendSequence: 2, payload: [2]));
        HdlcReceiveResult filled = local.Receive(Frame(HdlcWireFrameKind.Information, sendSequence: 0, payload: [0]));
        HdlcReceiveResult again = local.Receive(Frame(HdlcWireFrameKind.Information, sendSequence: 5, payload: [5]));

        Assert.Equal(HdlcWireFrameKind.Reject, HdlcWireFrame.Parse(first.Response!.Value).Kind);
        Assert.Null(first.Payload);
        HdlcWireFrame secondAnswer = HdlcWireFrame.Parse(second.Response!.Value);
        Assert.Equal(HdlcWireFrameKind.ReceiveReady, secondAnswer.Kind);
        Assert.Equal(0, secondAnswer.ReceiveSequence);
        Assert.Null(second.Payload);
        Assert.Equal(new byte[] { 0 }, filled.Payload!.Value.ToArray());
        Assert.Equal(HdlcWireFrameKind.Reject, HdlcWireFrame.Parse(again.Response!.Value).Kind);
    }

    [Fact]
    public void Receive_SabmWhileConnected_RenumbersOutstandingFramesAndAsksForThemToBeSentAgain()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        local.CreateInformation(new byte[] { 1 });
        local.CreateInformation(new byte[] { 2 });
        local.CreateInformation(new byte[] { 3 });
        local.Receive(Frame(HdlcWireFrameKind.ReceiveReady, receiveSequence: 1));

        HdlcReceiveResult result = local.Receive(Frame(HdlcWireFrameKind.SetAsynchronousBalancedMode));
        IReadOnlyList<ReadOnlyMemory<byte>> frames = local.CreateRetransmission();

        Assert.Equal(0, result.Acknowledged);
        Assert.True(result.Retransmit);
        Assert.Equal(HdlcWireFrameKind.UnnumberedAcknowledge, HdlcWireFrame.Parse(result.Response!.Value).Kind);
        Assert.Equal(2, local.OutstandingCount);
        Assert.Equal([0, 1], frames.Select(frame => HdlcWireFrame.Parse(frame).SendSequence));
        Assert.Equal([new byte[] { 2 }, new byte[] { 3 }], frames.Select(frame => HdlcWireFrame.Parse(frame).Payload.ToArray()), new ByteArrayComparer());
        Assert.Equal(2, HdlcWireFrame.Parse(local.CreateInformation(new byte[] { 4 })).SendSequence);
    }

    [Fact]
    public void Receive_SabmWhileNotConnected_DiscardsStaleOutstandingFramesAndReportsThem()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        local.CreateInformation(new byte[] { 1 });
        local.Receive(Frame(HdlcWireFrameKind.DisconnectedMode));

        HdlcReceiveResult result = local.Receive(Frame(HdlcWireFrameKind.SetAsynchronousBalancedMode));

        Assert.Equal(1, result.Acknowledged);
        Assert.Equal(0, local.OutstandingCount);
        Assert.False(result.Retransmit);
    }

    [Fact]
    public void Receive_UaForARequestSentWhileAlreadyConnected_ResetsTheLinkLikeThePeerDid()
    {
        HdlcStateMachine local = new(options, 0x11, 0x12);
        HdlcStateMachine remote = new(options, 0x12, 0x11);
        ReadOnlyMemory<byte> first = local.CreateConnect();
        ReadOnlyMemory<byte> second = local.CreateConnect();
        HdlcReceiveResult firstAnswer = remote.Receive(first);
        local.Receive(firstAnswer.Response!.Value);
        local.CreateInformation(new byte[] { 7 });
        remote.Receive(local.CreateInformation(new byte[] { 8 }));
        HdlcReceiveResult secondAnswer = remote.Receive(second);

        HdlcReceiveResult result = local.Receive(secondAnswer.Response!.Value);

        Assert.True(result.Retransmit);
        Assert.Equal(HdlcConnectionState.Connected, result.State);
        Assert.Equal([0, 1], local.CreateRetransmission().Select(frame => HdlcWireFrame.Parse(frame).SendSequence));
    }

    [Fact]
    public void Receive_UnsolicitedUaWhileConnected_LeavesTheLinkAlone()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        local.CreateInformation(new byte[] { 1 });

        HdlcReceiveResult result = local.Receive(Frame(HdlcWireFrameKind.UnnumberedAcknowledge));

        Assert.False(result.Retransmit);
        Assert.Equal(1, local.OutstandingCount);
    }

    [Fact]
    public void CreateConnect_DiscardsOutstandingFrames()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        local.CreateInformation(new byte[] { 1 });

        local.CreateConnect();

        Assert.Equal(0, local.OutstandingCount);
    }

    [Fact]
    public void Receive_SupervisoryFrameWhileDisconnected_IsIgnored()
    {
        HdlcStateMachine machine = new(options, 0x11, 0x12);

        HdlcReceiveResult result = machine.Receive(Frame(HdlcWireFrameKind.Reject, receiveSequence: 1));

        Assert.Equal(HdlcConnectionState.Disconnected, result.State);
        Assert.Equal(0, result.Acknowledged);
        Assert.False(result.Retransmit);
    }

    [Fact]
    public void FullWindowCycles_WithAcknowledgements_NeverExceedsWindow()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = EstablishConnectedPair();

        for (int round = 0; round < 5; round++)
        {
            List<HdlcReceiveResult> received = [];
            for (int i = 0; i < local.WindowSize; i++)
            {
                received.Add(remote.Receive(local.CreateInformation(new byte[] { (byte)i })));
            }

            Assert.Equal(local.WindowSize, local.OutstandingCount);
            local.Receive(received[^1].Response!.Value);
            Assert.Equal(0, local.OutstandingCount);
            Assert.Equal(Enumerable.Range(0, local.WindowSize).Select(i => (byte)i), received.Select(result => result.Payload!.Value.ToArray()[0]));
        }
    }

    [Fact]
    public void LostReject_TimerResendOfAlreadyDeliveredFrame_IsStillAcknowledged()
    {
        (HdlcStateMachine local, HdlcStateMachine remote) = EstablishConnectedPair();
        ReadOnlyMemory<byte> information = local.CreateInformation(new byte[] { 1 });
        remote.Receive(information);

        HdlcReceiveResult firstRepeat = remote.Receive(local.CreateRetransmission()[0]);
        HdlcReceiveResult secondRepeat = remote.Receive(local.CreateRetransmission()[0]);
        HdlcReceiveResult acknowledged = local.Receive(secondRepeat.Response!.Value);

        Assert.Equal(HdlcWireFrameKind.Reject, HdlcWireFrame.Parse(firstRepeat.Response!.Value).Kind);
        Assert.Null(firstRepeat.Payload);
        Assert.Null(secondRepeat.Payload);
        Assert.Equal(1, acknowledged.Acknowledged);
        Assert.False(acknowledged.Retransmit);
        Assert.Equal(0, local.OutstandingCount);
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y) => x is not null && y is not null && x.AsSpan().SequenceEqual(y);

        public int GetHashCode(byte[] obj) => obj.Length;
    }

    private Mock<IMemoryOwner<byte>> Owner(params byte[] data)
    {
        Mock<IMemoryOwner<byte>> owner = new();
        owner.SetupGet(x => x.Memory).Returns(data);
        return owner;
    }

    [Fact]
    public void CreateInformation_WithOwner_CarriesItsMemoryAndKeepsItUntilAcknowledged()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        Mock<IMemoryOwner<byte>> owner = Owner(7, 8);

        HdlcWireFrame frame = HdlcWireFrame.Parse(local.CreateInformation(owner.Object));

        Assert.Equal(new byte[] { 7, 8 }, frame.Payload.ToArray());
        owner.Verify(x => x.Dispose(), Times.Never);
        local.Receive(Frame(HdlcWireFrameKind.ReceiveReady, receiveSequence: 1));
        owner.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal(0, local.OutstandingCount);
    }

    [Fact]
    public void CreateInformation_WithOwner_WhenTheWindowIsFull_ThrowsWithoutTakingTheOwner()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        for (int i = 0; i < local.WindowSize; i++)
        {
            local.CreateInformation(new byte[] { 1 });
        }

        Mock<IMemoryOwner<byte>> owner = Owner(1);

        Assert.Throws<InvalidOperationException>(() => local.CreateInformation(owner.Object));
        owner.Verify(x => x.Dispose(), Times.Never);
    }

    [Fact]
    public void DiscardLastInformation_DisposesTheOwnerOfTheFrameItTakesBack()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        Mock<IMemoryOwner<byte>> owner = Owner(3);
        local.CreateInformation(owner.Object);

        local.DiscardLastInformation();

        owner.Verify(x => x.Dispose(), Times.Once);
        Assert.Equal(0, local.OutstandingCount);
    }

    [Fact]
    public void Receive_SabmWhileConnected_RenumbersOutstandingFramesWithoutDisposingTheirOwners()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        Mock<IMemoryOwner<byte>> owner = Owner(3);
        local.CreateInformation(owner.Object);

        local.Receive(Frame(HdlcWireFrameKind.SetAsynchronousBalancedMode));

        owner.Verify(x => x.Dispose(), Times.Never);
        Assert.Equal(1, local.OutstandingCount);
        Assert.Equal(new byte[] { 3 }, HdlcWireFrame.Parse(local.CreateRetransmission()[0]).Payload.ToArray());
        local.Receive(Frame(HdlcWireFrameKind.ReceiveReady, receiveSequence: 1));
        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void CreateConnect_WhileFramesAreOutstanding_DisposesTheirOwners()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        Mock<IMemoryOwner<byte>> owner = Owner(3);
        local.CreateInformation(owner.Object);

        local.CreateConnect();

        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_DisposesTheOwnersOfOutstandingFrames()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        Mock<IMemoryOwner<byte>> owner = Owner(3);
        local.CreateInformation(owner.Object);

        local.Dispose();

        owner.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void CreateInformation_AfterDispose_ThrowsWithoutTakingTheOwner()
    {
        (HdlcStateMachine local, _) = EstablishConnectedPair();
        local.Dispose();
        Mock<IMemoryOwner<byte>> owner = Owner(1);

        Assert.Throws<InvalidOperationException>(() => local.CreateInformation(owner.Object));
        Assert.Throws<InvalidOperationException>(() => local.CreateInformation(new byte[] { 1 }));
        owner.Verify(x => x.Dispose(), Times.Never);
    }
}
