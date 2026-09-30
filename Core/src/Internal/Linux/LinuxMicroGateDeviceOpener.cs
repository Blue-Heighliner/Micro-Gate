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
        // Setting the interface is best effort: it needs privileges the user may lack, and a device that cannot switch is still usable as it is.
        native.SetInterface(fileDescriptor, SynclinkConstants.InterfaceRs232);

        SynclinkParams parameters = new()
        {
            Mode = SynclinkConstants.ModeHdlc,
            Loopback = (byte)(options.Loopback ? 1 : 0),
            Flags = MapFlags(options),
            Encoding = MapEncoding(options.Link.Encoding),
            ClockSpeed = (nuint)options.Link.ClockSpeed,
            CrcType = MapCrc(options.Link.Crc),
            AddressFilter = SynclinkConstants.AddressFilterDisabled,
            PreambleLength = MapPreambleLength(options.PreambleLength),
            Preamble = MapPreamblePattern(options.PreamblePattern),
        };
        Check(native.SetParams(fileDescriptor, parameters), "set the port parameters", path);
        Check(native.SetTransmitIdle(fileDescriptor, MapIdlePattern(options.IdlePattern)), "set the idle pattern", path);
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

    private ushort MapFlags(MicroGatePeerOptions options) =>
        (ushort)(MapReceiveClockSource(options.Link.ReceiveClockSource) | MapTransmitClockSource(options.Link.TransmitClockSource) | MapPhaseLockedLoopDivisor(options.Link.PhaseLockedLoopDivisor) | MapUnderrunAction(options.UnderrunAction));

    private ushort MapReceiveClockSource(MicroGateReceiveClockSource value) => value switch
    {
        MicroGateReceiveClockSource.OwnPin => 0,
        MicroGateReceiveClockSource.OtherPin => SynclinkConstants.ReceiveClockOtherPin,
        MicroGateReceiveClockSource.PhaseLockedLoop => SynclinkConstants.ReceiveClockDpll,
        MicroGateReceiveClockSource.BaudRateGenerator => SynclinkConstants.ReceiveClockBrg,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapTransmitClockSource(MicroGateTransmitClockSource value) => value switch
    {
        MicroGateTransmitClockSource.OwnPin => 0,
        MicroGateTransmitClockSource.OtherPin => SynclinkConstants.TransmitClockOtherPin,
        MicroGateTransmitClockSource.PhaseLockedLoop => SynclinkConstants.TransmitClockDpll,
        MicroGateTransmitClockSource.BaudRateGenerator => SynclinkConstants.TransmitClockBrg,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapPhaseLockedLoopDivisor(MicroGatePhaseLockedLoopDivisor value) => value switch
    {
        MicroGatePhaseLockedLoopDivisor.DivideBy32 => 0,
        MicroGatePhaseLockedLoopDivisor.DivideBy8 => SynclinkConstants.DpllDivisor8,
        MicroGatePhaseLockedLoopDivisor.DivideBy16 => SynclinkConstants.DpllDivisor16,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapUnderrunAction(MicroGateUnderrunAction value) => value switch
    {
        MicroGateUnderrunAction.Abort7 => 0,
        MicroGateUnderrunAction.Abort15 => SynclinkConstants.UnderrunAbort15,
        MicroGateUnderrunAction.Flag => SynclinkConstants.UnderrunFlag,
        MicroGateUnderrunAction.InvalidFrameCheckSequence => SynclinkConstants.UnderrunBadCrc,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

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

    private int MapIdlePattern(MicroGateIdlePattern value) => value switch
    {
        MicroGateIdlePattern.Flags => 0,
        MicroGateIdlePattern.AlternatingZerosOnes => 1,
        MicroGateIdlePattern.Zeros => 2,
        MicroGateIdlePattern.Ones => 3,
        MicroGateIdlePattern.AlternatingMarkSpace => 4,
        MicroGateIdlePattern.Space => 5,
        MicroGateIdlePattern.Mark => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private byte MapPreambleLength(MicroGatePreambleLength value) => value switch
    {
        MicroGatePreambleLength.Bits8 => 0,
        MicroGatePreambleLength.Bits16 => 1,
        MicroGatePreambleLength.Bits32 => 2,
        MicroGatePreambleLength.Bits64 => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private byte MapPreamblePattern(MicroGatePreamblePattern value) => value switch
    {
        MicroGatePreamblePattern.None => 0,
        MicroGatePreamblePattern.Zeros => 1,
        MicroGatePreamblePattern.Flags => 2,
        MicroGatePreamblePattern.Alternating10 => 3,
        MicroGatePreamblePattern.Alternating01 => 4,
        MicroGatePreamblePattern.Ones => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
