namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Opens and configures MicroGate SyncLink devices installed on a Windows system. A port name is a device name as reported by <see cref="IWindowsMicroGatePorts.GetPorts"/>.
/// </summary>
/// <param name="native">The native device operations.</param>
/// <param name="busyRetryWindow">How long an open keeps retrying while the driver reports the device in use, or <see langword="null"/> for three seconds.</param>
internal sealed class WindowsMicroGateDeviceOpener(IWindowsNative native, TimeSpan? busyRetryWindow = null) : IMicroGateDeviceOpener
{
    private readonly TimeSpan busyRetryWindow = busyRetryWindow ?? TimeSpan.FromSeconds(3);
    private readonly TimeSpan busyRetryInterval = TimeSpan.FromMilliseconds(50);

    /// <inheritdoc />
    public IMicroGateDevice Open(string portName, HdlcPeerOptions options)
    {
        uint openStatus = OpenWhenReleased(portName, out nint handle);
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

    private void ConfigurePort(nint handle, HdlcPeerOptions options)
    {
        // Both options are best effort: setting the interface can need privileges the user lacks, and a device that cannot switch is still usable as it is.
        native.SetOption(handle, MghdlcConstants.OptionInterface, MghdlcConstants.InterfaceRs232);
        native.SetOption(handle, MghdlcConstants.OptionReceiveErrorMask, MghdlcConstants.OptionOn);
        MghdlcParams parameters = new()
        {
            Mode = MghdlcConstants.ModeHdlc,
            Loopback = (byte)(options.Loopback ? 1 : 0),
            Flags = MapFlags(options),
            Encoding = MapEncoding(options.Link.Encoding),
            ClockSpeed = (uint)options.Link.ClockSpeed,
            CrcType = MapCrc(options.Link.Crc),
            Addr = MghdlcConstants.AddressFilterDisabled,
            PreambleLength = MapPreambleLength(options.PreambleLength),
            PreamblePattern = MapPreamblePattern(options.PreamblePattern),
        };
        Check(native.SetParams(handle, parameters), "set the port parameters");
        Check(native.SetIdleMode(handle, MapIdlePattern(options.IdlePattern)), "set the idle pattern");
        Check(native.EnableReceiver(handle, true), "enable the receiver");
    }

    private void Check(uint status, string step)
    {
        if (status != MghdlcConstants.Success)
        {
            throw new IOException($"Failed to {step}.", new Win32Exception((int)status));
        }
    }

    private ushort MapFlags(HdlcPeerOptions options) =>
        (ushort)(MapReceiveClockSource(options.Link.ReceiveClockSource) | MapTransmitClockSource(options.Link.TransmitClockSource) | MapPhaseLockedLoopDivisor(options.Link.PhaseLockedLoopDivisor) | MapUnderrunAction(options.UnderrunAction));

    private ushort MapReceiveClockSource(HdlcReceiveClockSource value) => value switch
    {
        HdlcReceiveClockSource.OwnPin => 0,
        HdlcReceiveClockSource.OtherPin => MghdlcConstants.ReceiveClockOtherPin,
        HdlcReceiveClockSource.PhaseLockedLoop => MghdlcConstants.ReceiveClockDpll,
        HdlcReceiveClockSource.BaudRateGenerator => MghdlcConstants.ReceiveClockBrg,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapTransmitClockSource(HdlcTransmitClockSource value) => value switch
    {
        HdlcTransmitClockSource.OwnPin => 0,
        HdlcTransmitClockSource.OtherPin => MghdlcConstants.TransmitClockOtherPin,
        HdlcTransmitClockSource.PhaseLockedLoop => MghdlcConstants.TransmitClockDpll,
        HdlcTransmitClockSource.BaudRateGenerator => MghdlcConstants.TransmitClockBrg,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapPhaseLockedLoopDivisor(HdlcPhaseLockedLoopDivisor value) => value switch
    {
        HdlcPhaseLockedLoopDivisor.DivideBy32 => 0,
        HdlcPhaseLockedLoopDivisor.DivideBy8 => MghdlcConstants.DpllDivisor8,
        HdlcPhaseLockedLoopDivisor.DivideBy16 => MghdlcConstants.DpllDivisor16,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapUnderrunAction(HdlcUnderrunAction value) => value switch
    {
        HdlcUnderrunAction.Abort7 => 0,
        HdlcUnderrunAction.Abort15 => MghdlcConstants.UnderrunAbort15,
        HdlcUnderrunAction.Flag => MghdlcConstants.UnderrunFlag,
        HdlcUnderrunAction.InvalidFrameCheckSequence => MghdlcConstants.UnderrunBadCrc,
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

    private uint MapIdlePattern(HdlcIdlePattern value) => value switch
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
