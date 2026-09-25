namespace BlueHeighliner.MicroGate.Linux;

/// <summary>
/// Mirrors the native <c>struct _MGSL_PARAMS</c> layout defined by <c>synclink.h</c> on 64-bit Linux, where the C <c>unsigned long</c> fields are 8 bytes wide.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SynclinkParams
{
    /// <summary>
    /// The port mode, one of the <c>MGSL_MODE_*</c> constants in <see cref="SynclinkConstants"/>.
    /// </summary>
    public nuint Mode;

    /// <summary>
    /// Non-zero to enable internal loopback mode.
    /// </summary>
    public byte Loopback;

    /// <summary>
    /// A bitwise combination of the <c>HDLC_FLAG_*</c> constants in <see cref="SynclinkConstants"/>.
    /// </summary>
    public ushort Flags;

    /// <summary>
    /// The line encoding, one of the <c>HDLC_ENCODING_*</c> constants in <see cref="SynclinkConstants"/>.
    /// </summary>
    public byte Encoding;

    /// <summary>
    /// The externally generated clock speed, in bits per second.
    /// </summary>
    public nuint ClockSpeed;

    /// <summary>
    /// The receive HDLC address filter; 0xFF disables hardware filtering.
    /// </summary>
    public byte AddressFilter;

    /// <summary>
    /// The CRC mode, one of the <c>HDLC_CRC_*</c> constants in <see cref="SynclinkConstants"/>.
    /// </summary>
    public ushort CrcType;

    /// <summary>
    /// The transmitted preamble length, one of the <c>HDLC_PREAMBLE_LENGTH_*</c> constants in <see cref="SynclinkConstants"/>.
    /// </summary>
    public byte PreambleLength;

    /// <summary>
    /// The transmitted preamble pattern, one of the <c>HDLC_PREAMBLE_PATTERN_*</c> constants in <see cref="SynclinkConstants"/>.
    /// </summary>
    public byte Preamble;

    /// <summary>
    /// The asynchronous mode data rate, in bits per second. Unused in HDLC mode.
    /// </summary>
    public nuint DataRate;

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
