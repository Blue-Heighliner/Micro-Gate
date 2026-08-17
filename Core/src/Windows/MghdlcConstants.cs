namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Constants mirrored from the SyncLink Windows driver header <c>Mghdlc.h</c>.
/// </summary>
internal static class MghdlcConstants
{
    /// <summary>
    /// The maximum number of ports <c>MgslEnumeratePorts</c> can report.
    /// </summary>
    public const uint MaxPorts = 200;

    /// <summary>
    /// Selects HDLC synchronous mode in <see cref="MghdlcParams.Mode"/>.
    /// </summary>
    public const uint ModeHdlc = 2;

    /// <summary>
    /// Selects NRZ line encoding in <see cref="MghdlcParams.Encoding"/>.
    /// </summary>
    public const byte EncodingNrz = 0;

    /// <summary>
    /// Selects CRC-16-CCITT in <see cref="MghdlcParams.CrcType"/>.
    /// </summary>
    public const ushort Crc16Ccitt = 1;

    /// <summary>
    /// Disables the hardware receive HDLC address filter in <see cref="MghdlcParams.Addr"/>.
    /// </summary>
    public const byte AddressFilterDisabled = 0xFF;

    /// <summary>
    /// Selects the flag byte (0x7E) as the idle pattern transmitted between frames, for use with <c>MgslSetIdleMode</c>.
    /// </summary>
    public const uint TransmitIdleFlags = 0;

    /// <summary>
    /// The <c>BOOL</c> value that enables the transmitter or receiver, for use with <c>MgslEnableTransmitter</c>/<c>MgslEnableReceiver</c>.
    /// </summary>
    public const uint Enabled = 1;

    /// <summary>
    /// The <c>BOOL</c> value that disables the transmitter or receiver, canceling any blocked read or write, for use with <c>MgslEnableTransmitter</c>/<c>MgslEnableReceiver</c>.
    /// </summary>
    public const uint Disabled = 0;

    /// <summary>
    /// The <c>MgslOpenByName</c> success status.
    /// </summary>
    public const uint Success = 0;
}
