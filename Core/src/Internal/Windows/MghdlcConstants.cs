namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Constants mirrored from the SyncLink Windows driver header <c>Mghdlc.h</c>.
/// </summary>
internal static class MghdlcConstants
{
    /// <summary>
    /// Selects asynchronous mode in <see cref="MghdlcParams.Mode"/>.
    /// </summary>
    public static readonly uint ModeAsync = 1;

    /// <summary>
    /// Selects HDLC synchronous mode in <see cref="MghdlcParams.Mode"/>.
    /// </summary>
    public static readonly uint ModeHdlc = 2;

    /// <summary>
    /// The hardware receive HDLC address filter value in <see cref="MghdlcParams.Addr"/> that disables filtering.
    /// </summary>
    public static readonly byte AddressFilterDisabled = 0xFF;

    /// <summary>
    /// The <c>BOOL</c> value that enables the transmitter or receiver, for use with <c>MgslEnableTransmitter</c>/<c>MgslEnableReceiver</c>.
    /// </summary>
    public static readonly uint Enabled = 1;

    /// <summary>
    /// The <c>BOOL</c> value that disables the transmitter or receiver, canceling any blocked read or write, for use with <c>MgslEnableTransmitter</c>/<c>MgslEnableReceiver</c>.
    /// </summary>
    public static readonly uint Disabled = 0;

    /// <summary>
    /// The <c>MGSL_OPT_INTERFACE</c> option id for <c>MgslSetOption</c>, which selects the serial interface type.
    /// </summary>
    public static readonly uint OptionInterface = 6;

    /// <summary>
    /// The <c>MGSL_OPT_RX_ERROR_MASK</c> option id for <c>MgslSetOption</c>, which makes the driver silently discard HDLC frames received with errors. Without it <c>MgslReadWithStatus</c> returns zero for each such frame, which is indistinguishable from a cancelled read.
    /// </summary>
    public static readonly uint OptionReceiveErrorMask = 8;

    /// <summary>
    /// The <c>RxStatus_OK</c> status of <c>MgslReadWithStatus</c>: the read completed normally, so a zero byte count means a frame with no data bytes (a cancelled read reports <c>RxStatus_Cancel</c>).
    /// </summary>
    public static readonly int RxStatusOk = 0;

    /// <summary>
    /// The value that turns a boolean <c>MgslSetOption</c> option on.
    /// </summary>
    public static readonly uint OptionOn = 1;

    /// <summary>
    /// The <c>MGSL_INTERFACE_RS232</c> serial interface type for <see cref="OptionInterface"/>.
    /// </summary>
    public static readonly uint InterfaceRs232 = 1;

    /// <summary>
    /// The <c>MgslOpenByName</c> success status.
    /// </summary>
    public static readonly uint Success = 0;

    /// <summary>
    /// The Win32 <c>ERROR_DEVICE_IN_USE</c> code, which opening a port returns while another handle to it is open or still being released after a close.
    /// </summary>
    public static readonly uint DeviceInUse = 2404;

    /// <summary>
    /// The <c>HDLC_FLAG_RXC_TXCPIN</c> bit of <see cref="MghdlcParams.Flags"/>: the receive clock comes from the transmit clock (TXC) pin.
    /// </summary>
    public static readonly ushort ReceiveClockOtherPin = 0x8000;

    /// <summary>
    /// The <c>HDLC_FLAG_RXC_DPLL</c> bit of <see cref="MghdlcParams.Flags"/>: the receive clock comes from the digital phase locked loop.
    /// </summary>
    public static readonly ushort ReceiveClockDpll = 0x0100;

    /// <summary>
    /// The <c>HDLC_FLAG_RXC_BRG</c> bit of <see cref="MghdlcParams.Flags"/>: the receive clock comes from the internal baud rate generator.
    /// </summary>
    public static readonly ushort ReceiveClockBrg = 0x0200;

    /// <summary>
    /// The <c>HDLC_FLAG_TXC_RXCPIN</c> bit of <see cref="MghdlcParams.Flags"/>: the transmit clock comes from the receive clock (RXC) pin.
    /// </summary>
    public static readonly ushort TransmitClockOtherPin = 0x0008;

    /// <summary>
    /// The <c>HDLC_FLAG_TXC_DPLL</c> bit of <see cref="MghdlcParams.Flags"/>: the transmit clock comes from the digital phase locked loop.
    /// </summary>
    public static readonly ushort TransmitClockDpll = 0x0400;

    /// <summary>
    /// The <c>HDLC_FLAG_TXC_BRG</c> bit of <see cref="MghdlcParams.Flags"/>: the transmit clock comes from the internal baud rate generator.
    /// </summary>
    public static readonly ushort TransmitClockBrg = 0x0800;

    /// <summary>
    /// The <c>HDLC_FLAG_DPLL_DIV8</c> bit of <see cref="MghdlcParams.Flags"/>: the digital phase locked loop divides by 8.
    /// </summary>
    public static readonly ushort DpllDivisor8 = 0x1000;

    /// <summary>
    /// The <c>HDLC_FLAG_DPLL_DIV16</c> bit of <see cref="MghdlcParams.Flags"/>: the digital phase locked loop divides by 16.
    /// </summary>
    public static readonly ushort DpllDivisor16 = 0x2000;

    /// <summary>
    /// The <c>HDLC_FLAG_UNDERRUN_ABORT15</c> bit of <see cref="MghdlcParams.Flags"/>: a transmit underrun sends an abort sequence of fifteen one bits.
    /// </summary>
    public static readonly ushort UnderrunAbort15 = 0x0001;

    /// <summary>
    /// The <c>HDLC_FLAG_UNDERRUN_FLAG</c> bit of <see cref="MghdlcParams.Flags"/>: a transmit underrun sends a closing flag.
    /// </summary>
    public static readonly ushort UnderrunFlag = 0x0002;

    /// <summary>
    /// The <c>HDLC_FLAG_UNDERRUN_CRC</c> bit of <see cref="MghdlcParams.Flags"/>: a transmit underrun sends a deliberately invalid frame check sequence.
    /// </summary>
    public static readonly ushort UnderrunBadCrc = 0x0004;
}
