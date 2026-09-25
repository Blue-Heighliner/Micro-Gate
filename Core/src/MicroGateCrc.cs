namespace BlueHeighliner.MicroGate;

/// <summary>
/// The frame check sequence a MicroGate device appends to transmitted frames and verifies on received frames. Frames failing the check are discarded by the driver.
/// </summary>
public enum MicroGateCrc
{
    /// <summary>
    /// No frame check sequence.
    /// </summary>
    None = 0,

    /// <summary>
    /// 16-bit CRC-CCITT.
    /// </summary>
    Crc16Ccitt = 1,

    /// <summary>
    /// 32-bit CRC-CCITT.
    /// </summary>
    Crc32Ccitt = 2,
}
