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
    /// <param name="connector">The connector used to open connections to MicroGate devices.</param>
    public MainWindow(IMicroGatePortSource portSource, IMicroGateConnector connector)
    {
        this.portSource = portSource;
        this.connector = connector;
        InitializeComponent();

        LogListBox.ItemsSource = log;
        Loaded += async (_, _) => await RefreshPorts();
        Closed += (_, _) => connection?.Dispose();
    }

    private readonly IMicroGatePortSource portSource;
    private readonly IMicroGateConnector connector;
    private readonly ObservableCollection<string> log = [];
    private IMicroGateConnection? connection;

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
        if (connection is not null)
        {
            await Disconnect();
            return;
        }

        if (PortComboBox.SelectedItem is not string portName)
        {
            AppendLog("Select a port first.");
            return;
        }

        ConnectButton.IsEnabled = false;
        StatusText.Text = "Connecting...";

        try
        {
            connection = await connector.Connect(portName);
            connection.Received += OnReceived;
            connection.Disconnected += OnDisconnected;
            AppendLog($"Connected to {portName}.");
        }
        catch (Exception ex)
        {
            AppendLog($"Connect failed: {ex.Message}");
            connection = null;
        }

        ConnectButton.IsEnabled = true;
        UpdateConnectionState();
    }

    private async void Send_Click(object? sender, RoutedEventArgs e)
    {
        if (connection is null || string.IsNullOrEmpty(MessageTextBox.Text))
        {
            return;
        }

        string message = MessageTextBox.Text;
        byte[] data = Encoding.UTF8.GetBytes(message);

        try
        {
            await connection.Send(data);
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
        if (connection is null)
        {
            return;
        }

        connection.Received -= OnReceived;
        connection.Disconnected -= OnDisconnected;
        await connection.DisposeAsync();
        connection = null;
        AppendLog("Disconnected.");
        UpdateConnectionState();
    }

    private void OnReceived(object? sender, IMemoryOwner<byte> data)
    {
        string text;
        using (data)
        {
            text = Encoding.UTF8.GetString(data.Memory.Span);
        }

        Dispatcher.UIThread.Post(() => AppendLog($"Received: {text}"));
    }

    private void OnDisconnected(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            connection = null;
            AppendLog("Disconnected.");
            UpdateConnectionState();
        });

    private void UpdateConnectionState()
    {
        bool connected = connection is not null;
        ConnectButton.Content = connected ? "Disconnect" : "Connect";
        StatusText.Text = connected ? "Connected" : "Disconnected";
        SendButton.IsEnabled = connected;
    }

    private void AppendLog(string message) => log.Add($"{DateTime.Now:HH:mm:ss} {message}");
}
