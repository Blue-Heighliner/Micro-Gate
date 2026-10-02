#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Runs the Controller app from source. File-based app (dotnet run) - run from the repo root, e.g.
// `dotnet run Scripts/RunController.cs`.

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--project", "Controller/Controller.csproj")).Verify();
