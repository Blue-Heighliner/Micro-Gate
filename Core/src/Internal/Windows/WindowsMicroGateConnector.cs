namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Opens <see cref="IMicroGateConnection"/>s to MicroGate SyncLink devices installed on a Windows system.
/// </summary>
internal interface IWindowsMicroGateConnector
{
    /// <summary>
    /// Opens and configures the device for the specified port and establishes an asynchronous balanced mode connection over it.
    /// </summary>
    /// <param name="portName">The device name, as reported by <see cref="IWindowsMicroGatePorts.GetPorts"/>.</param>
    /// <param name="options">The device and HDLC configuration applied to the connection.</param>
    /// <param name="cancellation">A token that can be used to cancel the connect operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the opened connection.</returns>
    /// <exception cref="IOException">The device could not be opened, or a connection could not be established.</exception>
    /// <exception cref="PlatformNotSupportedException">The current operating system is not Windows.</exception>
    ValueTask<IMicroGateConnection> Connect(string portName, MicroGateConnectionOptions options, CancellationToken cancellation);
}

/// <summary>
/// <inheritdoc cref="IWindowsMicroGateConnector" />
/// </summary>
internal sealed class WindowsMicroGateConnector : IWindowsMicroGateConnector
{
    /// <inheritdoc />
    public async ValueTask<IMicroGateConnection> Connect(string portName, MicroGateConnectionOptions options, CancellationToken cancellation)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Windows MicroGate transport is only supported on Windows.");
        }

        uint openStatus = Mghdlc.MgslOpenByName(portName, out nint handle);
        if (openStatus != MghdlcConstants.Success)
        {
            throw new IOException($"Failed to open '{portName}'.", new Win32Exception((int)openStatus));
        }

        try
        {
            ConfigurePort(handle, options);
        }
        catch
        {
            Mghdlc.MgslClose(handle);
            throw;
        }

        WindowsMicroGateConnection connection = new(handle, new HdlcStateMachine(options));
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

    [SupportedOSPlatform("windows")]
    private void ConfigurePort(nint handle, MicroGateConnectionOptions options)
    {
        MghdlcParams parameters = new()
        {
            Mode = MghdlcConstants.ModeHdlc,
            Encoding = (byte)options.Encoding,
            CrcType = (ushort)options.Crc,
            Addr = options.HardwareAddressFilter ?? MghdlcConstants.AddressFilterDisabled,
        };
        Mghdlc.MgslSetParams(handle, ref parameters);

        Mghdlc.MgslSetIdleMode(handle, (uint)options.IdlePattern);
        Mghdlc.MgslEnableReceiver(handle, MghdlcConstants.Enabled);
        Mghdlc.MgslEnableTransmitter(handle, MghdlcConstants.Enabled);
    }
}
