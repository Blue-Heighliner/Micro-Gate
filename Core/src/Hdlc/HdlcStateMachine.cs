namespace BlueHeighliner.MicroGate.Hdlc;

/// <summary>
/// Drives an HDLC asynchronous balanced mode (ABM) connection, producing and consuming the address and control bytes of every frame per
/// https://en.wikipedia.org/wiki/High-Level_Data_Link_Control, so that platform-specific transports only need to move raw frame bytes.
/// </summary>
internal interface IHdlcStateMachine
{
    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    HdlcConnectionState State { get; }

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
    /// Creates a <see cref="HdlcFrameKind.Information"/> frame carrying <paramref name="payload"/>, addressed with the next send sequence number.
    /// </summary>
    /// <param name="payload">The data to carry in the frame's information field.</param>
    /// <returns>The raw frame bytes to transmit.</returns>
    /// <exception cref="InvalidOperationException">The state machine is not <see cref="HdlcConnectionState.Connected"/>.</exception>
    ReadOnlyMemory<byte> CreateInformation(ReadOnlyMemory<byte> payload);

    /// <summary>
    /// Parses and processes a raw received frame, updating the connection state and, for information frames addressed to this station, returning the delivered payload.
    /// </summary>
    /// <param name="data">The raw received frame bytes.</param>
    /// <returns>The <see cref="HdlcReceiveResult"/> describing how the frame was processed.</returns>
    /// <exception cref="HdlcFrameException">The frame is malformed or its control field does not encode a recognized frame kind.</exception>
    HdlcReceiveResult Receive(ReadOnlyMemory<byte> data);
}

/// <summary>
/// <inheritdoc cref="IHdlcStateMachine" />
/// </summary>
/// <param name="options">The station configuration to apply to every frame produced and every frame accepted.</param>
internal sealed class HdlcStateMachine(HdlcStationOptions options) : IHdlcStateMachine
{
    private const int SequenceModulus = 8;

    private int sendSequence;
    private int receiveSequence;

    public HdlcConnectionState State { get; private set; } = HdlcConnectionState.Disconnected;

    public ReadOnlyMemory<byte> CreateConnect()
    {
        sendSequence = 0;
        receiveSequence = 0;
        State = HdlcConnectionState.Connecting;
        return CreateUnnumberedFrame(HdlcFrameKind.SetAsynchronousBalancedMode, poll: true);
    }

    public ReadOnlyMemory<byte> CreateDisconnect()
    {
        State = HdlcConnectionState.Disconnecting;
        return CreateUnnumberedFrame(HdlcFrameKind.Disconnect, poll: true);
    }

    public ReadOnlyMemory<byte> CreateInformation(ReadOnlyMemory<byte> payload)
    {
        if (State != HdlcConnectionState.Connected)
        {
            throw new InvalidOperationException($"Cannot create an information frame while the state machine is {State}.");
        }

        HdlcFrame frame = new()
        {
            Address = options.Address,
            Kind = HdlcFrameKind.Information,
            PollFinal = false,
            SendSequence = sendSequence,
            ReceiveSequence = receiveSequence,
            Payload = payload,
        };
        sendSequence = (sendSequence + 1) % SequenceModulus;
        return frame.ToArray();
    }

    public HdlcReceiveResult Receive(ReadOnlyMemory<byte> data)
    {
        HdlcFrame frame = HdlcFrame.Parse(data.Span);

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
            _ => new HdlcReceiveResult { State = State },
        };
    }

    private HdlcReceiveResult ReceiveSetAsynchronousBalancedMode(HdlcFrame frame)
    {
        sendSequence = 0;
        receiveSequence = 0;
        State = HdlcConnectionState.Connected;
        return new HdlcReceiveResult
        {
            State = State,
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
        if (State == HdlcConnectionState.Connecting)
        {
            sendSequence = 0;
            receiveSequence = 0;
            State = HdlcConnectionState.Connected;
        }
        else if (State == HdlcConnectionState.Disconnecting)
        {
            State = HdlcConnectionState.Disconnected;
        }

        return new HdlcReceiveResult { State = State };
    }

    private HdlcReceiveResult ReceiveTerminal()
    {
        State = HdlcConnectionState.Disconnected;
        return new HdlcReceiveResult { State = State };
    }

    private HdlcReceiveResult ReceiveInformation(HdlcFrame frame)
    {
        if (State != HdlcConnectionState.Connected)
        {
            return new HdlcReceiveResult { State = State };
        }

        if (frame.SendSequence != receiveSequence)
        {
            return new HdlcReceiveResult
            {
                State = State,
                Response = CreateSupervisoryFrame(HdlcFrameKind.Reject, frame.PollFinal),
            };
        }

        receiveSequence = (receiveSequence + 1) % SequenceModulus;
        return new HdlcReceiveResult
        {
            State = State,
            Payload = frame.Payload,
            Response = CreateSupervisoryFrame(HdlcFrameKind.ReceiveReady, frame.PollFinal),
        };
    }

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
