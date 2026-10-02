namespace BlueHeighliner.MicroGate;

/// <summary>
/// Reads the controller's command line.
/// </summary>
internal interface IStartupParser
{
    /// <summary>
    /// Parses command-line arguments. <c>--mode Peer|Monitor|Passthrough</c> sets the initial mode (case-insensitive), and <c>--local 0-255</c> and <c>--remote 0-255</c> set the addresses, each also as <c>--name=value</c>. Unrelated arguments are left to the UI framework.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The options asked for, with a problem description if a value was missing or invalid; an option with a bad value keeps its default.</returns>
    StartupOptions Parse(IReadOnlyList<string> args);
}

/// <inheritdoc />
internal sealed class StartupParser : IStartupParser
{
    private readonly string modeOption = "--mode";
    private readonly string localOption = "--local";
    private readonly string remoteOption = "--remote";

    /// <inheritdoc />
    public StartupOptions Parse(IReadOnlyList<string> args)
    {
        ControllerMode mode = ControllerMode.Peer;
        byte? local = null;
        byte? remote = null;
        List<string> problems = [];

        for (int index = 0; index < args.Count; index++)
        {
            if (Matches(args[index], modeOption, out string? inline))
            {
                string value = inline ?? (index + 1 < args.Count ? args[++index] : string.Empty);
                if (Enum.TryParse(value, ignoreCase: true, out ControllerMode parsed) && Enum.IsDefined(parsed))
                {
                    mode = parsed;
                }
                else
                {
                    problems.Add($"Unknown mode '{value}'; use Peer, Monitor, or Passthrough.");
                }
            }
            else if (Matches(args[index], localOption, out inline))
            {
                local = ReadAddress(inline ?? (index + 1 < args.Count ? args[++index] : string.Empty), "local", problems) ?? local;
            }
            else if (Matches(args[index], remoteOption, out inline))
            {
                remote = ReadAddress(inline ?? (index + 1 < args.Count ? args[++index] : string.Empty), "remote", problems) ?? remote;
            }
        }

        return new StartupOptions { InitialMode = mode, LocalAddress = local, RemoteAddress = remote, Problem = problems.Count == 0 ? null : string.Join(" ", problems) };
    }

    private bool Matches(string argument, string option, out string? inline)
    {
        inline = null;
        if (string.Equals(argument, option, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (argument.StartsWith(option + "=", StringComparison.OrdinalIgnoreCase))
        {
            inline = argument[(option.Length + 1)..];
            return true;
        }

        return false;
    }

    private byte? ReadAddress(string value, string name, List<string> problems)
    {
        if (byte.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out byte address))
        {
            return address;
        }

        problems.Add($"The {name} address '{value}' must be a whole number from 0 to 255.");
        return null;
    }
}
