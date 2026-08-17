namespace BlueHeighliner.MicroGate;

/// <summary>
/// The Avalonia application entry point for the MicroGate sample.
/// </summary>
internal sealed partial class App : Application
{
    /// <summary>
    /// Gets or sets the service provider used to resolve the application's dependencies, configured by <see cref="Program.Main"/> before the Avalonia framework starts.
    /// </summary>
    internal static IServiceProvider Services { get; set; } = null!;

    /// <summary>
    /// Loads the application's XAML resources.
    /// </summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Creates the main window once the Avalonia framework has finished initializing.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
