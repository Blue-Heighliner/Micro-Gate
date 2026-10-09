namespace BlueHeighliner.MicroGate;

/// <summary>
/// The raw, frame-oriented transport of an opened and configured MicroGate SyncLink device. One read or write moves exactly one HDLC frame, exactly as the bytes appear between the flags, with no address or control field handling.
/// </summary>
internal interface IMicroGateDevice : IDisposable
{
    /// <summary>
    /// Blocks until a frame is received and copies it into <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">The buffer to receive the frame.</param>
    /// <returns>The number of bytes received, or zero or less once the device has been closed or its receiver disabled.</returns>
    int Read(byte[] buffer);

    /// <summary>
    /// Transmits a frame and returns once it has been handed to the device.
    /// </summary>
    /// <param name="frame">The raw frame bytes to transmit.</param>
    /// <exception cref="IOException">The frame could not be written to the device.</exception>
    void Write(ReadOnlyMemory<byte> frame);

    /// <summary>
    /// Enables the transmitter, which a device opens without so that it stays passive on the line until it first has something to send.
    /// </summary>
    /// <exception cref="IOException">The transmitter could not be enabled.</exception>
    void EnableTransmitter();

    /// <summary>
    /// Disables the receiver, which cancels any blocked <see cref="Read"/>.
    /// </summary>
    void DisableReceiver();

    /// <summary>
    /// Reads the modem status wires.
    /// </summary>
    /// <returns>The wires that are active, or <see langword="null"/> if the device cannot report them.</returns>
    MicroGateSignals? ReadSignals();

    /// <summary>
    /// Blocks until an input wire (data carrier detect, data set ready or clear to send) differs from <paramref name="current"/>, then reads the wires. It waits on the driver's events rather than polling, and a change that happened before the call is not missed.
    /// </summary>
    /// <param name="current">The wires as they were last read.</param>
    /// <returns>The wires after the change, or <see langword="null"/> once <see cref="CancelSignalWait"/> has been called or the device cannot report them.</returns>
    MicroGateSignals? WaitSignals(MicroGateSignals current);

    /// <summary>
    /// Wakes a blocked <see cref="WaitSignals"/>, which then returns <see langword="null"/>, and makes later calls return it at once. It may be called repeatedly, from any thread.
    /// </summary>
    void CancelSignalWait();

    /// <summary>
    /// Disables the transmitter, which cancels any blocked <see cref="Write"/> and discards data not yet sent.
    /// </summary>
    void DisableTransmitter();
}
