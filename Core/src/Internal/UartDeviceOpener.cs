namespace BlueHeighliner.MicroGate;

/// <summary>
/// Opens and configures a MicroGate SyncLink device for asynchronous serial operation on one platform.
/// </summary>
internal interface IUartDeviceOpener
{
    /// <summary>
    /// Opens the device for the specified port and applies the asynchronous settings, with its receiver enabled and its transmitter left disabled.
    /// </summary>
    /// <param name="portName">The port name, in the form the platform uses.</param>
    /// <param name="options">The asynchronous settings to apply.</param>
    /// <returns>The opened device, which reads and writes raw bytes with no frame boundaries.</returns>
    /// <exception cref="IOException">The device could not be opened or configured.</exception>
    IMicroGateDevice Open(string portName, UartPeerOptions options);
}
