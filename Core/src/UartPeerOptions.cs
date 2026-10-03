namespace BlueHeighliner.MicroGate;

/// <summary>
/// Configures a UART peer when it is started with <see cref="IUartPeer.Start"/>. Every setting has a default, so only the settings that differ need to be specified. The first four must match the remote station exactly.
/// </summary>
public sealed record UartPeerOptions
{
    /// <summary>
    /// Gets the data rate in bits per second, which is the rate of every bit on the line, start, data, parity, and stop bits alike. Must match the remote station. Defaults to 9600.
    /// </summary>
    public int BaudRate { get; init; } = 9600;

    /// <summary>
    /// Gets the number of data bits in each character, from 5 to 8. Must match the remote station. Defaults to 8.
    /// </summary>
    public int DataBits { get; init; } = 8;

    /// <summary>
    /// Gets the number of stop bits transmitted after each character. Defaults to <see cref="UartStopBits.One"/>. A receiver accepts either, so it need not match.
    /// </summary>
    public UartStopBits StopBits { get; init; } = UartStopBits.One;

    /// <summary>
    /// Gets the parity bit added to each character and checked on each received one; characters received with a parity error are discarded by the device. Must match the remote station. Defaults to <see cref="UartParity.None"/>.
    /// </summary>
    public UartParity Parity { get; init; } = UartParity.None;

    /// <summary>
    /// Gets a value indicating whether the device's internal loopback mode is enabled, looping transmitted data back to the receiver internally instead of sending it on the line, for self-test without a remote station. Defaults to <see langword="false"/>.
    /// </summary>
    public bool Loopback { get; init; }
}
