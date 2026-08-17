namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// P/Invoke declarations for the subset of libc used to operate a SyncLink tty device: opening, closing, configuring, and performing blocking reads and writes on the device's file descriptor.
/// </summary>
internal static partial class LibC
{
    /// <summary>
    /// Opens a file, per POSIX <c>open(2)</c>.
    /// </summary>
    /// <param name="pathname">The path of the file to open.</param>
    /// <param name="flags">A bitwise combination of file access and status flags.</param>
    /// <returns>The opened file descriptor, or -1 on failure.</returns>
    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    public static partial int open(string pathname, int flags);

    /// <summary>
    /// Closes a file descriptor, per POSIX <c>close(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to close.</param>
    /// <returns>0 on success, or -1 on failure.</returns>
    [LibraryImport("libc", SetLastError = true)]
    public static partial int close(int fd);

    /// <summary>
    /// Reads from a file descriptor, per POSIX <c>read(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to read from.</param>
    /// <param name="buffer">The buffer to receive the data read.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The number of bytes read, 0 at end of file, or -1 on failure.</returns>
    [LibraryImport("libc", SetLastError = true)]
    public static partial nint read(int fd, byte[] buffer, nuint count);

    /// <summary>
    /// Writes to a file descriptor, per POSIX <c>write(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to write to.</param>
    /// <param name="buffer">The buffer containing the data to write.</param>
    /// <param name="count">The number of bytes to write.</param>
    /// <returns>The number of bytes written, or -1 on failure.</returns>
    [LibraryImport("libc", SetLastError = true)]
    public static partial nint write(int fd, byte[] buffer, nuint count);

    /// <summary>
    /// Sets the file status flags of an open file descriptor, per POSIX <c>fcntl(2)</c> with <see cref="SynclinkConstants.FcntlSetFlags"/>.
    /// </summary>
    /// <param name="fd">The file descriptor to modify.</param>
    /// <param name="command">The <c>fcntl</c> command.</param>
    /// <param name="argument">The command argument.</param>
    /// <returns>A command-dependent result, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    public static partial int fcntl(int fd, int command, int argument);

    /// <summary>
    /// Retrieves the file status flags of an open file descriptor, per POSIX <c>fcntl(2)</c> with <see cref="SynclinkConstants.FcntlGetFlags"/>.
    /// </summary>
    /// <param name="fd">The file descriptor to query.</param>
    /// <param name="command">The <c>fcntl</c> command.</param>
    /// <returns>The current file status flags, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    public static partial int fcntl(int fd, int command);

    /// <summary>
    /// Performs a device-specific control operation carrying an <see cref="int"/> argument by reference, per POSIX <c>ioctl(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to operate on.</param>
    /// <param name="request">The device-specific request code.</param>
    /// <param name="argument">The request argument.</param>
    /// <returns>A request-dependent result, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static partial int ioctl(int fd, int request, ref int argument);

    /// <summary>
    /// Performs a device-specific control operation carrying a <see cref="SynclinkParams"/> argument by reference, per POSIX <c>ioctl(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to operate on.</param>
    /// <param name="request">The device-specific request code.</param>
    /// <param name="argument">The request argument.</param>
    /// <returns>A request-dependent result, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static partial int ioctl(int fd, int request, ref SynclinkParams argument);

    /// <summary>
    /// Performs a device-specific control operation carrying an immediate argument, per POSIX <c>ioctl(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to operate on.</param>
    /// <param name="request">The device-specific request code.</param>
    /// <param name="argument">The request argument.</param>
    /// <returns>A request-dependent result, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static partial int ioctl(int fd, int request, nint argument);

    /// <summary>
    /// Waits for all output written to a file descriptor to be transmitted, per POSIX <c>tcdrain(3)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to drain.</param>
    /// <returns>0 on success, or -1 on failure.</returns>
    [LibraryImport("libc", SetLastError = true)]
    public static partial int tcdrain(int fd);
}
