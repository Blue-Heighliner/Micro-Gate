namespace BlueHeighliner.MicroGate;

/// <summary>
/// The main window of the MicroGate controller: pick a port, connect as a peer, watch the data received, and send custom data. Data is shown and edited as a table of bytes (see <see cref="ByteGrid"/>).
/// </summary>
internal sealed partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="portSource">The port source used to enumerate available MicroGate devices.</param>
    /// <param name="peerFactory">The factory that creates a new, idle peer. A peer is single use, so one is created for every connection.</param>
    public MainWindow(IMicroGatePortSource portSource, IMicroGatePeerFactory peerFactory)
    {
        this.portSource = portSource;
        this.peerFactory = peerFactory;
        InitializeComponent();

        EncodingComboBox.ItemsSource = Enum.GetValues<MicroGateEncoding>();
        EncodingComboBox.SelectedItem = MicroGateEncoding.Nrz;
        CrcComboBox.ItemsSource = Enum.GetValues<MicroGateCrc>();
        CrcComboBox.SelectedItem = MicroGateCrc.Crc32Ccitt;
        InputModeComboBox.ItemsSource = new[] { "ASCII", "Raw values" };
        InputModeComboBox.SelectedIndex = 0;
        SendGrid.Cells = [];
        SendGrid.MaxCells = new MicroGatePeerOptions().MaxInfoField;
        SendGrid.Edited += (_, _) => UpdateSendCount();
        SendGrid.SubmitRequested += async (_, _) => await Send();
        LogListBox.ItemsSource = log;

        Loaded += async (_, _) => await RefreshPorts();
        Closed += (_, _) => peer?.Dispose();
    }

    private readonly IMicroGatePortSource portSource;
    private readonly IMicroGatePeerFactory peerFactory;
    private readonly ObservableCollection<LogEntry> log = [];
    private readonly List<IDisposable> subscriptions = [];
    private readonly int maxLogEntries = 5000;
    private IMicroGatePeer? peer;
    private CancellationTokenSource? connectCancellation;
    private int receivedCount;
    private int sentCount;

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
            AppendMessage($"Listing ports failed: {ex.Message}");
        }
    }

    private async void RefreshPorts_Click(object? sender, RoutedEventArgs e) => await RefreshPorts();

    private async void Connect_Click(object? sender, RoutedEventArgs e)
    {
        if (peer is not null)
        {
            await Release(peer);
            AppendMessage("Disconnected.");
            UpdateState();
            return;
        }

        if (PortComboBox.SelectedItem is not string portName)
        {
            AppendMessage("Select a port first.");
            return;
        }

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

        MicroGatePeerOptions options = new()
        {
            Link = new MicroGateLinkOptions
            {
                Encoding = EncodingComboBox.SelectedItem is MicroGateEncoding encoding ? encoding : MicroGateEncoding.Nrz,
                Crc = CrcComboBox.SelectedItem is MicroGateCrc crc ? crc : MicroGateCrc.Crc32Ccitt,
            },
        };

        IMicroGatePeer newPeer = peerFactory.Create();
        newPeer.Receiver = OnReceived;
        peer = newPeer;
        subscriptions.Add(newPeer.StateChanged.Subscribe(state => OnStateChanged(newPeer, state)));
        subscriptions.Add(newPeer.Exceptions.Subscribe(ex => Dispatcher.UIThread.Post(() => AppendMessage($"Error: {ex.Message}"))));

        CancellationTokenSource cancellation = new();
        connectCancellation = cancellation;
        PortStatusText.Text = portName;
        AppendMessage($"Connecting on {portName} as {address:X2} to {remoteAddress:X2}...");
        UpdateState();

        try
        {
            await newPeer.Start(portName, address, remoteAddress, options, cancellation.Token);
            AppendMessage("Connected.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            AppendMessage($"Connect failed: {ex.Message}");
            await Release(newPeer);
        }

        UpdateState();
    }

    private async Task Release(IMicroGatePeer released)
    {
        if (peer != released)
        {
            return;
        }

        peer = null;
        connectCancellation?.Cancel();
        connectCancellation = null;
        PortStatusText.Text = "No active port";
        foreach (IDisposable subscription in subscriptions)
        {
            subscription.Dispose();
        }

        subscriptions.Clear();
        await released.DisposeAsync();
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
            receivedCount++;
            AppendEntry(new LogEntry($"{DateTime.Now:HH:mm:ss.fff}  RX  {data.Length} bytes", data));
            UpdateCounts();
        });
    }

    private void OnStateChanged(IMicroGatePeer source, MicroGatePeerState state) =>
        Dispatcher.UIThread.Post(async () =>
        {
            if (state == MicroGatePeerState.Disconnected && peer == source)
            {
                await Release(source);
                AppendMessage("Connection lost.");
            }

            UpdateState();
        });

    private void UpdateState()
    {
        MicroGatePeerState state = peer?.State ?? MicroGatePeerState.Idle;
        ConnectButton.Content = peer is null ? "Connect" : "Disconnect";
        SendButton.IsEnabled = state == MicroGatePeerState.Connected;
        StatusText.Text = state.ToString();
    }

    private void UpdateCounts() => CountText.Text = $"{receivedCount} received, {sentCount} sent";

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
        if (peer is not { State: MicroGatePeerState.Connected } connected)
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
            sentCount++;
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

    private void ClearLog_Click(object? sender, RoutedEventArgs e)
    {
        log.Clear();
        receivedCount = 0;
        sentCount = 0;
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
