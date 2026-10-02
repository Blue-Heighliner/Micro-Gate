namespace BlueHeighliner.MicroGate;

/// <summary>
/// The main window of the MicroGate monitor: pick a port, start monitoring, and watch every frame observed on it, including the SABM, UA, DISC, DM, FRMR, RR, and RNR frames two other stations use to manage their own connection. The monitor never writes to the device.
/// </summary>
internal sealed partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="portSource">The port source used to enumerate available MicroGate devices.</param>
    /// <param name="monitorFactory">The factory that creates a new, idle monitor. A monitor is single use, so one is created for every start.</param>
    public MainWindow(IMicroGatePortSource portSource, IMicroGateMonitorFactory monitorFactory)
    {
        this.portSource = portSource;
        this.monitorFactory = monitorFactory;
        InitializeComponent();

        EncodingComboBox.ItemsSource = Enum.GetValues<MicroGateEncoding>();
        EncodingComboBox.SelectedItem = MicroGateEncoding.Nrz;
        CrcComboBox.ItemsSource = Enum.GetValues<MicroGateCrc>();
        CrcComboBox.SelectedItem = MicroGateCrc.Crc32Ccitt;
        ReceiveClockComboBox.ItemsSource = Enum.GetValues<MicroGateReceiveClockSource>();
        ReceiveClockComboBox.SelectedItem = new MicroGateMonitorOptions().ReceiveClockSource;
        DivisorComboBox.ItemsSource = Enum.GetValues<MicroGatePhaseLockedLoopDivisor>();
        DivisorComboBox.SelectedItem = new MicroGateMonitorOptions().PhaseLockedLoopDivisor;
        ClockSpeedTextBox.Text = new MicroGateMonitorOptions().ClockSpeed.ToString(CultureInfo.InvariantCulture);
        FrameListBox.ItemsSource = log;

        Loaded += async (_, _) => await RefreshPorts();
        Closed += (_, _) => monitor?.Dispose();
    }

    private readonly IMicroGatePortSource portSource;
    private readonly IMicroGateMonitorFactory monitorFactory;
    private readonly ObservableCollection<FrameLogEntry> log = [];
    private readonly List<IDisposable> subscriptions = [];
    private readonly int maxLogEntries = 5000;
    private IMicroGateMonitor? monitor;

    private async Task RefreshPorts()
    {
        try
        {
            IReadOnlyList<string> ports = await portSource.GetPorts();
            PortComboBox.ItemsSource = ports;
            if (ports.Count > 0)
            {
                PortComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Listing ports failed: {ex.Message}");
        }
    }

    private async void RefreshPorts_Click(object? sender, RoutedEventArgs e) => await RefreshPorts();

    private async void StartStop_Click(object? sender, RoutedEventArgs e)
    {
        if (monitor is not null)
        {
            await Stop();
            return;
        }

        if (PortComboBox.SelectedItem is not string portName)
        {
            AppendLog("Select a port first.");
            return;
        }

        if (BuildOptions() is not { } options)
        {
            return;
        }

        IMicroGateMonitor newMonitor = monitorFactory.Create();
        monitor = newMonitor;
        subscriptions.Add(newMonitor.Received.Subscribe(OnReceived));
        subscriptions.Add(newMonitor.StateChanged.Subscribe(state => OnStateChanged(newMonitor, state)));

        StartButton.IsEnabled = false;

        try
        {
            await newMonitor.Start(portName, options);
            PortStatusText.Text = portName;
            AppendLog($"Monitoring {portName}.");
        }
        catch (Exception ex)
        {
            AppendLog($"Start failed: {ex.Message}");
            await Release(newMonitor);
        }

        StartButton.IsEnabled = true;
        UpdateState();
    }

    private async Task Stop()
    {
        if (monitor is null)
        {
            return;
        }

        await Release(monitor);
        AppendLog("Stopped.");
        UpdateState();
    }

    private async Task Release(IMicroGateMonitor released)
    {
        if (monitor != released)
        {
            return;
        }

        monitor = null;
        PortStatusText.Text = "No active port";
        foreach (IDisposable subscription in subscriptions)
        {
            subscription.Dispose();
        }

        subscriptions.Clear();
        await released.DisposeAsync();
    }

    private MicroGateMonitorOptions? BuildOptions()
    {
        byte? addressFilter = null;
        string text = AddressFilterTextBox.Text?.Trim() ?? string.Empty;
        if (text.Length > 0)
        {
            if (!byte.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte parsed))
            {
                AppendLog($"'{text}' is not a valid hex byte (00-FF) for the address filter.");
                return null;
            }

            addressFilter = parsed;
        }

        if (!int.TryParse(ClockSpeedTextBox.Text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int clockSpeed) || clockSpeed < 1)
        {
            AppendLog("Clock speed must be a positive whole number.");
            return null;
        }

        return new MicroGateMonitorOptions
        {
            ReceiveClockSource = ReceiveClockComboBox.SelectedItem is MicroGateReceiveClockSource clockSource ? clockSource : MicroGateReceiveClockSource.OwnPin,
            PhaseLockedLoopDivisor = DivisorComboBox.SelectedItem is MicroGatePhaseLockedLoopDivisor divisor ? divisor : MicroGatePhaseLockedLoopDivisor.DivideBy32,
            ClockSpeed = clockSpeed,
            Encoding = EncodingComboBox.SelectedItem is MicroGateEncoding encoding ? encoding : MicroGateEncoding.Nrz,
            Crc = CrcComboBox.SelectedItem is MicroGateCrc crc ? crc : MicroGateCrc.Crc32Ccitt,
            HardwareAddressFilter = addressFilter,
        };
    }

    private void OnReceived(MicroGateFrame frame)
    {
        FrameLogEntry entry = new(MicroGateFrameFormatter.Format(frame), frame);
        Dispatcher.UIThread.Post(() =>
        {
            AppendEntry(entry);
            UpdateFrameCount();
        });
    }

    private void FrameListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        object? selected = FrameListBox.SelectedItem;
        foreach (FrameLogEntry entry in log)
        {
            entry.IsExpanded = entry == selected && entry.Frame is not null;
        }
    }

    private void OnStateChanged(IMicroGateMonitor source, MicroGateMonitorState state) =>
        Dispatcher.UIThread.Post(async () =>
        {
            if (state == MicroGateMonitorState.Stopped && monitor == source)
            {
                await Release(source);
                AppendLog("Stopped.");
            }

            UpdateState();
        });

    private void UpdateState()
    {
        MicroGateMonitorState state = monitor?.State ?? MicroGateMonitorState.Stopped;
        StartButton.Content = monitor is null ? "Start Monitoring" : "Stop Monitoring";
        StatusText.Text = state.ToString();
    }

    private void UpdateFrameCount()
    {
        int count = log.Count(entry => entry.Frame is not null);
        FrameCountText.Text = count == 1 ? "1 frame" : $"{count} frames";
    }

    private void ClearLog_Click(object? sender, RoutedEventArgs e)
    {
        log.Clear();
        UpdateFrameCount();
    }

    private async void SaveLog_Click(object? sender, RoutedEventArgs e)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            AppendLog("Saving is not supported on this platform.");
            return;
        }

        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save MicroGate Log",
            SuggestedFileName = $"microgate-log-{DateTime.Now:yyyyMMdd-HHmmss}",
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("MicroGate log") { Patterns = ["*.json"] }],
        });

        if (file is null)
        {
            return;
        }

        try
        {
            await using Stream stream = await file.OpenWriteAsync();
            await FrameLogSerializer.Save(log, stream);
            AppendLog($"Saved log to {file.Name}.");
        }
        catch (Exception ex)
        {
            AppendLog($"Save failed: {ex.Message}");
        }
    }

    private async void LoadLog_Click(object? sender, RoutedEventArgs e)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            AppendLog("Loading is not supported on this platform.");
            return;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load MicroGate Log",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("MicroGate log") { Patterns = ["*.json"] }],
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            await using Stream stream = await files[0].OpenReadAsync();
            IReadOnlyList<FrameLogEntry> loaded = await FrameLogSerializer.Load(stream);
            log.Clear();
            foreach (FrameLogEntry entry in loaded)
            {
                AppendEntry(entry);
            }

            UpdateFrameCount();
            AppendLog($"Loaded {loaded.Count} entries from {files[0].Name}.");
        }
        catch (Exception ex)
        {
            AppendLog($"Load failed: {ex.Message}");
        }
    }

    private void AppendLog(string message) => AppendEntry(new FrameLogEntry(message, null));

    private void AppendEntry(FrameLogEntry entry)
    {
        log.Add(entry);
        while (log.Count > maxLogEntries)
        {
            log.RemoveAt(0);
        }
    }
}
