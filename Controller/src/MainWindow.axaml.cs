namespace BlueHeighliner.MicroGate;

/// <summary>
/// The main window of the MicroGate controller, which runs in one of six <see cref="ControllerMode"/>s: over HDLC or over a plain asynchronous (UART) line, as a peer that sends and receives data, as a monitor that only observes, or as a passthrough between two ports. Frames and data are shown, and in the peer modes edited, as tables of bytes (see <see cref="ByteGrid"/>).
/// </summary>
internal sealed partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="portSource">The port source used to enumerate available MicroGate devices.</param>
    /// <param name="peerFactory">The factory that creates a new, idle HDLC peer. A peer is single use, so one is created for every connection.</param>
    /// <param name="uartPeerFactory">The factory that creates a new, idle UART peer, used the same way.</param>
    /// <param name="describer">The describer of the frames shown in monitor and passthrough mode.</param>
    /// <param name="serializer">The serializer that saves and loads the log.</param>
    /// <param name="startup">What the command line asked for, such as the mode to open in.</param>
    public MainWindow(IMicroGatePortSource portSource, IHdlcPeerFactory peerFactory, IUartPeerFactory uartPeerFactory, IFrameDescriber describer, ILogSerializer serializer, StartupOptions startup)
    {
        this.portSource = portSource;
        this.peerFactory = peerFactory;
        this.uartPeerFactory = uartPeerFactory;
        this.describer = describer;
        this.serializer = serializer;
        InitializeComponent();

        mode = startup.InitialMode;
        AddressTextBox.Text = startup.LocalAddress?.ToString(CultureInfo.InvariantCulture) ?? AddressTextBox.Text;
        RemoteAddressTextBox.Text = startup.RemoteAddress?.ToString(CultureInfo.InvariantCulture) ?? RemoteAddressTextBox.Text;
        ModeComboBox.Fill(mode);
        ModeComboBox.ItemTemplate = new FuncDataTemplate<ControllerMode>((item, _) => new TextBlock { Text = item.Title });
        LinkOptions.MaxInfoFieldChanged += (_, size) =>
        {
            maxInfoField = size;
            SendGrid.MaxCells = mode.IsUart ? uartMaxCells : size;
        };
        ColumnsTextBox.Text = columns.ToString(CultureInfo.InvariantCulture);
        InputModeComboBox.ItemsSource = new[] { "ASCII", "Raw values" };
        InputModeComboBox.SelectedIndex = 0;
        SendGrid.Cells = [];
        maxInfoField = new HdlcPeerOptions().MaxInfoField;
        SendGrid.MaxCells = maxInfoField;
        SendGrid.Edited += (_, _) => UpdateSendCount();
        SendGrid.SubmitRequested += async (_, _) => await Send();
        LogViewComboBox.Fill(LogView.Frames);
        LogListBox.ItemsSource = shown;
        LogListBox.AddHandler(PointerPressedEvent, LogListBox_PointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        LogListBox.KeyDown += LogListBox_KeyDown;

        UpdateMode();
        if (startup.Problem is { } problem)
        {
            AppendMessage(problem);
        }

        Loaded += async (_, _) => await RefreshPorts();
        Closed += (_, _) =>
        {
            foreach (IHdlcPeer open in peers)
            {
                open.Dispose();
            }

            foreach (IUartPeer open in uartPeers)
            {
                open.Dispose();
            }
        };
    }

    private readonly IMicroGatePortSource portSource;
    private readonly IHdlcPeerFactory peerFactory;
    private readonly IUartPeerFactory uartPeerFactory;
    private readonly IFrameDescriber describer;
    private readonly ILogSerializer serializer;
    private readonly List<IHdlcPeer> peers = [];
    private readonly List<IUartPeer> uartPeers = [];
    private readonly List<Channel<byte[]>> relays = [];
    private readonly List<LogEntry> log = [];
    private readonly ObservableCollection<LogEntry> shown = [];
    private readonly List<IDisposable> subscriptions = [];
    private readonly int maxLogEntries = 5000;
    private readonly int uartMaxCells = 65536;
    private ControllerMode mode = ControllerMode.HdlcPeer;
    private int maxInfoField;
    private CancellationTokenSource? connectCancellation;
    private int columns = 30;
    private LogView view = LogView.Frames;
    private int firstFrames;
    private int secondFrames;
    private string firstName = "A";
    private string secondName = "B";
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
        if (HasOpenPorts)
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

        HdlcPeerOptions? options = null;
        UartPeerOptions? uartOptions = null;
        if (mode.IsUart ? (uartOptions = BuildUartOptions()) is null : (options = BuildOptions()) is null)
        {
            return;
        }

        firstCount = 0;
        secondCount = 0;
        firstFrames = 0;
        secondFrames = 0;
        firstName = portName;
        secondName = PortBComboBox.SelectedItem as string ?? string.Empty;
        UpdateCounts();

        switch (mode)
        {
            case ControllerMode.HdlcPeer:
                await ConnectPeer(portName, options!);
                break;
            case ControllerMode.HdlcMonitor:
                await ConnectMonitor(portName, options!);
                break;
            case ControllerMode.HdlcPassthrough:
                await ConnectPassthrough(portName, options!);
                break;
            case ControllerMode.UartPeer:
            case ControllerMode.UartMonitor:
                await ConnectUart(portName, uartOptions!);
                break;
            default:
                await ConnectUartPassthrough(portName, uartOptions!);
                break;
        }

        UpdateState();
    }

    private async Task ConnectPeer(string portName, HdlcPeerOptions options)
    {
        if (!byte.TryParse(AddressTextBox.Text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out byte address)
            || !byte.TryParse(RemoteAddressTextBox.Text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out byte remoteAddress))
        {
            AppendMessage("Both addresses must be whole numbers from 0 to 255.");
            return;
        }

        if (address == remoteAddress)
        {
            AppendMessage("The local and remote addresses must differ.");
            return;
        }

        IHdlcPeer newPeer = Open(portName, string.Empty);
        newPeer.Receiver = OnReceived;
        subscriptions.Add(newPeer.Monitored.Subscribe(frame => OnFrame(frame, "Receive", true)));
        subscriptions.Add(newPeer.Transmitted.Subscribe(frame => OnFrame(frame, "Transmit", false)));
        CancellationToken cancellation = connectCancellation!.Token;
        AppendMessage($"Connecting on {portName} as {address} to {remoteAddress}...");
        UpdateState();

        try
        {
            await newPeer.Start(portName, options with { EnableMonitor = true }, cancellation);
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

    private async Task ConnectMonitor(string portName, HdlcPeerOptions options)
    {
        IHdlcPeer newPeer = Open(portName, string.Empty);
        subscriptions.Add(newPeer.Monitored.Subscribe(frame => OnFrame(frame, "Receive", true)));
        AppendMessage($"Monitoring {portName}...");

        if (await StartAll([(newPeer, portName)], options with { EnableMonitor = true }))
        {
            AppendMessage("Monitoring.");
        }
    }

    private async Task ConnectPassthrough(string portName, HdlcPeerOptions options)
    {
        if (PortBComboBox.SelectedItem is not string portNameB || portNameB == portName)
        {
            AppendMessage("Select two different ports.");
            return;
        }

        IHdlcPeer first = Open(portName, $"{portName}: ");
        IHdlcPeer second = Open(portNameB, $"{portNameB}: ");
        Channel<byte[]> firstToSecond = Channel.CreateUnbounded<byte[]>();
        Channel<byte[]> secondToFirst = Channel.CreateUnbounded<byte[]>();
        relays.Add(firstToSecond);
        relays.Add(secondToFirst);
        subscriptions.Add(first.Monitored.Subscribe(frame => OnRelayed(frame, $"{portName} > {portNameB}", true, firstToSecond)));
        subscriptions.Add(second.Monitored.Subscribe(frame => OnRelayed(frame, $"{portNameB} > {portName}", false, secondToFirst)));
        _ = Relay(firstToSecond.Reader, second, $"{portName} > {portNameB}");
        _ = Relay(secondToFirst.Reader, first, $"{portNameB} > {portName}");
        AppendMessage($"Passing through between {portName} and {portNameB}...");

        if (await StartAll([(first, portName), (second, portNameB)], options with { EnableMonitor = true }))
        {
            AppendMessage("Passing through.");
        }
    }

    private async Task ConnectUart(string portName, UartPeerOptions options)
    {
        IUartPeer newPeer = OpenUart(portName, string.Empty);
        newPeer.Receiver = owner => OnUartReceived(owner, "Receive", true, null);
        AppendMessage($"Opening {portName} at {Describe(options)}...");

        if (await StartAllUart([(newPeer, portName)], options))
        {
            AppendMessage(mode == ControllerMode.UartMonitor ? "Monitoring." : "Open.");
        }
    }

    private async Task ConnectUartPassthrough(string portName, UartPeerOptions options)
    {
        if (PortBComboBox.SelectedItem is not string portNameB || portNameB == portName)
        {
            AppendMessage("Select two different ports.");
            return;
        }

        IUartPeer first = OpenUart(portName, $"{portName}: ");
        IUartPeer second = OpenUart(portNameB, $"{portNameB}: ");
        Channel<byte[]> firstToSecond = Channel.CreateUnbounded<byte[]>();
        Channel<byte[]> secondToFirst = Channel.CreateUnbounded<byte[]>();
        relays.Add(firstToSecond);
        relays.Add(secondToFirst);
        first.Receiver = owner => OnUartReceived(owner, $"{portName} > {portNameB}", true, firstToSecond);
        second.Receiver = owner => OnUartReceived(owner, $"{portNameB} > {portName}", false, secondToFirst);
        _ = RelayUart(firstToSecond.Reader, second, $"{portName} > {portNameB}");
        _ = RelayUart(secondToFirst.Reader, first, $"{portNameB} > {portName}");
        AppendMessage($"Passing through between {portName} and {portNameB} at {Describe(options)}...");

        if (await StartAllUart([(first, portName), (second, portNameB)], options))
        {
            AppendMessage("Passing through.");
        }
    }

    private string Describe(UartPeerOptions options) =>
        $"{options.BaudRate} baud, {options.DataBits}{options.Parity.ToString()[0]}{(options.StopBits == UartStopBits.Two ? 2 : 1)}";

    private async Task<bool> StartAllUart(IReadOnlyList<(IUartPeer Peer, string PortName)> targets, UartPeerOptions options)
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

    private async Task<bool> StartAll(IReadOnlyList<(IHdlcPeer Peer, string PortName)> targets, HdlcPeerOptions options)
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

    private IHdlcPeer Open(string portName, string label)
    {
        IHdlcPeer opened = peerFactory.Create();
        peers.Add(opened);
        connectCancellation ??= new CancellationTokenSource();
        NotePort(portName);
        subscriptions.Add(opened.StateChanged.Subscribe(state => OnStateChanged(opened, state)));
        subscriptions.Add(opened.Exceptions.Subscribe(ex => Dispatcher.UIThread.Post(() => AppendMessage($"{label}Error: {ex.Message}"))));
        return opened;
    }

    private IUartPeer OpenUart(string portName, string label)
    {
        IUartPeer opened = uartPeerFactory.Create();
        uartPeers.Add(opened);
        connectCancellation ??= new CancellationTokenSource();
        NotePort(portName);
        subscriptions.Add(opened.StateChanged.Subscribe(state => OnUartStateChanged(opened, state)));
        subscriptions.Add(opened.Exceptions.Subscribe(ex => Dispatcher.UIThread.Post(() => AppendMessage($"{label}Error: {ex.Message}"))));
        return opened;
    }

    private void NotePort(string portName) => PortStatusText.Text = peers.Count + uartPeers.Count == 1 ? portName : $"{PortStatusText.Text}, {portName}";

    private async Task Release()
    {
        IHdlcPeer[] released = [.. peers];
        IUartPeer[] releasedUart = [.. uartPeers];
        peers.Clear();
        uartPeers.Clear();
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
        await Task.WhenAll(released.Select(peer => peer.DisposeAsync().AsTask()).Concat(releasedUart.Select(peer => peer.DisposeAsync().AsTask())));
    }

    private async Task Relay(ChannelReader<byte[]> reader, IHdlcPeer target, string direction)
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

    private async Task RelayUart(ChannelReader<byte[]> reader, IUartPeer target, string direction)
    {
        try
        {
            await foreach (byte[] data in reader.ReadAllAsync())
            {
                await target.Send(data);
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => AppendMessage($"Forwarding {direction} failed: {ex.Message}"));
        }
    }

    private void OnUartReceived(IMemoryOwner<byte> owner, string direction, bool isFirst, Channel<byte[]>? relay)
    {
        byte[] data;
        using (owner)
        {
            data = owner.Memory.ToArray();
        }

        relay?.Writer.TryWrite(data);
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

            AppendEntry(new LogEntry($"{DateTime.Now:HH:mm:ss.fff}  {direction}  {data.Length}B", data));
            UpdateCounts();
        });
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
            AppendEntry(new LogEntry($"{DateTime.Now:HH:mm:ss.fff}  Receive  {data.Length}B", data));
            UpdateCounts();
        });
    }

    private void OnFrame(HdlcFrame frame, string direction, bool isFirst)
    {
        byte[] data = (frame.Kind == HdlcFrameKind.Malformed ? frame.Raw : frame.Payload).ToArray();
        string text = $"{frame.Timestamp:HH:mm:ss.fff}  {direction}  {describer.Describe(frame)}";
        IReadOnlyList<LogField> fields = describer.Details(frame);
        Dispatcher.UIThread.Post(() =>
        {
            if (isFirst)
            {
                firstFrames++;
            }
            else
            {
                secondFrames++;
            }

            AppendEntry(new LogEntry(text, data, fields));
            if (mode != ControllerMode.HdlcPeer && frame.Kind == HdlcFrameKind.Information && !frame.Payload.IsEmpty)
            {
                if (isFirst)
                {
                    firstCount++;
                }
                else
                {
                    secondCount++;
                }

                AppendEntry(new LogEntry($"{frame.Timestamp:HH:mm:ss.fff}  {direction}  {data.Length}B", data));
            }

            UpdateCounts();
        });
    }

    private void OnRelayed(HdlcFrame frame, string direction, bool isFirst, Channel<byte[]> relay)
    {
        relay.Writer.TryWrite(frame.Raw.ToArray());
        OnFrame(frame, direction, isFirst);
    }

    private void OnStateChanged(IHdlcPeer source, HdlcPeerState state) =>
        Dispatcher.UIThread.Post(async () =>
        {
            if (state == HdlcPeerState.Disconnected && peers.Contains(source))
            {
                await Release();
                AppendMessage("Connection lost.");
            }

            UpdateState();
        });

    private void OnUartStateChanged(IUartPeer source, UartPeerState state) =>
        Dispatcher.UIThread.Post(async () =>
        {
            if (state == UartPeerState.Closed && uartPeers.Contains(source))
            {
                await Release();
                AppendMessage("Port closed.");
            }

            UpdateState();
        });

    private void UpdateMode()
    {
        PortHeaderText.Text = mode.IsPassthrough ? "Port A" : "Port";
        PortBPanel.IsVisible = mode.IsPassthrough;
        AddressesPanel.IsVisible = mode == ControllerMode.HdlcPeer;
        LinkOptions.SetMode(mode);
        SendPanel.IsVisible = mode.IsPeer;
        ViewPanel.IsVisible = !mode.IsUart;
        SendGrid.MaxCells = mode.IsUart ? uartMaxCells : maxInfoField;
        ApplyView();
        UpdateState();
        UpdateCounts();
    }

    private void UpdateState()
    {
        bool active = HasOpenPorts;
        ModeComboBox.IsEnabled = !active;
        ConnectButton.Content = (mode, active) switch
        {
            (ControllerMode.HdlcPeer, false) => "Connect",
            (ControllerMode.HdlcPeer, true) => "Disconnect",
            (ControllerMode.UartPeer, false) => "Open",
            (ControllerMode.UartPeer, true) => "Close",
            (_, false) => "Start",
            _ => "Stop",
        };

        if (mode.IsUart)
        {
            UartPeerState uartState = uartPeers.Count > 0 ? uartPeers.Min(open => open.State) : UartPeerState.Idle;
            SendButton.IsEnabled = mode == ControllerMode.UartPeer && uartState == UartPeerState.Open;
            StatusText.Text = uartState == UartPeerState.Open ? (mode switch { ControllerMode.UartMonitor => "Monitoring", ControllerMode.UartPassthrough => "Passing through", _ => "Open" }) : uartState.ToString();
            return;
        }

        HdlcPeerState state = peers.Count > 0 ? peers.Min(open => open.State) : HdlcPeerState.Idle;
        SendButton.IsEnabled = mode == ControllerMode.HdlcPeer && state == HdlcPeerState.Connected;
        StatusText.Text = state == HdlcPeerState.Ready ? (mode == ControllerMode.HdlcMonitor ? "Monitoring" : "Passing through") : state.ToString();
    }

    private void UpdateCounts()
    {
        bool frames = view == LogView.Frames && !mode.IsUart;
        int first = frames ? firstFrames : firstCount;
        int second = frames ? secondFrames : secondCount;
        CountText.Text = (mode, frames) switch
        {
            (ControllerMode.UartPeer, _) => $"{first} received, {second} sent",
            (ControllerMode.UartMonitor, _) => $"{first} received",
            (ControllerMode.HdlcPeer, true) => $"{first} frames received, {second} transmitted",
            (ControllerMode.HdlcPeer, false) => $"{first} received, {second} sent",
            (ControllerMode.HdlcMonitor, true) => first == 1 ? "1 frame" : $"{first} frames",
            (ControllerMode.HdlcMonitor, false) => $"{first} received",
            _ => $"{first} {firstName} > {secondName}, {second} {secondName} > {firstName}",
        };
    }

    private UartPeerOptions? BuildUartOptions()
    {
        if (LinkOptions.BuildUart(out string problem) is { } built)
        {
            return built;
        }

        AppendMessage(problem);
        return null;
    }

    private HdlcPeerOptions? BuildOptions()
    {
        if (LinkOptions.Build(mode, out string problem) is { } built)
        {
            return built;
        }

        AppendMessage(problem);
        return null;
    }

    private void UpdateSendCount() => SendCountText.Text = $"{SendGrid.Cells?.Count ?? 0}B";

    private void InputMode_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool raw = InputModeComboBox.SelectedIndex == 1;
        SendGrid.RawInput = raw;
    }

    private void LogListBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(LogListBox).Properties.IsLeftButtonPressed)
        {
            return;
        }

        for (Visual? visual = e.Source as Visual; visual is not null && visual != LogListBox; visual = visual.GetVisualParent())
        {
            if (visual is ByteGrid or SelectableTextBlock)
            {
                return;
            }

            if (visual is ListBoxItem { DataContext: LogEntry entry })
            {
                ToggleExpanded(entry);
                return;
            }
        }
    }

    private void LogListBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && LogListBox.SelectedItem is LogEntry entry)
        {
            ToggleExpanded(entry);
            e.Handled = true;
        }
    }

    private void ToggleExpanded(LogEntry entry)
    {
        if (!entry.HasData)
        {
            return;
        }

        bool expand = !entry.IsExpanded;
        foreach (LogEntry other in log)
        {
            other.IsExpanded = false;
        }

        entry.IsExpanded = expand;
    }

    private void AutoScroll_Changed(object? sender, RoutedEventArgs e)
    {
        if (shown.Count > 0 && AutoScrollToggle.IsChecked == true)
        {
            LogListBox.ScrollIntoView(shown[^1]);
        }
    }

    private async void Send_Click(object? sender, RoutedEventArgs e) => await Send();

    private async Task Send()
    {
        if (mode == ControllerMode.UartPeer)
        {
            await SendUart();
            return;
        }

        if (peers.Count != 1 || peers[0] is not { State: HdlcPeerState.Connected } connected)
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
            AppendMessage($"Cannot send: {data.Length}B exceeds the maximum payload of {connected.MaxPayloadSize}B.");
            return;
        }

        SendButton.IsEnabled = false;
        try
        {
            await connected.Send(data);
            secondCount++;
            AppendEntry(new LogEntry($"{DateTime.Now:HH:mm:ss.fff}  Transmit  {data.Length}B", data));
            SendGrid.Cells = [];
            UpdateSendCount();
            UpdateCounts();
        }
        catch (Exception ex)
        {
            AppendMessage($"Send failed: {ex.Message}");
        }

        UpdateState();
    }

    private async Task SendUart()
    {
        if (uartPeers.Count != 1 || uartPeers[0] is not { State: UartPeerState.Open } open)
        {
            AppendMessage("Not open.");
            return;
        }

        byte[] data = [.. (SendGrid.Cells ?? []).Select(cell => cell.Value)];
        if (data.Length == 0)
        {
            return;
        }

        SendButton.IsEnabled = false;
        try
        {
            await open.Send(data);
            secondCount++;
            AppendEntry(new LogEntry($"{DateTime.Now:HH:mm:ss.fff}  Transmit  {data.Length}B", data));
            SendGrid.Cells = [];
            UpdateSendCount();
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
                entry.Columns = columns;
                log.Add(entry);
            }

            ApplyView();

            firstCount = 0;
            secondCount = 0;
            firstFrames = 0;
            secondFrames = 0;
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
        shown.Clear();
        firstCount = 0;
        secondCount = 0;
        firstFrames = 0;
        secondFrames = 0;
        UpdateCounts();
    }

    private void AppendMessage(string message) => AppendEntry(new LogEntry($"{DateTime.Now:HH:mm:ss.fff}  {message}", null));

    private void Columns_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!int.TryParse(ColumnsTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value is < 1 or > 512 || value == columns)
        {
            return;
        }

        columns = value;
        SendGrid.Columns = value;
        foreach (LogEntry entry in log)
        {
            entry.Columns = value;
        }
    }

    private void LogView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LogViewComboBox.SelectedItem is LogView selected && selected != view)
        {
            view = selected;
            ApplyView();
            UpdateCounts();
        }
    }

    private bool HasOpenPorts => peers.Count + uartPeers.Count > 0;

    private bool IsShown(LogEntry entry) => mode.IsUart || (view == LogView.Frames ? entry.IsFrame || !entry.HasData : !entry.IsFrame);

    private void ApplyView()
    {
        shown.Clear();
        foreach (LogEntry entry in log.Where(IsShown))
        {
            shown.Add(entry);
        }

        if (shown.Count > 0 && AutoScrollToggle.IsChecked == true)
        {
            LogListBox.ScrollIntoView(shown[^1]);
        }
    }

    private void AppendEntry(LogEntry entry)
    {
        entry.Columns = columns;
        log.Add(entry);
        if (IsShown(entry))
        {
            shown.Add(entry);
        }

        while (log.Count > maxLogEntries)
        {
            shown.Remove(log[0]);
            log.RemoveAt(0);
        }

        if (AutoScrollToggle.IsChecked == true && IsShown(entry))
        {
            LogListBox.ScrollIntoView(entry);
        }
    }
}
