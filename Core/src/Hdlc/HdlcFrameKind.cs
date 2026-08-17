namespace BlueHeighliner.MicroGate.Hdlc;

/// <summary>
/// Identifies the kind of an HDLC frame, per the control field encoding described at
/// https://en.wikipedia.org/wiki/High-Level_Data_Link_Control.
/// </summary>
internal enum HdlcFrameKind
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
}
