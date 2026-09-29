#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Runs the Monitor app from source. File-based app (dotnet run) - run from the repo root, e.g.
// `dotnet run Scripts/RunMonitor.cs`.

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--project", "Monitor/Monitor.csproj")).Verify();
