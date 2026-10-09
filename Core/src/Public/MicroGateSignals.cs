namespace BlueHeighliner.MicroGate;

/// <summary>
/// The modem control and status wires of a device: the inputs it reads from the remote station (data carrier detect, data set ready, clear to send), which a station drives to say it is present and ready, and the outputs it drives itself (data terminal ready, request to send). A null modem cable typically crosses the remote station's DTR (data terminal ready) output onto DSR and DCD, and its RTS (request to send) output onto CTS, so a lost connection or powered-off remote station shows up as an input going inactive.
/// </summary>
[Flags]
public enum MicroGateSignals
{
    /// <summary>
    /// No signal.
    /// </summary>
    None = 0,

    /// <summary>
    /// Data carrier detect: the remote station, or the modem in between, reports that it sees the line.
    /// </summary>
    DataCarrierDetect = 1,

    /// <summary>
    /// Data set ready: the remote station, or the modem in between, is powered on and ready.
    /// </summary>
    DataSetReady = 2,

    /// <summary>
    /// Clear to send: the remote station is ready to accept data.
    /// </summary>
    ClearToSend = 4,

    /// <summary>
    /// Data terminal ready: this station's output that says it is present, which a device raises when it opens and drops when it closes. A remote station sees it as its own data carrier detect and data set ready when the cable crosses it.
    /// </summary>
    DataTerminalReady = 8,

    /// <summary>
    /// Request to send: this station's output that says it wants to send, which a device raises when it opens. A remote station sees it as its own clear to send when the cable crosses it.
    /// </summary>
    RequestToSend = 16,
}

/// <summary>
/// Describes a <see cref="MicroGateSignals"/> value.
/// </summary>
internal static class MicroGateSignalsExtensions
{
    private static readonly int dsrActive = 0x0001;
    private static readonly int dsrInactive = 0x0002;
    private static readonly int ctsActive = 0x0004;
    private static readonly int ctsInactive = 0x0008;
    private static readonly int dcdActive = 0x0010;
    private static readonly int dcdInactive = 0x0020;

    extension(MicroGateSignals signals)
    {
        /// <summary>
        /// Gets the driver event mask (<c>MgslEvent_*</c>, the same on Linux and Windows) that completes when any input wire (carrier detect, data set ready, clear to send) leaves the state it has in this value: the inactive event for each wire that is active and the active event for each that is not. A wait for the opposite state completes at once if the wire has already changed, so no change between reading the wires and waiting is missed.
        /// </summary>
        public int FlipEvents =>
            (signals.HasFlag(MicroGateSignals.DataCarrierDetect) ? dcdInactive : dcdActive)
            | (signals.HasFlag(MicroGateSignals.DataSetReady) ? dsrInactive : dsrActive)
            | (signals.HasFlag(MicroGateSignals.ClearToSend) ? ctsInactive : ctsActive);
    }
}
