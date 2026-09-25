namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Constants and computed ioctl request codes mirrored from the SyncLink Linux driver header <c>synclink.h</c>.
/// </summary>
internal static class SynclinkConstants
{
    private static readonly int iocNumberBits = 8;
    private static readonly int iocTypeBits = 8;
    private static readonly int iocSizeBits = 14;
    private static readonly int iocNumberShift = 0;
    private static readonly int iocTypeShift = iocNumberShift + iocNumberBits;
    private static readonly int iocSizeShift = iocTypeShift + iocTypeBits;
    private static readonly int iocDirectionShift = iocSizeShift + iocSizeBits;
    private static readonly int iocDirectionNone = 0;
    private static readonly int iocDirectionWrite = 1;
    private static readonly int magicNumber = 'm';

    /// <summary>
    /// Selects HDLC synchronous mode in <see cref="SynclinkParams.Mode"/>.
    /// </summary>
    public static readonly nuint ModeHdlc = 2;

    /// <summary>
    /// The hardware receive HDLC address filter value in <see cref="SynclinkParams.AddressFilter"/> that disables filtering.
    /// </summary>
    public static readonly byte AddressFilterDisabled = 0xFF;

    /// <summary>
    /// The <see cref="EnableTransmitter"/>/<see cref="EnableReceiver"/> value that enables the transmitter or receiver.
    /// </summary>
    public static readonly int Enabled = 1;

    /// <summary>
    /// The <see cref="EnableTransmitter"/>/<see cref="EnableReceiver"/> value that disables the transmitter or receiver, canceling any blocked read or write.
    /// </summary>
    public static readonly int Disabled = 0;

    /// <summary>
    /// The <c>N_HDLC</c> tty line discipline number, selecting frame-oriented processing of the device.
    /// </summary>
    public static readonly int LineDisciplineHdlc = 13;

    /// <summary>
    /// The <c>poll</c> event meaning data can be read.
    /// </summary>
    public static readonly short PollReadable = 0x0001;

    /// <summary>
    /// Opens the device without making it the process's controlling terminal.
    /// </summary>
    public static readonly int FileNoControllingTerminal = 0x0100;

    /// <summary>
    /// The read/write file access flag.
    /// </summary>
    public static readonly int FileAccessReadWrite = 0x0002;

    /// <summary>
    /// The non-blocking open flag, used so opening the device does not wait on DCD.
    /// </summary>
    public static readonly int FileStatusNonBlocking = 0x0800;

    /// <summary>
    /// Disables the non-blocking file status flag, so subsequent reads and writes block.
    /// </summary>
    public static readonly int FileStatusFlagMask = ~FileStatusNonBlocking;

    /// <summary>
    /// The <c>tcsetattr</c>/<c>fcntl</c> "get file status flags" command.
    /// </summary>
    public static readonly int FcntlGetFlags = 3;

    /// <summary>
    /// The <c>fcntl</c> "set file status flags" command.
    /// </summary>
    public static readonly int FcntlSetFlags = 4;

    /// <summary>
    /// The <c>ioctl</c> request that sets the tty line discipline.
    /// </summary>
    public static readonly int SetLineDiscipline = 0x5423;

    /// <summary>
    /// The <c>MGSL_IOCSPARAMS</c> request that sets the port's <see cref="SynclinkParams"/>.
    /// </summary>
    public static readonly int SetParams = Iow(0, Marshal.SizeOf<SynclinkParams>());

    /// <summary>
    /// The <c>MGSL_IOCSTXIDLE</c> request that sets the transmit idle pattern.
    /// </summary>
    public static readonly int SetTransmitIdle = Io(2);

    /// <summary>
    /// The <c>MGSL_IOCTXENABLE</c> request that enables or disables the transmitter.
    /// </summary>
    public static readonly int EnableTransmitter = Io(4);

    /// <summary>
    /// The <c>MGSL_IOCRXENABLE</c> request that enables or disables the receiver.
    /// </summary>
    public static readonly int EnableReceiver = Io(5);

    private static int Io(int number) =>
        (iocDirectionNone << iocDirectionShift) | (magicNumber << iocTypeShift) | (number << iocNumberShift);

    private static int Iow(int number, int size) =>
        (iocDirectionWrite << iocDirectionShift) | (magicNumber << iocTypeShift) | (number << iocNumberShift) | (size << iocSizeShift);
}
