namespace BlueHeighliner.MicroGate;

/// <summary>
/// Configures a connection opened by <see cref="IMicroGateConnector"/>. Every setting has a default, so only the settings that differ need to be specified.
/// </summary>
public sealed record MicroGateConnectionOptions
{
    /// <summary>
    /// Gets the line encoding of the MicroGate device. Defaults to <see cref="MicroGateEncoding.Nrz"/>.
    /// </summary>
    public MicroGateEncoding Encoding { get; init; } = MicroGateEncoding.Nrz;

    /// <summary>
    /// Gets the frame check sequence of the MicroGate device. Defaults to <see cref="MicroGateCrc.Crc16Ccitt"/>.
    /// </summary>
    public MicroGateCrc Crc { get; init; } = MicroGateCrc.Crc16Ccitt;

    /// <summary>
    /// Gets the pattern the MicroGate device transmits between frames. Defaults to <see cref="MicroGateIdlePattern.Flags"/>.
    /// </summary>
    public MicroGateIdlePattern IdlePattern { get; init; } = MicroGateIdlePattern.Flags;

    /// <summary>
    /// Gets the address the device's hardware receive filter accepts, or <see langword="null"/> to disable hardware filtering (the default), leaving address filtering to the HDLC layer.
    /// </summary>
    public byte? HardwareAddressFilter { get; init; }

    /// <summary>
    /// Gets the HDLC address byte this station sends in every frame, and expects to see in every frame it accepts from the peer station. Defaults to <c>0xFF</c>.
    /// </summary>
    public byte Address { get; init; } = 0xFF;

    /// <summary>
    /// Gets a value indicating whether the poll/final bit is disabled, so that it is always zero.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, the control byte of every HDLC frame sent leaves the poll/final bit at 0, regardless of the frame's role or the poll/final bit of the frame being answered.
    /// </remarks>
    public bool DisablePollFinalBit { get; init; }
}
