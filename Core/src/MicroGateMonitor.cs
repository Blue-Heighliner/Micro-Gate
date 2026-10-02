namespace BlueHeighliner.MicroGate;

/// <summary>
/// The lifecycle state of an <see cref="IMicroGateMonitor"/>. A monitor only moves forward: it is never returned to an earlier state.
/// </summary>
public enum MicroGateMonitorState
{
    /// <summary>
    /// The monitor has been created but <see cref="IMicroGateMonitor.Start"/> has not been called.
    /// </summary>
    Idle,

    /// <summary>
    /// <see cref="IMicroGateMonitor.Start"/> has been called and the device is open and being read.
    /// </summary>
    Monitoring,

    /// <summary>
    /// The monitor is finished: it lost its device, failed to start, or was disposed.
    /// </summary>
    Stopped,
}

/// <summary>
/// Passively observes the raw HDLC frames on a MicroGate SyncLink device, including the unnumbered and supervisory frames (SABM, UA, DISC, DM, FRMR, RR, RNR, REJ) two other stations use to manage their own asynchronous balanced mode connection, as well as the information frames carrying their data. A monitor never sends a frame of its own: it does not participate in the connection those two stations form, only observes it.
/// </summary>
public interface IMicroGateMonitor : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets a stream of the frames observed on the device, in the order they were received. The stream completes when the monitor becomes <see cref="MicroGateMonitorState.Stopped"/>.
    /// </summary>
    /// <remarks>
    /// The stream is hot: a frame that arrives while nothing is subscribed is discarded, so subscribe before calling <see cref="Start"/>. Frames, state changes, and completion are delivered one at a time, in order, and never while the monitor holds a lock.
    /// </remarks>
    IObservable<MicroGateFrame> Received { get; }

    /// <summary>
    /// Gets a stream of the monitor's state transitions, from the state after subscribing onward. The stream completes after emitting <see cref="MicroGateMonitorState.Stopped"/>.
    /// </summary>
    IObservable<MicroGateMonitorState> StateChanged { get; }

    /// <summary>
    /// Gets the current state of the monitor.
    /// </summary>
    MicroGateMonitorState State { get; }

    /// <summary>
    /// Opens the device for the specified port, read-only, and begins reporting the frames observed on it.
    /// </summary>
    /// <param name="portName">The name of the serial port the device is attached to.</param>
    /// <param name="options">The physical layer settings to apply, or <see langword="null"/> to use the defaults.</param>
    /// <param name="cancellation">A token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes once the device is open and being read.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="MicroGateMonitorOptions.ClockSpeed"/> is not positive.</exception>
    /// <exception cref="PlatformNotSupportedException">The current operating system is neither Windows nor Linux.</exception>
    /// <exception cref="InvalidOperationException">The monitor has already been started or has been disposed.</exception>
    /// <exception cref="IOException">The device could not be opened.</exception>
    /// <exception cref="ObjectDisposedException">The monitor was disposed before the device finished opening.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was canceled.</exception>
    /// <remarks>
    /// Any failure disposes the monitor, leaving it <see cref="MicroGateMonitorState.Stopped"/>.
    /// </remarks>
    ValueTask Start(string portName, MicroGateMonitorOptions? options = null, CancellationToken cancellation = default);
}

/// <summary>
/// <inheritdoc cref="IMicroGateMonitor" />
/// </summary>
public sealed class MicroGateMonitor : IMicroGateMonitor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MicroGateMonitor"/> class.
    /// </summary>
    public MicroGateMonitor()
        : this(new LinuxMicroGateMonitorDeviceOpener(new LinuxNative()), new WindowsMicroGateMonitorDeviceOpener(new WindowsNative()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MicroGateMonitor"/> class with the platform specific device openers to choose between.
    /// </summary>
    /// <param name="linuxOpener">The device opener used on Linux.</param>
    /// <param name="windowsOpener">The device opener used on Windows.</param>
    /// <param name="shutdownTimeout">How long disposal waits for the receive loop to stop before closing the device anyway, or <see langword="null"/> for five seconds.</param>
    internal MicroGateMonitor(IMicroGateMonitorDeviceOpener linuxOpener, IMicroGateMonitorDeviceOpener windowsOpener, TimeSpan? shutdownTimeout = null)
    {
        this.linuxOpener = linuxOpener;
        this.windowsOpener = windowsOpener;
        this.shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(5);
        Received = Isolate(received);
        StateChanged = Isolate(stateChanged);
    }

    private readonly IMicroGateMonitorDeviceOpener linuxOpener;
    private readonly IMicroGateMonitorDeviceOpener windowsOpener;
    private readonly TimeSpan shutdownTimeout;
    private readonly Subject<MicroGateFrame> received = new();
    private readonly Subject<MicroGateMonitorState> stateChanged = new();
    private readonly Queue<Action> notifications = new();
    private readonly Lock stateLock = new();
    private readonly int maxFrameSize = 65535;
    private IMicroGateDevice? device;
    private Task receiveLoopTask = Task.CompletedTask;
    private MicroGateMonitorState state = MicroGateMonitorState.Idle;
    private int notifyingThreadId;
    private bool finished;
    private bool disposed;

    /// <inheritdoc />
    public IObservable<MicroGateFrame> Received { get; }

    /// <inheritdoc />
    public IObservable<MicroGateMonitorState> StateChanged { get; }

    /// <inheritdoc />
    public MicroGateMonitorState State
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
    public async ValueTask Start(string portName, MicroGateMonitorOptions? options = null, CancellationToken cancellation = default)
    {
        options ??= new();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ClockSpeed, 0);
        IMicroGateMonitorDeviceOpener selectedOpener = SelectOpener();

        lock (stateLock)
        {
            if (state != MicroGateMonitorState.Idle || disposed)
            {
                throw new InvalidOperationException("The monitor has already been started or has been disposed.");
            }

            SetState(MicroGateMonitorState.Monitoring);
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
                    throw new ObjectDisposedException(nameof(MicroGateMonitor));
                }

                device = opened;
                receiveLoopTask = Task.Run(ReceiveLoop);
            }

            cancellation.ThrowIfCancellationRequested();
        }
        catch (Exception exception) when (exception is not ObjectDisposedException)
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
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
            fromCallback = notifyingThreadId == Environment.CurrentManagedThreadId;
        }

        if (opened is not null)
        {
            opened.DisableReceiver();
            opened.DisableTransmitter();

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

    private IMicroGateMonitorDeviceOpener SelectOpener()
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

    private async Task CloseWhenStopped(IMicroGateDevice opened)
    {
        try
        {
            await receiveLoopTask.WaitAsync(shutdownTimeout).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or AggregateException)
        {
        }

        opened.Dispose();
    }

    private void ReceiveLoop()
    {
        try
        {
            byte[] buffer = new byte[maxFrameSize];

            while (true)
            {
                int bytesRead = device!.Read(buffer);
                if (bytesRead <= 0)
                {
                    break;
                }

                MicroGateFrame frame = ParseFrame(buffer.AsSpan(0, bytesRead));
                PublishFrame(frame);
                Notify();
            }
        }
        finally
        {
            Finish();
        }
    }

    private void PublishFrame(MicroGateFrame frame)
    {
        if (!received.HasObservers)
        {
            return;
        }

        lock (stateLock)
        {
            if (!finished)
            {
                notifications.Enqueue(() => received.OnNext(frame));
            }
        }
    }

    private MicroGateFrame ParseFrame(ReadOnlySpan<byte> raw)
    {
        byte[] copy = raw.ToArray();

        try
        {
            HdlcFrame frame = HdlcFrame.Parse(copy);
            return new MicroGateFrame
            {
                Timestamp = DateTimeOffset.Now,
                Address = frame.Address,
                Kind = ToFrameKind(frame.Kind),
                PollFinal = frame.PollFinal,
                SendSequence = frame.Kind == HdlcFrameKind.Information ? frame.SendSequence : null,
                ReceiveSequence = frame.Kind is HdlcFrameKind.Information or HdlcFrameKind.ReceiveReady or HdlcFrameKind.ReceiveNotReady or HdlcFrameKind.Reject ? frame.ReceiveSequence : null,
                Payload = frame.Payload,
                Raw = copy,
            };
        }
        catch (HdlcFrameException exception)
        {
            return new MicroGateFrame
            {
                Timestamp = DateTimeOffset.Now,
                Address = copy.Length > 0 ? copy[0] : (byte)0,
                Kind = MicroGateFrameKind.Malformed,
                PollFinal = false,
                Raw = copy,
                ErrorMessage = exception.Message,
            };
        }
    }

    private MicroGateFrameKind ToFrameKind(HdlcFrameKind kind) => kind switch
    {
        HdlcFrameKind.Information => MicroGateFrameKind.Information,
        HdlcFrameKind.ReceiveReady => MicroGateFrameKind.ReceiveReady,
        HdlcFrameKind.ReceiveNotReady => MicroGateFrameKind.ReceiveNotReady,
        HdlcFrameKind.Reject => MicroGateFrameKind.Reject,
        HdlcFrameKind.SetAsynchronousBalancedMode => MicroGateFrameKind.SetAsynchronousBalancedMode,
        HdlcFrameKind.Disconnect => MicroGateFrameKind.Disconnect,
        HdlcFrameKind.UnnumberedAcknowledge => MicroGateFrameKind.UnnumberedAcknowledge,
        HdlcFrameKind.DisconnectedMode => MicroGateFrameKind.DisconnectedMode,
        HdlcFrameKind.FrameReject => MicroGateFrameKind.FrameReject,
        _ => MicroGateFrameKind.Malformed,
    };

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

    private void Finish()
    {
        lock (stateLock)
        {
            if (finished)
            {
                return;
            }

            finished = true;
            SetState(MicroGateMonitorState.Stopped);
            notifications.Enqueue(() =>
            {
                received.OnCompleted();
                stateChanged.OnCompleted();
            });
        }

        Notify();
    }

    private void SetState(MicroGateMonitorState newState)
    {
        if (state == MicroGateMonitorState.Stopped || state == newState)
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
