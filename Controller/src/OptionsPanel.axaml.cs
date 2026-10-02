namespace BlueHeighliner.MicroGate;

/// <summary>
/// The peer options, shown in an expandable sidebar section: the link settings that must match the remote station, and, depending on the <see cref="ControllerMode"/>, the transmit and connection settings.
/// </summary>
internal sealed partial class OptionsPanel : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptionsPanel"/> class with every setting at the library's default.
    /// </summary>
    public OptionsPanel()
    {
        InitializeComponent();

        MicroGatePeerOptions defaults = new();
        EncodingComboBox.Fill(defaults.Link.Encoding);
        CrcComboBox.Fill(defaults.Link.Crc);
        ReceiveClockComboBox.Fill(defaults.Link.ReceiveClockSource);
        TransmitClockComboBox.Fill(defaults.Link.TransmitClockSource);
        DivisorComboBox.Fill(defaults.Link.PhaseLockedLoopDivisor);
        IdlePatternComboBox.Fill(defaults.IdlePattern);
        PreamblePatternComboBox.Fill(defaults.PreamblePattern);
        PreambleLengthComboBox.Fill(defaults.PreambleLength);
        UnderrunComboBox.Fill(defaults.UnderrunAction);
        ClockSpeedTextBox.Text = defaults.Link.ClockSpeed.ToString(CultureInfo.InvariantCulture);
        MaxInfoFieldTextBox.Text = defaults.MaxInfoField.ToString(CultureInfo.InvariantCulture);
        TransmitWindowTextBox.Text = defaults.TransmitWindow.ToString(CultureInfo.InvariantCulture);
        RetryIntervalTextBox.Text = FormatSeconds(defaults.RetryInterval);
        RetransmitIntervalTextBox.Text = FormatSeconds(defaults.RetransmitInterval);
        MaxRetransmissionsTextBox.Text = defaults.MaxRetransmissions?.ToString(CultureInfo.InvariantCulture);
        AcknowledgeDelayTextBox.Text = FormatSeconds(defaults.AcknowledgeDelay);
        DisablePollFinalCheckBox.IsChecked = defaults.DisablePollFinalBit;
        LoopbackCheckBox.IsChecked = defaults.Loopback;
        MaxInfoFieldTextBox.TextChanged += (_, _) =>
        {
            if (int.TryParse(MaxInfoFieldTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int size) && size is >= 1 and <= 4090)
            {
                MaxInfoFieldChanged?.Invoke(this, size);
            }
        };
    }

    private string? problem;

    /// <summary>
    /// Occurs when the max info field setting is changed to a valid value, so the send table can follow it.
    /// </summary>
    public event EventHandler<int>? MaxInfoFieldChanged;

    /// <summary>
    /// Shows only the settings that apply to a mode: the link settings always, the transmit settings in peer and passthrough mode, and the connection settings in peer mode.
    /// </summary>
    /// <param name="mode">The mode the controller is in.</param>
    public void SetMode(ControllerMode mode)
    {
        TransmitPanel.IsVisible = mode != ControllerMode.Monitor;
        ConnectionPanel.IsVisible = mode == ControllerMode.Peer;
    }

    /// <summary>
    /// Reads the settings into peer options. Settings that do not apply to the mode keep their defaults, and are not validated.
    /// </summary>
    /// <param name="mode">The mode the controller is in.</param>
    /// <param name="error">A description of the first invalid setting when this returns <see langword="null"/>.</param>
    /// <returns>The options, or <see langword="null"/> if a setting is invalid.</returns>
    public MicroGatePeerOptions? Build(ControllerMode mode, out string error)
    {
        problem = null;
        error = string.Empty;
        MicroGatePeerOptions defaults = new();
        int maxInfoField = defaults.MaxInfoField;
        int transmitWindow = defaults.TransmitWindow;
        TimeSpan? retryInterval = defaults.RetryInterval;
        TimeSpan? retransmitInterval = defaults.RetransmitInterval;
        int? maxRetransmissions = defaults.MaxRetransmissions;
        TimeSpan acknowledgeDelay = defaults.AcknowledgeDelay;

        bool valid = TryParseInt(ClockSpeedTextBox, "Clock speed", 1, int.MaxValue, out int clockSpeed);
        if (valid && mode == ControllerMode.Peer)
        {
            valid = TryParseInt(MaxInfoFieldTextBox, "Max info field", 1, 4090, out maxInfoField)
                && TryParseInt(TransmitWindowTextBox, "Transmit window", 1, 7, out transmitWindow)
                && TryParseSeconds(RetryIntervalTextBox, "Connect retry interval", out retryInterval)
                && TryParseSeconds(RetransmitIntervalTextBox, "Retransmit interval", out retransmitInterval)
                && TryParseDelay(AcknowledgeDelayTextBox, "Acknowledge delay", out acknowledgeDelay);

            maxRetransmissions = null;
            if (valid && !string.IsNullOrWhiteSpace(MaxRetransmissionsTextBox.Text))
            {
                valid = TryParseInt(MaxRetransmissionsTextBox, "Max retransmissions", 0, int.MaxValue, out int parsed);
                maxRetransmissions = parsed;
            }
        }

        if (!valid)
        {
            error = problem ?? "A setting is invalid.";
            return null;
        }

        return new MicroGatePeerOptions
        {
            Link = new MicroGateLinkOptions
            {
                Encoding = EncodingComboBox.Pick<MicroGateEncoding>(),
                Crc = CrcComboBox.Pick<MicroGateCrc>(),
                ReceiveClockSource = ReceiveClockComboBox.Pick<MicroGateReceiveClockSource>(),
                TransmitClockSource = TransmitClockComboBox.Pick<MicroGateTransmitClockSource>(),
                PhaseLockedLoopDivisor = DivisorComboBox.Pick<MicroGatePhaseLockedLoopDivisor>(),
                ClockSpeed = clockSpeed,
            },
            IdlePattern = IdlePatternComboBox.Pick<MicroGateIdlePattern>(),
            PreamblePattern = PreamblePatternComboBox.Pick<MicroGatePreamblePattern>(),
            PreambleLength = PreambleLengthComboBox.Pick<MicroGatePreambleLength>(),
            UnderrunAction = UnderrunComboBox.Pick<MicroGateUnderrunAction>(),
            DisablePollFinalBit = mode == ControllerMode.Peer ? DisablePollFinalCheckBox.IsChecked == true : defaults.DisablePollFinalBit,
            MaxInfoField = maxInfoField,
            RetryInterval = retryInterval,
            RetransmitInterval = retransmitInterval,
            MaxRetransmissions = maxRetransmissions,
            AcknowledgeDelay = acknowledgeDelay,
            TransmitWindow = transmitWindow,
            Loopback = mode == ControllerMode.Peer && LoopbackCheckBox.IsChecked == true,
        };
    }

    private string FormatSeconds(TimeSpan? interval) => interval?.TotalSeconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private bool TryParseInt(TextBox box, string name, int min, int max, out int value)
    {
        if (int.TryParse(box.Text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max)
        {
            return true;
        }

        problem = max == int.MaxValue ? $"{name} must be a whole number of at least {min}." : $"{name} must be a whole number from {min} to {max}.";
        return false;
    }

    private bool TryParseDelay(TextBox box, string name, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        if (double.TryParse(box.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds >= 0 && seconds < 86400)
        {
            value = TimeSpan.FromSeconds(seconds);
            return true;
        }

        problem = $"{name} must be a number of seconds, zero or more.";
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

        problem = $"{name} must be a positive number of seconds, or blank for none.";
        return false;
    }
}
