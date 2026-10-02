# Project

Documentation for this repository's own tooling and workflow.

## Scripts

`Scripts/` holds file-based C# apps (`dotnet run Scripts/<Name>.cs`) for automated repository
actions.

- `Scripts/Test.cs` - runs the test suite with coverage collection and prints a summary.
- `Scripts/Verify.cs` - applies formatting fixes and regenerates the coverage badge.
- `Scripts/Publish.cs` - cuts a release (see Publishing below). A manual, human-only action.
- `Scripts/RunMonitor.cs` - runs the Monitor app from source (`dotnet run --project Monitor/Monitor.csproj`).
- `Scripts/RunController.cs` - runs the Controller app from source (`dotnet run --project Controller/Controller.csproj`).
- `Scripts/RunControllers.cs` - builds Controller once and runs two instances from source side by side, for two peers on two cabled devices (`RunControllers.task`).

## Publishing

Run `Scripts/Publish.cs` locally to cut a release:

1. It prompts for the version to publish (e.g. `1.2.3`). `Core/Core.csproj` carries no `<Version>` of its
   own, so this is what actually gets built and published.
2. It creates the GitHub Release (and its underlying tag) for that version locally via `gh release
   create`, done locally because a repo ruleset blocks the default `GITHUB_TOKEN` from creating tags.
3. It dispatches `build.yml`'s `workflow_dispatch` trigger with the version as input. The workflow
   verifies the dispatcher has Admin permission on the repo, refuses to run from anything but `main`,
   packs `Core/Core.csproj`, pushes the package to GitHub Packages and nuget.org, and uploads the
   `.nupkg`/`.snupkg` as release assets.
4. It waits for that run to finish, rolling the release/tag back if the workflow fails, so a failed
   publish never leaves one behind. If it can't confirm the run happened at all, it leaves the release
   in place instead, rather than risk deleting one that's still running.

## Workflows

- `.github/workflows/build.yml` - builds, verifies formatting (`dotnet format --verify-no-changes`,
  never applies fixes), and runs tests on every push/PR to `main`; its `publish` job (see Publishing
  above) only runs on `workflow_dispatch`, gated on the dispatcher having Admin permission on the repo
  and the run being on `main`, and pushes the package to both GitHub Packages and nuget.org.
- `.github/workflows/codeql.yml` - CodeQL security analysis on push/PR to `main` and a weekly schedule.

## Tests

`Tests/src/Unit/` mirrors `Core/src` and uses Moq for every dependency; it needs no real I/O. `Tests/src/Integration/` uses real I/O and is organized by scenario:

- Two peers joined by a loopback TCP socket pair standing in for the cable (one or both sides sending requests, ordering, large payloads, disconnect).
- The Linux device opener and native layer against a real pseudo-terminal, with a peer implementing the HDLC state machine on the master side, plus regular files and missing devices for failure paths.
- Port enumeration against a temporary folder tree shaped like `/dev` and `/dev/serial/by-id`.
- A link that drops chosen frames, checking that everything still arrives in order through rejects and the retransmit timer.
- A monitor against a real pseudo-terminal: frames written by a peer are reported correctly, and a read on the other end of the terminal times out, confirming the monitor never writes back.

Linux-only tests return early on other operating systems, and the pseudo-terminal tests return early if none can be created. The pseudo-terminal and file tests wrap the real native layer so that the SyncLink-specific configuration calls, which only a real device accepts, are skipped; a separate test checks that the real layer rejects a non-SyncLink device. The Windows native adapter can only be exercised on Windows with the driver installed, so it has no test.

## Sample

`Sample/` is an Avalonia demo application, not part of the published package. Run it with
`dotnet run --project Sample` (also available as the `RunSample.task` AutoDev task). It is built, formatted, and
lock-file-restored with the rest of the solution.

## Controller

`Controller/` is an Avalonia desktop application, not part of the published package, that connects as an `IMicroGatePeer`: the user picks a port, the two hex addresses, and the link encoding and CRC, then connects. Received and sent payloads appear in one log as rows of just a timestamp, direction, and byte count (received payloads are copied out of the pooled owner and disposed on the peer's delivery task, then posted to the UI thread). Selecting a row expands it into a table of its bytes, 30 cells wide with column numbers above and row numbers to the left, and collapses the previously expanded row. Cells show their ASCII character (control characters as abbreviations such as `LF`, values above 127 as numbers) and can be multi-selected (click, Ctrl, Shift, drag) and switched via the context menu between ASCII and the 0-255 value. The send box is the same table, editable: in ASCII input a typed character fills the selected cell and selects the next, in raw input a typed 0-255 value fills the cell and Tab (or a complete value) moves on; Enter sends. Its context menu inserts or replaces the selection with a control character (NUL, SOH, STX, ETX, LF, and so on) and deletes cells, which shortens the frame; the grid holds at most the peer's maximum payload. It looks like Monitor (same palette and layout) and is run (`RunController.task`), formatted, lock-file-restored, and published exactly like Monitor, with `SerialController` as the executable name.

## Monitor

`Monitor/` is an Avalonia desktop application, not part of the published package, that passively observes a MicroGate device: pick a port, start monitoring, and watch every frame in a scrolling log, including the frames two other stations use to manage their own connection. It never writes to the device (see [`Docs/Components/Monitor.md`](Components/Monitor.md)). The log can be saved to a JSON file and loaded back later, replacing whatever is currently on screen. Run it with `dotnet run --project Monitor` (also available as the `RunMonitor.task` AutoDev task). It is built, formatted, and lock-file-restored with the rest of the solution.

Unlike Sample, Monitor is meant to be handed to someone who does not have the .NET SDK installed, so it is published as one self-contained executable rather than run from source:

```sh
dotnet publish Monitor -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
dotnet publish Monitor -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

`Monitor.csproj` lists `linux-x64` and `win-x64` in `RuntimeIdentifiers` so both restore ahead of time; `SelfContained`, `PublishSingleFile`, and `IncludeNativeLibrariesForSelfExtract` are conditioned on a `RuntimeIdentifier` actually being set, so a plain `dotnet build`/`dotnet run` during development, and the solution-wide CI build, are unaffected and stay ordinary framework-dependent builds. The output is one executable (`SerialMonitor` on Linux, `SerialMonitor.exe` on Windows) with the .NET runtime, Avalonia, and every dependency bundled inside it. Nothing else is published beside it: managed debug information is embedded in the executable (`Monitor.csproj` sets `DebugType` to `embedded` and passes the same to `Core` through its project reference, only when a `RuntimeIdentifier` is set, so normal builds and the NuGet symbols package keep separate `.pdb` files), and the separate `.pdb` files the native libraries ship and the XML documentation file are removed from the publish list.
