namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Opens and configures MicroGate SyncLink devices attached to Linux tty devices for asynchronous serial operation. A port name is a tty device name under <c>/dev</c>, or a full path.
/// </summary>
/// <param name="native">The native device operations.</param>
internal sealed class LinuxUartDeviceOpener(ILinuxNative native) : IUartDeviceOpener
{
    private readonly string devicePathPrefix = "/dev/";

    /// <inheritdoc />
    public IMicroGateDevice Open(string portName, UartPeerOptions options)
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
            ConfigurePort(fileDescriptor, path, options);
        }
        catch
        {
            device.Dispose();
            throw;
        }

        return device;
    }

    /// <summary>
    /// Configures the device for byte-oriented asynchronous operation and enables its receiver only. The terminal is configured first, for the standard speeds, and the driver parameters then override it with the exact settings, which is how the vendor's documentation says to use a rate the terminal layer cannot express. The transmitter is enabled later, when the peer first sends.
    /// </summary>
    private void ConfigurePort(int fileDescriptor, string path, UartPeerOptions options)
    {
        Check(native.SelectTtyLineDiscipline(fileDescriptor), "select the terminal line discipline", path);
        Check(native.ConfigureAsynchronous(fileDescriptor, options.BaudRate, options.DataBits, options.StopBits == UartStopBits.Two ? 2 : 1, MapParity(options.Parity)), "configure the terminal", path);
        // Setting the interface is best effort: it needs privileges the user may lack, and a device that cannot switch is still usable as it is.
        native.SetInterface(fileDescriptor, SynclinkConstants.InterfaceRs232);

        SynclinkParams parameters = new()
        {
            Mode = SynclinkConstants.ModeAsync,
            Loopback = (byte)(options.Loopback ? 1 : 0),
            DataRate = (nuint)options.BaudRate,
            DataBits = (byte)options.DataBits,
            StopBits = (byte)(options.StopBits == UartStopBits.Two ? 2 : 1),
            Parity = (byte)MapParity(options.Parity),
        };
        Check(native.SetParams(fileDescriptor, parameters), "set the port parameters", path);
        Check(native.EnableReceiver(fileDescriptor, true), "enable the receiver", path);
        Check(native.ClearNonBlocking(fileDescriptor), "make the device blocking", path);
    }

    private void Check(int result, string step, string path)
    {
        if (result < 0)
        {
            throw new IOException($"Failed to {step} on '{path}'; it may not be a SyncLink device.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }
    }

    private int MapParity(UartParity value) => value switch
    {
        UartParity.None => 0,
        UartParity.Even => 1,
        UartParity.Odd => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
