namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Opens and configures MicroGate SyncLink devices attached to Linux tty devices. A port name is a tty device name under <c>/dev</c>, or a full path.
/// </summary>
/// <param name="native">The native device operations.</param>
internal sealed class LinuxMicroGateDeviceOpener(ILinuxNative native) : IMicroGateDeviceOpener
{
    private readonly string devicePathPrefix = "/dev/";

    /// <inheritdoc />
    public IMicroGateDevice Open(string portName, HdlcPeerOptions options)
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

    private void ConfigurePort(int fileDescriptor, string path, HdlcPeerOptions options)
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
        Check(native.ClearNonBlocking(fileDescriptor), "make the device blocking", path);
    }

    private void Check(int result, string step, string path)
    {
        if (result < 0)
        {
            throw new IOException($"Failed to {step} on '{path}'; it may not be a SyncLink device.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }
    }

    private ushort MapFlags(HdlcPeerOptions options) =>
        (ushort)(MapReceiveClockSource(options.Link.ReceiveClockSource) | MapTransmitClockSource(options.Link.TransmitClockSource) | MapPhaseLockedLoopDivisor(options.Link.PhaseLockedLoopDivisor) | MapUnderrunAction(options.UnderrunAction));

    private ushort MapReceiveClockSource(HdlcReceiveClockSource value) => value switch
    {
        HdlcReceiveClockSource.OwnPin => 0,
        HdlcReceiveClockSource.OtherPin => SynclinkConstants.ReceiveClockOtherPin,
        HdlcReceiveClockSource.PhaseLockedLoop => SynclinkConstants.ReceiveClockDpll,
        HdlcReceiveClockSource.BaudRateGenerator => SynclinkConstants.ReceiveClockBrg,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapTransmitClockSource(HdlcTransmitClockSource value) => value switch
    {
        HdlcTransmitClockSource.OwnPin => 0,
        HdlcTransmitClockSource.OtherPin => SynclinkConstants.TransmitClockOtherPin,
        HdlcTransmitClockSource.PhaseLockedLoop => SynclinkConstants.TransmitClockDpll,
        HdlcTransmitClockSource.BaudRateGenerator => SynclinkConstants.TransmitClockBrg,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapPhaseLockedLoopDivisor(HdlcPhaseLockedLoopDivisor value) => value switch
    {
        HdlcPhaseLockedLoopDivisor.DivideBy32 => 0,
        HdlcPhaseLockedLoopDivisor.DivideBy8 => SynclinkConstants.DpllDivisor8,
        HdlcPhaseLockedLoopDivisor.DivideBy16 => SynclinkConstants.DpllDivisor16,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapUnderrunAction(HdlcUnderrunAction value) => value switch
    {
        HdlcUnderrunAction.Abort7 => 0,
        HdlcUnderrunAction.Abort15 => SynclinkConstants.UnderrunAbort15,
        HdlcUnderrunAction.Flag => SynclinkConstants.UnderrunFlag,
        HdlcUnderrunAction.InvalidFrameCheckSequence => SynclinkConstants.UnderrunBadCrc,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private byte MapEncoding(HdlcEncoding value) => value switch
    {
        HdlcEncoding.Nrz => 0,
        HdlcEncoding.Nrzb => 1,
        HdlcEncoding.NrziMark => 2,
        HdlcEncoding.NrziSpace => 3,
        HdlcEncoding.BiphaseMark => 4,
        HdlcEncoding.BiphaseSpace => 5,
        HdlcEncoding.BiphaseLevel => 6,
        HdlcEncoding.DifferentialBiphaseLevel => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapCrc(HdlcCrc value) => value switch
    {
        HdlcCrc.None => 0,
        HdlcCrc.Crc16Ccitt => 1,
        HdlcCrc.Crc32Ccitt => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private int MapIdlePattern(HdlcIdlePattern value) => value switch
    {
        HdlcIdlePattern.Flags => 0,
        HdlcIdlePattern.AlternatingZerosOnes => 1,
        HdlcIdlePattern.Zeros => 2,
        HdlcIdlePattern.Ones => 3,
        HdlcIdlePattern.AlternatingMarkSpace => 4,
        HdlcIdlePattern.Space => 5,
        HdlcIdlePattern.Mark => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private byte MapPreambleLength(HdlcPreambleLength value) => value switch
    {
        HdlcPreambleLength.Bits8 => 0,
        HdlcPreambleLength.Bits16 => 1,
        HdlcPreambleLength.Bits32 => 2,
        HdlcPreambleLength.Bits64 => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private byte MapPreamblePattern(HdlcPreamblePattern value) => value switch
    {
        HdlcPreamblePattern.None => 0,
        HdlcPreamblePattern.Zeros => 1,
        HdlcPreamblePattern.Flags => 2,
        HdlcPreamblePattern.Alternating10 => 3,
        HdlcPreamblePattern.Alternating01 => 4,
        HdlcPreamblePattern.Ones => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
