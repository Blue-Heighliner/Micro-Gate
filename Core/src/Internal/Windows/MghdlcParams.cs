namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Mirrors the native <c>struct _MGSL_PARAMS</c> layout defined by <c>Mghdlc.h</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MghdlcParams
{
    /// <summary>
    /// The port mode, one of the <c>MGSL_MODE_*</c> constants in <see cref="MghdlcConstants"/>.
    /// </summary>
    public uint Mode;

    /// <summary>
    /// Non-zero to enable internal loopback mode.
    /// </summary>
    public byte Loopback;

    /// <summary>
    /// A bitwise combination of the <c>HDLC_FLAG_*</c> constants in <see cref="MghdlcConstants"/>.
    /// </summary>
    public ushort Flags;

    /// <summary>
    /// The line encoding, one of the <c>HDLC_ENCODING_*</c> constants in <see cref="MghdlcConstants"/>.
    /// </summary>
    public byte Encoding;

    /// <summary>
    /// The externally generated clock speed, in bits per second.
    /// </summary>
    public uint ClockSpeed;

    /// <summary>
    /// The receive HDLC address filter; 0xFF disables hardware filtering.
    /// </summary>
    public byte Addr;

    /// <summary>
    /// The CRC mode, one of the <c>HDLC_CRC_*</c> constants in <see cref="MghdlcConstants"/>.
    /// </summary>
    public ushort CrcType;

    /// <summary>
    /// The transmitted preamble length.
    /// </summary>
    public byte PreambleLength;

    /// <summary>
    /// The transmitted preamble pattern.
    /// </summary>
    public byte PreamblePattern;

    /// <summary>
    /// The asynchronous mode data rate, in bits per second. Unused in HDLC mode.
    /// </summary>
    public uint DataRate;

    /// <summary>
    /// The asynchronous mode data bit count. Unused in HDLC mode.
    /// </summary>
    public byte DataBits;

    /// <summary>
    /// The asynchronous mode stop bit count. Unused in HDLC mode.
    /// </summary>
    public byte StopBits;

    /// <summary>
    /// The asynchronous mode parity. Unused in HDLC mode.
    /// </summary>
    public byte Parity;
}
