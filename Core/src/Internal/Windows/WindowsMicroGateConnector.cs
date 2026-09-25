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
    ValueTask<IMicroGateConnection> Connect(string portName, MicroGateConnectionOptions options, CancellationToken cancellation);
}

/// <summary>
/// <inheritdoc cref="IWindowsMicroGateConnector" />
/// </summary>
/// <param name="native">The native device operations.</param>
internal sealed class WindowsMicroGateConnector(IWindowsNative native) : IWindowsMicroGateConnector
{
    /// <inheritdoc />
    public async ValueTask<IMicroGateConnection> Connect(string portName, MicroGateConnectionOptions options, CancellationToken cancellation)
    {
        uint openStatus = native.OpenByName(portName, out nint handle);
        if (openStatus != MghdlcConstants.Success)
        {
            throw new IOException($"Failed to open '{portName}'.", new Win32Exception((int)openStatus));
        }

        WindowsMicroGateDevice device = new(native, handle);
        try
        {
            ConfigurePort(handle, options);
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

    private void ConfigurePort(nint handle, MicroGateConnectionOptions options)
    {
        MghdlcParams parameters = new()
        {
            Mode = MghdlcConstants.ModeHdlc,
            Encoding = (byte)options.Encoding,
            CrcType = (ushort)options.Crc,
            Addr = options.HardwareAddressFilter ?? MghdlcConstants.AddressFilterDisabled,
        };
        native.SetParams(handle, parameters);

        native.SetIdleMode(handle, (uint)options.IdlePattern);
        native.EnableReceiver(handle, true);
        native.EnableTransmitter(handle, true);
    }
}
