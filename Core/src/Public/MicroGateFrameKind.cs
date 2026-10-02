namespace BlueHeighliner.MicroGate;

/// <summary>
/// Identifies the kind of a frame received on the device and reported by <see cref="IMicroGatePeer.Monitored"/>, per the HDLC control field encoding described at
/// https://en.wikipedia.org/wiki/High-Level_Data_Link_Control. Covers both information (data) frames and the unnumbered and supervisory frames MicroGate stations use to manage the asynchronous balanced mode connection.
/// </summary>
public enum MicroGateFrameKind
{
    /// <summary>
    /// An information (I) frame, carrying a sequenced payload.
    /// </summary>
    Information,

    /// <summary>
    /// A receive ready (RR) supervisory frame, acknowledging received information frames.
    /// </summary>
    ReceiveReady,

    /// <summary>
    /// A receive not ready (RNR) supervisory frame, acknowledging received information frames while signaling an inability to accept more.
    /// </summary>
    ReceiveNotReady,

    /// <summary>
    /// A reject (REJ) supervisory frame, requesting retransmission of information frames from the acknowledged sequence number.
    /// </summary>
    Reject,

    /// <summary>
    /// A set asynchronous balanced mode (SABM) unnumbered frame, requesting the peer establish an asynchronous balanced mode connection.
    /// </summary>
    SetAsynchronousBalancedMode,

    /// <summary>
    /// A disconnect (DISC) unnumbered frame, requesting the peer terminate the connection.
    /// </summary>
    Disconnect,

    /// <summary>
    /// An unnumbered acknowledge (UA) unnumbered frame, confirming acceptance of a <see cref="SetAsynchronousBalancedMode"/> or <see cref="Disconnect"/> frame.
    /// </summary>
    UnnumberedAcknowledge,

    /// <summary>
    /// A disconnected mode (DM) unnumbered frame, indicating the sender is not connected.
    /// </summary>
    DisconnectedMode,

    /// <summary>
    /// A frame reject (FRMR) unnumbered frame, indicating the sender received a frame it cannot process.
    /// </summary>
    FrameReject,

    /// <summary>
    /// The frame was too short to contain an address and control field, or its control field did not encode a recognized frame kind. <see cref="MicroGateFrame.ErrorMessage"/> describes the problem, and <see cref="MicroGateFrame.Raw"/> still holds the bytes as received.
    /// </summary>
    Malformed,
}
