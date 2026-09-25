namespace BlueHeighliner.MicroGate;

public sealed class MicroGatePortSourceIntegrationTests
{
    [Fact]
    public async Task GetPorts_OnLinux_ReturnsOnlyExistingDeviceNodes()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IReadOnlyList<string> ports = await new MicroGatePortSource().GetPorts();

        Assert.All(ports, port => Assert.True(File.Exists(Path.Combine("/dev", port))));
    }
}
