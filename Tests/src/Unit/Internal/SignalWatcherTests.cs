namespace BlueHeighliner.MicroGate;

public sealed class SignalWatcherTests
{
    private readonly Mock<IMicroGateDevice> device = new();
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(10);

    private static Action<MicroGateSignals> Record(List<MicroGateSignals> changes, SemaphoreSlim reported) => signals =>
    {
        lock (changes)
        {
            changes.Add(signals);
        }

        reported.Release();
    };

    [Fact]
    public async Task Watch_ReportsTheFirstReadingAndEachLaterChangeOnceWithoutPolling()
    {
        SignalSource source = new(MicroGateSignals.None);
        source.Attach(device);
        List<MicroGateSignals> changes = [];
        SemaphoreSlim reported = new(0);
        await using SignalWatcher watcher = new(device.Object, MicroGateSignals.None, Record(changes, reported), _ => { });
        Assert.True(await reported.WaitAsync(timeout));

        source.Set(MicroGateSignals.DataSetReady);
        Assert.True(await reported.WaitAsync(timeout));
        source.Set(MicroGateSignals.DataSetReady | MicroGateSignals.ClearToSend);
        Assert.True(await reported.WaitAsync(timeout));

        await Task.Delay(50);
        lock (changes)
        {
            Assert.Equal([MicroGateSignals.None, MicroGateSignals.DataSetReady, MicroGateSignals.DataSetReady | MicroGateSignals.ClearToSend], changes);
        }

        device.Verify(x => x.ReadSignals(), Times.Once);
    }

    [Fact]
    public async Task Watch_WhenAWatchedSignalGoesInactiveAfterAllWereActive_ReportsItLostOnce()
    {
        MicroGateSignals both = MicroGateSignals.DataSetReady | MicroGateSignals.DataCarrierDetect;
        SignalSource source = new(both);
        source.Attach(device);
        TaskCompletionSource<MicroGateSignals> lost = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        await using SignalWatcher watcher = new(device.Object, both, _ => { }, missing =>
        {
            Interlocked.Increment(ref calls);
            lost.TrySetResult(missing);
        });
        await Task.Delay(50);

        source.Set(MicroGateSignals.DataCarrierDetect);

        Assert.Equal(MicroGateSignals.DataSetReady, await lost.Task.WaitAsync(timeout));
        source.Set(MicroGateSignals.None);
        await Task.Delay(50);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Watch_WhenAWatchedSignalWasNeverActive_NeverReportsItLost()
    {
        SignalSource source = new(MicroGateSignals.DataCarrierDetect);
        source.Attach(device);
        int calls = 0;
        await using SignalWatcher watcher = new(device.Object, MicroGateSignals.DataCarrierDetect | MicroGateSignals.DataSetReady, _ => { }, _ => Interlocked.Increment(ref calls));

        source.Set(MicroGateSignals.None);
        await Task.Delay(100);

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Watch_WithNoWatchedSignals_NeverReportsLoss()
    {
        SignalSource source = new(MicroGateSignals.DataSetReady);
        source.Attach(device);
        int calls = 0;
        await using SignalWatcher watcher = new(device.Object, MicroGateSignals.None, _ => { }, _ => Interlocked.Increment(ref calls));

        source.Set(MicroGateSignals.None);
        await Task.Delay(100);

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Watch_DoesNotReportLossForChangesToTheOutputWires()
    {
        MicroGateSignals watched = MicroGateSignals.DataCarrierDetect | MicroGateSignals.DataSetReady;
        SignalSource source = new(watched | MicroGateSignals.DataTerminalReady);
        source.Attach(device);
        int calls = 0;
        await using SignalWatcher watcher = new(device.Object, watched, _ => { }, _ => Interlocked.Increment(ref calls));
        await Task.Delay(50);

        source.Set(watched | MicroGateSignals.RequestToSend);
        await Task.Delay(100);

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Watch_WhenTheDeviceCannotReportSignals_StopsQuietly()
    {
        int calls = 0;
        await using SignalWatcher watcher = new(device.Object, MicroGateSignals.DataSetReady, _ => Interlocked.Increment(ref calls), _ => Interlocked.Increment(ref calls));

        await Task.Delay(100);

        Assert.Equal(0, calls);
        device.Verify(x => x.ReadSignals(), Times.Once);
        device.Verify(x => x.WaitSignals(It.IsAny<MicroGateSignals>()), Times.Never);
    }

    [Fact]
    public async Task Dispose_WakesTheBlockedWaitAndStopsWatching()
    {
        SignalSource source = new(MicroGateSignals.None);
        source.Attach(device);
        SignalWatcher watcher = new(device.Object, MicroGateSignals.None, _ => { }, _ => { });
        await Task.Delay(50);

        await watcher.DisposeAsync().AsTask().WaitAsync(timeout);

        device.Verify(x => x.CancelSignalWait(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Dispose_KeepsCancellingUntilAWakeUpThatCameTooEarlyIsNotLost()
    {
        int cancels = 0;
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        device.Setup(x => x.ReadSignals()).Returns(MicroGateSignals.None);
        device.Setup(x => x.WaitSignals(It.IsAny<MicroGateSignals>())).Returns(() =>
        {
            release.Task.Wait();
            return null;
        });
        device.Setup(x => x.CancelSignalWait()).Callback(() =>
        {
            if (Interlocked.Increment(ref cancels) == 3)
            {
                release.TrySetResult();
            }
        });
        SignalWatcher watcher = new(device.Object, MicroGateSignals.None, _ => { }, _ => { });
        await Task.Delay(50);

        await watcher.DisposeAsync().AsTask().WaitAsync(timeout);

        Assert.True(cancels >= 3);
    }

    [Fact]
    public async Task Watch_WithoutAChangedCallback_StillReportsLoss()
    {
        MicroGateSignals both = MicroGateSignals.DataSetReady | MicroGateSignals.DataCarrierDetect;
        SignalSource source = new(both);
        source.Attach(device);
        TaskCompletionSource<MicroGateSignals> lost = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using SignalWatcher watcher = new(device.Object, both, null, missing => lost.TrySetResult(missing));
        await Task.Delay(50);

        source.Set(MicroGateSignals.DataSetReady);

        Assert.Equal(MicroGateSignals.DataCarrierDetect, await lost.Task.WaitAsync(timeout));
    }
}
