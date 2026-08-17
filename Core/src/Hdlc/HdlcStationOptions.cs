namespace BlueHeighliner.MicroGate.Hdlc;

/// <summary>
/// Configures an <see cref="IHdlcStateMachine"/>.
/// </summary>
public sealed record HdlcStationOptions
{
    /// <summary>
    /// Gets the HDLC address byte this station sends in every frame, and expects to see in every frame it accepts from the peer station.
    /// </summary>
    public required byte Address { get; init; }

    /// <summary>
    /// Gets a value indicating whether the poll/final bit is disabled.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, the control byte of every frame produced by the state machine leaves the poll/final bit at 0, regardless of the frame's role.
    /// </remarks>
    public bool DisablePollFinalBit { get; init; }
}
