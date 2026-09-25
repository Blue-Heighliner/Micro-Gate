namespace BlueHeighliner.MicroGate;

public sealed class MicroGatePortSourceCancellationTests
{
    [Fact]
    public async Task GetPorts_WhenAlreadyCanceled_ThrowsWithoutEnumerating()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Mock<ILinuxMicroGatePorts> linuxPorts = new();
        MicroGatePortSource source = new(linuxPorts.Object, new Mock<IWindowsMicroGatePorts>().Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await source.GetPorts(new CancellationToken(true)));

        linuxPorts.VerifyNoOtherCalls();
    }
}
