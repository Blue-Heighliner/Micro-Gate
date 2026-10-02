namespace BlueHeighliner.MicroGate;

/// <summary>
/// The main window of the MicroGate controller, which runs in one of three <see cref="ControllerMode"/>s: as a peer that connects and sends data, as a monitor that only observes frames, or as a passthrough between two ports. Frames and data are shown, and in peer mode edited, as tables of bytes (see <see cref="ByteGrid"/>).
/// </summary>
internal sealed partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="portSource">The port source used to enumerate available MicroGate devices.</param>
    /// <param name="peerFactory">The factory that creates a new, idle peer. A peer is single use, so one is created for every connection.</param>
    /// <param name="describer">The describer of the frames shown in monitor and passthrough mode.</param>
    /// <param name="serializer">The serializer that saves and loads the log.</param>
    public MainWindow(IMicroGatePortSource portSource, IMicroGatePeerFactory peerFactory, IFrameDescriber describer, ILogSerializer serializer)
    {
        this.portSource = portSource;
        this.peerFactory = peerFactory;
        this.describer = describer;
        this.serializer = serializer;
        InitializeComponent();

        MicroGatePeerOptions defaults = new();
        Fill(ModeComboBox, ControllerMode.Peer);
        Fill(EncodingComboBox, defaults.Link.Encoding);
        Fill(CrcComboBox, defaults.Link.Crc);
        Fill(ReceiveClockComboBox, defaults.Link.ReceiveClockSource);
        Fill(TransmitClockComboBox, defaults.Link.TransmitClockSource);
        Fill(DivisorComboBox, defaults.Link.PhaseLockedLoopDivisor);
        Fill(IdlePatternComboBox, defaults.IdlePattern);
        Fill(PreamblePatternComboBox, defaults.PreamblePattern);
        Fill(PreambleLengthComboBox, defaults.PreambleLength);
        Fill(UnderrunComboBox, defaults.UnderrunAction);
        ClockSpeedTextBox.Text = defaults.Link.ClockSpeed.ToString(CultureInfo.InvariantCulture);
        MaxInfoFieldTextBox.Text = defaults.MaxInfoField.ToString(CultureInfo.InvariantCulture);
        TransmitWindowTextBox.Text = defaults.TransmitWindow.ToString(CultureInfo.InvariantCulture);
        RetryIntervalTextBox.Text = FormatSeconds(defaults.RetryInterval);
        RetransmitIntervalTextBox.Text = FormatSeconds(defaults.RetransmitInterval);
        MaxRetransmissionsTextBox.Text = defaults.MaxRetransmissions?.ToString(CultureInfo.InvariantCulture);
        DisablePollFinalCheckBox.IsChecked = defaults.DisablePollFinalBit;
        LoopbackCheckBox.IsChecked = defaults.Loopback;
        MaxInfoFieldTextBox.TextChanged += (_, _) =>
        {
            if (int.TryParse(MaxInfoFieldTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int size) && size is >= 1 and <= 4090)
            {
                SendGrid.MaxCells = size;
            }
        };
        InputModeComboBox.ItemsSource = new[] { "ASCII", "Raw values" };
        InputModeComboBox.SelectedIndex = 0;
        SendGrid.Cells = [];
        SendGrid.MaxCells = defaults.MaxInfoField;
        SendGrid.Edited += (_, _) => UpdateSendCount();
        SendGrid.SubmitRequested += async (_, _) => await Send();
        LogListBox.ItemsSource = log;

        UpdateMode();

        Loaded += async (_, _) => await RefreshPorts();
        Closed += (_, _) =>
        {
            foreach (IMicroGatePeer open in peers)
            {
                open.Dispose();
            }
        };
    }

    private readonly IMicroGatePortSource portSource;
    private readonly IMicroGatePeerFactory peerFactory;
    private readonly IFrameDescriber describer;
    private readonly ILogSerializer serializer;
    private readonly List<IMicroGatePeer> peers = [];
    private readonly List<Channel<byte[]>> relays = [];
    private readonly ObservableCollection<LogEntry> log = [];
    private readonly List<IDisposable> subscriptions = [];
    private readonly int maxLogEntries = 5000;
    private ControllerMode mode = ControllerMode.Peer;
    private CancellationTokenSource? connectCancellation;
    private int firstCount;
    private int secondCount;

    private async Task RefreshPorts()
    {
        try
        {
            IReadOnlyList<string> ports = await portSource.GetPorts();
            PortComboBox.ItemsSource = ports;
            PortBComboBox.ItemsSource = ports;
            if (ports.Count > 0)
            {
                PortComboBox.SelectedIndex = 0;
                PortBComboBox.SelectedIndex = ports.Count > 1 ? 1 : 0;
            }
        }
        catch (Exception ex)
        {
            AppendMessage($"Listing ports failed: {ex.Message}");
        }
    }

    private async void RefreshPorts_Click(object? sender, RoutedEventArgs e) => await RefreshPorts();

    private void Mode_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModeComboBox.SelectedItem is ControllerMode selected && selected != mode)
        {
            mode = selected;
            UpdateMode();
        }
    }

    private async void Connect_Click(object? sender, RoutedEventArgs e)
    {
        if (peers.Count > 0)
        {
            await Release();
            AppendMessage("Disconnected.");
            UpdateState();
            return;
        }

        if (PortComboBox.SelectedItem is not string portName)
        {
            AppendMessage("Select a port first.");
            return;
        }

        if (BuildOptions() is not { } options)
        {
            return;
        }

        firstCount = 0;
        secondCount = 0;
        UpdateCounts();

        switch (mode)
        {
            case ControllerMode.Peer:
                await ConnectPeer(portName, options);
                break;
            case ControllerMode.Monitor:
                await ConnectMonitor(portName, options);
                break;
            default:
                await ConnectPassthrough(portName, options);
                break;
        }

        UpdateState();
    }

    private async Task ConnectPeer(string portName, MicroGatePeerOptions options)
    {
        if (!byte.TryParse(AddressTextBox.Text?.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte address)
            || !byte.TryParse(RemoteAddressTextBox.Text?.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte remoteAddress))
        {
            AppendMessage("Both addresses must be hex bytes (00-FF).");
            return;
        }

        if (address == remoteAddress)
        {
            AppendMessage("The local and remote addresses must differ.");
            return;
        }

        IMicroGatePeer newPeer = Open(portName, string.Empty);
        newPeer.Receiver = OnReceived;
        CancellationToken cancellation = connectCancellation!.Token;
        AppendMessage($"Connecting on {portName} as {address:X2} to {remoteAddress:X2}...");
        UpdateState();

        try
        {
            await newPeer.Start(portName, options, cancellation);
            await newPeer.Connect(address, remoteAddress, cancellation);
            AppendMessage("Connected.");
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or ObjectDisposedException))
        {
            AppendMessage($"Connect failed: {ex.Message}");
            await Release();
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
        }
    }

    private async Task ConnectMonitor(string portName, MicroGatePeerOptions options)
    {
        IMicroGatePeer newPeer = Open(portName, string.Empty);
        subscriptions.Add(newPeer.Monitored.Subscribe(frame => OnFrame(frame, "RX", true)));
        AppendMessage($"Monitoring {portName}...");

        if (await StartAll([(newPeer, portName)], options with { EnableMonitor = true }))
        {
            AppendMessage("Monitoring.");
        }
    }

    private async Task ConnectPassthrough(string portName, MicroGatePeerOptions options)
    {
        if (PortBComboBox.SelectedItem is not string portNameB || portNameB == portName)
        {
            AppendMessage("Select two different ports.");
            return;
        }

        IMicroGatePeer first = Open(portName, "A: ");
        IMicroGatePeer second = Open(portNameB, "B: ");
        Channel<byte[]> firstToSecond = Channel.CreateUnbounded<byte[]>();
        Channel<byte[]> secondToFirst = Channel.CreateUnbounded<byte[]>();
        relays.Add(firstToSecond);
        relays.Add(secondToFirst);
        subscriptions.Add(first.Monitored.Subscribe(frame => OnRelayed(frame, "A > B", true, firstToSecond)));
        subscriptions.Add(second.Monitored.Subscribe(frame => OnRelayed(frame, "B > A", false, secondToFirst)));
        _ = Relay(firstToSecond.Reader, second, "A > B");
        _ = Relay(secondToFirst.Reader, first, "B > A");
        AppendMessage($"Passing through between {portName} (A) and {portNameB} (B)...");

        if (await StartAll([(first, portName), (second, portNameB)], options with { EnableMonitor = true }))
        {
            AppendMessage("Passing through.");
        }
    }

    private async Task<bool> StartAll(IReadOnlyList<(IMicroGatePeer Peer, string PortName)> targets, MicroGatePeerOptions options)
    {
        UpdateState();
        try
        {
            await Task.WhenAll(targets.Select(target => target.Peer.Start(target.PortName, options, connectCancellation!.Token).AsTask()));
            return true;
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or ObjectDisposedException))
        {
            AppendMessage($"Start failed: {ex.Message}");
            await Release();
            return false;
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return false;
        }
    }

    private IMicroGatePeer Open(string portName, string label)
    {
        IMicroGatePeer opened = peerFactory.Create();
        peers.Add(opened);
        connectCancellation ??= new CancellationTokenSource();
        PortStatusText.Text = string.Join(", ", peers.Count == 1 ? [portName] : [PortStatusText.Text, portName]);
        subscriptions.Add(opened.StateChanged.Subscribe(state => OnStateChanged(opened, state)));
        subscriptions.Add(opened.Exceptions.Subscribe(ex => Dispatcher.UIThread.Post(() => AppendMessage($"{label}Error: {ex.Message}"))));
        return opened;
    }

    private async Task Release()
    {
        IMicroGatePeer[] released = [.. peers];
        peers.Clear();
        connectCancellation?.Cancel();
        connectCancellation = null;
        foreach (Channel<byte[]> relay in relays)
        {
            relay.Writer.TryComplete();
        }

        relays.Clear();
        PortStatusText.Text = "No active port";
        foreach (IDisposable subscription in subscriptions)
        {
            subscription.Dispose();
        }

        subscriptions.Clear();
        await Task.WhenAll(released.Select(released => released.DisposeAsync().AsTask()));
    }

    private async Task Relay(ChannelReader<byte[]> reader, IMicroGatePeer target, string direction)
    {
        try
        {
            await foreach (byte[] raw in reader.ReadAllAsync())
            {
                await target.Forward(raw);
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => AppendMessage($"Forwarding {direction} failed: {ex.Message}"));
        }
    }

    private void OnReceived(IMemoryOwner<byte> owner)
    {
        byte[] data;
        using (owner)
        {
            data = owner.Memory.ToArray();
        }

        Dispatcher.UIThread.Post(() =>
        {
            firstCount++;
            AppendEntry(new LogEntry($"{DateTime.Now:HH:mm:ss.fff}  RX  {data.Length} bytes", data));
            UpdateCounts();
        });
    }

    private void OnFrame(MicroGateFrame frame, string direction, bool isFirst)
    {
        byte[] raw = frame.Raw.ToArray();
        string text = $"{frame.Timestamp:HH:mm:ss.fff}  {direction}  {describer.Describe(frame)}";
        Dispatcher.UIThread.Post(() =>
        {
            if (isFirst)
            {
                firstCount++;
            }
            else
            {
                secondCount++;
            }

            AppendEntry(new LogEntry(text, raw));
            UpdateCounts();
        });
    }

    private void OnRelayed(MicroGateFrame frame, string direction, bool isFirst, Channel<byte[]> relay)
    {
        relay.Writer.TryWrite(frame.Raw.ToArray());
        OnFrame(frame, direction, isFirst);
    }

    private void OnStateChanged(IMicroGatePeer source, MicroGatePeerState state) =>
        Dispatcher.UIThread.Post(async () =>
        {
            if (state == MicroGatePeerState.Disconnected && peers.Contains(source))
            {
                await Release();
                AppendMessage("Connection lost.");
            }

            UpdateState();
        });

    private void UpdateMode()
    {
        bool isPeer = mode == ControllerMode.Peer;
        bool isPassthrough = mode == ControllerMode.Passthrough;
        PortHeaderText.Text = isPassthrough ? "PORT A" : "PORT";
        PortBPanel.IsVisible = isPassthrough;
        AddressesPanel.IsVisible = isPeer;
        TransmitPanel.IsVisible = mode != ControllerMode.Monitor;
        ConnectionPanel.IsVisible = isPeer;
        SendPanel.IsVisible = isPeer;
        UpdateState();
        UpdateCounts();
    }

    private void UpdateState()
    {
        bool active = peers.Count > 0;
        MicroGatePeerState state = active ? peers.Min(open => open.State) : MicroGatePeerState.Idle;
        ModeComboBox.IsEnabled = !active;
        ConnectButton.Content = (mode, active) switch
        {
            (ControllerMode.Peer, false) => "Connect",
            (ControllerMode.Peer, true) => "Disconnect",
            (ControllerMode.Monitor, false) => "Start Monitoring",
            (ControllerMode.Passthrough, false) => "Start Passthrough",
            _ => "Stop",
        };
        SendButton.IsEnabled = mode == ControllerMode.Peer && state == MicroGatePeerState.Connected;
        StatusText.Text = state == MicroGatePeerState.Ready ? (mode == ControllerMode.Monitor ? "Monitoring" : "Passing through") : state.ToString();
    }

    private void UpdateCounts() => CountText.Text = mode switch
    {
        ControllerMode.Peer => $"{firstCount} received, {secondCount} sent",
        ControllerMode.Monitor => firstCount == 1 ? "1 frame" : $"{firstCount} frames",
        _ => $"{firstCount} A > B, {secondCount} B > A",
    };

    private MicroGatePeerOptions? BuildOptions()
    {
        MicroGatePeerOptions defaults = new();
        int maxInfoField = defaults.MaxInfoField;
        int transmitWindow = defaults.TransmitWindow;
        TimeSpan? retryInterval = defaults.RetryInterval;
        TimeSpan? retransmitInterval = defaults.RetransmitInterval;
        int? maxRetransmissions = defaults.MaxRetransmissions;

        if (!TryParseInt(ClockSpeedTextBox, "Clock speed", 1, int.MaxValue, out int clockSpeed))
        {
            return null;
        }

        if (mode == ControllerMode.Peer)
        {
            if (!TryParseInt(MaxInfoFieldTextBox, "Max info field", 1, 4090, out maxInfoField)
                || !TryParseInt(TransmitWindowTextBox, "Transmit window", 1, 7, out transmitWindow)
                || !TryParseSeconds(RetryIntervalTextBox, "Connect retry interval", out retryInterval)
                || !TryParseSeconds(RetransmitIntervalTextBox, "Retransmit interval", out retransmitInterval))
            {
                return null;
            }

            maxRetransmissions = null;
            if (!string.IsNullOrWhiteSpace(MaxRetransmissionsTextBox.Text))
            {
                if (!TryParseInt(MaxRetransmissionsTextBox, "Max retransmissions", 0, int.MaxValue, out int parsed))
                {
                    return null;
                }

                maxRetransmissions = parsed;
            }
        }

        return new MicroGatePeerOptions
        {
            Link = new MicroGateLinkOptions
            {
                Encoding = Pick<MicroGateEncoding>(EncodingComboBox),
                Crc = Pick<MicroGateCrc>(CrcComboBox),
                ReceiveClockSource = Pick<MicroGateReceiveClockSource>(ReceiveClockComboBox),
                TransmitClockSource = Pick<MicroGateTransmitClockSource>(TransmitClockComboBox),
                PhaseLockedLoopDivisor = Pick<MicroGatePhaseLockedLoopDivisor>(DivisorComboBox),
                ClockSpeed = clockSpeed,
            },
            IdlePattern = Pick<MicroGateIdlePattern>(IdlePatternComboBox),
            PreamblePattern = Pick<MicroGatePreamblePattern>(PreamblePatternComboBox),
            PreambleLength = Pick<MicroGatePreambleLength>(PreambleLengthComboBox),
            UnderrunAction = Pick<MicroGateUnderrunAction>(UnderrunComboBox),
            DisablePollFinalBit = mode == ControllerMode.Peer ? DisablePollFinalCheckBox.IsChecked == true : defaults.DisablePollFinalBit,
            MaxInfoField = maxInfoField,
            RetryInterval = retryInterval,
            RetransmitInterval = retransmitInterval,
            MaxRetransmissions = maxRetransmissions,
            TransmitWindow = transmitWindow,
            Loopback = mode == ControllerMode.Peer && LoopbackCheckBox.IsChecked == true,
        };
    }

    private void Fill<T>(ComboBox box, T selected)
        where T : struct, Enum
    {
        box.ItemsSource = Enum.GetValues<T>();
        box.SelectedItem = selected;
    }

    private T Pick<T>(ComboBox box)
        where T : struct, Enum => box.SelectedItem is T value ? value : default;

    private string FormatSeconds(TimeSpan? interval) => interval?.TotalSeconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private bool TryParseInt(TextBox box, string name, int min, int max, out int value)
    {
        if (int.TryParse(box.Text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max)
        {
            return true;
        }

        AppendMessage(max == int.MaxValue ? $"{name} must be a whole number of at least {min}." : $"{name} must be a whole number from {min} to {max}.");
        return false;
    }

    private bool TryParseSeconds(TextBox box, string name, out TimeSpan? value)
    {
        value = null;
        string text = box.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds > 0 && seconds < 86400)
        {
            value = TimeSpan.FromSeconds(seconds);
            return true;
        }

        AppendMessage($"{name} must be a positive number of seconds, or blank for none.");
        return false;
    }

    private void UpdateSendCount() => SendCountText.Text = SendGrid.Cells?.Count == 1 ? "1 byte" : $"{SendGrid.Cells?.Count ?? 0} bytes";

    private void InputMode_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool raw = InputModeComboBox.SelectedIndex == 1;
        SendGrid.RawInput = raw;
        SendHintText.Text = raw
            ? "Select a cell and type a 0-255 value; Tab moves to the next cell. Right click for control characters and delete. Enter sends."
            : "Select a cell and type characters; each fills a cell and moves to the next. Right click for control characters and delete. Enter sends.";
    }

    private void LogListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        object? selected = LogListBox.SelectedItem;
        foreach (LogEntry entry in log)
        {
            entry.IsExpanded = entry == selected && entry.HasData;
        }
    }

    private async void Send_Click(object? sender, RoutedEventArgs e) => await Send();

    private async Task Send()
    {
        if (peers.Count != 1 || peers[0] is not { State: MicroGatePeerState.Connected } connected)
        {
            AppendMessage("Not connected.");
            return;
        }

        byte[] data = [.. (SendGrid.Cells ?? []).Select(cell => cell.Value)];

        if (data.Length == 0)
        {
            return;
        }

        if (data.Length > connected.MaxPayloadSize)
        {
            AppendMessage($"Cannot send: {data.Length} bytes exceeds the maximum payload of {connected.MaxPayloadSize}.");
            return;
        }

        SendButton.IsEnabled = false;
        try
        {
            await connected.Send(data);
            secondCount++;
            AppendEntry(new LogEntry($"{DateTime.Now:HH:mm:ss.fff}  TX  {data.Length} bytes", data));
            UpdateCounts();
        }
        catch (Exception ex)
        {
            AppendMessage($"Send failed: {ex.Message}");
        }

        UpdateState();
    }

    private void ClearSend_Click(object? sender, RoutedEventArgs e)
    {
        SendGrid.Cells = [];
        UpdateSendCount();
    }

    private async void SaveLog_Click(object? sender, RoutedEventArgs e)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            AppendMessage("Saving is not supported on this platform.");
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
            stream.SetLength(0);
            await serializer.Save(log, stream);
            AppendMessage($"Saved {log.Count} rows to {file.Name}.");
        }
        catch (Exception ex)
        {
            AppendMessage($"Save failed: {ex.Message}");
        }
    }

    private async void LoadLog_Click(object? sender, RoutedEventArgs e)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            AppendMessage("Loading is not supported on this platform.");
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
            IReadOnlyList<LogEntry> loaded = await serializer.Load(stream);
            log.Clear();
            foreach (LogEntry entry in loaded)
            {
                log.Add(entry);
            }

            firstCount = 0;
            secondCount = 0;
            UpdateCounts();
            AppendMessage($"Loaded {loaded.Count} rows from {files[0].Name}.");
        }
        catch (Exception ex)
        {
            AppendMessage($"Load failed: {ex.Message}");
        }
    }

    private void ClearLog_Click(object? sender, RoutedEventArgs e)
    {
        log.Clear();
        firstCount = 0;
        secondCount = 0;
        UpdateCounts();
    }

    private void AppendMessage(string message) => AppendEntry(new LogEntry($"{DateTime.Now:HH:mm:ss.fff}  {message}", null));

    private void AppendEntry(LogEntry entry)
    {
        log.Add(entry);
        while (log.Count > maxLogEntries)
        {
            log.RemoveAt(0);
        }

        if (LogListBox.SelectedItem is null)
        {
            LogListBox.ScrollIntoView(entry);
        }
    }
}
