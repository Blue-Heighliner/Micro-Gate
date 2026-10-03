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
    public IMicroGateDevice Open(string portName, MicroGatePeerOptions options)
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

    private void ConfigurePort(nint handle, MicroGatePeerOptions options)
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

    private ushort MapFlags(MicroGatePeerOptions options) =>
        (ushort)(MapReceiveClockSource(options.Link.ReceiveClockSource) | MapTransmitClockSource(options.Link.TransmitClockSource) | MapPhaseLockedLoopDivisor(options.Link.PhaseLockedLoopDivisor) | MapUnderrunAction(options.UnderrunAction));

    private ushort MapReceiveClockSource(MicroGateReceiveClockSource value) => value switch
    {
        MicroGateReceiveClockSource.OwnPin => 0,
        MicroGateReceiveClockSource.OtherPin => MghdlcConstants.ReceiveClockOtherPin,
        MicroGateReceiveClockSource.PhaseLockedLoop => MghdlcConstants.ReceiveClockDpll,
        MicroGateReceiveClockSource.BaudRateGenerator => MghdlcConstants.ReceiveClockBrg,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapTransmitClockSource(MicroGateTransmitClockSource value) => value switch
    {
        MicroGateTransmitClockSource.OwnPin => 0,
        MicroGateTransmitClockSource.OtherPin => MghdlcConstants.TransmitClockOtherPin,
        MicroGateTransmitClockSource.PhaseLockedLoop => MghdlcConstants.TransmitClockDpll,
        MicroGateTransmitClockSource.BaudRateGenerator => MghdlcConstants.TransmitClockBrg,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapPhaseLockedLoopDivisor(MicroGatePhaseLockedLoopDivisor value) => value switch
    {
        MicroGatePhaseLockedLoopDivisor.DivideBy32 => 0,
        MicroGatePhaseLockedLoopDivisor.DivideBy8 => MghdlcConstants.DpllDivisor8,
        MicroGatePhaseLockedLoopDivisor.DivideBy16 => MghdlcConstants.DpllDivisor16,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapUnderrunAction(MicroGateUnderrunAction value) => value switch
    {
        MicroGateUnderrunAction.Abort7 => 0,
        MicroGateUnderrunAction.Abort15 => MghdlcConstants.UnderrunAbort15,
        MicroGateUnderrunAction.Flag => MghdlcConstants.UnderrunFlag,
        MicroGateUnderrunAction.InvalidFrameCheckSequence => MghdlcConstants.UnderrunBadCrc,
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

    private uint MapIdlePattern(MicroGateIdlePattern value) => value switch
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
