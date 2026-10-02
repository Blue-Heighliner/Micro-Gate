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
    /// Sets a device option.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="optionId">The option to set, one of the driver's <c>MGSL_OPT_*</c> ids.</param>
    /// <param name="value">The option's value.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint SetOption(nint handle, uint optionId, uint value);

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
    /// Cancels a blocked write issued from another thread.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint CancelTransmit(nint handle);

    /// <summary>
    /// Cancels a blocked read issued from another thread.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint CancelReceive(nint handle);

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
    /// Waits for everything written to be transmitted.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <returns>0 on success, or a nonzero value on failure.</returns>
    int WaitAllSent(nint handle);

    /// <summary>
    /// Enumerates the installed SyncLink ports.
    /// </summary>
    /// <returns>One entry per installed port.</returns>
    /// <exception cref="IOException">The driver failed to enumerate the ports.</exception>
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
    public uint SetOption(nint handle, uint optionId, uint value) => Mghdlc.MgslSetOption(handle, optionId, value);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint EnableReceiver(nint handle, bool enabled) => Mghdlc.MgslEnableReceiver(handle, enabled ? MghdlcConstants.Enabled : MghdlcConstants.Disabled);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint EnableTransmitter(nint handle, bool enabled) => Mghdlc.MgslEnableTransmitter(handle, enabled ? MghdlcConstants.Enabled : MghdlcConstants.Disabled);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint CancelTransmit(nint handle) => Mghdlc.MgslCancelTransmit(handle);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint CancelReceive(nint handle) => Mghdlc.MgslCancelReceive(handle);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public int Read(nint handle, byte[] buffer) => Mghdlc.MgslRead(handle, buffer, buffer.Length);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public int Write(nint handle, byte[] buffer) => Mghdlc.MgslWrite(handle, buffer, buffer.Length);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public int WaitAllSent(nint handle) => Mghdlc.MgslWaitAllSent(handle);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public unsafe MghdlcPort[] EnumeratePorts()
    {
        uint status = Mghdlc.MgslEnumeratePorts(null, 0, out uint count);
        if (status != MghdlcConstants.Success)
        {
            throw new IOException("Failed to count the SyncLink ports.", new Win32Exception((int)status));
        }

        if (count == 0)
        {
            return [];
        }

        MghdlcPort[] buffer = new MghdlcPort[count];

        fixed (MghdlcPort* ports = buffer)
        {
            status = Mghdlc.MgslEnumeratePorts(ports, (uint)(buffer.Length * sizeof(MghdlcPort)), out count);
        }

        if (status != MghdlcConstants.Success)
        {
            throw new IOException("Failed to enumerate the SyncLink ports.", new Win32Exception((int)status));
        }

        return buffer[..(int)Math.Min(count, (uint)buffer.Length)];
    }
}
