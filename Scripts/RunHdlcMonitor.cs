#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Runs one Controller app from source in HDLC monitor mode. File-based app (dotnet run) - run from the repo root,
// e.g. `dotnet run Scripts/RunHdlcMonitor.cs`.

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--project", "Controller/Controller.csproj", "--", "--mode", "HdlcMonitor")).Verify();
