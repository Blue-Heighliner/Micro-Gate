namespace BlueHeighliner.MicroGate;

internal sealed partial class PseudoTerminal : IDisposable
{
    private PseudoTerminal(int master, int slave, string slavePath)
    {
        this.master = master;
        this.slave = slave;
        SlavePath = slavePath;
    }

    private readonly int master;
    private readonly int slave;
    private int masterClosed;

    public string SlavePath { get; }

    public static PseudoTerminal? Create()
    {
        int master = OpenPt(0x102);
        if (master < 0)
        {
            return null;
        }

        byte[] name = new byte[256];
        if (GrantPt(master) != 0 || UnlockPt(master) != 0 || PtsNameR(master, name, (nuint)name.Length) != 0)
        {
            Close(master);
            return null;
        }

        string path = Encoding.UTF8.GetString(name, 0, Array.IndexOf(name, (byte)0));
        int slave = Open(path, 0x102);
        if (slave < 0)
        {
            Close(master);
            return null;
        }

        byte[] termios = new byte[256];
        TcGetAttr(slave, termios);
        CfMakeRaw(termios);
        TcSetAttr(slave, 0, termios);

        return new PseudoTerminal(master, slave, path);
    }

    public int Read(byte[] buffer, int timeoutMilliseconds)
    {
        PollFd poll = new() { Fd = master, Events = 1 };
        if (Poll(ref poll, 1, timeoutMilliseconds) == 0)
        {
            return -2;
        }

        return (int)ReadNative(master, buffer, (nuint)buffer.Length);
    }

    public void Write(ReadOnlySpan<byte> data) => WriteNative(master, data.ToArray(), (nuint)data.Length);

    public void CloseMaster()
    {
        if (Interlocked.Exchange(ref masterClosed, 1) == 0)
        {
            Close(master);
        }
    }

    public void Dispose()
    {
        CloseMaster();
        Close(slave);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int Fd;
        public short Events;
        public short Revents;
    }

    [LibraryImport("libc", EntryPoint = "poll")]
    private static partial int Poll(ref PollFd descriptors, nuint count, int timeout);

    [LibraryImport("libc", EntryPoint = "posix_openpt")]
    private static partial int OpenPt(int flags);

    [LibraryImport("libc", EntryPoint = "grantpt")]
    private static partial int GrantPt(int fd);

    [LibraryImport("libc", EntryPoint = "unlockpt")]
    private static partial int UnlockPt(int fd);

    [LibraryImport("libc", EntryPoint = "ptsname_r")]
    private static partial int PtsNameR(int fd, byte[] buffer, nuint size);

    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "close")]
    private static partial int Close(int fd);

    [LibraryImport("libc", EntryPoint = "read")]
    private static partial nint ReadNative(int fd, byte[] buffer, nuint count);

    [LibraryImport("libc", EntryPoint = "write")]
    private static partial nint WriteNative(int fd, byte[] buffer, nuint count);

    [LibraryImport("libc", EntryPoint = "tcgetattr")]
    private static partial int TcGetAttr(int fd, byte[] termios);

    [LibraryImport("libc", EntryPoint = "tcsetattr")]
    private static partial int TcSetAttr(int fd, int actions, byte[] termios);

    [LibraryImport("libc", EntryPoint = "cfmakeraw")]
    private static partial void CfMakeRaw(byte[] termios);
}
