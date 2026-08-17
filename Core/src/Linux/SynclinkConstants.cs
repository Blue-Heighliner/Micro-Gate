namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Constants and computed ioctl request codes mirrored from the SyncLink Linux driver header <c>synclink.h</c>.
/// </summary>
internal static class SynclinkConstants
{
    private const int IocNumberBits = 8;
    private const int IocTypeBits = 8;
    private const int IocSizeBits = 14;
    private const int IocNumberShift = 0;
    private const int IocTypeShift = IocNumberShift + IocNumberBits;
    private const int IocSizeShift = IocTypeShift + IocTypeBits;
    private const int IocDirectionShift = IocSizeShift + IocSizeBits;
    private const int IocDirectionNone = 0;
    private const int IocDirectionWrite = 1;
    private const int IocDirectionRead = 2;
    private const int MagicNumber = 'm';

    /// <summary>
    /// Selects HDLC synchronous mode in <see cref="SynclinkParams.Mode"/>.
    /// </summary>
    public const nuint ModeHdlc = 2;

    /// <summary>
    /// Selects NRZ line encoding in <see cref="SynclinkParams.Encoding"/>.
    /// </summary>
    public const byte EncodingNrz = 0;

    /// <summary>
    /// Selects CRC-16-CCITT in <see cref="SynclinkParams.CrcType"/>.
    /// </summary>
    public const ushort Crc16Ccitt = 1;

    /// <summary>
    /// Disables the hardware receive HDLC address filter in <see cref="SynclinkParams.AddressFilter"/>.
    /// </summary>
    public const byte AddressFilterDisabled = 0xFF;

    /// <summary>
    /// Selects the flag byte (0x7E) as the idle pattern transmitted between frames.
    /// </summary>
    public const int TransmitIdleFlags = 0;

    /// <summary>
    /// The <see cref="EnableTransmitter"/>/<see cref="EnableReceiver"/> value that enables the transmitter or receiver.
    /// </summary>
    public const int Enabled = 1;

    /// <summary>
    /// The <see cref="EnableTransmitter"/>/<see cref="EnableReceiver"/> value that disables the transmitter or receiver, canceling any blocked read or write.
    /// </summary>
    public const int Disabled = 0;

    /// <summary>
    /// The <c>N_HDLC</c> tty line discipline number, selecting frame-oriented processing of the device.
    /// </summary>
    public const int LineDisciplineHdlc = 13;

    /// <summary>
    /// The read/write file access flag.
    /// </summary>
    public const int FileAccessReadWrite = 0x0002;

    /// <summary>
    /// The non-blocking open flag, used so opening the device does not wait on DCD.
    /// </summary>
    public const int FileStatusNonBlocking = 0x0800;

    /// <summary>
    /// Disables the non-blocking file status flag, so subsequent reads and writes block.
    /// </summary>
    public const int FileStatusFlagMask = ~FileStatusNonBlocking;

    /// <summary>
    /// The <c>tcsetattr</c>/<c>fcntl</c> "get file status flags" command.
    /// </summary>
    public const int FcntlGetFlags = 3;

    /// <summary>
    /// The <c>fcntl</c> "set file status flags" command.
    /// </summary>
    public const int FcntlSetFlags = 4;

    /// <summary>
    /// The <c>ioctl</c> request that sets the tty line discipline.
    /// </summary>
    public const int SetLineDiscipline = 0x5423;

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
        (IocDirectionNone << IocDirectionShift) | (MagicNumber << IocTypeShift) | (number << IocNumberShift);

    private static int Iow(int number, int size) =>
        (IocDirectionWrite << IocDirectionShift) | (MagicNumber << IocTypeShift) | (number << IocNumberShift) | (size << IocSizeShift);
}
