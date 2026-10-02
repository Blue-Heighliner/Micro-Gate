namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Opens and configures MicroGate SyncLink devices installed on a Windows system for read-only monitoring. A port name is a device name as reported by <see cref="IWindowsMicroGatePorts.GetPorts"/>.
/// </summary>
/// <param name="native">The native device operations.</param>
internal sealed class WindowsMicroGateMonitorDeviceOpener(IWindowsNative native) : IMicroGateMonitorDeviceOpener
{
    /// <inheritdoc />
    public IMicroGateDevice Open(string portName, MicroGateMonitorOptions options)
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

        return device;
    }

    /// <summary>
    /// Configures the device for HDLC framing and enables its receiver only. The transmitter is deliberately never enabled: monitoring never writes to the device, and this is the layer that makes that structurally true instead of merely a convention observed elsewhere in the code.
    /// </summary>
    private void ConfigurePort(nint handle, MicroGateMonitorOptions options)
    {
        // Both options are best effort: setting the interface can need privileges the user lacks, and a device that cannot switch is still usable as it is.
        native.SetOption(handle, MghdlcConstants.OptionInterface, MghdlcConstants.InterfaceRs232);
        native.SetOption(handle, MghdlcConstants.OptionReceiveErrorMask, MghdlcConstants.OptionOn);
        MghdlcParams parameters = new()
        {
            Mode = MghdlcConstants.ModeHdlc,
            Flags = (ushort)(MapReceiveClockSource(options.ReceiveClockSource) | MapPhaseLockedLoopDivisor(options.PhaseLockedLoopDivisor)),
            Encoding = MapEncoding(options.Encoding),
            ClockSpeed = (uint)options.ClockSpeed,
            CrcType = MapCrc(options.Crc),
            Addr = options.HardwareAddressFilter ?? MghdlcConstants.AddressFilterDisabled,
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

    private ushort MapReceiveClockSource(MicroGateReceiveClockSource value) => value switch
    {
        MicroGateReceiveClockSource.OwnPin => 0,
        MicroGateReceiveClockSource.OtherPin => MghdlcConstants.ReceiveClockOtherPin,
        MicroGateReceiveClockSource.PhaseLockedLoop => MghdlcConstants.ReceiveClockDpll,
        MicroGateReceiveClockSource.BaudRateGenerator => MghdlcConstants.ReceiveClockBrg,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private ushort MapPhaseLockedLoopDivisor(MicroGatePhaseLockedLoopDivisor value) => value switch
    {
        MicroGatePhaseLockedLoopDivisor.DivideBy32 => 0,
        MicroGatePhaseLockedLoopDivisor.DivideBy8 => MghdlcConstants.DpllDivisor8,
        MicroGatePhaseLockedLoopDivisor.DivideBy16 => MghdlcConstants.DpllDivisor16,
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
}
