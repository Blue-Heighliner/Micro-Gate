namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// The <c>mghdlc.dll</c> base API operations used on Windows, expressed without pointers so the code driving them can be exercised without a device.
/// </summary>
internal interface IWindowsNative
{
    /// <summary>
    /// Opens a SyncLink port by device name.
    /// </summary>
    /// <param name="portName">The device name.</param>
    /// <param name="handle">Receives the opened device handle.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint OpenByName(string portName, out nint handle);

    /// <summary>
    /// Closes a device handle.
    /// </summary>
    /// <param name="handle">The handle to close.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint Close(nint handle);

    /// <summary>
    /// Applies the port parameters.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="parameters">The port parameters.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint SetParams(nint handle, MghdlcParams parameters);

    /// <summary>
    /// Sets the pattern transmitted between frames.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="idleMode">The numeric idle pattern.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint SetIdleMode(nint handle, uint idleMode);

    /// <summary>
    /// Enables or disables the receiver. Disabling cancels any blocked read.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="enabled">Whether the receiver is enabled.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint EnableReceiver(nint handle, bool enabled);

    /// <summary>
    /// Enables or disables the transmitter.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="enabled">Whether the transmitter is enabled.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint EnableTransmitter(nint handle, bool enabled);

    /// <summary>
    /// Reads one frame, blocking until one is available.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="buffer">The buffer to receive the frame.</param>
    /// <returns>The number of bytes read, or zero or less on failure or cancellation.</returns>
    int Read(nint handle, byte[] buffer);

    /// <summary>
    /// Writes one frame.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="buffer">The frame bytes.</param>
    /// <returns>The number of bytes written.</returns>
    int Write(nint handle, byte[] buffer);

    /// <summary>
    /// Enumerates the installed SyncLink ports.
    /// </summary>
    /// <returns>One entry per installed port.</returns>
    MghdlcPort[] EnumeratePorts();
}

/// <summary>
/// <inheritdoc cref="IWindowsNative" />
/// </summary>
internal sealed class WindowsNative : IWindowsNative
{
    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint OpenByName(string portName, out nint handle) => Mghdlc.MgslOpenByName(portName, out handle);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint Close(nint handle) => Mghdlc.MgslClose(handle);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint SetParams(nint handle, MghdlcParams parameters) => Mghdlc.MgslSetParams(handle, ref parameters);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint SetIdleMode(nint handle, uint idleMode) => Mghdlc.MgslSetIdleMode(handle, idleMode);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint EnableReceiver(nint handle, bool enabled) => Mghdlc.MgslEnableReceiver(handle, enabled ? MghdlcConstants.Enabled : MghdlcConstants.Disabled);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint EnableTransmitter(nint handle, bool enabled) => Mghdlc.MgslEnableTransmitter(handle, enabled ? MghdlcConstants.Enabled : MghdlcConstants.Disabled);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public int Read(nint handle, byte[] buffer) => Mghdlc.MgslRead(handle, buffer, buffer.Length);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public int Write(nint handle, byte[] buffer) => Mghdlc.MgslWrite(handle, buffer, buffer.Length);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public unsafe MghdlcPort[] EnumeratePorts()
    {
        MghdlcPort[] buffer = new MghdlcPort[MghdlcConstants.MaxPorts];

        fixed (MghdlcPort* ports = buffer)
        {
            uint bufferSize = (uint)(buffer.Length * sizeof(MghdlcPort));
            Mghdlc.MgslEnumeratePorts(ports, bufferSize, out uint portCount);
            return buffer[..(int)portCount];
        }
    }
}
