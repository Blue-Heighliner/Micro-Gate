#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Runs two Controller apps from source side by side in UART peer mode, for exercising two asynchronous serial peers
// against each other over two cabled devices. File-based app (dotnet run) - run from the repo root, e.g.
// `dotnet run Scripts/RunUartPeers.cs`. Built once up front so the two instances do not race to build the same project.

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "build", "Controller/Controller.csproj")).Verify();

Task<RunResult>[] instances = [.. Enumerable.Range(0, 2).Select(_ => Script.Run("dotnet", "run", "--no-build", "--project", "Controller/Controller.csproj", "--", "--mode", "UartPeer"))];

foreach (RunResult result in await Task.WhenAll(instances))
{
    result.Verify();
}
