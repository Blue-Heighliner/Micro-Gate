namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Opens and configures MicroGate SyncLink devices attached to Linux tty devices. A port name is a tty device name under <c>/dev</c>, or a full path.
/// </summary>
/// <param name="native">The native device operations.</param>
internal sealed class LinuxMicroGateDeviceOpener(ILinuxNative native) : IMicroGateDeviceOpener
{
    private readonly string devicePathPrefix = "/dev/";

    /// <inheritdoc />
    public IMicroGateDevice Open(string portName, MicroGatePeerOptions options)
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

    private void ConfigurePort(int fileDescriptor, string path, MicroGatePeerOptions options)
    {
        Check(native.SelectHdlcLineDiscipline(fileDescriptor), "select the HDLC line discipline", path);

        SynclinkParams parameters = new()
        {
            Mode = SynclinkConstants.ModeHdlc,
            Encoding = (byte)options.Encoding,
            CrcType = (ushort)options.Crc,
            AddressFilter = options.HardwareAddressFilter ?? SynclinkConstants.AddressFilterDisabled,
        };
        Check(native.SetParams(fileDescriptor, parameters), "set the port parameters", path);
        Check(native.SetTransmitIdle(fileDescriptor, (int)options.IdlePattern), "set the idle pattern", path);
        Check(native.EnableReceiver(fileDescriptor, true), "enable the receiver", path);
        Check(native.EnableTransmitter(fileDescriptor, true), "enable the transmitter", path);
        Check(native.ClearNonBlocking(fileDescriptor), "make the device blocking", path);
    }

    private void Check(int result, string step, string path)
    {
        if (result < 0)
        {
            throw new IOException($"Failed to {step} on '{path}'; it may not be a SyncLink device.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }
    }
}
