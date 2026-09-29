namespace BlueHeighliner.MicroGate.Hdlc;

/// <summary>
/// Drives an HDLC asynchronous balanced mode (ABM) connection, producing and consuming the address and control bytes of every frame per
/// https://en.wikipedia.org/wiki/High-Level_Data_Link_Control, so that platform-specific transports only need to move raw frame bytes. Information frames are numbered modulo 8 and kept until acknowledged, so lost frames can be sent again (go-back-N), and a link reset renumbers the unacknowledged frames instead of losing them.
/// </summary>
/// <remarks>
/// Not thread safe: callers serialize access.
/// </remarks>
internal interface IHdlcStateMachine
{
    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    HdlcConnectionState State { get; }

    /// <summary>
    /// Gets the maximum number of information frames that may be sent before one is acknowledged.
    /// </summary>
    int WindowSize { get; }

    /// <summary>
    /// Gets the number of information frames sent and not yet acknowledged.
    /// </summary>
    int OutstandingCount { get; }

    /// <summary>
    /// Creates a <see cref="HdlcFrameKind.SetAsynchronousBalancedMode"/> frame requesting the peer establish a connection, and transitions the state machine to <see cref="HdlcConnectionState.Connecting"/>.
    /// </summary>
    /// <returns>The raw frame bytes to transmit.</returns>
    ReadOnlyMemory<byte> CreateConnect();

    /// <summary>
    /// Creates a <see cref="HdlcFrameKind.Disconnect"/> frame requesting the peer terminate the connection, and transitions the state machine to <see cref="HdlcConnectionState.Disconnecting"/>.
    /// </summary>
    /// <returns>The raw frame bytes to transmit.</returns>
    ReadOnlyMemory<byte> CreateDisconnect();

    /// <summary>
    /// Creates a <see cref="HdlcFrameKind.Information"/> frame carrying a copy of <paramref name="payload"/>, addressed with the next send sequence number, and keeps it until it is acknowledged.
    /// </summary>
    /// <param name="payload">The data to carry in the frame's information field.</param>
    /// <returns>The raw frame bytes to transmit.</returns>
    /// <exception cref="InvalidOperationException">The state machine is not <see cref="HdlcConnectionState.Connected"/>, or <see cref="WindowSize"/> frames are already outstanding.</exception>
    ReadOnlyMemory<byte> CreateInformation(ReadOnlyMemory<byte> payload);

    /// <summary>
    /// Creates every unacknowledged information frame again, oldest first, carrying the current receive sequence number.
    /// </summary>
    /// <returns>The raw frame bytes to transmit, empty if nothing is outstanding.</returns>
    IReadOnlyList<ReadOnlyMemory<byte>> CreateRetransmission();

    /// <summary>
    /// Forgets the information frame most recently created, and takes its sequence number back, because it could not be transmitted. Does nothing if no frame is outstanding.
    /// </summary>
    void DiscardLastInformation();

    /// <summary>
    /// Parses and processes a raw received frame, updating the connection state and, for information frames addressed to this station, returning the delivered payload. The delivered payload references <paramref name="data"/> and is only valid until that memory is reused.
    /// </summary>
    /// <param name="data">The raw received frame bytes.</param>
    /// <returns>The <see cref="HdlcReceiveResult"/> describing how the frame was processed.</returns>
    /// <exception cref="HdlcFrameException">The frame is malformed or its control field does not encode a recognized frame kind.</exception>
    HdlcReceiveResult Receive(ReadOnlyMemory<byte> data);
}

/// <summary>
/// <inheritdoc cref="IHdlcStateMachine" />
/// </summary>
/// <param name="options">The peer options whose address and poll/final settings apply to every frame produced and every frame accepted.</param>
internal sealed class HdlcStateMachine(MicroGatePeerOptions options) : IHdlcStateMachine
{
    private readonly int sequenceModulus = 8;
    private readonly Queue<(int Sequence, byte[] Payload)> outstanding = new();
    private int pendingConnectRequests;
    private int sendSequence;
    private int receiveSequence;
    private bool rejectSent;

    /// <inheritdoc />
    public HdlcConnectionState State { get; private set; } = HdlcConnectionState.Disconnected;

    /// <inheritdoc />
    public int WindowSize => options.TransmitWindow;

    /// <inheritdoc />
    public int OutstandingCount => outstanding.Count;

    /// <inheritdoc />
    public ReadOnlyMemory<byte> CreateConnect()
    {
        Restart();
        pendingConnectRequests++;
        State = HdlcConnectionState.Connecting;
        return CreateUnnumberedFrame(HdlcFrameKind.SetAsynchronousBalancedMode, poll: true);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> CreateDisconnect()
    {
        State = HdlcConnectionState.Disconnecting;
        return CreateUnnumberedFrame(HdlcFrameKind.Disconnect, poll: true);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> CreateInformation(ReadOnlyMemory<byte> payload)
    {
        if (State != HdlcConnectionState.Connected)
        {
            throw new InvalidOperationException($"Cannot create an information frame while the state machine is {State}.");
        }

        if (outstanding.Count >= WindowSize)
        {
            throw new InvalidOperationException("Cannot create an information frame while the send window is full.");
        }

        byte[] kept = payload.ToArray();
        outstanding.Enqueue((sendSequence, kept));
        byte[] frame = CreateInformationFrame(sendSequence, kept);
        sendSequence = (sendSequence + 1) % sequenceModulus;
        return frame;
    }

    /// <inheritdoc />
    public IReadOnlyList<ReadOnlyMemory<byte>> CreateRetransmission() =>
        [.. outstanding.Select(frame => (ReadOnlyMemory<byte>)CreateInformationFrame(frame.Sequence, frame.Payload))];

    /// <inheritdoc />
    public void DiscardLastInformation()
    {
        if (outstanding.Count == 0)
        {
            return;
        }

        (int Sequence, byte[] Payload)[] kept = outstanding.ToArray();
        outstanding.Clear();
        foreach ((int Sequence, byte[] Payload) frame in kept[..^1])
        {
            outstanding.Enqueue(frame);
        }

        sendSequence = (sendSequence + sequenceModulus - 1) % sequenceModulus;
    }

    /// <inheritdoc />
    public HdlcReceiveResult Receive(ReadOnlyMemory<byte> data)
    {
        HdlcFrame frame = HdlcFrame.Parse(data);

        if (frame.Address != options.Address)
        {
            return new HdlcReceiveResult { State = State };
        }

        return frame.Kind switch
        {
            HdlcFrameKind.SetAsynchronousBalancedMode => ReceiveSetAsynchronousBalancedMode(frame),
            HdlcFrameKind.Disconnect => ReceiveDisconnect(frame),
            HdlcFrameKind.UnnumberedAcknowledge => ReceiveUnnumberedAcknowledge(),
            HdlcFrameKind.DisconnectedMode or HdlcFrameKind.FrameReject => ReceiveTerminal(),
            HdlcFrameKind.Information => ReceiveInformation(frame),
            HdlcFrameKind.ReceiveReady or HdlcFrameKind.ReceiveNotReady => ReceiveSupervisory(frame, retransmit: false),
            HdlcFrameKind.Reject => ReceiveSupervisory(frame, retransmit: true),
            _ => new HdlcReceiveResult { State = State },
        };
    }

    private HdlcReceiveResult ReceiveSetAsynchronousBalancedMode(HdlcFrame frame)
    {
        bool wasConnected = State == HdlcConnectionState.Connected;
        int discarded = outstanding.Count;

        if (wasConnected)
        {
            Renumber();
            discarded = 0;
        }
        else
        {
            Restart();
        }

        State = HdlcConnectionState.Connected;
        return new HdlcReceiveResult
        {
            State = State,
            Acknowledged = discarded,
            Retransmit = outstanding.Count > 0,
            Response = CreateUnnumberedFrame(HdlcFrameKind.UnnumberedAcknowledge, frame.PollFinal),
        };
    }

    private HdlcReceiveResult ReceiveDisconnect(HdlcFrame frame)
    {
        State = HdlcConnectionState.Disconnected;
        return new HdlcReceiveResult
        {
            State = State,
            Response = CreateUnnumberedFrame(HdlcFrameKind.UnnumberedAcknowledge, frame.PollFinal),
        };
    }

    private HdlcReceiveResult ReceiveUnnumberedAcknowledge()
    {
        bool answersRequest = pendingConnectRequests > 0;
        if (answersRequest)
        {
            pendingConnectRequests--;
        }

        if (State == HdlcConnectionState.Connecting)
        {
            Restart();
            State = HdlcConnectionState.Connected;
        }
        else if (State == HdlcConnectionState.Disconnecting)
        {
            State = HdlcConnectionState.Disconnected;
        }
        else if (State == HdlcConnectionState.Connected && answersRequest)
        {
            Renumber();
            return new HdlcReceiveResult { State = State, Retransmit = outstanding.Count > 0 };
        }

        return new HdlcReceiveResult { State = State };
    }

    private HdlcReceiveResult ReceiveTerminal()
    {
        State = HdlcConnectionState.Disconnected;
        return new HdlcReceiveResult { State = State };
    }

    private HdlcReceiveResult ReceiveSupervisory(HdlcFrame frame, bool retransmit)
    {
        if (State != HdlcConnectionState.Connected)
        {
            return new HdlcReceiveResult { State = State };
        }

        int acknowledged = Acknowledge(frame.ReceiveSequence);
        return new HdlcReceiveResult
        {
            State = State,
            Acknowledged = acknowledged,
            Retransmit = retransmit && outstanding.Count > 0,
        };
    }

    private HdlcReceiveResult ReceiveInformation(HdlcFrame frame)
    {
        if (State != HdlcConnectionState.Connected)
        {
            return new HdlcReceiveResult { State = State };
        }

        int acknowledged = Acknowledge(frame.ReceiveSequence);

        if (frame.SendSequence != receiveSequence)
        {
            HdlcFrameKind answer = rejectSent ? HdlcFrameKind.ReceiveReady : HdlcFrameKind.Reject;
            rejectSent = true;
            return new HdlcReceiveResult
            {
                State = State,
                Acknowledged = acknowledged,
                Response = CreateSupervisoryFrame(answer, frame.PollFinal),
            };
        }

        rejectSent = false;
        receiveSequence = (receiveSequence + 1) % sequenceModulus;
        return new HdlcReceiveResult
        {
            State = State,
            Acknowledged = acknowledged,
            Payload = frame.Payload,
            Response = CreateSupervisoryFrame(HdlcFrameKind.ReceiveReady, frame.PollFinal),
        };
    }

    private int Acknowledge(int peerReceiveSequence)
    {
        if (outstanding.Count == 0)
        {
            return 0;
        }

        int acknowledged = (peerReceiveSequence - outstanding.Peek().Sequence + sequenceModulus) % sequenceModulus;
        if (acknowledged > outstanding.Count)
        {
            return 0;
        }

        for (int i = 0; i < acknowledged; i++)
        {
            outstanding.Dequeue();
        }

        return acknowledged;
    }

    private void Restart()
    {
        outstanding.Clear();
        sendSequence = 0;
        receiveSequence = 0;
        rejectSent = false;
    }

    private void Renumber()
    {
        (int Sequence, byte[] Payload)[] kept = outstanding.ToArray();
        Restart();
        foreach ((int Sequence, byte[] Payload) frame in kept)
        {
            outstanding.Enqueue((sendSequence, frame.Payload));
            sendSequence++;
        }
    }

    private byte[] CreateInformationFrame(int sequence, byte[] payload) =>
        new HdlcFrame
        {
            Address = options.Address,
            Kind = HdlcFrameKind.Information,
            PollFinal = false,
            SendSequence = sequence,
            ReceiveSequence = receiveSequence,
            Payload = payload,
        }.ToArray();

    private byte[] CreateUnnumberedFrame(HdlcFrameKind kind, bool poll) =>
        new HdlcFrame
        {
            Address = options.Address,
            Kind = kind,
            PollFinal = poll && !options.DisablePollFinalBit,
        }.ToArray();

    private byte[] CreateSupervisoryFrame(HdlcFrameKind kind, bool final) =>
        new HdlcFrame
        {
            Address = options.Address,
            Kind = kind,
            PollFinal = final && !options.DisablePollFinalBit,
            ReceiveSequence = receiveSequence,
        }.ToArray();
}
