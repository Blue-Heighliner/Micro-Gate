namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// An <see cref="IMicroGateConnection"/> to a MicroGate SyncLink device attached to a Windows system, exchanging HDLC frames through the SyncLink driver's raw bit-framing base API (<c>mghdlc.dll</c>) while <see cref="HdlcStateMachine"/> maintains the asynchronous balanced mode connection and generates each frame's address and control bytes.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsMicroGateConnection : IMicroGateConnection
{
    private const int MaxFrameSize = 65535;

    public static async ValueTask<IMicroGateConnection> Connect(string portName, HdlcStationOptions options, CancellationToken cancellationToken)
    {
        uint openStatus = Mghdlc.MgslOpenByName(portName, out nint handle);
        if (openStatus != MghdlcConstants.Success)
        {
            throw new IOException($"Failed to open '{portName}'.", new Win32Exception((int)openStatus));
        }

        try
        {
            ConfigurePort(handle);
        }
        catch
        {
            Mghdlc.MgslClose(handle);
            throw;
        }

        WindowsMicroGateConnection connection = new(handle, new HdlcStateMachine(options));
        try
        {
            await Task.Run(() => connection.WriteFrame(connection.stateMachine.CreateConnect()), cancellationToken).ConfigureAwait(false);
            await connection.connectionEstablished.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return connection;
    }

    private static void ConfigurePort(nint handle)
    {
        MghdlcParams parameters = new()
        {
            Mode = MghdlcConstants.ModeHdlc,
            Encoding = MghdlcConstants.EncodingNrz,
            CrcType = MghdlcConstants.Crc16Ccitt,
            Addr = MghdlcConstants.AddressFilterDisabled,
        };
        Mghdlc.MgslSetParams(handle, ref parameters);

        Mghdlc.MgslSetIdleMode(handle, MghdlcConstants.TransmitIdleFlags);
        Mghdlc.MgslEnableReceiver(handle, MghdlcConstants.Enabled);
        Mghdlc.MgslEnableTransmitter(handle, MghdlcConstants.Enabled);
    }

    private WindowsMicroGateConnection(nint handle, IHdlcStateMachine stateMachine)
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
    private bool disposed;

    public event EventHandler? Disconnected;

    public event EventHandler<IMemoryOwner<byte>>? Received;

    public bool IsConnected => stateMachine.State == HdlcConnectionState.Connected;

    public ValueTask Send(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) =>
        new(Task.Run(() => WriteFrame(stateMachine.CreateInformation(data)), cancellationToken));

    public ValueTask Send(IMemoryOwner<byte> data, CancellationToken cancellationToken = default)
    {
        async Task SendAndDispose()
        {
            using (data)
            {
                await Task.Run(() => WriteFrame(stateMachine.CreateInformation(data.Memory)), cancellationToken).ConfigureAwait(false);
            }
        }

        return new ValueTask(SendAndDispose());
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

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

    private async Task ReceiveLoop()
    {
        byte[] buffer = new byte[MaxFrameSize];
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
