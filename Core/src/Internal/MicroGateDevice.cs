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
    /// Disables the receiver, which cancels any blocked <see cref="Read"/>.
    /// </summary>
    void DisableReceiver();

    /// <summary>
    /// Disables the transmitter, which cancels any blocked <see cref="Write"/> and discards data not yet sent.
    /// </summary>
    void DisableTransmitter();
}
