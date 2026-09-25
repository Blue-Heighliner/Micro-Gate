namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Opens <see cref="IMicroGateConnection"/>s to MicroGate SyncLink devices attached to Linux tty devices.
/// </summary>
internal interface ILinuxMicroGateConnector
{
    /// <summary>
    /// Opens and configures the tty device for the specified port and establishes an asynchronous balanced mode connection over it.
    /// </summary>
    /// <param name="portName">The name of the tty device, with or without the <c>/dev/</c> prefix.</param>
    /// <param name="options">The device and HDLC configuration applied to the connection.</param>
    /// <param name="cancellation">A token that can be used to cancel the connect operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the opened connection.</returns>
    /// <exception cref="IOException">The device could not be opened, or a connection could not be established.</exception>
    ValueTask<IMicroGateConnection> Connect(string portName, MicroGateConnectionOptions options, CancellationToken cancellation);
}

/// <summary>
/// <inheritdoc cref="ILinuxMicroGateConnector" />
/// </summary>
internal sealed class LinuxMicroGateConnector : ILinuxMicroGateConnector
{
    private readonly string devicePathPrefix = "/dev/";

    /// <inheritdoc />
    public async ValueTask<IMicroGateConnection> Connect(string portName, MicroGateConnectionOptions options, CancellationToken cancellation)
    {
        string path = portName.StartsWith(devicePathPrefix, StringComparison.Ordinal) ? portName : devicePathPrefix + portName;
        int fileDescriptor = LibC.Open(path, SynclinkConstants.FileAccessReadWrite | SynclinkConstants.FileStatusNonBlocking);
        if (fileDescriptor < 0)
        {
            throw new IOException($"Failed to open '{path}'.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        try
        {
            ConfigurePort(fileDescriptor, options);
        }
        catch
        {
            LibC.Close(fileDescriptor);
            throw;
        }

        LinuxMicroGateConnection connection = new(fileDescriptor, new HdlcStateMachine(options));
        try
        {
            await connection.Establish(cancellation).ConfigureAwait(false);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return connection;
    }

    private void ConfigurePort(int fileDescriptor, MicroGateConnectionOptions options)
    {
        int lineDiscipline = SynclinkConstants.LineDisciplineHdlc;
        LibC.Ioctl(fileDescriptor, SynclinkConstants.SetLineDiscipline, ref lineDiscipline);

        SynclinkParams parameters = new()
        {
            Mode = SynclinkConstants.ModeHdlc,
            Encoding = (byte)options.Encoding,
            CrcType = (ushort)options.Crc,
            AddressFilter = options.HardwareAddressFilter ?? SynclinkConstants.AddressFilterDisabled,
        };
        LibC.Ioctl(fileDescriptor, SynclinkConstants.SetParams, ref parameters);

        LibC.Ioctl(fileDescriptor, SynclinkConstants.SetTransmitIdle, (nint)options.IdlePattern);
        LibC.Ioctl(fileDescriptor, SynclinkConstants.EnableReceiver, (nint)SynclinkConstants.Enabled);
        LibC.Ioctl(fileDescriptor, SynclinkConstants.EnableTransmitter, (nint)SynclinkConstants.Enabled);

        int flags = LibC.Fcntl(fileDescriptor, SynclinkConstants.FcntlGetFlags);
        LibC.Fcntl(fileDescriptor, SynclinkConstants.FcntlSetFlags, flags & SynclinkConstants.FileStatusFlagMask);
    }
}
