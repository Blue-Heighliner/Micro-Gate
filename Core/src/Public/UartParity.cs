namespace BlueHeighliner.MicroGate;

/// <summary>
/// The parity bit appended to every character of an asynchronous serial link.
/// </summary>
public enum UartParity
{
    /// <summary>
    /// No parity bit is used.
    /// </summary>
    None,

    /// <summary>
    /// A bit is added so that the number of one bits in the character, including the parity bit, is even.
    /// </summary>
    Even,

    /// <summary>
    /// A bit is added so that the number of one bits in the character, including the parity bit, is odd.
    /// </summary>
    Odd,
}
