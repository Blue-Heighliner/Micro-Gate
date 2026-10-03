namespace BlueHeighliner.MicroGate;

/// <summary>
/// How the controller uses the MicroGate ports it opens, and which protocol it speaks on them: HDLC frames or a plain asynchronous (UART) byte stream.
/// </summary>
internal enum ControllerMode
{
    /// <summary>
    /// Opens one port and forms an HDLC connection with a remote peer, so data can be sent and received.
    /// </summary>
    HdlcPeer,

    /// <summary>
    /// Opens one port and only observes the HDLC frames received on it, forming no connection and sending nothing.
    /// </summary>
    HdlcMonitor,

    /// <summary>
    /// Opens two ports and relays every HDLC frame received on each to the other, as if the controller were not between them, logging the frames in both directions.
    /// </summary>
    HdlcPassthrough,

    /// <summary>
    /// Opens one port as an asynchronous serial port, so bytes can be sent and received.
    /// </summary>
    UartPeer,

    /// <summary>
    /// Opens one port as an asynchronous serial port and only logs the bytes received on it, sending nothing.
    /// </summary>
    UartMonitor,

    /// <summary>
    /// Opens two ports as asynchronous serial ports and relays every byte received on each to the other, logging the bytes in both directions.
    /// </summary>
    UartPassthrough,
}

/// <summary>
/// Describes a <see cref="ControllerMode"/>.
/// </summary>
internal static class ControllerModeExtensions
{
    extension(ControllerMode mode)
    {
        /// <summary>
        /// Gets the name shown for the mode, such as <c>HDLC Peer</c>.
        /// </summary>
        public string Title => mode switch
        {
            ControllerMode.HdlcPeer => "HDLC Peer",
            ControllerMode.HdlcMonitor => "HDLC Monitor",
            ControllerMode.HdlcPassthrough => "HDLC Passthrough",
            ControllerMode.UartPeer => "UART Peer",
            ControllerMode.UartMonitor => "UART Monitor",
            ControllerMode.UartPassthrough => "UART Passthrough",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        /// <summary>
        /// Gets a value indicating whether the mode uses asynchronous serial ports rather than HDLC.
        /// </summary>
        public bool IsUart => mode is ControllerMode.UartPeer or ControllerMode.UartMonitor or ControllerMode.UartPassthrough;

        /// <summary>
        /// Gets a value indicating whether the mode relays between two ports.
        /// </summary>
        public bool IsPassthrough => mode is ControllerMode.HdlcPassthrough or ControllerMode.UartPassthrough;

        /// <summary>
        /// Gets a value indicating whether the mode only observes one port and sends nothing.
        /// </summary>
        public bool IsMonitor => mode is ControllerMode.HdlcMonitor or ControllerMode.UartMonitor;

        /// <summary>
        /// Gets a value indicating whether the mode lets the user send data on one port.
        /// </summary>
        public bool IsPeer => mode is ControllerMode.HdlcPeer or ControllerMode.UartPeer;
    }
}
