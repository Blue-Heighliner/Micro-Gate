namespace BlueHeighliner.MicroGate;

/// <summary>
/// The Avalonia application entry point for the MicroGate sample.
/// </summary>
internal sealed partial class App : Application
{
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
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
