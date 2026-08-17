namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// An <see cref="IMicroGateConnection"/> to a MicroGate SyncLink device attached to a Linux tty device, exchanging HDLC frames through the SyncLink driver's raw bit-framing layer while <see cref="HdlcStateMachine"/> maintains the asynchronous balanced mode connection and generates each frame's address and control bytes.
/// </summary>
internal sealed class LinuxMicroGateConnection : IMicroGateConnection
{
    private const string DevicePathPrefix = "/dev/";
    private const int MaxFrameSize = 65535;

    public static async ValueTask<IMicroGateConnection> Connect(string portName, HdlcStationOptions options, CancellationToken cancellationToken)
    {
        string path = portName.StartsWith(DevicePathPrefix, StringComparison.Ordinal) ? portName : DevicePathPrefix + portName;
        int fileDescriptor = LibC.open(path, SynclinkConstants.FileAccessReadWrite | SynclinkConstants.FileStatusNonBlocking);
        if (fileDescriptor < 0)
        {
            throw new IOException($"Failed to open '{path}'.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        try
        {
            ConfigurePort(fileDescriptor);
        }
        catch
        {
            LibC.close(fileDescriptor);
            throw;
        }

        LinuxMicroGateConnection connection = new(fileDescriptor, new HdlcStateMachine(options));
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

    private static void ConfigurePort(int fileDescriptor)
    {
        int lineDiscipline = SynclinkConstants.LineDisciplineHdlc;
        LibC.ioctl(fileDescriptor, SynclinkConstants.SetLineDiscipline, ref lineDiscipline);

        SynclinkParams parameters = new()
        {
            Mode = SynclinkConstants.ModeHdlc,
            Encoding = SynclinkConstants.EncodingNrz,
            CrcType = SynclinkConstants.Crc16Ccitt,
            AddressFilter = SynclinkConstants.AddressFilterDisabled,
        };
        LibC.ioctl(fileDescriptor, SynclinkConstants.SetParams, ref parameters);

        LibC.ioctl(fileDescriptor, SynclinkConstants.SetTransmitIdle, (nint)SynclinkConstants.TransmitIdleFlags);
        LibC.ioctl(fileDescriptor, SynclinkConstants.EnableReceiver, (nint)SynclinkConstants.Enabled);
        LibC.ioctl(fileDescriptor, SynclinkConstants.EnableTransmitter, (nint)SynclinkConstants.Enabled);

        int flags = LibC.fcntl(fileDescriptor, SynclinkConstants.FcntlGetFlags);
        LibC.fcntl(fileDescriptor, SynclinkConstants.FcntlSetFlags, flags & SynclinkConstants.FileStatusFlagMask);
    }

    private LinuxMicroGateConnection(int fileDescriptor, IHdlcStateMachine stateMachine)
    {
        this.fileDescriptor = fileDescriptor;
        this.stateMachine = stateMachine;
        receiveLoopTask = Task.Run(ReceiveLoop);
    }

    private readonly int fileDescriptor;
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

        LibC.ioctl(fileDescriptor, SynclinkConstants.EnableReceiver, (nint)SynclinkConstants.Disabled);

        try
        {
            await receiveLoopTask.ConfigureAwait(false);
        }
        catch
        {
        }

        LibC.close(fileDescriptor);
    }

    private async Task ReceiveLoop()
    {
        byte[] buffer = new byte[MaxFrameSize];
        bool wasConnected = false;

        while (true)
        {
            nint bytesRead = LibC.read(fileDescriptor, buffer, (nuint)buffer.Length);
            if (bytesRead <= 0)
            {
                break;
            }

            HdlcReceiveResult result;
            try
            {
                result = stateMachine.Receive(buffer.AsMemory(0, (int)bytesRead));
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
            nint bytesWritten = LibC.write(fileDescriptor, buffer, (nuint)buffer.Length);
            if (bytesWritten != buffer.Length)
            {
                throw new IOException("Failed to write the frame to the device.", new Win32Exception(Marshal.GetLastPInvokeError()));
            }

            LibC.tcdrain(fileDescriptor);
        }
    }
}
