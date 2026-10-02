#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Runs two Controller apps from source side by side, for exercising two peers against each other over two
// cabled devices. File-based app (dotnet run) - run from the repo root, e.g. `dotnet run Scripts/RunControllers.cs`.
// Built once up front so the two instances do not race to build the same project.

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "build", "Controller/Controller.csproj")).Verify();

Task<RunResult> first = Script.Run("dotnet", "run", "--no-build", "--project", "Controller/Controller.csproj");
Task<RunResult> second = Script.Run("dotnet", "run", "--no-build", "--project", "Controller/Controller.csproj");

foreach (RunResult result in await Task.WhenAll(first, second))
{
    result.Verify();
}
