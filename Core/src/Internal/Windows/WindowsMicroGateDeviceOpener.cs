namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Opens and configures MicroGate SyncLink devices installed on a Windows system. A port name is a device name as reported by <see cref="IWindowsMicroGatePorts.GetPorts"/>.
/// </summary>
/// <param name="native">The native device operations.</param>
internal sealed class WindowsMicroGateDeviceOpener(IWindowsNative native) : IMicroGateDeviceOpener
{
    /// <inheritdoc />
    public IMicroGateDevice Open(string portName, MicroGatePeerOptions options)
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

    private void ConfigurePort(nint handle, MicroGatePeerOptions options)
    {
        MghdlcParams parameters = new()
        {
            Mode = MghdlcConstants.ModeHdlc,
            Encoding = (byte)options.Encoding,
            CrcType = (ushort)options.Crc,
            Addr = options.HardwareAddressFilter ?? MghdlcConstants.AddressFilterDisabled,
        };
        Check(native.SetParams(handle, parameters), "set the port parameters");
        Check(native.SetIdleMode(handle, (uint)options.IdlePattern), "set the idle pattern");
        Check(native.EnableReceiver(handle, true), "enable the receiver");
        Check(native.EnableTransmitter(handle, true), "enable the transmitter");
    }

    private void Check(uint status, string step)
    {
        if (status != MghdlcConstants.Success)
        {
            throw new IOException($"Failed to {step}.", new Win32Exception((int)status));
        }
    }
}
