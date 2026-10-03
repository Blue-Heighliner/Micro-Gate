namespace BlueHeighliner.MicroGate;

/// <summary>
/// One end of an asynchronous serial link over a MicroGate SyncLink device: a plain byte stream, with start and stop bits and optional parity on every character and no frames, addresses, acknowledgements, or connection. A peer is created first, so observers can subscribe before the device is open, and then started with the port and settings to use. It is single use: once <see cref="UartPeerState.Closed"/> it cannot be started again.
/// </summary>
public interface IUartPeer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets or sets the delegate that is given the bytes received from the remote station, or <see langword="null"/> (the default) to receive nothing. Each call is given an <see cref="IMemoryOwner{T}"/> holding the bytes that arrived together, in order and without gaps. A byte stream has no message boundaries, so the chunks are an artifact of how the device delivered the bytes and a message may span several or share one. The delegate owns the owner from then on and must dispose it as soon as it has finished with the data.
    /// </summary>
    /// <remarks>
    /// Bytes that arrive while no delegate is set are discarded, so set it before calling <see cref="Start"/>. The delegate is called from a task of the peer's own, never while the peer holds a lock and never from the thread that reads the device, so it may block, including on <see cref="Send"/>, and may dispose the peer. Each call returns before the next one starts, so a slow delegate holds up later chunks, which queue up in memory. An exception thrown by the delegate is reported on <see cref="Exceptions"/> and the next chunk is delivered. Bytes received before the peer became <see cref="UartPeerState.Closed"/> are still delivered after it, unless the peer has been disposed, in which case they are discarded.
    /// </remarks>
    Action<IMemoryOwner<byte>>? Receiver { get; set; }

    /// <summary>
    /// Gets a stream of the peer's state transitions, from the state after subscribing onward. The stream completes after emitting <see cref="UartPeerState.Closed"/>, once the bytes received before then have been delivered to <see cref="Receiver"/>.
    /// </summary>
    IObservable<UartPeerState> StateChanged { get; }

    /// <summary>
    /// Gets a stream of the exceptions that occur without closing the peer: an exception thrown by the <see cref="Receiver"/> delegate or by a <see cref="StateChanged"/> observer. The stream completes together with <see cref="StateChanged"/>.
    /// </summary>
    /// <remarks>
    /// The stream is hot: an exception that occurs while nothing is subscribed is lost, so subscribe before calling <see cref="Start"/>. An exception thrown by an observer of this stream itself is ignored. Failures that close the peer are not reported here: they end it, which <see cref="StateChanged"/> shows.
    /// </remarks>
    IObservable<Exception> Exceptions { get; }

    /// <summary>
    /// Gets the current state of the peer.
    /// </summary>
    UartPeerState State { get; }

    /// <summary>
    /// Opens the device for the specified port, applies the settings, and starts receiving, completing once the peer is <see cref="UartPeerState.Open"/>. Nothing is transmitted until the first <see cref="Send"/>, so a peer that only receives leaves the transmit line at its idle state.
    /// </summary>
    /// <param name="portName">The name of the serial port the device is attached to.</param>
    /// <param name="options">The asynchronous settings to apply, or <see langword="null"/> to use the defaults.</param>
    /// <param name="cancellation">A token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the peer is open.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="UartPeerOptions.BaudRate"/> is zero or negative, <see cref="UartPeerOptions.DataBits"/> is outside 5 to 8, or <see cref="UartPeerOptions.StopBits"/> or <see cref="UartPeerOptions.Parity"/> is not a defined value.</exception>
    /// <exception cref="PlatformNotSupportedException">The current operating system is neither Windows nor Linux.</exception>
    /// <exception cref="InvalidOperationException">The peer has already been started or has been disposed.</exception>
    /// <exception cref="IOException">The device could not be opened or configured.</exception>
    /// <exception cref="ObjectDisposedException">The peer was disposed before the device finished opening.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was canceled.</exception>
    /// <remarks>
    /// An invalid argument or unsupported platform leaves the peer <see cref="UartPeerState.Idle"/>; any failure after that disposes the peer, leaving it <see cref="UartPeerState.Closed"/>.
    /// </remarks>
    ValueTask Start(string portName, UartPeerOptions? options = null, CancellationToken cancellation = default);

    /// <summary>
    /// Sends bytes to the remote station, completing once all of them have been handed to the device. Sends are serialized, so the bytes of one call are never interleaved with those of another. There is no acknowledgement or flow control at this level: nothing tells the sender whether the remote station received the bytes, or was fast enough to keep up.
    /// </summary>
    /// <param name="data">The bytes to send, any length. They are written in pieces the device accepts, and the memory must stay valid until the call completes.</param>
    /// <param name="cancellation">A token that can be used to cancel the send operation, which stops before the next piece.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the data has been handed to the device.</returns>
    /// <exception cref="InvalidOperationException">The peer is not <see cref="UartPeerState.Open"/>.</exception>
    /// <exception cref="IOException">The bytes could not be written to the device.</exception>
    ValueTask Send(ReadOnlyMemory<byte> data, CancellationToken cancellation = default);
}

/// <summary>
/// An <see cref="IUartPeer"/> over an <see cref="IMicroGateDevice"/> that it opens when started, reading and writing the device's raw characters.
/// </summary>
public sealed class UartPeer : IUartPeer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UartPeer"/> class.
    /// </summary>
    public UartPeer()
        : this(new LinuxUartDeviceOpener(new LinuxNative()), new WindowsUartDeviceOpener(new WindowsNative()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UartPeer"/> class with the platform specific device openers to choose between.
    /// </summary>
    /// <param name="linuxOpener">The device opener used on Linux.</param>
    /// <param name="windowsOpener">The device opener used on Windows.</param>
    /// <param name="shutdownTimeout">How long disposal waits for the background work to stop before closing the device anyway, or <see langword="null"/> for five seconds.</param>
    internal UartPeer(IUartDeviceOpener linuxOpener, IUartDeviceOpener windowsOpener, TimeSpan? shutdownTimeout = null)
    {
        this.linuxOpener = linuxOpener;
        this.windowsOpener = windowsOpener;
        this.shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(5);
        StateChanged = Isolate(stateChanged, Report);
        Exceptions = Isolate(exceptions, static _ => { });
    }

    private readonly IUartDeviceOpener linuxOpener;
    private readonly IUartDeviceOpener windowsOpener;
    private readonly TimeSpan shutdownTimeout;
    private readonly Channel<IMemoryOwner<byte>> delivery = Channel.CreateUnbounded<IMemoryOwner<byte>>();
    private readonly Subject<UartPeerState> stateChanged = new();
    private readonly Subject<Exception> exceptions = new();
    private readonly Queue<Exception> pendingExceptions = new();
    private readonly Queue<Action> notifications = new();
    private readonly Lock stateLock = new();
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly AsyncLocal<bool> insideReceiver = new();
    private readonly int readBufferSize = 4096;
    private readonly int maxWriteSize = 4096;
    private IMicroGateDevice? device;
    private Task receiveLoopTask = Task.CompletedTask;
    private Task deliveryTask = Task.CompletedTask;
    private Action<IMemoryOwner<byte>>? receiver;
    private UartPeerState state = UartPeerState.Idle;
    private int notifyingThreadId;
    private bool deliveryStarted;
    private bool transmitterEnabled;
    private bool started;
    private bool finished;
    private bool disposed;

    /// <inheritdoc />
    public Action<IMemoryOwner<byte>>? Receiver
    {
        get => Volatile.Read(ref receiver);
        set => Volatile.Write(ref receiver, value);
    }

    /// <inheritdoc />
    public IObservable<UartPeerState> StateChanged { get; }

    /// <inheritdoc />
    public IObservable<Exception> Exceptions { get; }

    /// <inheritdoc />
    public UartPeerState State
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
    public async ValueTask Start(string portName, UartPeerOptions? options = null, CancellationToken cancellation = default)
    {
        options ??= new();
        ValidateOptions(options);
        IUartDeviceOpener selectedOpener = SelectOpener();

        lock (stateLock)
        {
            if (started || disposed)
            {
                throw new InvalidOperationException("The peer has already been started or has been disposed.");
            }

            started = true;
        }

        try
        {
            IMicroGateDevice opened = await Task.Run(() => selectedOpener.Open(portName, options)).ConfigureAwait(false);

            lock (stateLock)
            {
                if (disposed)
                {
                    opened.Dispose();
                    throw new ObjectDisposedException(nameof(UartPeer));
                }

                device = opened;
                receiveLoopTask = Task.Run(ReceiveLoop);
                deliveryStarted = true;
                deliveryTask = Task.Run(Deliver);
                SetState(UartPeerState.Open);
            }

            Notify();
            cancellation.ThrowIfCancellationRequested();
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not ObjectDisposedException)
        {
            await DisposeAfterFailure(exception).ConfigureAwait(false);
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
        IMicroGateDevice opened = RequireOpen();
        await sendLock.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            await Task.Run(() => Write(opened, data, cancellation), cancellation).ConfigureAwait(false);
        }
        finally
        {
            sendLock.Release();
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

    private void ValidateOptions(UartPeerOptions candidate)
    {
        if (candidate.BaudRate <= 0)
        {
            throw new ArgumentOutOfRangeException("options", "The baud rate must be greater than zero.");
        }

        if (candidate.DataBits is < 5 or > 8)
        {
            throw new ArgumentOutOfRangeException("options", "The number of data bits must be between 5 and 8.");
        }

        if (!Enum.IsDefined(candidate.StopBits))
        {
            throw new ArgumentOutOfRangeException("options", "The stop bits setting is not a defined value.");
        }

        if (!Enum.IsDefined(candidate.Parity))
        {
            throw new ArgumentOutOfRangeException("options", "The parity setting is not a defined value.");
        }
    }

    private IUartDeviceOpener SelectOpener()
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

    private IMicroGateDevice RequireOpen()
    {
        lock (stateLock)
        {
            if (state != UartPeerState.Open || device is null)
            {
                throw new InvalidOperationException("The peer is not open.");
            }

            return device;
        }
    }

    private void Write(IMicroGateDevice opened, ReadOnlyMemory<byte> data, CancellationToken cancellation)
    {
        if (!transmitterEnabled)
        {
            opened.EnableTransmitter();
            transmitterEnabled = true;
        }

        for (int offset = 0; offset < data.Length; offset += maxWriteSize)
        {
            cancellation.ThrowIfCancellationRequested();
            opened.Write(data.Slice(offset, Math.Min(maxWriteSize, data.Length - offset)));
        }
    }

    private async Task DisposeAfterFailure(Exception exception)
    {
        bool disposedElsewhere;
        lock (stateLock)
        {
            disposedElsewhere = disposed;
        }

        await DisposeAsync().ConfigureAwait(false);

        if (disposedElsewhere)
        {
            throw new ObjectDisposedException(nameof(UartPeer), exception);
        }
    }

    private async Task CloseWhenStopped(IMicroGateDevice opened)
    {
        try
        {
            await Task.WhenAll(receiveLoopTask, deliveryTask).WaitAsync(shutdownTimeout).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or AggregateException)
        {
        }

        if (transmitterEnabled)
        {
            opened.DisableTransmitter();
        }

        opened.Dispose();
    }

    private void ReceiveLoop()
    {
        try
        {
            byte[] buffer = new byte[readBufferSize];

            while (true)
            {
                int bytesRead = device!.Read(buffer);
                if (bytesRead <= 0)
                {
                    break;
                }

                PooledBuffer owner = new(buffer.AsSpan(0, bytesRead));
                if (!delivery.Writer.TryWrite(owner))
                {
                    owner.Dispose();
                }
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }

        Finish();
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

    private void Report(Exception exception)
    {
        lock (stateLock)
        {
            pendingExceptions.Enqueue(exception);
            notifications.Enqueue(PublishExceptions);
        }

        Notify();
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

    private void Finish()
    {
        lock (stateLock)
        {
            if (finished)
            {
                return;
            }

            finished = true;
            SetState(UartPeerState.Closed);
            if (!deliveryStarted)
            {
                notifications.Enqueue(CompleteStreams);
            }
        }

        delivery.Writer.TryComplete();
        Notify();
    }

    private void SetState(UartPeerState newState)
    {
        if (state == UartPeerState.Closed || state == newState)
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
