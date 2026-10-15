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
    private static readonly int iocDirectionRead = 2;
    private static readonly int magicNumber = 'm';

    /// <summary>
    /// Selects asynchronous mode in <see cref="SynclinkParams.Mode"/>.
    /// </summary>
    public static readonly nuint ModeAsync = 1;

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
    /// The <c>HDLC_FLAG_RXC_TXCPIN</c> bit of <see cref="SynclinkParams.Flags"/>: the receive clock comes from the transmit clock (TXC) pin.
    /// </summary>
    public static readonly ushort ReceiveClockOtherPin = 0x8000;

    /// <summary>
    /// The <c>HDLC_FLAG_RXC_DPLL</c> bit of <see cref="SynclinkParams.Flags"/>: the receive clock comes from the digital phase locked loop.
    /// </summary>
    public static readonly ushort ReceiveClockDpll = 0x0100;

    /// <summary>
    /// The <c>HDLC_FLAG_RXC_BRG</c> bit of <see cref="SynclinkParams.Flags"/>: the receive clock comes from the internal baud rate generator.
    /// </summary>
    public static readonly ushort ReceiveClockBrg = 0x0200;

    /// <summary>
    /// The <c>HDLC_FLAG_TXC_RXCPIN</c> bit of <see cref="SynclinkParams.Flags"/>: the transmit clock comes from the receive clock (RXC) pin.
    /// </summary>
    public static readonly ushort TransmitClockOtherPin = 0x0008;

    /// <summary>
    /// The <c>HDLC_FLAG_TXC_DPLL</c> bit of <see cref="SynclinkParams.Flags"/>: the transmit clock comes from the digital phase locked loop.
    /// </summary>
    public static readonly ushort TransmitClockDpll = 0x0400;

    /// <summary>
    /// The <c>HDLC_FLAG_TXC_BRG</c> bit of <see cref="SynclinkParams.Flags"/>: the transmit clock comes from the internal baud rate generator.
    /// </summary>
    public static readonly ushort TransmitClockBrg = 0x0800;

    /// <summary>
    /// The <c>HDLC_FLAG_DPLL_DIV8</c> bit of <see cref="SynclinkParams.Flags"/>: the digital phase locked loop divides by 8.
    /// </summary>
    public static readonly ushort DpllDivisor8 = 0x1000;

    /// <summary>
    /// The <c>HDLC_FLAG_DPLL_DIV16</c> bit of <see cref="SynclinkParams.Flags"/>: the digital phase locked loop divides by 16.
    /// </summary>
    public static readonly ushort DpllDivisor16 = 0x2000;

    /// <summary>
    /// The <c>HDLC_FLAG_UNDERRUN_ABORT15</c> bit of <see cref="SynclinkParams.Flags"/>: a transmit underrun sends an abort sequence of fifteen one bits.
    /// </summary>
    public static readonly ushort UnderrunAbort15 = 0x0001;

    /// <summary>
    /// The <c>HDLC_FLAG_UNDERRUN_FLAG</c> bit of <see cref="SynclinkParams.Flags"/>: a transmit underrun sends a closing flag.
    /// </summary>
    public static readonly ushort UnderrunFlag = 0x0002;

    /// <summary>
    /// The <c>HDLC_FLAG_UNDERRUN_CRC</c> bit of <see cref="SynclinkParams.Flags"/>: a transmit underrun sends a deliberately invalid frame check sequence.
    /// </summary>
    public static readonly ushort UnderrunBadCrc = 0x0004;

    /// <summary>
    /// The <c>N_HDLC</c> tty line discipline number, selecting frame-oriented processing of the device.
    /// </summary>
    public static readonly int LineDisciplineHdlc = 13;

    /// <summary>
    /// The <c>N_TTY</c> line discipline, the byte oriented default used for every protocol except HDLC.
    /// </summary>
    public static readonly int LineDisciplineTty = 0;

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
    public static readonly nuint SetLineDiscipline = 0x5423;

    /// <summary>
    /// The <c>MGSL_IOCSPARAMS</c> request that sets the port's <see cref="SynclinkParams"/>.
    /// </summary>
    public static readonly nuint SetParams = Iow(0, Marshal.SizeOf<SynclinkParams>());

    /// <summary>
    /// The <c>MGSL_IOCSTXIDLE</c> request that sets the transmit idle pattern.
    /// </summary>
    public static readonly nuint SetTransmitIdle = Io(2);

    /// <summary>
    /// The <c>MGSL_IOCSIF</c> request that sets the serial interface type.
    /// </summary>
    public static readonly nuint SetInterface = Io(10);

    /// <summary>
    /// The <c>MGSL_INTERFACE_RS232</c> serial interface type for <see cref="SetInterface"/>.
    /// </summary>
    public static readonly int InterfaceRs232 = 1;

    /// <summary>
    /// The <c>MGSL_IOCTXENABLE</c> request that enables or disables the transmitter.
    /// </summary>
    public static readonly nuint EnableTransmitter = Io(4);

    /// <summary>
    /// The <c>MGSL_IOCRXENABLE</c> request that enables or disables the receiver.
    /// </summary>
    public static readonly nuint EnableReceiver = Io(5);

    /// <summary>
    /// The <c>MGSL_IOCWAITEVENT</c> request that blocks until one of the events in its argument has occurred, and, for a modem wire event, also completes at once while the wire is already in the state the event names.
    /// </summary>
    public static readonly nuint WaitEvent = Iowr(8, sizeof(int));

    /// <summary>
    /// The signal number (<c>SIGURG</c>, whose default action is to be ignored) that interrupts a thread blocked in a wait, so the wait returns <c>EINTR</c> instead of being restarted.
    /// </summary>
    public static readonly int InterruptSignal = 23;

    /// <summary>
    /// The <c>TIOCMGET</c> request that reads the modem control and status wires.
    /// </summary>
    public static readonly nuint GetModemSignals = 0x5415;

    /// <summary>
    /// The <c>TIOCMBIS</c> request that turns on the modem output wires named in its argument.
    /// </summary>
    public static readonly nuint RaiseModemSignals = 0x5416;

    /// <summary>
    /// The <c>TIOCMBIC</c> request that turns off the modem output wires named in its argument.
    /// </summary>
    public static readonly nuint LowerModemSignals = 0x5417;

    /// <summary>
    /// The <c>TIOCM_DTR</c> bit: the data terminal ready output.
    /// </summary>
    public static readonly int ModemDataTerminalReady = 0x002;

    /// <summary>
    /// The <c>TIOCM_RTS</c> bit: the request to send output.
    /// </summary>
    public static readonly int ModemRequestToSend = 0x004;

    /// <summary>
    /// The <c>TIOCM_CTS</c> bit: the clear to send input.
    /// </summary>
    public static readonly int ModemClearToSend = 0x020;

    /// <summary>
    /// The <c>TIOCM_CAR</c> bit: the data carrier detect input.
    /// </summary>
    public static readonly int ModemCarrierDetect = 0x040;

    /// <summary>
    /// The <c>TIOCM_DSR</c> bit: the data set ready input.
    /// </summary>
    public static readonly int ModemDataSetReady = 0x100;

    private static nuint Io(int number) =>
        (nuint)(uint)((iocDirectionNone << iocDirectionShift) | (magicNumber << iocTypeShift) | (number << iocNumberShift));

    private static nuint Iowr(int number, int size) =>
        (nuint)(uint)(((iocDirectionRead | iocDirectionWrite) << iocDirectionShift) | (magicNumber << iocTypeShift) | (number << iocNumberShift) | (size << iocSizeShift));

    private static nuint Iow(int number, int size) =>
        (nuint)(uint)((iocDirectionWrite << iocDirectionShift) | (magicNumber << iocTypeShift) | (number << iocNumberShift) | (size << iocSizeShift));
}
