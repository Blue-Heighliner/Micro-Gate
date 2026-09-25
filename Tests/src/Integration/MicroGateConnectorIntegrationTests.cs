namespace BlueHeighliner.MicroGate;

public sealed class MicroGateConnectorIntegrationTests : IDisposable
{
    private readonly string file = Path.GetTempFileName();

    public void Dispose() => File.Delete(file);

    [Fact]
    public async Task Connect_ToMissingDevice_ThrowsIoException()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        await Assert.ThrowsAsync<IOException>(async () => await new MicroGateConnector().Connect("microgate-does-not-exist"));
    }

    [Fact]
    public async Task Connect_ToUnresponsiveDevice_SendsSabmThenCancels()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using CancellationTokenSource cancellation = new();
        MicroGateConnectionOptions options = new() { Address = 0x05 };

        Task<IMicroGateConnection> pending = new MicroGateConnector().Connect(file, options, cancellation.Token).AsTask();
        using CancellationTokenSource giveUp = new(TimeSpan.FromSeconds(10));
        while (new FileInfo(file).Length < 2)
        {
            await Task.Delay(10, giveUp.Token);
        }

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(new byte[] { 0x05, 0x3F }, await File.ReadAllBytesAsync(file));
    }

    [Fact]
    public async Task Connect_WithPollFinalDisabled_SendsSabmWithoutPollBit()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using CancellationTokenSource cancellation = new();
        MicroGateConnectionOptions options = new() { Address = 0x05, DisablePollFinalBit = true };

        Task<IMicroGateConnection> pending = new MicroGateConnector().Connect(file, options, cancellation.Token).AsTask();
        using CancellationTokenSource giveUp = new(TimeSpan.FromSeconds(10));
        while (new FileInfo(file).Length < 2)
        {
            await Task.Delay(10, giveUp.Token);
        }

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(new byte[] { 0x05, 0x2F }, await File.ReadAllBytesAsync(file));
    }
}
