namespace BlueHeighliner.MicroGate;

/// <summary>
/// Hosts the application entry point for the MicroGate sample.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The managed entry point of the application.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the application.</param>
    [STAThread]
    public static void Main(string[] args)
    {
        ServiceCollection services = new();
        services.AddSingleton(new HdlcStationOptions { Address = 0xFF });
        services.AddConventionServices(typeof(IMicroGateConnector).Assembly);
        services.AddTransient<MainWindow>();
        App.Services = services.BuildServiceProvider();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Configures the Avalonia <see cref="AppBuilder"/> used to run the application; also used by the visual designer.
    /// </summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { OverlayPopups = true })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
