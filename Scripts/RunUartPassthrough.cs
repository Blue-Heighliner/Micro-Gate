#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Runs three Controller apps from source side by side: two in UART peer mode and one in UART passthrough mode, for
// exercising two asynchronous serial peers talking through a passthrough over four cabled devices (the passthrough
// uses two of them, so it must not be given two devices that are cabled to each other). File-based app (dotnet run) -
// run from the repo root, e.g. `dotnet run Scripts/RunUartPassthrough.cs`. Built once up front so the instances do not
// race to build the same project.

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "build", "Controller/Controller.csproj")).Verify();

string[] modes = ["UartPeer", "UartPeer", "UartPassthrough"];
Task<RunResult>[] instances = [.. modes.Select(mode => Script.Run("dotnet", "run", "--no-build", "--project", "Controller/Controller.csproj", "--", "--mode", mode))];

foreach (RunResult result in await Task.WhenAll(instances))
{
    result.Verify();
}
