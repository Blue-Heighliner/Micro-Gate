namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Opens and configures MicroGate SyncLink devices attached to Linux tty devices for read-only monitoring. A port name is a tty device name under <c>/dev</c>, or a full path.
/// </summary>
/// <param name="native">The native device operations.</param>
internal sealed class LinuxMicroGateMonitorDeviceOpener(ILinuxNative native) : IMicroGateMonitorDeviceOpener
{
    private readonly string devicePathPrefix = "/dev/";

    /// <inheritdoc />
    public IMicroGateDevice Open(string portName, MicroGateMonitorOptions options)
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
    /// Configures the device for HDLC framing and enables its receiver only. The transmitter is deliberately never enabled: monitoring never writes to the device, and this is the layer that makes that structurally true instead of merely a convention observed elsewhere in the code.
    /// </summary>
    private void ConfigurePort(int fileDescriptor, string path, MicroGateMonitorOptions options)
    {
        Check(native.SelectHdlcLineDiscipline(fileDescriptor), "select the HDLC line discipline", path);

        SynclinkParams parameters = new()
        {
            Mode = SynclinkConstants.ModeHdlc,
            Encoding = MapEncoding(options.Encoding),
            CrcType = MapCrc(options.Crc),
            AddressFilter = options.HardwareAddressFilter ?? SynclinkConstants.AddressFilterDisabled,
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

    private byte MapEncoding(MicroGateEncoding value) => value switch
    {
        MicroGateEncoding.Nrz => 0,
        MicroGateEncoding.Nrzb => 1,
        MicroGateEncoding.NrziMark => 2,
        MicroGateEncoding.NrziSpace => 3,
        MicroGateEncoding.BiphaseMark => 4,
        MicroGateEncoding.BiphaseSpace => 5,
        MicroGateEncoding.BiphaseLevel => 6,
        MicroGateEncoding.DifferentialBiphaseLevel => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapCrc(MicroGateCrc value) => value switch
    {
        MicroGateCrc.None => 0,
        MicroGateCrc.Crc16Ccitt => 1,
        MicroGateCrc.Crc32Ccitt => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
