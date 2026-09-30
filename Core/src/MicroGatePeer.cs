namespace BlueHeighliner.MicroGate;

/// <summary>
/// One end of an HDLC asynchronous balanced mode link over a MicroGate SyncLink device. A peer is created first, so observers can subscribe before the link exists, and then started with the port and options to use. It is single use: once <see cref="MicroGatePeerState.Disconnected"/> it cannot be started again.
/// </summary>
public interface IMicroGatePeer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets or sets the delegate that is given the data received from the remote peer, one call per HDLC information frame, in order and without gaps, or <see langword="null"/> (the default) to receive nothing. Each call is given an <see cref="IMemoryOwner{T}"/> whose memory is exactly the frame's data, and which the delegate owns from then on: it must dispose it as soon as it has finished with the data, which returns the memory to the pool, and may keep it or hand it to another thread until then. The peer never disposes it after the call, so an owner that is never disposed is only garbage collected.
    /// </summary>
    /// <remarks>
    /// Data that arrives while no delegate is set is acknowledged to the remote peer and then discarded, so set it before calling <see cref="Start"/>. The delegate is called from a task of the peer's own, never while the peer holds a lock and never from the thread that reads the device, so it may block, including on <see cref="Send(ReadOnlyMemory{byte}, CancellationToken)"/>, and may dispose the peer, without stalling the acknowledgement of received frames. Each call returns before the next one starts, so a slow delegate holds up later frames, which are acknowledged as they arrive and so queue up in memory. An exception thrown by the delegate does not break the connection: it is reported on <see cref="Exceptions"/> and the next frame is delivered. The peer does not dispose the owner it was given, since the delegate may already have handed it on. Data received before the peer became <see cref="MicroGatePeerState.Disconnected"/> is still delivered after it, unless the peer has been disposed, in which case it is discarded.
    /// </remarks>
    Action<IMemoryOwner<byte>>? Receiver { get; set; }

    /// <summary>
    /// Gets a stream of the peer's state transitions, from the state after subscribing onward. The stream completes after emitting <see cref="MicroGatePeerState.Disconnected"/>, once the data received before then has been delivered to <see cref="Receiver"/>.
    /// </summary>
    IObservable<MicroGatePeerState> StateChanged { get; }

    /// <summary>
    /// Gets a stream of the exceptions that occur without breaking the connection, so the peer carries on after each: an exception thrown by the <see cref="Receiver"/> delegate or by a <see cref="StateChanged"/> observer, and a received frame too short to parse, which is dropped. The stream completes together with <see cref="StateChanged"/>.
    /// </summary>
    /// <remarks>
    /// The stream is hot: an exception that occurs while nothing is subscribed is lost, so subscribe before calling <see cref="Start"/>. Exceptions are delivered one at a time, in order, and never while the peer holds a lock, together with the state changes. An exception thrown by an observer of this stream itself is ignored, since there is nowhere left to report it. Failures that do break the connection are not reported here: they end the connection, which <see cref="StateChanged"/> shows.
    /// </remarks>
    IObservable<Exception> Exceptions { get; }

    /// <summary>
    /// Gets the current state of the peer.
    /// </summary>
    MicroGatePeerState State { get; }

    /// <summary>
    /// Gets a value indicating whether the peer is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the largest payload, in bytes, that can be sent in one frame. Reflects <see cref="MicroGatePeerOptions.MaxInfoField"/> from the most recent <see cref="Start"/>, or its default before the peer has been started.
    /// </summary>
    int MaxPayloadSize { get; }

    /// <summary>
    /// Opens the device for the specified port and establishes the link with the remote peer, completing once it is up. The peer sends connection requests at <see cref="MicroGatePeerOptions.RetryInterval"/> (none if that is <see langword="null"/>) and also accepts a request from the remote peer, so the two sides need no fixed initiator.
    /// </summary>
    /// <param name="portName">The name of the serial port the device is attached to.</param>
    /// <param name="address">The HDLC address of this station: carried by every response it sends and expected on every command it receives. Must match the address the remote station sends its commands to.</param>
    /// <param name="remoteAddress">The HDLC address of the remote station: carried by every command this station sends and expected on every response it receives. Must match the remote station's own address, and differ from <paramref name="address"/>. ADCCP and HDLC identify the sender of a response and the receiver of a command through it, so a link always needs two different addresses.</param>
    /// <param name="options">The device and HDLC settings to apply, or <see langword="null"/> to use the defaults.</param>
    /// <param name="cancellation">A token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the peer is <see cref="MicroGatePeerState.Connected"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="address"/> and <paramref name="remoteAddress"/> are the same.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="MicroGatePeerOptions.RetryInterval"/> or <see cref="MicroGatePeerOptions.RetransmitInterval"/> is zero, negative, or longer than <see cref="int.MaxValue"/> milliseconds, <see cref="MicroGatePeerOptions.MaxRetransmissions"/> is zero or negative, <see cref="MicroGatePeerOptions.TransmitWindow"/> is outside 1 to 7, <see cref="MicroGatePeerOptions.MaxInfoField"/> is outside 1 to 4090, or <see cref="MicroGateLinkOptions.ClockSpeed"/> is zero or negative.</exception>
    /// <exception cref="PlatformNotSupportedException">The current operating system is neither Windows nor Linux.</exception>
    /// <exception cref="InvalidOperationException">The peer has already been started or has been disposed.</exception>
    /// <exception cref="IOException">The device could not be opened, or it closed before a connection was established.</exception>
    /// <exception cref="ObjectDisposedException">The peer was disposed before the connection was established.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was canceled.</exception>
    /// <remarks>
    /// An invalid argument or unsupported platform leaves the peer <see cref="MicroGatePeerState.Idle"/>; any failure after that disposes the peer, leaving it <see cref="MicroGatePeerState.Disconnected"/>.
    /// </remarks>
    ValueTask Start(string portName, byte address, byte remoteAddress, MicroGatePeerOptions? options = null, CancellationToken cancellation = default);

    /// <summary>
    /// Sends data to the remote peer as one HDLC information frame. Frames are numbered and kept until the remote peer acknowledges them, and are sent again if it rejects them or does not answer, so data arrives in order and without gaps while the peer stays connected. Only a limited number of frames may be unacknowledged at once; further sends wait for an acknowledgement.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellation">A token that can be used to cancel the send operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the data has been sent.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="data"/> is longer than <see cref="MaxPayloadSize"/>.</exception>
    /// <exception cref="InvalidOperationException">The peer is not connected.</exception>
    /// <exception cref="IOException">The frame could not be written to the device (it is then not sent and not kept, so the send may be repeated), or the peer disconnected while the send waited for the window.</exception>
    ValueTask Send(ReadOnlyMemory<byte> data, CancellationToken cancellation = default);

    /// <summary>
    /// Sends pooled data to the remote peer as one HDLC information frame, with the same delivery guarantees and waiting as <see cref="Send(ReadOnlyMemory{byte}, CancellationToken)"/>.
    /// </summary>
    /// <param name="data">The pooled data to send, all of its memory. Ownership is transferred to the peer, which sends from the memory itself, without copying, and disposes it once the remote peer has acknowledged the frame, or the send has failed or the peer has ended.</param>
    /// <param name="cancellation">A token that can be used to cancel the send operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the data has been sent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The data is longer than <see cref="MaxPayloadSize"/>.</exception>
    /// <exception cref="InvalidOperationException">The peer is not connected.</exception>
    /// <exception cref="IOException">The frame could not be written to the device (it is then not sent and not kept, so the send may be repeated), or the peer disconnected while the send waited for the window.</exception>
    /// <remarks>
    /// The owner is disposed on every failure too, so the caller never disposes it after this call.
    /// </remarks>
    ValueTask Send(IMemoryOwner<byte> data, CancellationToken cancellation = default);
}

/// <summary>
/// An <see cref="IMicroGatePeer"/> over an <see cref="IMicroGateDevice"/> that it opens when started, exchanging HDLC frames through the device's raw bit-framing layer while <see cref="IHdlcStateMachine"/> maintains the asynchronous balanced mode connection, numbers and acknowledges information frames, and generates each frame's address and control bytes.
/// </summary>
public sealed class MicroGatePeer : IMicroGatePeer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MicroGatePeer"/> class.
    /// </summary>
    public MicroGatePeer()
        : this(new LinuxMicroGateDeviceOpener(new LinuxNative()), new WindowsMicroGateDeviceOpener(new WindowsNative()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MicroGatePeer"/> class with the platform specific device openers to choose between.
    /// </summary>
    /// <param name="linuxOpener">The device opener used on Linux.</param>
    /// <param name="windowsOpener">The device opener used on Windows.</param>
    /// <param name="shutdownTimeout">How long disposal waits for the disconnect to be sent and the background work to stop before closing the device anyway, or <see langword="null"/> for five seconds.</param>
    internal MicroGatePeer(IMicroGateDeviceOpener linuxOpener, IMicroGateDeviceOpener windowsOpener, TimeSpan? shutdownTimeout = null)
    {
        this.linuxOpener = linuxOpener;
        this.windowsOpener = windowsOpener;
        this.shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(5);
        StateChanged = Isolate(stateChanged, Report);
        Exceptions = Isolate(exceptions, static _ => { });
        connectionEstablished.Task.ContinueWith(static task => task.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
    }

    private readonly IMicroGateDeviceOpener linuxOpener;
    private readonly IMicroGateDeviceOpener windowsOpener;
    private readonly TimeSpan shutdownTimeout;
    private readonly Channel<IMemoryOwner<byte>> delivery = Channel.CreateUnbounded<IMemoryOwner<byte>>();
    private readonly Subject<MicroGatePeerState> stateChanged = new();
    private readonly Subject<Exception> exceptions = new();
    private readonly Queue<Exception> pendingExceptions = new();
    private readonly Queue<Action> notifications = new();
    private readonly Lock stateLock = new();
    private readonly Lock writeLock = new();
    private readonly Lock protocolLock = new();
    private readonly AsyncLocal<bool> insideReceiver = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource connectionEstablished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly int maxFrameSize = 65535;
    private readonly int maxTransmitWindow = 7;
    private readonly int maxInfoField = 4090;
    private readonly TimeSpan maxInterval = TimeSpan.FromMilliseconds(int.MaxValue);
    private MicroGatePeerOptions options = new();
    private TaskCompletionSource? peerBusyGate;
    private IHdlcStateMachine? stateMachine;
    private SemaphoreSlim? sendWindow;
    private IMicroGateDevice? device;
    private Task receiveLoopTask = Task.CompletedTask;
    private Task retransmitTask = Task.CompletedTask;
    private Task deliveryTask = Task.CompletedTask;
    private bool deliveryStarted;
    private Action<IMemoryOwner<byte>>? receiver;
    private MicroGatePeerState state = MicroGatePeerState.Idle;
    private long unacknowledgedSince;
    private int retransmitAttempts;
    private int notifyingThreadId;
    private int inboundPending;
    private bool finished;
    private bool disposed;

    /// <inheritdoc />
    public Action<IMemoryOwner<byte>>? Receiver
    {
        get => Volatile.Read(ref receiver);
        set => Volatile.Write(ref receiver, value);
    }

    /// <inheritdoc />
    public IObservable<MicroGatePeerState> StateChanged { get; }

    /// <inheritdoc />
    public IObservable<Exception> Exceptions { get; }

    /// <inheritdoc />
    public MicroGatePeerState State
    {
        get
        {
            lock (stateLock)
            {
                return state;
            }
        }
    }

    /// <inheritdoc />
    public bool IsConnected => State == MicroGatePeerState.Connected;

    /// <inheritdoc />
    public int MaxPayloadSize => options.MaxInfoField;

    private IHdlcStateMachine Machine => stateMachine ?? throw new InvalidOperationException("The peer has not been started.");

    /// <inheritdoc />
    public async ValueTask Start(string portName, byte address, byte remoteAddress, MicroGatePeerOptions? options = null, CancellationToken cancellation = default)
    {
        if (address == remoteAddress)
        {
            throw new ArgumentException("The address and the remote address must be different.", nameof(remoteAddress));
        }

        options ??= new();
        ValidateOptions(options);
        IMicroGateDeviceOpener selectedOpener = SelectOpener();

        lock (stateLock)
        {
            if (state != MicroGatePeerState.Idle || disposed)
            {
                throw new InvalidOperationException("The peer has already been started or has been disposed.");
            }

            this.options = options;
            stateMachine = new HdlcStateMachine(options, address, remoteAddress);
            sendWindow = new SemaphoreSlim(stateMachine.WindowSize, stateMachine.WindowSize);
            SetState(MicroGatePeerState.Connecting);
        }

        Notify();

        try
        {
            IMicroGateDevice opened = await Task.Run(() => selectedOpener.Open(portName, options)).ConfigureAwait(false);

            lock (stateLock)
            {
                if (disposed)
                {
                    opened.Dispose();
                    throw new ObjectDisposedException(nameof(MicroGatePeer));
                }

                device = opened;
                receiveLoopTask = Task.Run(ReceiveLoop);
                deliveryStarted = true;
                deliveryTask = Task.Run(Deliver);
                retransmitTask = options.RetransmitInterval is null ? Task.CompletedTask : Task.Run(Retransmit);
            }

            cancellation.ThrowIfCancellationRequested();
            await Establish(cancellation).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not ObjectDisposedException)
        {
            bool disposedElsewhere;
            lock (stateLock)
            {
                disposedElsewhere = disposed;
            }

            await DisposeAsync().ConfigureAwait(false);

            if (disposedElsewhere)
            {
                throw new ObjectDisposedException(nameof(MicroGatePeer), exception);
            }

            throw;
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask Send(ReadOnlyMemory<byte> data, CancellationToken cancellation = default)
    {
        ValidateSize(data.Length);
        IHdlcStateMachine machine = RequireConnected();
        await WaitForPeerReady(cancellation).ConfigureAwait(false);
        await AcquireSendSlot(cancellation).ConfigureAwait(false);
        await SendFrame(machine, created => created.CreateInformation(data), cancellation).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask Send(IMemoryOwner<byte> data, CancellationToken cancellation = default)
    {
        bool handedOver = false;

        try
        {
            ValidateSize(data.Memory.Length);
            IHdlcStateMachine machine = RequireConnected();
            await WaitForPeerReady(cancellation).ConfigureAwait(false);
            await AcquireSendSlot(cancellation).ConfigureAwait(false);
            await SendFrame(
                machine,
                created =>
                {
                    ReadOnlyMemory<byte> frame = created.CreateInformation(data);
                    handedOver = true;
                    return frame;
                },
                cancellation).ConfigureAwait(false);
        }
        finally
        {
            if (!handedOver)
            {
                data.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IMicroGateDevice? opened;
        bool fromCallback;
        lock (stateLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            opened = device;
            fromCallback = notifyingThreadId == Environment.CurrentManagedThreadId || insideReceiver.Value;
        }

        if (opened is not null)
        {
            if (Protocol(() => Machine.State) == HdlcConnectionState.Connected)
            {
                await SendDisconnect(opened).ConfigureAwait(false);
            }

            await lifetime.CancelAsync().ConfigureAwait(false);
            opened.DisableReceiver();

            if (fromCallback)
            {
                _ = CloseWhenStopped(opened);
            }
            else
            {
                await CloseWhenStopped(opened).ConfigureAwait(false);
            }
        }

        Finish();
    }

    private void ValidateOptions(MicroGatePeerOptions candidate)
    {
        if (candidate.RetryInterval <= TimeSpan.Zero || candidate.RetryInterval > maxInterval)
        {
            throw new ArgumentOutOfRangeException("options", "The retry interval must be greater than zero and at most int.MaxValue milliseconds.");
        }

        if (candidate.RetransmitInterval <= TimeSpan.Zero || candidate.RetransmitInterval > maxInterval)
        {
            throw new ArgumentOutOfRangeException("options", "The retransmit interval must be greater than zero and at most int.MaxValue milliseconds.");
        }

        if (candidate.MaxRetransmissions <= 0)
        {
            throw new ArgumentOutOfRangeException("options", "The maximum number of retransmissions must be greater than zero.");
        }

        if (candidate.TransmitWindow < 1 || candidate.TransmitWindow > maxTransmitWindow)
        {
            throw new ArgumentOutOfRangeException("options", $"The transmit window must be between 1 and {maxTransmitWindow}.");
        }

        if (candidate.MaxInfoField < 1 || candidate.MaxInfoField > maxInfoField)
        {
            throw new ArgumentOutOfRangeException("options", $"The maximum info field size must be between 1 and {maxInfoField} bytes.");
        }

        if (candidate.Link.ClockSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException("options", "The clock speed must be greater than zero.");
        }
    }

    private IMicroGateDeviceOpener SelectOpener()
    {
        if (OperatingSystem.IsWindows())
        {
            return windowsOpener;
        }

        if (OperatingSystem.IsLinux())
        {
            return linuxOpener;
        }

        throw new PlatformNotSupportedException("MicroGate SyncLink devices are only supported on Windows and Linux.");
    }

    private IHdlcStateMachine RequireConnected()
    {
        IHdlcStateMachine machine = Machine;
        if (!IsConnected)
        {
            throw new InvalidOperationException("The peer is not connected.");
        }

        return machine;
    }

    private async Task Establish(CancellationToken cancellation)
    {
        while (true)
        {
            if (options.RetryInterval is not null)
            {
                await Task.Run(() => WriteFrame(CreateConnectRequest), cancellation).ConfigureAwait(false);
            }

            try
            {
                await connectionEstablished.Task.WaitAsync(options.RetryInterval ?? Timeout.InfiniteTimeSpan, cancellation).ConfigureAwait(false);
                return;
            }
            catch (TimeoutException)
            {
            }
        }
    }

    private async Task SendDisconnect(IMicroGateDevice opened)
    {
        try
        {
            await Task.Run(() => WriteFrame(() => Protocol(() => Machine.CreateDisconnect()))).WaitAsync(shutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            opened.DisableTransmitter();
        }
        catch (IOException)
        {
        }
    }

    private async Task CloseWhenStopped(IMicroGateDevice opened)
    {
        try
        {
            await WaitForBackgroundTasks().WaitAsync(shutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }

        opened.Dispose();
    }

    private void ValidateSize(int length)
    {
        if (length > MaxPayloadSize)
        {
            throw new ArgumentOutOfRangeException("data", length, $"A payload can be at most {MaxPayloadSize} bytes.");
        }
    }

    private async Task WaitForPeerReady(CancellationToken cancellation)
    {
        TaskCompletionSource? gate;
        lock (stateLock)
        {
            gate = peerBusyGate;
        }

        if (gate is null)
        {
            return;
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        try
        {
            await gate.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new IOException("The peer disconnected while waiting to send.");
        }
    }

    private void UpdatePeerBusy(bool busy)
    {
        TaskCompletionSource? released = null;
        lock (stateLock)
        {
            if (busy && peerBusyGate is null)
            {
                peerBusyGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            else if (!busy && peerBusyGate is not null)
            {
                released = peerBusyGate;
                peerBusyGate = null;
            }
        }

        released?.TrySetResult();
    }

    private async Task AcquireSendSlot(CancellationToken cancellation)
    {
        SemaphoreSlim window = sendWindow!;
        if (await window.WaitAsync(0, CancellationToken.None).ConfigureAwait(false))
        {
            return;
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        try
        {
            await window.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new IOException("The peer disconnected while waiting to send.");
        }
    }

    private async Task SendFrame(IHdlcStateMachine machine, Func<IHdlcStateMachine, ReadOnlyMemory<byte>> createInformation, CancellationToken cancellation)
    {
        bool holdsFrame = false;

        try
        {
            await Task.Run(
                () => WriteFrame(
                    () =>
                    {
                        ReadOnlyMemory<byte> frame = Protocol(() => createInformation(machine));
                        holdsFrame = true;
                        return frame;
                    },
                    () =>
                    {
                        Protocol(() =>
                        {
                            machine.DiscardLastInformation();
                            if (machine.OutstandingCount == 0)
                            {
                                unacknowledgedSince = 0;
                            }

                            return 0;
                        });
                        holdsFrame = false;
                    },
                    () => Protocol(() =>
                    {
                        if (unacknowledgedSince == 0 && machine.OutstandingCount > 0)
                        {
                            unacknowledgedSince = Stopwatch.GetTimestamp();
                        }

                        return 0;
                    })),
                cancellation).ConfigureAwait(false);
        }
        catch
        {
            if (!holdsFrame)
            {
                ReleaseSendSlots(1);
            }

            throw;
        }
    }

    private void ReleaseSendSlots(int count)
    {
        try
        {
            sendWindow?.Release(count);
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private ReadOnlyMemory<byte> CreateConnectRequest() =>
        Protocol(() => Machine.State == HdlcConnectionState.Connected ? ReadOnlyMemory<byte>.Empty : Machine.CreateConnect());

    private T Protocol<T>(Func<T> action)
    {
        lock (protocolLock)
        {
            return action();
        }
    }

    private void ReceiveLoop()
    {
        Exception? failure = null;

        try
        {
            byte[] buffer = new byte[maxFrameSize];
            bool wasConnected = false;

            while (true)
            {
                int bytesRead = device!.Read(buffer);
                if (bytesRead <= 0)
                {
                    break;
                }

                Volatile.Write(ref inboundPending, 1);
                HdlcReceiveResult? result;
                try
                {
                    result = Process(buffer.AsMemory(0, bytesRead));
                }
                finally
                {
                    Volatile.Write(ref inboundPending, 0);
                }

                if (result is null)
                {
                    Notify();
                    continue;
                }

                if (result.Acknowledged > 0)
                {
                    ReleaseSendSlots(result.Acknowledged);
                }

                UpdatePeerBusy(Protocol(() => Machine.PeerBusy));

                if (result.Payload is { } payload && Receiver is not null)
                {
                    PooledBuffer owner = new(payload.Span);
                    if (!delivery.Writer.TryWrite(owner))
                    {
                        owner.Dispose();
                    }
                }

                if (result.State == HdlcConnectionState.Connected)
                {
                    wasConnected = true;
                    lock (stateLock)
                    {
                        SetState(MicroGatePeerState.Connected);
                    }

                    connectionEstablished.TrySetResult();
                }
                else if (wasConnected)
                {
                    break;
                }

                Notify();
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        connectionEstablished.TrySetException(new IOException(failure is null ? "The device closed before a connection was established." : "The device failed.", failure));
        Finish();
    }

    private HdlcReceiveResult? Process(ReadOnlyMemory<byte> frame)
    {
        lock (writeLock)
        {
            HdlcReceiveResult result;
            try
            {
                result = Protocol(() =>
                {
                    HdlcReceiveResult outcome = Machine.Receive(frame);
                    if (outcome.Acknowledged > 0)
                    {
                        unacknowledgedSince = Machine.OutstandingCount > 0 ? Stopwatch.GetTimestamp() : 0;
                        retransmitAttempts = 0;
                    }

                    return outcome;
                });
            }
            catch (HdlcFrameException exception)
            {
                Enqueue(exception);
                return null;
            }

            if (result.Response is { } response)
            {
                device!.Write(response);
            }

            if (result.Retransmit)
            {
                foreach (ReadOnlyMemory<byte> retransmission in CreateRetransmission())
                {
                    device!.Write(retransmission);
                }

                RestartAcknowledgementTimer();
            }

            return result;
        }
    }

    private bool IsWriting()
    {
        if (!writeLock.TryEnter())
        {
            return true;
        }

        writeLock.Exit();
        return false;
    }

    private void RestartAcknowledgementTimer() =>
        Protocol(() =>
        {
            if (stateMachine is { OutstandingCount: > 0 })
            {
                unacknowledgedSince = Stopwatch.GetTimestamp();
            }

            return 0;
        });

    private IReadOnlyList<ReadOnlyMemory<byte>> CreateRetransmission() =>
        Protocol(() => Machine.State == HdlcConnectionState.Connected ? Machine.CreateRetransmission() : []);

    private async Task Retransmit()
    {
        TimeSpan interval = options.RetransmitInterval!.Value;
        long intervalTicks = (long)(interval.TotalSeconds * Stopwatch.Frequency);

        try
        {
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(Math.Max(1, interval.TotalMilliseconds / 2)));
            while (await timer.WaitForNextTickAsync(lifetime.Token).ConfigureAwait(false))
            {
                if (Volatile.Read(ref inboundPending) != 0 || IsWriting())
                {
                    continue;
                }

                (bool resend, bool giveUp) = Protocol(() =>
                {
                    if (unacknowledgedSince == 0 || Stopwatch.GetTimestamp() - unacknowledgedSince < intervalTicks)
                    {
                        return (false, false);
                    }

                    unacknowledgedSince = Stopwatch.GetTimestamp();
                    retransmitAttempts++;
                    return (true, options.MaxRetransmissions is { } limit && retransmitAttempts > limit);
                });

                if (giveUp)
                {
                    Fail(new IOException("The remote peer stopped acknowledging."));
                    return;
                }

                if (resend)
                {
                    WriteFrames(CreateRetransmission);
                    RestartAcknowledgementTimer();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void Fail(Exception cause)
    {
        connectionEstablished.TrySetException(new IOException("The link failed.", cause));
        Finish();

        try
        {
            device?.DisableReceiver();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }
    }

    private async Task WaitForBackgroundTasks()
    {
        try
        {
            await Task.WhenAll(receiveLoopTask, retransmitTask, deliveryTask).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private IObservable<T> Isolate<T>(IObservable<T> source, Action<Exception> onObserverFailure) =>
        Observable.Create<T>(observer => source.Subscribe(
            value =>
            {
                try
                {
                    observer.OnNext(value);
                }
                catch (Exception exception)
                {
                    onObserverFailure(exception);
                }
            },
            observer.OnError,
            () =>
            {
                try
                {
                    observer.OnCompleted();
                }
                catch (Exception exception)
                {
                    onObserverFailure(exception);
                }
            }));

    private void Enqueue(Exception exception)
    {
        lock (stateLock)
        {
            pendingExceptions.Enqueue(exception);
            notifications.Enqueue(PublishExceptions);
        }
    }

    private void PublishExceptions()
    {
        while (true)
        {
            Exception? next;
            lock (stateLock)
            {
                if (!pendingExceptions.TryDequeue(out next))
                {
                    return;
                }
            }

            exceptions.OnNext(next);
        }
    }

    private void CompleteStreams()
    {
        stateChanged.OnCompleted();
        PublishExceptions();
        exceptions.OnCompleted();
    }

    private void Report(Exception exception)
    {
        Enqueue(exception);
        Notify();
    }

    private async Task Deliver()
    {
        insideReceiver.Value = true;

        await foreach (IMemoryOwner<byte> owner in delivery.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            Action<IMemoryOwner<byte>>? current = Receiver;
            if (current is null || IsDisposed())
            {
                owner.Dispose();
                continue;
            }

            try
            {
                current(owner);
            }
            catch (Exception exception)
            {
                Report(exception);
            }
        }

        lock (stateLock)
        {
            notifications.Enqueue(CompleteStreams);
        }

        Notify();
    }

    private bool IsDisposed()
    {
        lock (stateLock)
        {
            return disposed;
        }
    }

    private void WriteFrame(Func<ReadOnlyMemory<byte>> createFrame, Action? onFailure = null, Action? onWritten = null)
    {
        lock (writeLock)
        {
            ReadOnlyMemory<byte> frame = createFrame();
            if (frame.IsEmpty)
            {
                return;
            }

            try
            {
                device!.Write(frame);
            }
            catch
            {
                onFailure?.Invoke();
                throw;
            }

            onWritten?.Invoke();
        }
    }

    private void WriteFrames(Func<IReadOnlyList<ReadOnlyMemory<byte>>> createFrames)
    {
        lock (writeLock)
        {
            foreach (ReadOnlyMemory<byte> frame in createFrames())
            {
                device!.Write(frame);
            }
        }
    }

    private void Finish()
    {
        lock (stateLock)
        {
            if (finished)
            {
                return;
            }

            finished = true;
            SetState(MicroGatePeerState.Disconnected);
            if (!deliveryStarted)
            {
                notifications.Enqueue(CompleteStreams);
            }
        }

        delivery.Writer.TryComplete();
        lifetime.Cancel();
        Protocol(() =>
        {
            stateMachine?.Dispose();
            return 0;
        });
        Notify();
    }

    private void SetState(MicroGatePeerState newState)
    {
        if (state == MicroGatePeerState.Disconnected || state == newState)
        {
            return;
        }

        state = newState;
        notifications.Enqueue(() => stateChanged.OnNext(newState));
    }

    private void Notify()
    {
        while (true)
        {
            Action next;
            lock (stateLock)
            {
                if (notifyingThreadId != 0 || !notifications.TryDequeue(out Action? dequeued))
                {
                    return;
                }

                next = dequeued;
                notifyingThreadId = Environment.CurrentManagedThreadId;
            }

            try
            {
                next();
            }
            finally
            {
                lock (stateLock)
                {
                    notifyingThreadId = 0;
                }
            }
        }
    }
}
