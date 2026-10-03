namespace BlueHeighliner.MicroGate;

/// <summary>
/// Opens and configures MicroGate SyncLink devices for one operating system.
/// </summary>
internal interface IMicroGateDeviceOpener
{
    /// <summary>
    /// Opens the device for the specified port and applies the physical layer configuration from <paramref name="options"/>.
    /// </summary>
    /// <param name="portName">The name of the port to open.</param>
    /// <param name="options">The device configuration to apply.</param>
    /// <returns>The opened and configured device.</returns>
    /// <exception cref="IOException">The device could not be opened.</exception>
    IMicroGateDevice Open(string portName, HdlcPeerOptions options);
}
