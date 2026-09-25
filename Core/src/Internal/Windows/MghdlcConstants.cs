namespace BlueHeighliner.MicroGate.Windows;

/// <summary>
/// Constants mirrored from the SyncLink Windows driver header <c>Mghdlc.h</c>.
/// </summary>
internal static class MghdlcConstants
{
    /// <summary>
    /// The maximum number of ports <c>MgslEnumeratePorts</c> can report.
    /// </summary>
    public static readonly uint MaxPorts = 200;

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
    /// The <c>MgslOpenByName</c> success status.
    /// </summary>
    public static readonly uint Success = 0;
}
