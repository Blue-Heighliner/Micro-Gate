namespace BlueHeighliner.MicroGate.Hdlc;

/// <summary>
/// Drives an HDLC asynchronous balanced mode (ABM) connection, producing and consuming the address and control bytes of every frame per
/// https://en.wikipedia.org/wiki/High-Level_Data_Link_Control, so that platform-specific transports only need to move raw frame bytes. Information frames are numbered modulo 8 and kept until acknowledged, so lost frames can be sent again (go-back-N), and a link reset renumbers the unacknowledged frames instead of losing them.
/// </summary>
/// <remarks>
/// Not thread safe: callers serialize access.
/// </remarks>
internal interface IHdlcStateMachine : IDisposable
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
    /// Gets a value indicating whether the remote station last reported it cannot accept information frames (receive not ready), until it next sends a frame that shows it can.
    /// </summary>
    bool PeerBusy { get; }

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
    /// <exception cref="InvalidOperationException">The state machine is not <see cref="HdlcConnectionState.Connected"/>, has been disposed, or <see cref="WindowSize"/> frames are already outstanding.</exception>
    ReadOnlyMemory<byte> CreateInformation(ReadOnlyMemory<byte> payload);

    /// <summary>
    /// Creates a <see cref="HdlcFrameKind.Information"/> frame carrying the memory of <paramref name="payload"/>, addressed with the next send sequence number, and keeps it, without copying, until it is acknowledged. Ownership of <paramref name="payload"/> passes to the state machine only if the call succeeds; it is then disposed once the frame is acknowledged, discarded, or the state machine is reset or disposed.
    /// </summary>
    /// <param name="payload">The owner of the data to carry in the frame's information field.</param>
    /// <returns>The raw frame bytes to transmit.</returns>
    /// <exception cref="InvalidOperationException">The state machine is not <see cref="HdlcConnectionState.Connected"/>, has been disposed, or <see cref="WindowSize"/> frames are already outstanding.</exception>
    ReadOnlyMemory<byte> CreateInformation(IMemoryOwner<byte> payload);

    /// <summary>
    /// Creates every unacknowledged information frame again, oldest first, carrying the current receive sequence number.
    /// </summary>
    /// <returns>The raw frame bytes to transmit, empty if nothing is outstanding.</returns>
    IReadOnlyList<ReadOnlyMemory<byte>> CreateRetransmission();

    /// <summary>
    /// Gets a value indicating whether information frames have been received that no frame sent since has acknowledged yet. The caller is expected to call <see cref="CreateAcknowledgement"/> after <see cref="MicroGatePeerOptions.AcknowledgeDelay"/> if no information frame has carried the acknowledgement by then.
    /// </summary>
    bool AcknowledgementPending { get; }

    /// <summary>
    /// Creates an RR response acknowledging the information frames received, for when no information frame was sent in time to carry the acknowledgement.
    /// </summary>
    /// <returns>The raw frame bytes to transmit, empty if nothing is waiting to be acknowledged.</returns>
    ReadOnlyMemory<byte> CreateAcknowledgement();

    /// <summary>
    /// Creates what to send when the remote station has not acknowledged outstanding information frames in time: a single RR command with the poll bit set, which the remote station must answer with a final response carrying its N(R), so the sender learns what to resend, as the ADCCP and HDLC timeout procedure prescribes. When the poll/final bit is disabled it cannot poll, and falls back to <see cref="CreateRetransmission"/>.
    /// </summary>
    /// <returns>The raw frames to transmit.</returns>
    IReadOnlyList<ReadOnlyMemory<byte>> CreateTimeoutRecovery();

    /// <summary>
    /// Forgets the information frame most recently created, and takes its sequence number back, because it could not be transmitted. Does nothing if no frame is outstanding.
    /// </summary>
    void DiscardLastInformation();

    /// <summary>
    /// Parses and processes a raw received frame, updating the connection state and, for information frames addressed to this station, returning the delivered payload. The delivered payload references <paramref name="data"/> and is only valid until that memory is reused.
    /// </summary>
    /// <param name="data">The raw received frame bytes.</param>
    /// <returns>The <see cref="HdlcReceiveResult"/> describing how the frame was processed.</returns>
    /// <exception cref="HdlcFrameException">The frame is too short to contain an address and a control field.</exception>
    HdlcReceiveResult Receive(ReadOnlyMemory<byte> data);
}

/// <summary>
/// <inheritdoc cref="IHdlcStateMachine" />
/// </summary>
/// <param name="options">The peer options whose poll/final and window settings apply to every frame produced and every frame accepted.</param>
/// <param name="address">The address of this station, carried by every response it sends and expected on every command it receives.</param>
/// <param name="remoteAddress">The address of the remote station, carried by every command this station sends and expected on every response it receives. Must differ from <paramref name="address"/>.</param>
internal sealed class HdlcStateMachine(MicroGatePeerOptions options, byte address, byte remoteAddress) : IHdlcStateMachine
{
    private readonly int sequenceModulus = 8;
    private readonly byte pollFinalMask = 0x10;
    private readonly byte broadcastAddress = 0xFF;
    private readonly int acknowledgeThreshold = 4;
    private readonly byte unnumberedMask = 0x03;
    private readonly byte testControl = 0xE3;
    private readonly byte[] unsupportedModeCommands = [0x83, 0xCF, 0x6F, 0x4F];
    private readonly byte[] ignoredCommands = [0x03, 0xAF];
    private readonly Queue<(int Sequence, IMemoryOwner<byte> Payload, int Length)> outstanding = new();
    private int pendingConnectRequests;
    private int sendSequence;
    private int receiveSequence;
    private bool rejectSent;
    private bool pollOutstanding;
    private bool acknowledgementPending;
    private int unacknowledgedReceived;
    private bool peerBusy;
    private bool disposed;

    /// <inheritdoc />
    public HdlcConnectionState State { get; private set; } = HdlcConnectionState.Disconnected;

    /// <inheritdoc />
    public int WindowSize => options.TransmitWindow;

    /// <inheritdoc />
    public int OutstandingCount => outstanding.Count;

    /// <inheritdoc />
    public bool PeerBusy => peerBusy;

    /// <inheritdoc />
    public bool AcknowledgementPending => acknowledgementPending;

    private byte LocalAddress => address;

    private byte RemoteAddress => remoteAddress;

    /// <inheritdoc />
    public ReadOnlyMemory<byte> CreateConnect()
    {
        Restart();
        pendingConnectRequests++;
        State = HdlcConnectionState.Connecting;
        return CreateUnnumberedFrame(HdlcFrameKind.SetAsynchronousBalancedMode, broadcastAddress, poll: false);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> CreateDisconnect()
    {
        State = HdlcConnectionState.Disconnecting;
        return CreateUnnumberedFrame(HdlcFrameKind.Disconnect, RemoteAddress, poll: false);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> CreateInformation(ReadOnlyMemory<byte> payload)
    {
        EnsureCanSend();
        return Keep(new PooledBuffer(payload.Span), payload.Length);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> CreateInformation(IMemoryOwner<byte> payload)
    {
        EnsureCanSend();
        return Keep(payload, payload.Memory.Length);
    }

    /// <inheritdoc />
    public IReadOnlyList<ReadOnlyMemory<byte>> CreateRetransmission() =>
        [.. outstanding.Select(frame => (ReadOnlyMemory<byte>)CreateInformationFrame(frame.Sequence, frame.Payload.Memory[..frame.Length]))];

    /// <inheritdoc />
    public ReadOnlyMemory<byte> CreateAcknowledgement() =>
        acknowledgementPending && State == HdlcConnectionState.Connected ? CreateSupervisoryFrame(HdlcFrameKind.ReceiveReady, final: false) : ReadOnlyMemory<byte>.Empty;

    /// <inheritdoc />
    public IReadOnlyList<ReadOnlyMemory<byte>> CreateTimeoutRecovery()
    {
        if (options.DisablePollFinalBit)
        {
            return CreateRetransmission();
        }

        pollOutstanding = true;
        ClearPendingAcknowledgement();
        return [new HdlcFrame { Address = RemoteAddress, Kind = HdlcFrameKind.ReceiveReady, PollFinal = true, ReceiveSequence = receiveSequence }.ToArray()];
    }

    /// <inheritdoc />
    public void DiscardLastInformation()
    {
        if (outstanding.Count == 0)
        {
            return;
        }

        (int Sequence, IMemoryOwner<byte> Payload, int Length)[] kept = outstanding.ToArray();
        outstanding.Clear();
        foreach ((int Sequence, IMemoryOwner<byte> Payload, int Length) frame in kept[..^1])
        {
            outstanding.Enqueue(frame);
        }

        kept[^1].Payload.Dispose();
        sendSequence = (sendSequence + sequenceModulus - 1) % sequenceModulus;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        disposed = true;
        ReleaseOutstanding();
    }

    /// <inheritdoc />
    public HdlcReceiveResult Receive(ReadOnlyMemory<byte> data)
    {
        HdlcFrame frame;
        try
        {
            frame = HdlcFrame.Parse(data);
        }
        catch (HdlcFrameException) when (data.Length >= 2)
        {
            return ReceiveUnrecognized(data);
        }

        bool broadcast = frame.Address == broadcastAddress;
        if (frame.Address != LocalAddress && frame.Address != RemoteAddress && !broadcast)
        {
            return new HdlcReceiveResult { State = State };
        }

        bool command = frame.Address == LocalAddress || broadcast;
        bool response = frame.Address == RemoteAddress;

        return frame.Kind switch
        {
            HdlcFrameKind.SetAsynchronousBalancedMode when command => ReceiveSetAsynchronousBalancedMode(frame),
            HdlcFrameKind.Disconnect when command => ReceiveDisconnect(frame),
            HdlcFrameKind.UnnumberedAcknowledge when response => ReceiveUnnumberedAcknowledge(),
            HdlcFrameKind.DisconnectedMode or HdlcFrameKind.FrameReject when response => ReceiveTerminal(),
            HdlcFrameKind.Information when command => ReceiveInformation(frame),
            HdlcFrameKind.ReceiveReady or HdlcFrameKind.ReceiveNotReady => ReceiveSupervisory(frame, retransmit: false, command),
            HdlcFrameKind.Reject => ReceiveSupervisory(frame, retransmit: true, command),
            _ => new HdlcReceiveResult { State = State },
        };
    }

    private HdlcReceiveResult ReceiveUnrecognized(ReadOnlyMemory<byte> data)
    {
        byte control = data.Span[1];
        if (data.Span[0] != LocalAddress)
        {
            return new HdlcReceiveResult { State = State };
        }

        bool final = (control & pollFinalMask) != 0 && !options.DisablePollFinalBit;
        byte command = (byte)(control & ~pollFinalMask);

        if ((control & unnumberedMask) == unnumberedMask)
        {
            if (Array.IndexOf(unsupportedModeCommands, command) >= 0)
            {
                return new HdlcReceiveResult { State = State, Response = CreateUnnumberedFrame(HdlcFrameKind.DisconnectedMode, LocalAddress, poll: final) };
            }

            if (command == testControl)
            {
                byte[] echo = data.ToArray();
                echo[0] = LocalAddress;
                echo[1] = (byte)(testControl | (final ? pollFinalMask : 0));
                return new HdlcReceiveResult { State = State, Response = echo };
            }

            if (Array.IndexOf(ignoredCommands, command) >= 0)
            {
                return new HdlcReceiveResult { State = State };
            }
        }

        return new HdlcReceiveResult { State = State, Response = CreateFrameReject(control, final) };
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
            Response = CreateUnnumberedFrame(HdlcFrameKind.UnnumberedAcknowledge, LocalAddress, frame.PollFinal),
        };
    }

    private HdlcReceiveResult ReceiveDisconnect(HdlcFrame frame)
    {
        HdlcFrameKind answer = State == HdlcConnectionState.Disconnected ? HdlcFrameKind.DisconnectedMode : HdlcFrameKind.UnnumberedAcknowledge;
        State = HdlcConnectionState.Disconnected;
        return new HdlcReceiveResult
        {
            State = State,
            Response = CreateUnnumberedFrame(answer, LocalAddress, frame.PollFinal),
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

    private HdlcReceiveResult ReceiveSupervisory(HdlcFrame frame, bool retransmit, bool command)
    {
        if (State != HdlcConnectionState.Connected)
        {
            return ReceiveWhileNotConnected(frame);
        }

        peerBusy = frame.Kind == HdlcFrameKind.ReceiveNotReady;
        int acknowledged = Acknowledge(frame.ReceiveSequence);
        bool poll = command && frame.PollFinal;
        bool answersPoll = !command && frame.PollFinal && pollOutstanding;
        if (answersPoll)
        {
            pollOutstanding = false;
        }

        return new HdlcReceiveResult
        {
            State = State,
            Acknowledged = acknowledged,
            Retransmit = (retransmit || (answersPoll && !peerBusy)) && outstanding.Count > 0,
            Response = poll ? (ReadOnlyMemory<byte>?)CreateSupervisoryFrame(HdlcFrameKind.ReceiveReady, final: true) : null,
        };
    }

    private HdlcReceiveResult ReceiveWhileNotConnected(HdlcFrame frame) =>
        State == HdlcConnectionState.Disconnected
            ? new HdlcReceiveResult { State = State, Response = CreateUnnumberedFrame(HdlcFrameKind.DisconnectedMode, LocalAddress, frame.PollFinal) }
            : new HdlcReceiveResult { State = State };

    private HdlcReceiveResult ReceiveInformation(HdlcFrame frame)
    {
        if (State != HdlcConnectionState.Connected)
        {
            return ReceiveWhileNotConnected(frame);
        }

        peerBusy = false;
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
        unacknowledgedReceived++;
        bool immediate = frame.PollFinal || options.AcknowledgeDelay == TimeSpan.Zero || unacknowledgedReceived >= acknowledgeThreshold;
        acknowledgementPending = true;
        return new HdlcReceiveResult
        {
            State = State,
            Acknowledged = acknowledged,
            Payload = frame.Payload,
            Response = immediate ? (ReadOnlyMemory<byte>?)CreateSupervisoryFrame(HdlcFrameKind.ReceiveReady, frame.PollFinal) : null,
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
            outstanding.Dequeue().Payload.Dispose();
        }

        return acknowledged;
    }

    private void EnsureCanSend()
    {
        if (disposed)
        {
            throw new InvalidOperationException("Cannot create an information frame after the state machine has been disposed.");
        }

        if (State != HdlcConnectionState.Connected)
        {
            throw new InvalidOperationException($"Cannot create an information frame while the state machine is {State}.");
        }

        if (outstanding.Count >= WindowSize)
        {
            throw new InvalidOperationException("Cannot create an information frame while the send window is full.");
        }
    }

    private ReadOnlyMemory<byte> Keep(IMemoryOwner<byte> payload, int length)
    {
        outstanding.Enqueue((sendSequence, payload, length));
        byte[] frame = CreateInformationFrame(sendSequence, payload.Memory[..length]);
        sendSequence = (sendSequence + 1) % sequenceModulus;
        return frame;
    }

    private void ReleaseOutstanding()
    {
        foreach ((int Sequence, IMemoryOwner<byte> Payload, int Length) frame in outstanding)
        {
            frame.Payload.Dispose();
        }

        outstanding.Clear();
    }

    private void ResetSequences()
    {
        sendSequence = 0;
        receiveSequence = 0;
        rejectSent = false;
        pollOutstanding = false;
        acknowledgementPending = false;
        unacknowledgedReceived = 0;
        peerBusy = false;
    }

    private void Restart()
    {
        ReleaseOutstanding();
        ResetSequences();
    }

    private void Renumber()
    {
        (int Sequence, IMemoryOwner<byte> Payload, int Length)[] kept = outstanding.ToArray();
        outstanding.Clear();
        ResetSequences();
        foreach ((int Sequence, IMemoryOwner<byte> Payload, int Length) frame in kept)
        {
            outstanding.Enqueue((sendSequence, frame.Payload, frame.Length));
            sendSequence++;
        }
    }

    private void ClearPendingAcknowledgement()
    {
        acknowledgementPending = false;
        unacknowledgedReceived = 0;
    }

    private byte[] CreateInformationFrame(int sequence, ReadOnlyMemory<byte> payload)
    {
        ClearPendingAcknowledgement();
        return new HdlcFrame
        {
            Address = RemoteAddress,
            Kind = HdlcFrameKind.Information,
            PollFinal = false,
            SendSequence = sequence,
            ReceiveSequence = receiveSequence,
            Payload = payload,
        }.ToArray();
    }

    private byte[] CreateUnnumberedFrame(HdlcFrameKind kind, byte address, bool poll) =>
        new HdlcFrame
        {
            Address = address,
            Kind = kind,
            PollFinal = poll && !options.DisablePollFinalBit,
        }.ToArray();

    private byte[] CreateSupervisoryFrame(HdlcFrameKind kind, bool final)
    {
        ClearPendingAcknowledgement();
        return new HdlcFrame
        {
            Address = LocalAddress,
            Kind = kind,
            PollFinal = final && !options.DisablePollFinalBit,
            ReceiveSequence = receiveSequence,
        }.ToArray();
    }

    private byte[] CreateFrameReject(byte rejectedControl, bool final) =>
        new HdlcFrame
        {
            Address = LocalAddress,
            Kind = HdlcFrameKind.FrameReject,
            PollFinal = final,
            Payload = new byte[] { rejectedControl, (byte)((sendSequence << 1) | (receiveSequence << 5)), 0x01 },
        }.ToArray();
}
