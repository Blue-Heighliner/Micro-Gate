namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Opens <see cref="IMicroGateConnection"/>s to MicroGate SyncLink devices attached to Linux tty devices.
/// </summary>
internal interface ILinuxMicroGateConnector
{
    /// <summary>
    /// Opens and configures the tty device for the specified port and establishes an asynchronous balanced mode connection over it.
    /// </summary>
    /// <param name="portName">The name of the tty device under <c>/dev</c>, or its full path.</param>
    /// <param name="options">The device and HDLC configuration applied to the connection.</param>
    /// <param name="cancellation">A token that can be used to cancel the connect operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the opened connection.</returns>
    /// <exception cref="IOException">The device could not be opened, or a connection could not be established.</exception>
    ValueTask<IMicroGateConnection> Connect(string portName, MicroGateConnectionOptions options, CancellationToken cancellation);
}

/// <summary>
/// <inheritdoc cref="ILinuxMicroGateConnector" />
/// </summary>
/// <param name="native">The native device operations.</param>
internal sealed class LinuxMicroGateConnector(ILinuxNative native) : ILinuxMicroGateConnector
{
    private readonly string devicePathPrefix = "/dev/";

    /// <inheritdoc />
    public async ValueTask<IMicroGateConnection> Connect(string portName, MicroGateConnectionOptions options, CancellationToken cancellation)
    {
        string path = Path.IsPathRooted(portName) ? portName : devicePathPrefix + portName;
        int fileDescriptor = native.Open(path);
        if (fileDescriptor < 0)
        {
            throw new IOException($"Failed to open '{path}'.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        LinuxMicroGateDevice device = new(native, fileDescriptor);
        try
        {
            ConfigurePort(fileDescriptor, options);
        }
        catch
        {
            device.Dispose();
            throw;
        }

        MicroGateDeviceConnection connection = new(device, new HdlcStateMachine(options));
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
        native.SelectHdlcLineDiscipline(fileDescriptor);

        SynclinkParams parameters = new()
        {
            Mode = SynclinkConstants.ModeHdlc,
            Encoding = (byte)options.Encoding,
            CrcType = (ushort)options.Crc,
            AddressFilter = options.HardwareAddressFilter ?? SynclinkConstants.AddressFilterDisabled,
        };
        native.SetParams(fileDescriptor, parameters);

        native.SetTransmitIdle(fileDescriptor, (int)options.IdlePattern);
        native.EnableReceiver(fileDescriptor, true);
        native.EnableTransmitter(fileDescriptor, true);
        native.ClearNonBlocking(fileDescriptor);
    }
}
