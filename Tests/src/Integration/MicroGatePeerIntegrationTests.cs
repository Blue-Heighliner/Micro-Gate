namespace BlueHeighliner.MicroGate;

public sealed class MicroGatePeerIntegrationTests : IDisposable
{
    private readonly string file = Path.GetTempFileName();

    public void Dispose() => File.Delete(file);

    [Fact]
    public async Task Start_OnMissingDevice_ThrowsIoException()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IMicroGatePeer peer = new MicroGatePeer();

        await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect("microgate-does-not-exist", 0x06, 0x05));

        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_OnDeviceThatNeverAnswers_SendsSabmThenFailsWhenDeviceEnds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IMicroGatePeer peer = CreateTolerantPeer();

        await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect(file, 0x06, 0x05, new MicroGatePeerOptions()));

        Assert.Equal(new byte[] { 0x05, 0x2F }, await File.ReadAllBytesAsync(file));
    }

    [Fact]
    public async Task Start_WithPollFinalEnabled_SendsSabmWithPollBit()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IMicroGatePeer peer = CreateTolerantPeer();

        await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect(file, 0x06, 0x05, new MicroGatePeerOptions { DisablePollFinalBit = false }));

        Assert.Equal(new byte[] { 0x05, 0x3F }, await File.ReadAllBytesAsync(file));
    }

    [Fact]
    public async Task Start_WithoutRetryOnDeviceThatEnds_FailsWithoutWriting()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IMicroGatePeer peer = CreateTolerantPeer();
        TestObserver<MicroGatePeerState> states = new();
        peer.StateChanged.Subscribe(states);

        await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect(file, 0x06, 0x05, new MicroGatePeerOptions { RetryInterval = null }));

        Assert.Empty(await File.ReadAllBytesAsync(file));
        Assert.Equal([MicroGatePeerState.Ready, MicroGatePeerState.Connecting, MicroGatePeerState.Disconnected], states.Seen);
    }

    [Fact]
    public async Task Start_OnDeviceThatIsNotSyncLink_ThrowsIoExceptionNamingTheDevice()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IMicroGatePeer peer = new MicroGatePeer();

        IOException exception = await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect(file, 0x06, 0x05));

        Assert.Contains(file, exception.Message);
        Assert.Contains("SyncLink", exception.Message);
        Assert.Equal(MicroGatePeerState.Disconnected, peer.State);
        Assert.Empty(await File.ReadAllBytesAsync(file));
    }

    private MicroGatePeer CreateTolerantPeer() => new(new LinuxMicroGateDeviceOpener(new TolerantLinuxNative(new LinuxNative())), new Mock<IMicroGateDeviceOpener>().Object);
}
