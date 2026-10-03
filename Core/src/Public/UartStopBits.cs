namespace BlueHeighliner.MicroGate;

/// <summary>
/// The number of stop bits transmitted after every character of an asynchronous serial link. A receiver always recognizes either.
/// </summary>
public enum UartStopBits
{
    /// <summary>
    /// One stop bit.
    /// </summary>
    One,

    /// <summary>
    /// Two stop bits.
    /// </summary>
    Two,
}
