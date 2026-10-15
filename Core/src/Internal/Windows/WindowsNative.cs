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
    /// Reads the serial control and status signals.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="signals">Receives the active signals as <c>SerialSignal_*</c> bits.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint GetSignals(nint handle, out byte signals);

    /// <summary>
    /// Blocks until one of the given modem wire events occurs, or <see cref="CancelSignalWait"/> is called.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="mask">The <c>MgslEvent_*</c> events of interest.</param>
    /// <param name="events">Receives the events that occurred, or zero if the wait was cancelled.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint WaitSignalEvents(nint handle, uint mask, out uint events);

    /// <summary>
    /// Cancels a blocked <see cref="WaitSignalEvents"/> issued from another thread.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint CancelSignalWait(nint handle);

    /// <summary>
    /// Turns on the data terminal ready and request to send outputs, which tell the remote station this one is present.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint RaiseSignals(nint handle);

    /// <summary>
    /// Turns off the data terminal ready and request to send outputs, which tells the remote station this one has gone.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    uint LowerSignals(nint handle);

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
    /// Reads one frame, blocking until one is available. A frame with no data bytes is skipped rather than reported as zero.
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
    public uint GetSignals(nint handle, out byte signals) => Mghdlc.MgslGetSerialSignals(handle, out signals);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint WaitSignalEvents(nint handle, uint mask, out uint events)
    {
        events = 0;
        nint overlapped = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlapped>());
        nint received = Marshal.AllocHGlobal(sizeof(uint));
        nint signal = Kernel32.CreateEvent(0, 1, 0, 0);
        try
        {
            Marshal.WriteInt32(received, 0);
            Marshal.StructureToPtr(new NativeOverlapped { EventHandle = signal }, overlapped, false);
            uint status = Mghdlc.MgslWaitEvent(handle, mask, received, overlapped);
            if (status == MghdlcConstants.IoPending)
            {
                Kernel32.WaitForSingleObject(signal, uint.MaxValue);
                status = MghdlcConstants.Success;
            }

            events = (uint)Marshal.ReadInt32(received);
            return status;
        }
        finally
        {
            Kernel32.CloseHandle(signal);
            Marshal.FreeHGlobal(received);
            Marshal.FreeHGlobal(overlapped);
        }
    }

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint CancelSignalWait(nint handle) => Mghdlc.MgslCancelWaitEvent(handle);

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint RaiseSignals(nint handle) => Mghdlc.MgslSetSerialSignals(handle, (byte)(MghdlcConstants.SignalDataTerminalReady | MghdlcConstants.SignalRequestToSend));

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public uint LowerSignals(nint handle) => Mghdlc.MgslSetSerialSignals(handle, 0);

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
    public int Read(nint handle, byte[] buffer)
    {
        while (true)
        {
            int count = Mghdlc.MgslReadWithStatus(handle, buffer, buffer.Length, out int status);
            if (count != 0 || status != MghdlcConstants.RxStatusOk)
            {
                return count;
            }
        }
    }

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
