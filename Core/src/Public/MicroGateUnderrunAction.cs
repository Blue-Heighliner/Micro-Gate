namespace BlueHeighliner.MicroGate;

/// <summary>
/// What a MicroGate device transmits when the transmitter underruns (runs out of data to send before the frame is complete).
/// </summary>
public enum MicroGateUnderrunAction
{
    /// <summary>
    /// An abort sequence of seven consecutive one bits.
    /// </summary>
    Abort7,

    /// <summary>
    /// An abort sequence of fifteen consecutive one bits.
    /// </summary>
    Abort15,

    /// <summary>
    /// A closing HDLC flag, ending the frame as if it were complete.
    /// </summary>
    Flag,

    /// <summary>
    /// A deliberately invalid frame check sequence, ending the frame so the receiver's check fails and discards it.
    /// </summary>
    InvalidFrameCheckSequence,
}
