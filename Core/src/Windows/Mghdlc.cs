namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// P/Invoke declarations for the base (non link-layer) API of <c>mghdlc.dll</c>: the subset of <c>Mgsl*</c> functions used to move raw, bit-framed HDLC frames across a SyncLink port. The link-layer (<c>MgslDl*</c>) asynchronous balanced mode engine built into the driver is intentionally not used, so that Windows and Linux share the same <see cref="HdlcStateMachine"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class Mghdlc
{
    /// <summary>
    /// Opens a SyncLink port by device name, per <c>MgslOpenByName</c>.
    /// </summary>
    /// <param name="portName">The device name, as reported by <see cref="MgslEnumeratePorts"/>.</param>
    /// <param name="handle">Receives the opened device handle.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    [LibraryImport("mghdlc.dll", EntryPoint = "MgslOpenByName")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    public static partial uint MgslOpenByName([MarshalAs(UnmanagedType.LPStr)] string portName, out nint handle);

    /// <summary>
    /// Closes a device handle opened by <see cref="MgslOpenByName"/>, per <c>MgslClose</c>.
    /// </summary>
    /// <param name="handle">The device handle to close.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    [LibraryImport("mghdlc.dll", EntryPoint = "MgslClose")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    public static partial uint MgslClose(nint handle);

    /// <summary>
    /// Sets the port's <see cref="MghdlcParams"/>, resetting and reconfiguring the hardware, per <c>MgslSetParams</c>.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="parameters">The parameters to apply.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    [LibraryImport("mghdlc.dll", EntryPoint = "MgslSetParams")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    public static partial uint MgslSetParams(nint handle, ref MghdlcParams parameters);

    /// <summary>
    /// Sets the pattern transmitted between frames, per <c>MgslSetIdleMode</c>.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="idleMode">The idle mode, one of the <c>HDLC_TXIDLE_*</c> constants.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    [LibraryImport("mghdlc.dll", EntryPoint = "MgslSetIdleMode")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    public static partial uint MgslSetIdleMode(nint handle, uint idleMode);

    /// <summary>
    /// Enables or disables the transmitter, per <c>MgslEnableTransmitter</c>. Disabling cancels a blocked <see cref="MgslWrite"/>.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="enableFlag">Non-zero to enable, zero to disable.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    [LibraryImport("mghdlc.dll", EntryPoint = "MgslEnableTransmitter")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    public static partial uint MgslEnableTransmitter(nint handle, uint enableFlag);

    /// <summary>
    /// Enables or disables the receiver, per <c>MgslEnableReceiver</c>. Disabling cancels a blocked <see cref="MgslRead"/>.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="enableFlag">Non-zero to enable, zero to disable.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    [LibraryImport("mghdlc.dll", EntryPoint = "MgslEnableReceiver")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    public static partial uint MgslEnableReceiver(nint handle, uint enableFlag);

    /// <summary>
    /// Blocks until an entire HDLC frame has been written, per <c>MgslWrite</c>.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="buffer">The frame bytes to write.</param>
    /// <param name="size">The number of bytes to write.</param>
    /// <returns>The number of bytes written.</returns>
    [LibraryImport("mghdlc.dll", EntryPoint = "MgslWrite")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    public static partial int MgslWrite(nint handle, byte[] buffer, int size);

    /// <summary>
    /// Blocks until an entire HDLC frame has been read, per <c>MgslRead</c>.
    /// </summary>
    /// <param name="handle">The device handle.</param>
    /// <param name="buffer">The buffer to receive the frame bytes.</param>
    /// <param name="size">The capacity of <paramref name="buffer"/>.</param>
    /// <returns>The number of bytes read.</returns>
    [LibraryImport("mghdlc.dll", EntryPoint = "MgslRead")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    public static partial int MgslRead(nint handle, byte[] buffer, int size);

    /// <summary>
    /// Lists the SyncLink ports installed on the local machine, per <c>MgslEnumeratePorts</c>.
    /// </summary>
    /// <param name="ports">The buffer to receive the enumerated ports.</param>
    /// <param name="bufferSize">The capacity of <paramref name="ports"/>, in bytes.</param>
    /// <param name="portCount">Receives the number of ports written to <paramref name="ports"/>.</param>
    /// <returns>0 on success, or a Win32 error code.</returns>
    [LibraryImport("mghdlc.dll", EntryPoint = "MgslEnumeratePorts")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    public static unsafe partial uint MgslEnumeratePorts(MghdlcPort* ports, uint bufferSize, out uint portCount);
}
