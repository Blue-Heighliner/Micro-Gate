namespace BlueHeighliner.MicroGate;

public sealed class HdlcPeerIntegrationTests : IDisposable
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

        IHdlcPeer peer = new HdlcPeer();

        await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect("microgate-does-not-exist", 0x06, 0x05));

        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
    }

    [Fact]
    public async Task Start_OnDeviceThatNeverAnswers_SendsSabmThenFailsWhenDeviceEnds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IHdlcPeer peer = CreateTolerantPeer();

        await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect(file, 0x06, 0x05, new HdlcPeerOptions()));

        Assert.Equal(new byte[] { 0xFF, 0x2F }, await File.ReadAllBytesAsync(file));
    }

    [Fact]
    public async Task Start_WithoutRetryOnDeviceThatEnds_FailsWithoutWriting()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IHdlcPeer peer = CreateTolerantPeer();
        TestObserver<HdlcPeerState> states = new();
        peer.StateChanged.Subscribe(states);

        await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect(file, 0x06, 0x05, new HdlcPeerOptions { RetryInterval = null }));

        Assert.Empty(await File.ReadAllBytesAsync(file));
        Assert.Equal([HdlcPeerState.Ready, HdlcPeerState.Connecting, HdlcPeerState.Disconnected], states.Seen);
    }

    [Fact]
    public async Task Start_OnDeviceThatIsNotSyncLink_ThrowsIoExceptionNamingTheDevice()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        IHdlcPeer peer = new HdlcPeer();

        IOException exception = await Assert.ThrowsAsync<IOException>(async () => await peer.StartAndConnect(file, 0x06, 0x05));

        Assert.Contains(file, exception.Message);
        Assert.Contains("SyncLink", exception.Message);
        Assert.Equal(HdlcPeerState.Disconnected, peer.State);
        Assert.Empty(await File.ReadAllBytesAsync(file));
    }

    private HdlcPeer CreateTolerantPeer() => new(new LinuxMicroGateDeviceOpener(new TolerantLinuxNative(new LinuxNative())), new Mock<IMicroGateDeviceOpener>().Object);
}
