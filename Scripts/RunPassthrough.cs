#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Runs three Controller apps from source side by side: two in peer mode, each addressed to the other (local 1 /
// remote 3 and local 3 / remote 1), and one in passthrough mode, for exercising two peers talking through a
// passthrough over four cabled devices (the passthrough uses two of them, so it must not be given two devices that
// are cabled to each other). File-based app (dotnet run) - run from the repo root, e.g.
// `dotnet run Scripts/RunPassthrough.cs`. Built once up front so the instances do not race to build the same project.

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "build", "Controller/Controller.csproj")).Verify();

string[][] arguments =
[
    ["--mode", "Peer", "--local", "1", "--remote", "3"],
    ["--mode", "Peer", "--local", "3", "--remote", "1"],
    ["--mode", "Passthrough"],
];
Task<RunResult>[] instances = [.. arguments.Select(instance => Script.Run("dotnet", ["run", "--no-build", "--project", "Controller/Controller.csproj", "--", .. instance]))];

foreach (RunResult result in await Task.WhenAll(instances))
{
    result.Verify();
}
