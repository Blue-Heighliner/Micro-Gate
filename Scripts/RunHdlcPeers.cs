#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Runs two Controller apps from source side by side in HDLC peer mode, each addressed to the other (local 1 / remote 3
// and local 3 / remote 1), for exercising two peers against each other over two cabled devices. File-based app
// (dotnet run) - run from the repo root, e.g. `dotnet run Scripts/RunHdlcPeers.cs`. Built once up front so the two
// instances do not race to build the same project.

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "build", "Controller/Controller.csproj")).Verify();

string[][] addresses = [["1", "3"], ["3", "1"]];
Task<RunResult>[] instances = [.. addresses.Select(pair => Script.Run("dotnet", "run", "--no-build", "--project", "Controller/Controller.csproj", "--", "--mode", "HdlcPeer", "--local", pair[0], "--remote", pair[1]))];

foreach (RunResult result in await Task.WhenAll(instances))
{
    result.Verify();
}
