namespace BlueHeighliner.MicroGate;

/// <summary>
/// Creates <see cref="IMicroGateMonitor"/>s. A monitor can be started only once, so anything that needs a new one, such as a service resolved from a dependency injection container, takes this factory and creates a monitor for each one instead of holding a single monitor.
/// </summary>
public interface IMicroGateMonitorFactory
{
    /// <summary>
    /// Creates a new, idle monitor.
    /// </summary>
    /// <returns>The new monitor, which the caller owns and must dispose.</returns>
    IMicroGateMonitor Create();
}

/// <summary>
/// <inheritdoc cref="IMicroGateMonitorFactory" />
/// </summary>
public sealed class MicroGateMonitorFactory : IMicroGateMonitorFactory
{
    /// <inheritdoc />
    public IMicroGateMonitor Create() => new MicroGateMonitor();
}
