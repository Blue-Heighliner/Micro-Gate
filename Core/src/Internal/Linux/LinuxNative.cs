namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// The SyncLink tty device operations used on Linux, expressed without native types so the code driving them can be exercised without a device.
/// </summary>
internal interface ILinuxNative
{
    /// <summary>
    /// Opens a device path for reading and writing without blocking.
    /// </summary>
    /// <param name="path">The full device path.</param>
    /// <returns>The opened file descriptor, or -1 on failure.</returns>
    int Open(string path);

    /// <summary>
    /// Closes a file descriptor.
    /// </summary>
    /// <param name="fileDescriptor">The file descriptor to close.</param>
    /// <returns>0 on success, or -1 on failure.</returns>
    int Close(int fileDescriptor);

    /// <summary>
    /// Reads one frame, blocking until one is available.
    /// </summary>
    /// <param name="fileDescriptor">The file descriptor to read from.</param>
    /// <param name="buffer">The buffer to receive the frame.</param>
    /// <returns>The number of bytes read, 0 at end of file, or -1 on failure.</returns>
    int Read(int fileDescriptor, byte[] buffer);

    /// <summary>
    /// Writes one frame.
    /// </summary>
    /// <param name="fileDescriptor">The file descriptor to write to.</param>
    /// <param name="buffer">The frame bytes.</param>
    /// <returns>The number of bytes written, or -1 on failure.</returns>
    int Write(int fileDescriptor, byte[] buffer);

    /// <summary>
    /// Waits for all output written to the descriptor to be transmitted.
    /// </summary>
    /// <param name="fileDescriptor">The file descriptor to drain.</param>
    /// <returns>0 on success, or -1 on failure.</returns>
    int Drain(int fileDescriptor);

    /// <summary>
    /// Selects the frame-oriented HDLC line discipline for the device.
    /// </summary>
    /// <param name="fileDescriptor">The file descriptor to configure.</param>
    /// <returns>A driver-dependent result, or -1 on failure.</returns>
    int SelectHdlcLineDiscipline(int fileDescriptor);

    /// <summary>
    /// Applies the port parameters.
    /// </summary>
    /// <param name="fileDescriptor">The file descriptor to configure.</param>
    /// <param name="parameters">The port parameters.</param>
    /// <returns>A driver-dependent result, or -1 on failure.</returns>
    int SetParams(int fileDescriptor, SynclinkParams parameters);

    /// <summary>
    /// Sets the pattern transmitted between frames.
    /// </summary>
    /// <param name="fileDescriptor">The file descriptor to configure.</param>
    /// <param name="idlePattern">The numeric idle pattern.</param>
    /// <returns>A driver-dependent result, or -1 on failure.</returns>
    int SetTransmitIdle(int fileDescriptor, int idlePattern);

    /// <summary>
    /// Enables or disables the receiver. Disabling cancels any blocked read.
    /// </summary>
    /// <param name="fileDescriptor">The file descriptor to configure.</param>
    /// <param name="enabled">Whether the receiver is enabled.</param>
    /// <returns>A driver-dependent result, or -1 on failure.</returns>
    int EnableReceiver(int fileDescriptor, bool enabled);

    /// <summary>
    /// Enables or disables the transmitter.
    /// </summary>
    /// <param name="fileDescriptor">The file descriptor to configure.</param>
    /// <param name="enabled">Whether the transmitter is enabled.</param>
    /// <returns>A driver-dependent result, or -1 on failure.</returns>
    int EnableTransmitter(int fileDescriptor, bool enabled);

    /// <summary>
    /// Clears the non-blocking status flag so reads and writes block.
    /// </summary>
    /// <param name="fileDescriptor">The file descriptor to configure.</param>
    /// <returns>The result of setting the flags, or -1 on failure.</returns>
    int ClearNonBlocking(int fileDescriptor);
}

/// <summary>
/// <inheritdoc cref="ILinuxNative" />
/// </summary>
internal sealed class LinuxNative : ILinuxNative
{
    /// <inheritdoc />
    public int Open(string path) => LibC.Open(path, SynclinkConstants.FileAccessReadWrite | SynclinkConstants.FileStatusNonBlocking);

    /// <inheritdoc />
    public int Close(int fileDescriptor) => LibC.Close(fileDescriptor);

    /// <inheritdoc />
    public int Read(int fileDescriptor, byte[] buffer) => (int)LibC.Read(fileDescriptor, buffer, (nuint)buffer.Length);

    /// <inheritdoc />
    public int Write(int fileDescriptor, byte[] buffer) => (int)LibC.Write(fileDescriptor, buffer, (nuint)buffer.Length);

    /// <inheritdoc />
    public int Drain(int fileDescriptor) => LibC.Tcdrain(fileDescriptor);

    /// <inheritdoc />
    public int SelectHdlcLineDiscipline(int fileDescriptor)
    {
        int lineDiscipline = SynclinkConstants.LineDisciplineHdlc;
        return LibC.Ioctl(fileDescriptor, SynclinkConstants.SetLineDiscipline, ref lineDiscipline);
    }

    /// <inheritdoc />
    public int SetParams(int fileDescriptor, SynclinkParams parameters) => LibC.Ioctl(fileDescriptor, SynclinkConstants.SetParams, ref parameters);

    /// <inheritdoc />
    public int SetTransmitIdle(int fileDescriptor, int idlePattern) => LibC.Ioctl(fileDescriptor, SynclinkConstants.SetTransmitIdle, idlePattern);

    /// <inheritdoc />
    public int EnableReceiver(int fileDescriptor, bool enabled) => LibC.Ioctl(fileDescriptor, SynclinkConstants.EnableReceiver, enabled ? SynclinkConstants.Enabled : SynclinkConstants.Disabled);

    /// <inheritdoc />
    public int EnableTransmitter(int fileDescriptor, bool enabled) => LibC.Ioctl(fileDescriptor, SynclinkConstants.EnableTransmitter, enabled ? SynclinkConstants.Enabled : SynclinkConstants.Disabled);

    /// <inheritdoc />
    public int ClearNonBlocking(int fileDescriptor)
    {
        int flags = LibC.Fcntl(fileDescriptor, SynclinkConstants.FcntlGetFlags);
        return LibC.Fcntl(fileDescriptor, SynclinkConstants.FcntlSetFlags, flags & SynclinkConstants.FileStatusFlagMask);
    }
}
