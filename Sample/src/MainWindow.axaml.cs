namespace BlueHeighliner.MicroGate;

/// <summary>
/// The main window of the MicroGate sample application, demonstrating port enumeration, connecting, and sending and receiving data with <c>Core</c>.
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

        LogListBox.ItemsSource = log;
        Loaded += async (_, _) => await RefreshPorts();
        Closed += (_, _) => peer?.Dispose();
    }

    private readonly IMicroGatePortSource portSource;
    private readonly IMicroGatePeerFactory peerFactory;
    private readonly ObservableCollection<string> log = [];
    private readonly List<IDisposable> subscriptions = [];
    private IMicroGatePeer? peer;

    private async Task RefreshPorts()
    {
        IReadOnlyList<string> ports = await portSource.GetPorts();
        PortComboBox.ItemsSource = ports;
        if (ports.Count > 0)
        {
            PortComboBox.SelectedIndex = 0;
        }
    }

    private async void RefreshPorts_Click(object? sender, RoutedEventArgs e) => await RefreshPorts();

    private async void Connect_Click(object? sender, RoutedEventArgs e)
    {
        if (peer is not null)
        {
            await Disconnect();
            return;
        }

        if (PortComboBox.SelectedItem is not string portName)
        {
            AppendLog("Select a port first.");
            return;
        }

        IMicroGatePeer newPeer = peerFactory.Create();
        peer = newPeer;
        subscriptions.Add(newPeer.Received.Subscribe(OnReceived));
        subscriptions.Add(newPeer.StateChanged.Subscribe(state => OnStateChanged(newPeer, state)));

        ConnectButton.IsEnabled = false;

        try
        {
            await newPeer.Start(portName);
            AppendLog($"Connected to {portName}.");
        }
        catch (Exception ex)
        {
            AppendLog($"Connect failed: {ex.Message}");
            await Release(newPeer);
        }

        ConnectButton.IsEnabled = true;
        UpdateConnectionState();
    }

    private async void Send_Click(object? sender, RoutedEventArgs e)
    {
        if (peer is not { IsConnected: true } connected || string.IsNullOrEmpty(MessageTextBox.Text))
        {
            return;
        }

        string message = MessageTextBox.Text;
        byte[] data = Encoding.UTF8.GetBytes(message);

        try
        {
            await connected.Send(data);
            AppendLog($"Sent: {message}");
            MessageTextBox.Text = string.Empty;
        }
        catch (Exception ex)
        {
            AppendLog($"Send failed: {ex.Message}");
        }
    }

    private async Task Disconnect()
    {
        if (peer is null)
        {
            return;
        }

        await Release(peer);
        AppendLog("Disconnected.");
        UpdateConnectionState();
    }

    private async Task Release(IMicroGatePeer released)
    {
        if (peer != released)
        {
            return;
        }

        peer = null;
        foreach (IDisposable subscription in subscriptions)
        {
            subscription.Dispose();
        }

        subscriptions.Clear();
        await released.DisposeAsync();
    }

    private void OnReceived(ReadOnlyMemory<byte> data)
    {
        string text = Encoding.UTF8.GetString(data.Span);
        Dispatcher.UIThread.Post(() => AppendLog($"Received: {text}"));
    }

    private void OnStateChanged(IMicroGatePeer source, MicroGatePeerState state) =>
        Dispatcher.UIThread.Post(async () =>
        {
            if (state == MicroGatePeerState.Disconnected && peer == source)
            {
                await Release(source);
                AppendLog("Disconnected.");
            }

            UpdateConnectionState();
        });

    private void UpdateConnectionState()
    {
        MicroGatePeerState state = peer?.State ?? MicroGatePeerState.Disconnected;
        ConnectButton.Content = peer is null ? "Connect" : "Disconnect";
        StatusText.Text = state == MicroGatePeerState.Connecting ? "Connecting..." : state.ToString();
        SendButton.IsEnabled = state == MicroGatePeerState.Connected;
    }

    private void AppendLog(string message) => log.Add($"{DateTime.Now:HH:mm:ss} {message}");
}
