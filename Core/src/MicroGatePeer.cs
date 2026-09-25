namespace BlueHeighliner.MicroGate;

/// <summary>
/// One end of an HDLC asynchronous balanced mode link over a MicroGate SyncLink device. A peer is created first, so observers can subscribe before the link exists, and then started with the port and options to use. It is single use: once <see cref="MicroGatePeerState.Disconnected"/> it cannot be started again.
/// </summary>
public interface IMicroGatePeer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets a stream of the data received from the remote peer, one item per HDLC information frame, in order and without gaps. The stream completes when the peer becomes <see cref="MicroGatePeerState.Disconnected"/>.
    /// </summary>
    /// <remarks>
    /// The stream is hot: data that arrives while nothing is subscribed is acknowledged to the remote peer and then discarded, so subscribe before calling <see cref="Start"/>. Every observer is given the same memory, which is backed by an array allocated for that frame and is never reused, so it may be kept or handed to another thread. It is shared and must not be modified. An observer that throws from <c>OnNext</c> is unsubscribed and does not affect other observers or the peer.
    /// </remarks>
    IObservable<ReadOnlyMemory<byte>> Received { get; }

    /// <summary>
    /// Gets a stream of the peer's state transitions, from the state after subscribing onward. The stream completes after emitting <see cref="MicroGatePeerState.Disconnected"/>.
    /// </summary>
    IObservable<MicroGatePeerState> StateChanged { get; }

    /// <summary>
    /// Gets the current state of the peer.
    /// </summary>
    MicroGatePeerState State { get; }

    /// <summary>
    /// Gets a value indicating whether the peer is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the largest payload, in bytes, that can be sent in one frame. The MicroGate drivers discard received frames larger than 4096 bytes, and the frame also carries an address, a control field, and a frame check sequence.
    /// </summary>
    int MaxPayloadSize { get; }

    /// <summary>
    /// Opens the device for the specified port and establishes the link with the remote peer, completing once it is up. The peer sends connection requests at <see cref="MicroGatePeerOptions.RetryInterval"/> (none if that is <see langword="null"/>) and also accepts a request from the remote peer, so the two sides need no fixed initiator.
    /// </summary>
    /// <param name="portName">The name of the serial port the device is attached to.</param>
    /// <param name="options">The device and HDLC settings to apply, or <see langword="null"/> to use the defaults.</param>
    /// <param name="cancellation">A token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the peer is <see cref="MicroGatePeerState.Connected"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="MicroGatePeerOptions.RetryInterval"/>, <see cref="MicroGatePeerOptions.RetransmitInterval"/>, or <see cref="MicroGatePeerOptions.MaxRetransmissions"/> is zero or negative.</exception>
    /// <exception cref="PlatformNotSupportedException">The current operating system is neither Windows nor Linux.</exception>
    /// <exception cref="InvalidOperationException">The peer has already been started or has been disposed.</exception>
    /// <exception cref="IOException">The device could not be opened, or it closed before a connection was established.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was canceled.</exception>
    /// <remarks>
    /// An invalid argument or unsupported platform leaves the peer <see cref="MicroGatePeerState.Idle"/>; any failure after that disposes the peer, leaving it <see cref="MicroGatePeerState.Disconnected"/>.
    /// </remarks>
    ValueTask Start(string portName, MicroGatePeerOptions? options = null, CancellationToken cancellation = default);

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
    /// <param name="data">The pooled data to send. Ownership is transferred to the peer, which disposes it once the data has been sent or the send has failed.</param>
    /// <param name="cancellation">A token that can be used to cancel the send operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the data has been sent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The data is longer than <see cref="MaxPayloadSize"/>.</exception>
    /// <exception cref="InvalidOperationException">The peer is not connected.</exception>
    /// <exception cref="IOException">The frame could not be written to the device (it is then not sent and not kept, so the send may be repeated), or the peer disconnected while the send waited for the window.</exception>
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
        Received = Isolate(received);
        StateChanged = Isolate(stateChanged);
        connectionEstablished.Task.ContinueWith(static task => task.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
    }

    private readonly IMicroGateDeviceOpener linuxOpener;
    private readonly IMicroGateDeviceOpener windowsOpener;
    private readonly TimeSpan shutdownTimeout;
    private readonly Subject<ReadOnlyMemory<byte>> received = new();
    private readonly Subject<MicroGatePeerState> stateChanged = new();
    private readonly Queue<Action> notifications = new();
    private readonly Lock stateLock = new();
    private readonly Lock writeLock = new();
    private readonly Lock protocolLock = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource connectionEstablished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly int maxFrameSize = 65535;
    private MicroGatePeerOptions options = new();
    private IHdlcStateMachine? stateMachine;
    private SemaphoreSlim? sendWindow;
    private IMicroGateDevice? device;
    private Task receiveLoopTask = Task.CompletedTask;
    private Task retransmitTask = Task.CompletedTask;
    private MicroGatePeerState state = MicroGatePeerState.Idle;
    private long unacknowledgedSince;
    private int retransmitAttempts;
    private int loopThreadId;
    private bool notifying;
    private bool finished;
    private bool disposed;
    private bool disposeOnLoopExit;

    /// <inheritdoc />
    public IObservable<ReadOnlyMemory<byte>> Received { get; }

    /// <inheritdoc />
    public IObservable<MicroGatePeerState> StateChanged { get; }

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
    public int MaxPayloadSize { get; } = 4090;

    private IHdlcStateMachine Machine => stateMachine ?? throw new InvalidOperationException("The peer has not been started.");

    /// <inheritdoc />
    public async ValueTask Start(string portName, MicroGatePeerOptions? options = null, CancellationToken cancellation = default)
    {
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
            stateMachine = new HdlcStateMachine(options);
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
                retransmitTask = options.RetransmitInterval is null ? Task.CompletedTask : Task.Run(Retransmit);
            }

            cancellation.ThrowIfCancellationRequested();
            await Establish(cancellation).ConfigureAwait(false);
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
        await AcquireSendSlot(cancellation).ConfigureAwait(false);
        await SendFrame(machine, data, cancellation).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask Send(IMemoryOwner<byte> data, CancellationToken cancellation = default)
    {
        using (data)
        {
            ValidateSize(data.Memory.Length);
            IHdlcStateMachine machine = RequireConnected();
            await AcquireSendSlot(cancellation).ConfigureAwait(false);
            await SendFrame(machine, data.Memory, cancellation).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IMicroGateDevice? opened;
        lock (stateLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            opened = device;
        }

        if (opened is not null)
        {
            bool onLoopThread = Environment.CurrentManagedThreadId == Volatile.Read(ref loopThreadId);

            if (Protocol(() => Machine.State) == HdlcConnectionState.Connected)
            {
                await SendDisconnect(opened, onLoopThread).ConfigureAwait(false);
            }

            await lifetime.CancelAsync().ConfigureAwait(false);
            opened.DisableReceiver();

            if (onLoopThread)
            {
                Volatile.Write(ref disposeOnLoopExit, true);
            }
            else
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
        }

        Finish();
    }

    private void ValidateOptions(MicroGatePeerOptions candidate)
    {
        if (candidate.RetryInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException("options", "The retry interval must be greater than zero.");
        }

        if (candidate.RetransmitInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException("options", "The retransmit interval must be greater than zero.");
        }

        if (candidate.MaxRetransmissions <= 0)
        {
            throw new ArgumentOutOfRangeException("options", "The maximum number of retransmissions must be greater than zero.");
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

    private async Task SendDisconnect(IMicroGateDevice opened, bool onLoopThread)
    {
        Task write = Task.Run(() => WriteFrame(() => Protocol(() => Machine.CreateDisconnect())));

        try
        {
            if (onLoopThread)
            {
                if (!write.Wait(shutdownTimeout))
                {
                    opened.DisableTransmitter();
                }
            }
            else
            {
                await write.WaitAsync(shutdownTimeout).ConfigureAwait(false);
            }
        }
        catch (TimeoutException)
        {
            opened.DisableTransmitter();
        }
        catch (Exception exception) when (exception is IOException or AggregateException)
        {
        }
    }

    private void ValidateSize(int length)
    {
        if (length > MaxPayloadSize)
        {
            throw new ArgumentOutOfRangeException("data", length, $"A payload can be at most {MaxPayloadSize} bytes.");
        }
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

    private async Task SendFrame(IHdlcStateMachine machine, ReadOnlyMemory<byte> data, CancellationToken cancellation)
    {
        bool holdsFrame = false;

        try
        {
            await Task.Run(
                () => WriteFrame(
                    () =>
                    {
                        ReadOnlyMemory<byte> frame = Protocol(() =>
                        {
                            ReadOnlyMemory<byte> created = machine.CreateInformation(data);
                            if (unacknowledgedSince == 0)
                            {
                                unacknowledgedSince = Stopwatch.GetTimestamp();
                            }

                            return created;
                        });
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
                    }),
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
        Volatile.Write(ref loopThreadId, Environment.CurrentManagedThreadId);
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

                HdlcReceiveResult result;
                try
                {
                    result = Protocol(() =>
                    {
                        HdlcReceiveResult outcome = Machine.Receive(buffer.AsMemory(0, bytesRead));
                        if (outcome.Acknowledged > 0)
                        {
                            unacknowledgedSince = Machine.OutstandingCount > 0 ? Stopwatch.GetTimestamp() : 0;
                            retransmitAttempts = 0;
                        }

                        return outcome;
                    });
                }
                catch (HdlcFrameException)
                {
                    continue;
                }

                if (result.Acknowledged > 0)
                {
                    ReleaseSendSlots(result.Acknowledged);
                }

                if (result.Response is { } response)
                {
                    WriteFrame(() => response);
                }

                if (result.Retransmit)
                {
                    WriteFrames(CreateRetransmission);
                }

                if (result.Payload is { } payload && this.received.HasObservers)
                {
                    this.received.OnNext(payload.ToArray());
                }

                if (result.State == HdlcConnectionState.Connected)
                {
                    wasConnected = true;
                    lock (stateLock)
                    {
                        SetState(MicroGatePeerState.Connected);
                    }

                    Notify();
                    connectionEstablished.TrySetResult();
                }
                else if (wasConnected)
                {
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        connectionEstablished.TrySetException(new IOException(failure is null ? "The device closed before a connection was established." : "The device failed.", failure));
        Finish();

        if (Volatile.Read(ref disposeOnLoopExit))
        {
            try
            {
                retransmitTask.Wait(shutdownTimeout);
            }
            catch (AggregateException)
            {
            }

            device!.Dispose();
        }

        Volatile.Write(ref loopThreadId, 0);
    }

    private IReadOnlyList<ReadOnlyMemory<byte>> CreateRetransmission() =>
        Protocol(() => Machine.State == HdlcConnectionState.Connected ? Machine.CreateRetransmission() : []);

    private async Task Retransmit()
    {
        TimeSpan interval = options.RetransmitInterval!.Value;
        long intervalTicks = (long)(interval.TotalSeconds * Stopwatch.Frequency);

        try
        {
            using PeriodicTimer timer = new(TimeSpan.FromTicks(Math.Max(1, interval.Ticks / 2)));
            while (await timer.WaitForNextTickAsync(lifetime.Token).ConfigureAwait(false))
            {
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
            await Task.WhenAll(receiveLoopTask, retransmitTask).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private IObservable<T> Isolate<T>(IObservable<T> source) =>
        Observable.Create<T>(observer => source.Subscribe(
            value =>
            {
                try
                {
                    observer.OnNext(value);
                }
                catch
                {
                }
            },
            observer.OnError,
            () =>
            {
                try
                {
                    observer.OnCompleted();
                }
                catch
                {
                }
            }));

    private void WriteFrame(Func<ReadOnlyMemory<byte>> createFrame, Action? onFailure = null)
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
            notifications.Enqueue(() =>
            {
                received.OnCompleted();
                stateChanged.OnCompleted();
            });
        }

        lifetime.Cancel();
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
                if (notifying || !notifications.TryDequeue(out Action? dequeued))
                {
                    return;
                }

                next = dequeued;
                notifying = true;
            }

            try
            {
                next();
            }
            finally
            {
                lock (stateLock)
                {
                    notifying = false;
                }
            }
        }
    }
}
