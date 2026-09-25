namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// An <see cref="IMicroGateConnection"/> to a MicroGate SyncLink device attached to a Windows system, exchanging HDLC frames through the SyncLink driver's raw bit-framing base API (<c>mghdlc.dll</c>) while <see cref="HdlcStateMachine"/> maintains the asynchronous balanced mode connection and generates each frame's address and control bytes.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsMicroGateConnection : IMicroGateConnection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsMicroGateConnection"/> class and starts its receive loop.
    /// </summary>
    /// <param name="handle">The opened and configured device handle, owned by the connection from this point on.</param>
    /// <param name="stateMachine">The state machine that maintains the asynchronous balanced mode connection.</param>
    public WindowsMicroGateConnection(nint handle, IHdlcStateMachine stateMachine)
    {
        this.handle = handle;
        this.stateMachine = stateMachine;
        receiveLoopTask = Task.Run(ReceiveLoop);
    }

    private readonly nint handle;
    private readonly IHdlcStateMachine stateMachine;
    private readonly Lock writeLock = new();
    private readonly TaskCompletionSource connectionEstablished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task receiveLoopTask;
    private readonly int maxFrameSize = 65535;
    private bool disposed;

    /// <inheritdoc />
    public event EventHandler? Disconnected;

    /// <inheritdoc />
    public event EventHandler<IMemoryOwner<byte>>? Received;

    /// <inheritdoc />
    public bool IsConnected => stateMachine.State == HdlcConnectionState.Connected;

    /// <inheritdoc />
    public ValueTask Send(ReadOnlyMemory<byte> data, CancellationToken cancellation = default) =>
        new(Task.Run(() => WriteFrame(stateMachine.CreateInformation(data)), cancellation));

    /// <inheritdoc />
    public ValueTask Send(IMemoryOwner<byte> data, CancellationToken cancellation = default)
    {
        async Task SendAndDispose()
        {
            using (data)
            {
                await Task.Run(() => WriteFrame(stateMachine.CreateInformation(data.Memory)), cancellation).ConfigureAwait(false);
            }
        }

        return new ValueTask(SendAndDispose());
    }

    /// <summary>
    /// Sends a set asynchronous balanced mode frame and waits for the connection to be established.
    /// </summary>
    /// <param name="cancellation">A token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> that completes once the state machine reports the connection as established.</returns>
    /// <exception cref="IOException">The frame could not be written to the device.</exception>
    public async Task Establish(CancellationToken cancellation)
    {
        await Task.Run(() => WriteFrame(stateMachine.CreateConnect()), cancellation).ConfigureAwait(false);
        await connectionEstablished.Task.WaitAsync(cancellation).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (stateMachine.State == HdlcConnectionState.Connected)
        {
            try
            {
                await Task.Run(() => WriteFrame(stateMachine.CreateDisconnect())).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
        }

        Mghdlc.MgslEnableReceiver(handle, MghdlcConstants.Disabled);

        try
        {
            await receiveLoopTask.ConfigureAwait(false);
        }
        catch
        {
        }

        Mghdlc.MgslClose(handle);
    }

    private void ReceiveLoop()
    {
        byte[] buffer = new byte[maxFrameSize];
        bool wasConnected = false;

        while (true)
        {
            int bytesRead = Mghdlc.MgslRead(handle, buffer, buffer.Length);
            if (bytesRead <= 0)
            {
                break;
            }

            HdlcReceiveResult result;
            try
            {
                result = stateMachine.Receive(buffer.AsMemory(0, bytesRead));
            }
            catch (HdlcFrameException)
            {
                continue;
            }

            if (result.Response is { } response)
            {
                WriteFrame(response);
            }

            if (result.Payload is { } payload)
            {
                IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(payload.Length);
                payload.CopyTo(owner.Memory);
                Received?.Invoke(this, new LimitedMemoryOwner(owner, payload.Length));
            }

            if (result.State == HdlcConnectionState.Connected)
            {
                wasConnected = true;
                connectionEstablished.TrySetResult();
            }
            else if (wasConnected)
            {
                wasConnected = false;
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        if (wasConnected)
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void WriteFrame(ReadOnlyMemory<byte> frame)
    {
        byte[] buffer = frame.ToArray();

        lock (writeLock)
        {
            int bytesWritten = Mghdlc.MgslWrite(handle, buffer, buffer.Length);
            if (bytesWritten != buffer.Length)
            {
                throw new IOException("Failed to write the frame to the device.");
            }
        }
    }
}
