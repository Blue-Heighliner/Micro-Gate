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
    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    public static partial int Open(string pathname, int flags);

    /// <summary>
    /// Closes a file descriptor, per POSIX <c>close(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to close.</param>
    /// <returns>0 on success, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    public static partial int Close(int fd);

    /// <summary>
    /// Reads from a file descriptor, per POSIX <c>read(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to read from.</param>
    /// <param name="buffer">The buffer to receive the data read.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The number of bytes read, 0 at end of file, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "read", SetLastError = true)]
    public static partial nint Read(int fd, byte[] buffer, nuint count);

    /// <summary>
    /// Writes to a file descriptor, per POSIX <c>write(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to write to.</param>
    /// <param name="buffer">The buffer containing the data to write.</param>
    /// <param name="count">The number of bytes to write.</param>
    /// <returns>The number of bytes written, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "write", SetLastError = true)]
    public static partial nint Write(int fd, byte[] buffer, nuint count);

    /// <summary>
    /// Sets the file status flags of an open file descriptor, per POSIX <c>fcntl(2)</c> with <see cref="SynclinkConstants.FcntlSetFlags"/>.
    /// </summary>
    /// <param name="fd">The file descriptor to modify.</param>
    /// <param name="command">The <c>fcntl</c> command.</param>
    /// <param name="argument">The command argument.</param>
    /// <returns>A command-dependent result, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    public static partial int Fcntl(int fd, int command, int argument);

    /// <summary>
    /// Retrieves the file status flags of an open file descriptor, per POSIX <c>fcntl(2)</c> with <see cref="SynclinkConstants.FcntlGetFlags"/>.
    /// </summary>
    /// <param name="fd">The file descriptor to query.</param>
    /// <param name="command">The <c>fcntl</c> command.</param>
    /// <returns>The current file status flags, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    public static partial int Fcntl(int fd, int command);

    /// <summary>
    /// Performs a device-specific control operation carrying an <see cref="int"/> argument by reference, per POSIX <c>ioctl(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to operate on.</param>
    /// <param name="request">The device-specific request code, an <c>unsigned long</c> in the C declaration.</param>
    /// <param name="argument">The request argument.</param>
    /// <returns>A request-dependent result, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static partial int Ioctl(int fd, nuint request, ref int argument);

    /// <summary>
    /// Performs a device-specific control operation carrying a <see cref="SynclinkParams"/> argument by reference, per POSIX <c>ioctl(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to operate on.</param>
    /// <param name="request">The device-specific request code, an <c>unsigned long</c> in the C declaration.</param>
    /// <param name="argument">The request argument.</param>
    /// <returns>A request-dependent result, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static partial int Ioctl(int fd, nuint request, ref SynclinkParams argument);

    /// <summary>
    /// Performs a device-specific control operation carrying an immediate argument, per POSIX <c>ioctl(2)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to operate on.</param>
    /// <param name="request">The device-specific request code, an <c>unsigned long</c> in the C declaration.</param>
    /// <param name="argument">The request argument.</param>
    /// <returns>A request-dependent result, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static partial int Ioctl(int fd, nuint request, nint argument);

    /// <summary>
    /// Waits for a file descriptor to become ready, per POSIX <c>poll(2)</c>.
    /// </summary>
    /// <param name="descriptor">The descriptor and events to watch; the returned events are stored back into it.</param>
    /// <param name="count">The number of descriptors, which is 1.</param>
    /// <param name="timeout">The longest time to wait, in milliseconds.</param>
    /// <returns>The number of ready descriptors, 0 on timeout, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "poll", SetLastError = true)]
    public static partial int Poll(ref PollDescriptor descriptor, nuint count, int timeout);

    /// <summary>
    /// Reads the terminal settings of a file descriptor, per POSIX <c>tcgetattr(3)</c>.
    /// </summary>
    /// <param name="fd">The terminal file descriptor.</param>
    /// <param name="termios">A buffer, at least the size of the platform's <c>struct termios</c>, to receive the settings.</param>
    /// <returns>0 on success, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "tcgetattr", SetLastError = true)]
    public static partial int Tcgetattr(int fd, [In, Out] byte[] termios);

    /// <summary>
    /// Applies terminal settings to a file descriptor, per POSIX <c>tcsetattr(3)</c>.
    /// </summary>
    /// <param name="fd">The terminal file descriptor.</param>
    /// <param name="optionalActions">When the change takes effect; 0 is <c>TCSANOW</c>, at once.</param>
    /// <param name="termios">The settings to apply.</param>
    /// <returns>0 on success, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "tcsetattr", SetLastError = true)]
    public static partial int Tcsetattr(int fd, int optionalActions, [In, Out] byte[] termios);

    /// <summary>
    /// Changes terminal settings to raw mode, per <c>cfmakeraw(3)</c>.
    /// </summary>
    /// <param name="termios">The settings to change.</param>
    [LibraryImport("libc", EntryPoint = "cfmakeraw")]
    public static partial void Cfmakeraw([In, Out] byte[] termios);

    /// <summary>
    /// Sets the input speed in terminal settings, per <c>cfsetispeed(3)</c>.
    /// </summary>
    /// <param name="termios">The settings to change.</param>
    /// <param name="speed">The speed constant, such as <c>B9600</c>.</param>
    /// <returns>0 on success, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "cfsetispeed", SetLastError = true)]
    public static partial int Cfsetispeed([In, Out] byte[] termios, uint speed);

    /// <summary>
    /// Sets the output speed in terminal settings, per <c>cfsetospeed(3)</c>.
    /// </summary>
    /// <param name="termios">The settings to change.</param>
    /// <param name="speed">The speed constant, such as <c>B9600</c>.</param>
    /// <returns>0 on success, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "cfsetospeed", SetLastError = true)]
    public static partial int Cfsetospeed([In, Out] byte[] termios, uint speed);

    /// <summary>
    /// Waits for all output written to a file descriptor to be transmitted, per POSIX <c>tcdrain(3)</c>.
    /// </summary>
    /// <param name="fd">The file descriptor to drain.</param>
    /// <returns>0 on success, or -1 on failure.</returns>
    [LibraryImport("libc", EntryPoint = "tcdrain", SetLastError = true)]
    public static partial int Tcdrain(int fd);
}
