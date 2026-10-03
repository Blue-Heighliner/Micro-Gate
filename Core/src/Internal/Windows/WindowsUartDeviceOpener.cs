namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Opens and configures MicroGate SyncLink devices for asynchronous serial operation on Windows. A port name is the device name the driver reports.
/// </summary>
/// <param name="native">The native device operations.</param>
/// <param name="busyRetryWindow">How long to keep retrying while the driver still reports the port in use after an earlier close, or <see langword="null"/> for three seconds.</param>
internal sealed class WindowsUartDeviceOpener(IWindowsNative native, TimeSpan? busyRetryWindow = null) : IUartDeviceOpener
{
    private readonly TimeSpan busyRetryWindow = busyRetryWindow ?? TimeSpan.FromSeconds(3);
    private readonly TimeSpan busyRetryInterval = TimeSpan.FromMilliseconds(50);

    /// <inheritdoc />
    public IMicroGateDevice Open(string portName, UartPeerOptions options)
    {
        uint openStatus = OpenWhenReleased(portName, out nint handle);
        if (openStatus != MghdlcConstants.Success)
        {
            throw new IOException($"Failed to open '{portName}'.", new Win32Exception((int)openStatus));
        }

        WindowsUartDevice device = new(native, handle);
        try
        {
            ConfigurePort(handle, options);
        }
        catch
        {
            device.Dispose();
            throw;
        }

        return device;
    }

    private uint OpenWhenReleased(string portName, out nint handle)
    {
        // The driver keeps a port reserved for up to a second after MgslClose returns, so reopening right after a dispose reports it in use; Linux releases it at once.
        Stopwatch stopwatch = Stopwatch.StartNew();
        uint status = native.OpenByName(portName, out handle);
        while (status == MghdlcConstants.DeviceInUse && stopwatch.Elapsed < busyRetryWindow)
        {
            Thread.Sleep(busyRetryInterval);
            status = native.OpenByName(portName, out handle);
        }

        return status;
    }

    private void ConfigurePort(nint handle, UartPeerOptions options)
    {
        // Best effort: setting the interface can need privileges the user lacks, and a device that cannot switch is still usable as it is.
        native.SetOption(handle, MghdlcConstants.OptionInterface, MghdlcConstants.InterfaceRs232);
        MghdlcParams parameters = new()
        {
            Mode = MghdlcConstants.ModeAsync,
            Loopback = (byte)(options.Loopback ? 1 : 0),
            DataRate = (uint)options.BaudRate,
            DataBits = (byte)options.DataBits,
            StopBits = (byte)(options.StopBits == UartStopBits.Two ? 2 : 1),
            Parity = MapParity(options.Parity),
        };
        Check(native.SetParams(handle, parameters), "set the port parameters");
        Check(native.EnableReceiver(handle, true), "enable the receiver");
    }

    private void Check(uint status, string step)
    {
        if (status != MghdlcConstants.Success)
        {
            throw new IOException($"Failed to {step}.", new Win32Exception((int)status));
        }
    }

    private byte MapParity(UartParity value) => value switch
    {
        UartParity.None => 0,
        UartParity.Even => 1,
        UartParity.Odd => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
