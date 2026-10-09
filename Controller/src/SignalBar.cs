namespace BlueHeighliner.MicroGate;

/// <summary>
/// A bar that shows the live state of the modem wires (the inputs DCD, DSR and CTS and the outputs DTR and RTS) of each open port, as lamps that are lit while the wire is active. Before a port has been read, and while no port is open, nothing is lit.
/// </summary>
internal sealed class SignalBar : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignalBar"/> class, showing no port.
    /// </summary>
    public SignalBar()
    {
        panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24, Margin = new Thickness(16, 6) };
        Content = panel;
        Reset();
    }

    private readonly StackPanel panel;
    private readonly List<Dictionary<MicroGateSignals, Ellipse>> lamps = [];
    private readonly IBrush activeBrush = new SolidColorBrush(Color.Parse("#3FB950"));
    private readonly IBrush inactiveBrush = new SolidColorBrush(Color.Parse("#4A4A4A"));
    private readonly IBrush textBrush = new SolidColorBrush(Color.Parse("#9D9D9D"));

    /// <summary>
    /// Removes every port, leaving the bar showing that none is open.
    /// </summary>
    public void Reset()
    {
        lamps.Clear();
        panel.Children.Clear();
        panel.Children.Add(Label("WIRE SIGNALS"));
        panel.Children.Add(Label("no active port"));
    }

    /// <summary>
    /// Adds a port to the bar with all of its lamps off, after any ports already added.
    /// </summary>
    /// <param name="name">The name shown for the port.</param>
    public void AddPort(string name)
    {
        if (lamps.Count == 0)
        {
            panel.Children.RemoveAt(1);
        }

        Dictionary<MicroGateSignals, Ellipse> group = [];
        StackPanel row = new() { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(Label(name));
        foreach ((MicroGateSignals signal, string text) in new[] { (MicroGateSignals.DataCarrierDetect, "DCD"), (MicroGateSignals.DataSetReady, "DSR"), (MicroGateSignals.ClearToSend, "CTS"), (MicroGateSignals.DataTerminalReady, "DTR"), (MicroGateSignals.RequestToSend, "RTS") })
        {
            Ellipse lamp = new() { Width = 10, Height = 10, Fill = inactiveBrush, VerticalAlignment = VerticalAlignment.Center };
            group.Add(signal, lamp);
            StackPanel item = new() { Orientation = Orientation.Horizontal, Spacing = 5 };
            item.Children.Add(lamp);
            item.Children.Add(Label(text));
            row.Children.Add(item);
        }

        lamps.Add(group);
        panel.Children.Add(row);
    }

    /// <summary>
    /// Lights the lamps of a port for the wires that are active and darkens the others.
    /// </summary>
    /// <param name="index">The index of the port, in the order the ports were added.</param>
    /// <param name="signals">The wires that are active.</param>
    public void Update(int index, MicroGateSignals signals)
    {
        if (index < 0 || index >= lamps.Count)
        {
            return;
        }

        foreach ((MicroGateSignals signal, Ellipse lamp) in lamps[index])
        {
            lamp.Fill = signals.HasFlag(signal) ? activeBrush : inactiveBrush;
        }
    }

    private TextBlock Label(string text) => new() { Text = text, FontSize = 12, Foreground = textBrush, VerticalAlignment = VerticalAlignment.Center };
}
